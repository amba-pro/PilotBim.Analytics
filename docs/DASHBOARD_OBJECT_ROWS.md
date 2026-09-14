# Dashboard Object Rows

Status: **DB-3 BLOCKED_DB3_FULL_COVERAGE** (whole-project stream)  
**DB-3.1: complete TypeId dataset materializer added** (unused by dashboard UI)  
Date: 2026-09-14

## Purpose

Normalized one-row-per-Pilot-object store so a future ObjectRows query engine can Filter / GroupBy / Count over **complete** CurrentProject data.

DB-3 was specified to **reuse an existing full object stream** from the inventory scan, without a second project scan and without building KPIs from samples.

That stream **does not exist** in current code.

## Coverage Contract

**FULL_OBJECT_STREAM: NO**

Current inventory never retains every project `IDataObject` (or even every id) in one collection.

| What exists | Coverage |
|-------------|----------|
| `ISearchResult.Total` per type | **counts only** — not objects |
| `SearchByType(..., maxResults)` | **capped ID list** (`MaxResults` = scan `SampleLimit`) |
| `SampleType` / `ApplySampledObjects` | **loaded samples** ≤ SampleLimit |
| Hierarchy walk | **budgeted visit**; per-type `Samples` capped at SampleLimit |
| Creator aggregation | **budgeted** second sample (500 / 5k / 50k) |
| Sticky sample buffers | **tiny** (history 5, system fields 20, documents 20) |

Invariant that was **not** claimed and must not be faked:

> DashboardObjectRow count == analytics-eligible project population

That population is only known as **aggregates** (`report.ObjectsFound` = sum of type `ObjectCount` from search Total or walk counts). Object **values** (attributes, creator, state) are known only for samples.

A dashboard that GroupBy `attribute:{typeId}:{name}` from sample buffers would silently under-count. **Forbidden.**

## Source Pipeline

`InventoryService.Run`

```
GetTypes / states / orgs / persons     → metadata (not objects)
        │
        ├─ ISearchService present
        │     ObjectSamplingCoordinator.SampleAllTypes
        │       SearchByType(typeId, SampleLimit)     → IDs (capped)
        │       SubscribeObjects(ids)                 → IDataObject samples
        │       ApplySampledObjects                   → attr fill + buffers
        │     CreatorAggregationService.Aggregate     → more capped samples
        │
        └─ ISearchService null
              HierarchyWalkSampler.Walk               → budgeted BFS
              ApplyWalkBucket                         → samples from buckets
        │
        BIM index / remarks                           → counts + more samples
        ProjectAnalyticsService.Build                 → AGGREGATES only
```

`ScanMode` / `SampleLimit` (`CapabilityStatus.ScanMode` enum): Fast=50, Standard=200, Full=10000.  
Even **Full** is a cap, not “all objects”.

### Object-pass table

| Location | Method | Objects seen | Full or sample? | Attributes? | System fields? | Once? | Capture rows here? | Extra SDK? |
|----------|--------|--------------|-----------------|-------------|----------------|-------|-------------------|------------|
| `TypeDiscoveryService` | `DiscoverTypes` | none | n/a | metadata shells | n/a | once | no | GetTypes |
| `PilotObjectScanner` | `SearchByType` | **IDs only**, `MaxResults` | **sample IDs** | no | no | per type | IDs incomplete | search |
| `PilotObjectSampler` | `SampleType` | loaded subset of those IDs | **sample** | yes on loaded | yes on loaded | per type | **would be partial** | SubscribeObjects |
| `ObjectSamplingCoordinator` | `ApplySampledObjects` | same samples | **sample** | profiled | recorded into count dicts | per type | **no** (incomplete) | no extra |
| `HierarchyWalkSampler` | `Walk` | visitBudget 2k/25k/100k; `Samples` per type ≤ SampleLimit | **truncated sample** | yes on visited | yes | once if no search | only if Truncated=false **and** Samples==Count for every type — **not guaranteed** | Subscribe per node |
| `CreatorAggregationService` | `Aggregate` | budget 500/5k/50k | **sample** | incidental | creator/created | extra pass | **no** | extra SampleType |
| `RemarkAnalyticsService` | `Analyze` | samplePerType 10/30/100 | **sample** | remark attrs | some | extra | **no** | SampleType |
| `BimDiscoveryService` / index | search counts | BIM **elements** not Pilot cards | counts / capped hits | n/a | n/a | once | different entity | BIM search |
| `InventoryService` | buffers | 5–20 objects | **sample** | limited | example only | once | **no** | no |
| `ProjectAnalyticsService` | `Build` | none | **aggregates** | fill rates | count maps | once | **no** | no |
| `LoadChildren` | UI tree | one node's children | lazy UI | yes | yes | on expand | not scan | Subscribe |

**A. FULL OBJECT STREAM:** none  
**B. SAMPLE-ONLY STREAMS:** SampleType, walk buckets, creator-agg, remarks, buffers  
**C. AGGREGATE-ONLY:** `ObjectCount` / `ISearchResult.Total`, snapshot distributions, BIM element counts

## Object Identity

Not implemented. Would have been:

| Field | Type in SDK / inventory | Notes |
|-------|-------------------------|-------|
| ObjectId | `Guid` (`IDataObject.Id`) | stable |
| ParentId | `Guid` (`IDataObject.ParentId`) | |
| TypeId | `int` (`IType.Id`) | |

No production DTO was added.

## Row Schema

Not implemented. Intended sketch (for a future full stream only):

```
DashboardObjectRow
  ObjectId : Guid
  TypeId   : int
  ParentId : Guid
  Fields   : sparse map fieldId → DashboardFieldValue
```

Field ids: DB-1 `DashboardFieldIds` only.

## Field Value Schema

Not implemented. Intended: typed StableKey + optional DisplayText; never display-as-identity for User/Enum/Reference.

## System Fields

| FieldId | Source | Available for all rows today? | Stored in DB-3 |
|---------|--------|-------------------------------|----------------|
| `system:objectId` | `IDataObject.Id` | only on **sampled** loaded objects | **no** |
| `system:typeId` | Type.Id | same | **no** |
| `system:parentId` | ParentId | same | **no** |
| `system:creatorId` | Creator.Id | same; counts may be budgeted | **no** |
| `system:created` | Created | same | **no** |
| `system:objectState` | ObjectStateInfo.State | same | **no** |
| `system:createdMonth` | derive from Created | same; snapshot has aggregate | **no** (derive later) |
| `system:userState` | UserState **attribute** | only if present on sample | **no** |
| `system:responsible` | OrgUnit attributes | sampled hits only | **no** |

## Custom Attributes

Coverage: **NONE** for a complete project set.

At `ApplySampledObjects`, `obj.Attributes` **is** readable **without** a further SDK get — but only for objects already in the sample.  
Loading attributes for **unsampled** objects requires `SubscribeObjects` on those IDs = **extra materialization** (new full load).

**Extra SDK reads to complete the project:** **YES** (not done).

## Stable Identity

Unchanged: catalog ids. No row store.

## Null Semantics

N/A (no rows).

## Unsupported Values

N/A.

## Lifecycle

No row collection. Scan still publishes `ProjectInventoryReport` + snapshot aggregates. Buffers cleared at start of `SampleAllTypes` (Stage 6).

## Repeated Scan

N/A for ObjectRows. Existing buffer clear still applies to samples.

## Callback Safety

N/A for ObjectRows. Sample callbacks still use `CallbackWaitSession` / `ShouldAccept`.

## Deduplication

N/A.

## Performance

Pilot SDK calls per widget: **0** (unchanged; no ObjectRows engine)  
Additional project scans: **0** (blocked path not added)

## Memory

**UNCHANGED** — no per-object store.

If a future full materialization were added: **MEMORY_RISK_HIGH** (one row + sparse attributes × `ObjectsFound`; Full mode already admits up to 10k **samples per type**, a true full project can be much larger).

## Query Engine Integration

DB-2 `SnapshotWidgetQueryEngine` remains the only executor.  
ObjectRows engine **not** started.

Preset Count+GroupBy on snapshot dimensions remains valid for **those aggregates**. Arbitrary attribute GroupBy remains **UnsupportedQuery**.

## Known Limitations

1. No reusable full `IDataObject` stream in `InventoryService.Run`.
2. Search `Total` ≠ loaded objects.
3. `ScanMode.Full` SampleLimit=10000 is still a sample cap.
4. Hierarchy walk `Truncated` is normal on large trees.
5. Cannot honestly set `IsComplete=true` on any current object list.
6. Building rows from buffers would reintroduce sample-as-truth (rejected).

## Minimal next architectural step (not implemented)

**Do not** raise `SampleLimit` silently inside the existing inventory scan (changes timeouts, memory, Stage 6 buffers, current analytics).

Product choice required:

1. **Keep V1 = DB-2 snapshot widgets** for preset dimensions; defer arbitrary GroupBy.  
2. **Add an explicit, once-per-refresh full materialization pass** (new stage, not silent DB-3):
   - per type: `SearchByType(typeId, Total)` (or SDK pagination if required)
   - `SubscribeObjects` all IDs **once**
   - build `DashboardObjectRows` + coverage (`loaded` vs `Total`)
   - **this is an additional full load** vs today’s inventory
   - gate it (opt-in / dashboard refresh mode)
   - Pilot runtime validation mandatory
   - still **0 scans per widget** after the shared pass

Until DB-3.1, ObjectRows stayed blocked for whole-project coverage. DB-3.1 implements (2) **per explicit TypeId only**, not a full-project load.

## DB-3.1 Complete Type Dataset

Status: **implemented, not wired to dashboard**  
Date: 2026-09-14  
Feasibility: **FULL_TYPE_LOAD_SUPPORTED_WITH_LIMIT**

### Why whole-project eager load was rejected

DB-3 proved there is no reusable full `IDataObject` stream. Inventory sampling (`SampleLimit` 50 / 200 / 10000) is not complete data. Loading every object of every type when a dashboard opens would be an unbounded project-wide SDK cost and a high memory risk.

Custom dashboard attributes already have type-scoped identity: `attribute:{typeId}:{attributeName}`.

### Why TypeId is the V1 boundary

V1 arbitrary-attribute analytics is:

- current project
- **one explicit Entity TypeId**
- the complete object set for that type

Many widgets may later share one TypeId dataset. Types that are not requested are not loaded.

PATH A remains `ProjectAnalyticsSnapshot` → `SnapshotWidgetQueryEngine` (aggregates).  
PATH B is `TypeId` → `DashboardTypeDatasetMaterializer` → `DashboardObjectRows` (object-level).

### Search semantics

Evidence: Ascon.Pilot.SDK 25.9 (`Ascon.Pilot.SDK.dll` reflection).

| API | Signature / type |
|-----|------------------|
| `IQueryBuilder.MaxResults` | `MaxResults(Int32)` |
| Skip / Offset / page | **none** |
| `ISearchResult.Total` | `Int64` |
| `ISearchResult.Result` | `IEnumerable<Guid>` |
| `ISearchService.Search` | `IObservable<ISearchResult>` |
| `IObjectsRepository.SubscribeObjects` | `IObservable<IDataObject> SubscribeObjects(IEnumerable<Guid>)` |

Existing wrapper: `PilotObjectScanner.SearchByType(int typeId, int maxResults, Action<IReadOnlyList<Guid>, long> onDone, ...)`.

**Total vs MaxResults:** inventory already treats `Total` as the type population while `Result` is capped by `maxResults`. Completeness must **not** assume `Result.Count == Total` unless they are equal after a request with `maxResults = Total` (when `Total <= Int32.MaxValue`).

**Paging:** not exposed. Continuation is not available.

**Backend cap:** **unknown** (NeedsRuntime). If the server silently returns fewer IDs than `Total`, coverage is **Partial**, never Complete.

**Int32 limit:** if `Total > Int32.MaxValue`, MaxResults cannot request the full population → **Failed** (`TotalExceedsInt32`). No unchecked `(int)Total`.

### Coverage definition

`DashboardTypeCoverage`: `Complete` | `Partial` | `Failed`

A dataset is **Complete** only when:

- the load outcome succeeded (not timeout / cancel / fail / overflow), **and**
- `LoadedUniqueCount == ExpectedCount`

No percentage heuristic. Empty type (`Total = 0`, loaded 0) is **Complete** with empty rows.

Timeout with some rows → **Partial**. Timeout with none → **Failed**. Cancel / fail / overflow → **Failed**.

`ExpectedCount` is `ISearchResult.Total` from the type search.

### Row model

`DashboardObjectRow` (internal, no WPF):

- `Guid ObjectId`
- `int TypeId`
- `Guid? ParentId` (null when `Guid.Empty`)
- `IReadOnlyDictionary<string, DashboardFieldValue> Fields` — sparse, DB-1 ids

### Field values

`DashboardFieldValue`: `Kind`, `Value` (typed), `StableKey?`, `DisplayText?`

Display text is never identity. Kinds follow DB-1 `DashboardFieldType` (Text, Integer, Number, Boolean, DateTime, Enum, User, Reference, Guid). Unsupported runtime values skip that field and increment `SkippedUnsupportedValues`; the dataset continues.

`system:createdMonth` is **not** stored (derive later from `system:created`).

`system:userState` / `system:responsible` are copied from the first in-memory `UserState` / `OrgUnit` attribute when present (same object, no extra SDK call).

### Custom attributes

Loaded from existing `IDataObject.Attributes` after `SubscribeObjects`.  
**Extra SDK call per attribute: NO.**

Ids: `attribute:{typeId}:{attributeName}` via `DashboardFieldIds.Attribute`.

### Materializer

`DashboardTypeDatasetMaterializer.Materialize(TypeId, CancellationToken)` → `DashboardTypeDataset`

Not a widget operation. Does not accept `WidgetQueryDefinition` / `DashboardWidgetQuery`.

Per TypeId SDK work:

1. `SearchByType(typeId, 1)` — read `Total` (probe)
2. if `Total == 0` → Complete empty
3. if `Total > Int32.MaxValue` → Failed
4. `SearchByType(typeId, (int)Total)` — ID set (skipped when Total is 1 and probe already returned that id)
5. `SubscribeObjects` those unique IDs once

No static cache. Future dashboard session should own TypeId datasets:

```
Dashboard refresh/session
        │
        ├── TypeId 12 dataset
        ├── TypeId 34 dataset
        └── TypeId 77 dataset

Widgets A/B on Type 12 share ONE Type 12 dataset.
```

### Cancellation

`CallbackWaitSession` + `CancellationToken` (same Stage 9 pattern as inventory sampling). Cancel or abandon → coverage not Complete. Late callbacks must pass `ShouldAccept` (`DashboardTypeCallbackCollector`); they must not mutate an abandoned set.

### Timeout

Search and subscribe each wait **30 seconds** — same as `ObjectSamplingCoordinator` sample wait. Timeout never yields Complete.

Pilot SDK subscriptions are not cancellable here; timeout **abandons** acceptance, it does not stop SDK work.

### Deduplication

ObjectId (`Guid`) identity. Duplicate SubscribeObjects callbacks: **first wins**, no silent overwrite. `LoadedUniqueCount` is unique Guid count.

### Memory

Sparse `fieldId → value` per row. Catalog descriptors are not copied onto rows. No columnar store.

Diagnostic log (no object payloads):

`Dashboard type materialization: TypeId=… Expected=… Loaded=… Coverage=… FieldValues=… DurationMs=…`

Area: `dashboard-type-dataset`.

Risk for one large type: **MEDIUM** (bounded by that type’s population, not the whole project). A remarks type with tens of thousands of objects plus attributes can still be heavy — runtime canary required.

### Performance

- Per dataset TypeId: 1–2 searches + 1 SubscribeObjects batch
- Per widget: **0** (materializer is unused by UI)
- Inventory sample limits: **unchanged**
- Dashboard presenter / query engine / persistence: **unchanged**

### Runtime validation

**REQUIRED: YES.** Do not run automatically.

Canary:

1. Pick one moderate TypeId (example: remarks / «Замечания к ЦИМ»).
2. Record search `Total`.
3. Call `Materialize`.
4. Verify `LoadedUniqueCount == Total` and `Coverage = Complete`.
5. Record elapsed time, memory delta if practical, log errors, try cancel.
6. Repeat on one larger TypeId.
7. No full-project stress yet.

If `LoadedUniqueCount < Total` with a successful wait, treat as possible silent server cap — do not claim Complete.

### Known SDK limits

- `MaxResults` is Int32; no search paging
- Backend result cap **unknown**
- `SubscribeObjects` disposable still discarded (TD-24, same as rest of plugin); late-callback safety is `ShouldAccept`
- Completeness of `Total` vs actually loadable objects is NeedsRuntime
- Materializer is **not** called from `AnalyticsDashboardPresenter` / widget editor / `ChartDataService` / snapshot engine

### DB-4 recommendation (not implemented)

ObjectRows-backed widget query engine:

- same DB-2 query contract
- plus `EntityTypeId` + CurrentProject + Count + one arbitrary Dimension
- input: **Complete** `DashboardTypeDataset` only
- **reject** if `Coverage != Complete`
- filters later (DB-5)

