# Dashboard Query Coordinator

Internal session orchestrator introduced in DB-6. Not persisted. Not bound to UI. Not a MEF export.

## Purpose

Give future Widget Editor one operation:

```
WidgetQueryResult result = await coordinator.ExecuteAsync(query, cancellationToken);
```

The coordinator owns routing, shared TypeId materialization, session cache, and session lifetime. UI must not choose an engine, call the materializer, or know whether another widget already loaded a TypeId.

## Session Ownership

One `DashboardQueryCoordinator` is one dashboard data session:

- one `ProjectAnalyticsSnapshot` (read-only)
- one `DashboardFieldCatalog` (already built; not rebuilt per query)
- one Pilot project context implied by those inputs
- one in-memory TypeId dataset cache

Refresh / rescan: dispose the old coordinator and construct a new one. Do not mutate a live session with `Reset()`.

Construction does **not** materialize anything. No all-Type warm-up. No disk load.

## Routing

Deterministic V1. Not based on chart type, localized labels, `WidgetKind`, or `ChartSource`.

| `query.EntityTypeId` | Path |
|----------------------|------|
| `null` | `SnapshotWidgetQueryEngine` against the session snapshot |
| non-null | shared Type dataset → `ObjectRowsWidgetQueryEngine` |

The coordinator does not mutate `DashboardWidgetQuery`. Two widgets may share a query instance.

## Snapshot Queries

Examples: project Types, Creators, UserStates, scalar project Count.

- no materializer call
- no dataset cache access
- snapshot engine remains the authority

ObjectRows-only filters on a project-wide query (`EntityTypeId == null` and `Filters` non-empty) stay `UnsupportedQuery`. The coordinator does **not** guess a TypeId.

## Type-Scoped Queries

For `EntityTypeId != null`:

1. obtain the shared `DashboardTypeDataset` for that TypeId (cache miss → provider once);
2. execute `ObjectRowsWidgetQueryEngine.Execute(dataset, catalog, query)`.

Materialization is **not** inside the ObjectRows engine. Query-result rows are **not** cached. After a Complete dataset is in memory, grouping/filters are cheap.

## Dataset Materialization

Production seam:

```
internal interface IDashboardTypeDatasetProvider
{
    DashboardTypeDataset Materialize(int typeId, CancellationToken cancellationToken);
}
```

One production implementation: `DashboardTypeDatasetProvider` → `DashboardTypeDatasetMaterializer.Materialize`.

Evidence: `Materialize` is **synchronous/blocking** (`CallbackWaitSession` Wait, 30s search + 30s subscribe). The coordinator therefore starts **one** `Task.Run` per TypeId cache miss so `ExecuteAsync` does not block the WPF thread. Subsequent widgets await the same task; they do not `Task.Run` again.

No SDK calls except that cache-miss materialization.

## Type Dataset Cache

| | |
|--|--|
| Key | TypeId (`int`) |
| Value | `Task<DashboardTypeDataset>` (the operation, not only the completed dataset) |
| Scope | this coordinator instance |
| Static / global / LocalAppData | **no** |

Caches Complete, Partial, Failed, and unexpectedly faulted tasks for the session. New coordinator may retry.

Does **not** cache `WidgetQueryResult`. Does **not** cache the field catalog.

`CachedTypeCount` is an internal diagnostic, not a public API.

## Concurrent Coalescing

Lock around lookup/create/store only. Materialize outside the lock.

Same uncached TypeId, sequential or concurrent widgets: **one** provider invocation.

Different TypeIds: one materialization each. No cross-type reuse.

Do not use a `ConcurrentDictionary` factory that can start two expensive loads before one wins.

## Partial / Failed Cache Behavior

First materialization Partial or Failed is cached. Other widgets for that TypeId reuse it. ObjectRows returns `IncompleteData` (empty numbers). No retry storm against Pilot in the same session.

## Cancellation

Two concepts:

**Session cancellation.** Dispose / dashboard close / data refresh cancels `_sessionCts`. Active shared materializations observe that token. Late materializer callbacks remain gated by existing `CallbackWaitSession` (no late mutation of a disposed wait).

**Caller cancellation (DB-6 V1 — deferred).** `ExecuteAsync(..., cancellationToken)` accepts a token for future API shape. V1 **ignores** the caller token. Shared materialization uses the session token only. One widget preview stopping must not cancel another widget’s TypeId load.

Per-widget wait cancellation is intentionally not implemented on .NET Framework 4.7.2 in this stage. Do not treat a cancelled caller token as a query status.

## Dispose

Implements `IDisposable` because it owns `CancellationTokenSource`.

1. marks disposed
2. cancels `_sessionCts`
3. new `ExecuteAsync` throws `ObjectDisposedException`
4. disposes CTS
5. clears cache references

Does **not** `Task.Wait` / `.Result` / `.GetAwaiter().GetResult()`. Does not wait for background work.

In-flight `ExecuteAsync` after dispose does not return normal `Success` (throws `ObjectDisposedException` once the shared operation settles or is cancelled).

`ExecuteAsync` after Dispose is a programmer/lifecycle error, not a `WidgetQueryResult`.

## Refresh / New Session

New scan → new snapshot + catalog → new coordinator. Old cache dies with Dispose. Same TypeId in the new session materializes again.

## Threading

| Path | Threading |
|------|-----------|
| Snapshot | in-memory, `Task.FromResult` — no `Task.Run` |
| TypeId cache miss | one `Task.Run` around blocking `Materialize` |
| TypeId cache hit | await existing task, then in-memory ObjectRows |
| UI thread | must not block on materialization |

Continuations use `ConfigureAwait(false)`. Cached datasets are treated as immutable; ObjectRows does not clone complete row lists per widget.

## Performance

Invariant: **one TypeId = at most one materialization attempt per coordinator session**, regardless of widget count or concurrency.

| Query | SDK / provider |
|-------|----------------|
| Snapshot (`EntityTypeId` null) | 0 |
| First TypeId query | 1 shared materialization |
| Later same TypeId (any filters/dimension) | 0 |
| Different TypeId | 1 additional |

No auto-preload. No enumerate-all-types.

## Error Semantics

Expected materializer outcomes already use `DashboardTypeDataset.Coverage` (`Complete` / `Partial` / `Failed`). Coordinator preserves them. ObjectRows maps non-Complete → `IncompleteData`.

Unexpected provider exceptions: logged (`DashboardQuery`), the faulted task is **cached** for the session (do not hammer SDK), exception propagates. No fake Success dataset. New coordinator may retry.

Snapshot unsupported/invalid results pass through unchanged.

## Future Widget Editor Contract

```
using (var coordinator = new DashboardQueryCoordinator(snapshot, catalog, repository, search))
{
    var result = await coordinator.ExecuteAsync(query, token);
    // bind WidgetQueryResult — Status / Dataset / Reason
}
```

Editor does not reference:

- `SnapshotWidgetQueryEngine`
- `ObjectRowsWidgetQueryEngine`
- `DashboardTypeDatasetMaterializer`
- cache internals

Uncached TypeId queries can be long-running (search + subscribe timeouts). Future VM states (Idle / Loading / Success / Empty / Incomplete / Unsupported / Error) belong to UI, not this type.

## Logging

Area `DashboardQuery`:

- `Type dataset MISS TypeId=…` on cache miss only
- `Type dataset materialized TypeId= Coverage= Expected= Loaded= ElapsedMs=`

No object/attribute payloads. No per-widget HIT spam.

## Architecture proof

**Scenario A** — widget “project object types” (`EntityTypeId` null, `system:typeId`): Snapshot engine, **0** materializer calls.

**Scenario B** — Widget 1 TypeId=100 group by attr A; Widget 2 TypeId=100 filter attr B group by attr C: **1** materialization of TypeId 100, **2** in-memory executions.

**Scenario C** — Widget 1 TypeId=100, Widget 2 TypeId=200: **2** materializations, one per TypeId.

## Non-Goals

- Widget Editor / WPF loading UI
- Dashboard persistence V2 / `dashboard-layout.json`
- Grid, visualizations, dashboard-level filters
- Query-result cache
- Field catalog cache
- Cross-session / static / disk object-row cache
- Public API / MEF
- DB-R1 canary execution (canary remains env-gated and unused in normal runs)
- DB-7
