# Stage 6 InventoryService Audit

Date: 2026-09-08  
Scope: Stage 6.0 baseline + Stage 6.1 analysis only  
Production code changes: **none**

## Baseline

| Check | Result |
|-------|--------|
| HEAD | `b8df8f3` |
| origin/main | `b8df8f3` (synced) |
| Working tree | clean |
| Build | PASS |
| Errors / Warnings | 0 / 0 |
| Tests | 100 passed, 0 failed, 0 skipped |

## Current Consumers

| Caller | Method used | Notes |
|--------|-------------|-------|
| `AnalyticsCommandService.CreateInventoryService` | **creates** | Only composition site (`new InventoryService(...)`) |
| `AnalyticsCommandService.OpenAnalytics` | creates window with inventory | one instance per open |
| `AnalyticsCommandService.OpenCatalog` | creates window with inventory | one instance per open |
| `AnalyticsWindow.Refresh_Click` | `Run` | via `Task.Run` |
| `InventoryWindow.Scan_Click` | `Run` | via `Task.Run` |
| `InventoryWindow` structure tree | `GetRootObject`, `LoadChildren` | UI expansion; no timeout |
| Unit tests | `ResolveObjectCount` only | pure helper |

Lifetime: one `InventoryService` per window open; **rescans reuse the same instance**.

## Public API

| Member | Kind | Used in production? |
|--------|------|---------------------|
| ctor `(IObjectsRepository, ISearchService, IModelStorageProvider, IModelSearchManager)` | public | yes — command service |
| `Run(ScanMode, CancellationToken, Action<string>)` | public | yes — both windows |
| `LoadChildren(Guid, Action<IReadOnlyList<IDataObject>>)` | public | yes — InventoryWindow |
| `GetRootObject()` | public | yes — InventoryWindow |
| `ResolveObjectCount(long, int)` | internal static | tests + SampleAllTypes |

Private instance: `SampleAllTypes`, `ApplyWalkBucket`, `ApplySampledObjects`  
Private static: `RecordCreatorAndCreatedDate`, `RecordResponsible`, `Cancel`, `Zone`, `FailZone`

**Size:** ~605 LOC; 3 public instance methods; 3 private instance; 5 private static; 1 internal static; 7 fields (4 injected readonly SDK + 3 mutable sample buffers).

## Call Graph

### `Run`

```
Caller (AnalyticsWindow / InventoryWindow)
  → InventoryService.Run(...)
      → new ProjectInventoryReport
      → PilotSdkDiscoveryService.PopulateStaticSdkInventory
      → TypeDiscoveryService.DiscoverTypes
      → StateDiscoveryService.DiscoverStates
      → StateMappingService.ApplyOverrides
      → OrganisationDiscoveryService → orgMap
      → PersonDiscoveryService.DiscoverPersons(orgMap)   // needs orgs first
      → [if !_search] HierarchyWalkSampler.Walk(PilotObjectScanner)
      → SampleAllTypes(...)
          → AttributeDiscoveryService / PilotObjectScanner / PilotObjectSampler / DocumentDiscoveryService
          → per type: walk bucket OR SampleType + CallbackWaitSession(30s)
          → ApplySampledObjects → instance buffers + creator/responsible maps
          → [if search] CreatorAggregationService.Aggregate
      → StateDiscoveryService.ApplyObservedUsage          // needs sampled Types
      → DocumentDiscoveryService.BuildCapabilities
      → HistoryDiscoveryService.BuildCapabilities + SampleHistory(buffer)
      → BimDiscoveryService.Discover(..., walk, mode)
      → RemarkAnalyticsService.Analyze
      → report.DocumentSamples = _documentSampleBuffer   // alias, not copy
      → SystemFieldDiscoveryService.EnrichFromSample(buffer)
      → flatten AllAttributes / ObjectsFound
      → CapabilityMatrixService.Build → FinalStatus / AnalyticsReadiness
```

Conditional branches: search vs hierarchy walk; cancel checkpoints throughout; FailZone soft-fail for some zones; outer catch → `BLOCKED_BY_SDK`.

### `LoadChildren`

```
InventoryWindow
  → InventoryService.LoadChildren(parentId, onDone)
      → new PilotObjectScanner.TryGetCached
      → new PilotObjectScanner.SubscribeObjects (children and/or parent)
      → onDone(list)  // no Wait timeout; callback-driven
```

### `GetRootObject`

```
InventoryWindow
  → InventoryService.GetRootObject
      → _repository.GetRootObject() | null + log
```

## Responsibilities

| Responsibility | Methods | Dependencies | SDK-bound? | Stateful? | Candidate extraction? |
|----------------|---------|--------------|------------|-----------|------------------------|
| SDK composition boundary | ctor | injected Pilot/BIM interfaces | YES | no | **NO** — already correct |
| Full scan workflow orchestration | `Run` | ~15 discovery types | via children | buffers | **YES** — primary surface |
| Type sampling + counts + tallies | `SampleAllTypes`, `Apply*`, `Record*` | scanner/sampler/attr/doc, `CallbackWaitSession` | YES | **instance buffers** | **YES** — clearest slice |
| Zone / cancel bookkeeping | `Zone`, `FailZone`, `Cancel`, Progress | logger, report | no | report-local | **MAYBE** (tiny) |
| History/system/doc sample buffering | fill in Apply; consume in Run | History / SystemField / Doc | YES | **buffers** | **YES** with sampling |
| Structure tree lazy load | `LoadChildren` | PilotObjectScanner ×2–4 | YES | no class state | **MAYBE** later |
| Root object access | `GetRootObject` | repository | YES | no | **NO** / keep façade |
| Count semantics | `ResolveObjectCount` | none | no | no | **NO** — already pure |
| Final matrix / status | delegated | `CapabilityMatrixService` | soft | no | **NO** — already extracted |

## Dependencies

All production `new` inside `InventoryService` (excluding trivial BCL DTO/`List`/`HashSet`/`ZoneResult`):

| Dependency | Created where | Frequency | Stateful | SDK | Could inject? | Should inject now? | Category |
|------------|---------------|-----------|----------|-----|---------------|-------------------|----------|
| `ProjectInventoryReport` | Run | 1×/Run | report | no | no | no | Report assembler |
| `PilotSdkDiscoveryService` | Run | 1×/Run | no | reflection | yes | no | Discovery helper |
| `TypeDiscoveryService` | Run | 1×/Run | no | YES | yes | no | Discovery |
| `StateDiscoveryService` | Run | **2×/Run** | no | YES | yes | no | Discovery |
| `StateMappingService` | Run | 1×/Run | soft | no | yes | no | Enricher |
| `OrganisationDiscoveryService` | Run | 1×/Run | no | YES | yes | no | Discovery |
| `PersonDiscoveryService` | Run | 1×/Run | no | YES | yes | no | Discovery |
| `PilotObjectScanner` | Run walk | 1×/Run | façade | YES | yes | later reuse | SDK adapter |
| `HierarchyWalkSampler` | Run if no search | 0–1×/Run | walk result | YES | yes | no | SDK adapter |
| `DocumentDiscoveryService` | Run + SampleAllTypes | 2×/Run | no | soft | yes | no | Discovery |
| `HistoryDiscoveryService` | Run | 1×/Run | no | YES | yes | no | Discovery |
| `BimDiscoveryService` | Run | 1×/Run | no | YES | yes | no | Discovery |
| `RemarkAnalyticsService` | Run | 1×/Run | no | YES | yes | no | Discovery |
| `SystemFieldDiscoveryService` | Run | 1×/Run | no | soft | yes | no | Enricher |
| `CapabilityMatrixService` | Run | 1×/Run | no | no | yes | no | Report assembler |
| `AttributeDiscoveryService` | SampleAllTypes | 1×/Sample | accumulators | soft | yes | no | Discovery |
| `PilotObjectScanner` | SampleAllTypes | 2nd/Run | façade | YES | yes | later | SDK adapter |
| `PilotObjectSampler` | SampleAllTypes | 1× | no | YES | yes | no | SDK adapter |
| `CallbackWaitSession` | SampleAllTypes | **per type** | wait gate | no | no | **no** | Sync helper |
| `CreatorAggregationService` | SampleAllTypes | 0–1 if search | no | YES | yes | no | Discovery |
| `PilotObjectScanner` | LoadChildren | **2–4×/call** | façade | YES | yes | later | SDK adapter |

**Could inject ≠ should inject.** Ctor-injecting all discovery types moves factory noise without shrinking responsibilities.

## Lifetimes

| Dependency | Intended lifetime | Safe as long-lived field? | Behavior risk if moved to ctor |
|------------|-------------------|---------------------------|--------------------------------|
| Most discovery services | per Run (today) | usually yes if stateless | Low |
| `CallbackWaitSession` | per wait | **must stay per-wait** | **High** — shared gate corrupts waits |
| `ProjectInventoryReport` | per Run | must stay per Run | High |
| `HierarchyWalkResult` | per Run | per Run | High if reused |
| `PilotObjectScanner` | per use (today) | yes (stateless façade) | Low |
| Sample buffers | **instance fields** | already long-lived | **Already wrong across Run** (see Mutable State) |

Do **not** “simply move all `new` to constructor” as Stage 6.2 — that does not fix god-factory orchestration and can hide lifetime bugs.

## Mutable State

| State | Owner today | Lifetime | Mutation points | Safe to move? |
|-------|-------------|----------|-----------------|---------------|
| `_repository` / `_search` / `_storageProvider` / `_searchManager` | InventoryService | per instance | ctor only | KEEP injected |
| `_historySampleBuffer` | InventoryService | **instance; never Cleared** | ApplySampledObjects (cap 5) | **HIGH RISK** — sticky across rescans |
| `_systemFieldSampleBuffer` | InventoryService | **instance; never Cleared** | ApplySampledObjects (cap 20) | **HIGH RISK** |
| `_documentSampleBuffer` | InventoryService | **instance; never Cleared** | ApplySampledObjects (cap 20); **aliased** to `report.DocumentSamples` | **HIGH RISK** |
| Local `report` | Run | per Run | entire pipeline | OK |
| Local `orgMap` / `walk` | Run | per Run | early stages | OK |

**STOP-class finding:** same window, second `Run` → buffers already full → History / SystemField / DocumentSamples may not refresh. Also `report.DocumentSamples = _documentSampleBuffer` shares the list reference.

## Timeout / Error Boundaries

| Operation | Timeout semantics | Error semantics | Current owner | Extraction risk |
|-----------|-------------------|-----------------|---------------|-----------------|
| Outer Run | none | catch → `BLOCKED_BY_SDK` | InventoryService | High if catch moved carelessly |
| States / Orgs / Persons / Docs / History zones | none (History sample ~8s inside service) | `FailZone` → zone Error; Run continues | InventoryService | Medium — keep FailZone policy |
| Search / hierarchy | walk budget | Partial Search zone | InventoryService | Medium |
| Per-type SampleType | **30s** `CallbackWaitSession` | TimedOut/Failed → type Partial + Warnings | SampleAllTypes | **High** — must stay with sampling owner |
| BIM Discover | internal 10s waits / Sleep poll | BIM owns try/Partial | BimDiscoveryService | Low if left delegated |
| Remarks | internal 20s | no local FailZone; can bubble outer | RemarkAnalyticsService | Medium |
| Capability matrix | none | sets FinalStatus / readiness | CapabilityMatrixService | Keep last |
| LoadChildren | **none** | log + partial onDone | InventoryService | Medium |
| GetRootObject | none | null + log | InventoryService | Low |
| Cancel checkpoints | n/a | `PARTIAL_RUNTIME_INVENTORY` | InventoryService | Keep order |

Decomposition must **not** blur who interprets TimedOut vs Failed vs Partial vs empty success (Stage 4 semantics).

## Report Data Flow

```
Input (mode, token, progress)
  → construct ProjectInventoryReport
  → SDK static inventory
  → type discovery
  → state discovery + mapping
  → org discovery → person discovery
  → search OR hierarchy walk
  → type sampling / enrichment (buffers filled)
  → state observed usage (needs samples)
  → document + history capabilities/samples
  → BIM discovery (uses walk)
  → remark analytics
  → assign DocumentSamples + system fields
  → aggregate counts
  → capability matrix / FinalStatus
  → performance notes
```

**Order dependencies that must not be reordered blindly:**

1. Orgs → Persons  
2. Types → Sample → State usage  
3. Walk before Sample (fallback) and BIM  
4. Buffers filled before History / SystemField / DocumentSamples  
5. Capability matrix last  

## Why InventoryService Is a God-Factory

Not merely “many `new`s.” Classified roles:

| Role | What it does | Complexity share |
|------|--------------|------------------|
| **Workflow orchestration** | Ordered multi-zone pipeline, cancel, Partial policy | **Main source** |
| **Composition / factory** | Constructs ~15 collaborators per Run + many scanners | High (noise) |
| **SDK boundary** | Holds Pilot/BIM services; structure APIs | Medium (appropriate) |
| **Report assembly** | Fills/aliases `ProjectInventoryReport` | Medium |
| **UI structure support** | `LoadChildren` / `GetRootObject` bolted onto same type | Medium coupling |
| **Sync/timeout policy** | Owns type-sample 30s; delegates others | Scattered |

**Main complexity source:** orchestration of the full discovery graph inside one type, with sampling buffers and structure browsing attached — not any single algorithm.

## Decomposition Options

| Option | Scope | Benefit | Risk | Behavior risk | Testability gain | Recommended? |
|--------|-------|---------|------|---------------|------------------|--------------|
| **A — Extract sampling coordinator** | `SampleAllTypes` + Apply/Record + buffers + CreatorAggregation call; Clear/copy buffers | Shrinks Run; isolates 30s timeout; fixes sticky buffers | Medium wiring | Medium if Clear/copy wrong; rescan output may **improve** vs today | High (buffer tests without Pilot) | **YES — Stage 6.2** |
| **B — Move entire Run to InventoryPipeline** | Full Run body; façade keeps structure APIs | Clear orchestrator type | Large diff; reorder risk | Medium | Medium | Later (6.3+) |
| **C — Only reuse scanner + Clear buffers** | InventoryService only | Low diff; fixes sticky samples | Does not reduce god-factory shape | Low–Med (observable rescan fix) | Needs multi-Run test | Partial; do **with A**, not alone |

## IInventoryService Decision

**Verdict: NOT NEEDED (now)** — **MAYBE LATER** after façade is thin.

**Why not now:** Public API is a façade over a god-factory. An interface would freeze `Run`/`LoadChildren`/`GetRootObject` without reducing internal coupling. Mocking the whole façade is not a sufficient reason. Revisit when sampling/pipeline are extracted and UI needs a narrow fake.

## PilotObjectScanner Mini-Audit

| Location | Count |
|----------|-------|
| `InventoryService` | **6** (Run×2 + LoadChildren×4 sites) |
| `BimDiscoveryService` | **3** |
| `RemarkAnalyticsService` | **1** |
| **Project total** | **10** |

- Constructor args are the same pattern: `(_repository, _search)`.
- Class is a **stateless façade**; repository cache is external.
- Multiple `new`s are cheap but noisy; LoadChildren can create up to 4 per expansion.
- Reuse is safe in principle; not the primary Stage 6 decomposition goal.
- Relates to Stage 6 as cleanup alongside sampling extract; full project-wide reuse can be separate.

## Static Logger Assessment

`AnalyticsLogger` used in InventoryService for progress, FailZone, sample warnings, structure/root errors, outer catch.

- Does **not** block decomposition.
- Not a unit-test blocker for buffer/sampling extract (characterization can avoid logger assertions).
- **Verdict: KEEP** now; **DEFER** abstraction; **REFACTOR LATER** only if alternate sinks needed.

## Risks

| Risk | Severity | Implication for Stage 6.2 |
|------|----------|---------------------------|
| Uncleared instance buffers + DocumentSamples alias | **HIGH** | Must Clear/copy when extracting sampling |
| Accidental reorder of discovery stages | HIGH | Preserve call order exactly |
| Blurring timeout ownership (30s sample) | HIGH | Keep Wait semantics with sampling owner |
| Changing public `Run`/`LoadChildren` signatures | HIGH | **Do not** — STOP if required |
| Async rewrite / GetResult / MEF / DI | — | Explicit non-goals |
| “Fixing” sticky buffer changes observable rescan data | MED | Document as bugfix if included; add regression test |

No STOP condition that blocks **starting** Stage 6.2 Option A — but Stage 6.2 must treat buffer lifetime as part of the extract, not accidental behavior change without tests.

## Recommended Stage 6.2

See dedicated section below.

## Deferred

- Full `InventoryPipeline` extract (Option B)
- `IInventoryService`
- MEF-export Inventory / Discovery
- DI framework / factory explosion
- `LoadChildren` rewrite / structure service
- Project-wide PilotObjectScanner reuse alone
- AnalyticsLogger DI
- Async GetResult rewrite
- UI / VM / localization / AnalyticsWindow changes

---

## Stage 6.2 Recommendation

### Change

Extract **object type sampling** from `InventoryService` into a dedicated coordinator type (e.g. `ObjectSamplingCoordinator` / `TypeInventorySampler`) that owns:

- `SampleAllTypes` / `ApplyWalkBucket` / `ApplySampledObjects` / creator-responsible recording  
- the three sample buffers  
- per-type `CallbackWaitSession` (30s) semantics  
- optional `CreatorAggregationService` call  

At the start of each sampling pass: **Clear** buffers. When assigning `DocumentSamples`, **copy** (or ToList) — do not alias the live buffer.

`Run` continues to orchestrate discovery stages and calls the coordinator once.

### Why first

- Smallest extract that removes the densest orchestration + timeout + mutable-state cluster  
- Directly addresses a proven lifetime bug (sticky buffers on rescan)  
- Improves testability without Pilot host  
- Does not require interfaces, MEF, UI, or async rewrite  
- Leaves BIM/Remark/matrix where they already are  

### Files likely affected

- `src/PilotBim.Analytics/Services/InventoryService.cs`
- New: `src/PilotBim.Analytics/Services/ObjectSamplingCoordinator.cs` (or under `Discovery/`)
- Tests: new focused tests for buffer clear / DocumentSamples copy; keep `InventoryServiceObjectCountTests`

### Expected production behavior

**UNCHANGED** for first-scan path and zone/timeout semantics.  
**Possible intentional fix:** second scan in the same window refreshes History/SystemField/Document samples (today sticky) — must be covered by a test and called out in the Stage 6.2 commit.

### Expected test strategy

1. Keep existing `ResolveObjectCount` characterization tests.  
2. Add deterministic tests: buffers cleared between passes; `DocumentSamples` is not the same list instance as the internal buffer.  
3. No real Pilot runtime required for those tests.  
4. Manual smoke: two scans in one Inventory window → samples refresh.

### Explicit non-goals

- No `IInventoryService`  
- No MEF / DI framework changes  
- No moving BIM / Remark / CapabilityMatrix into the new type  
- No `LoadChildren` rewrite  
- No AnalyticsLogger changes  
- No AnalyticsWindow / ViewModel / XAML changes  
- No timeout value changes  
- No Stage 6.3 pipeline extract in the same commit  
