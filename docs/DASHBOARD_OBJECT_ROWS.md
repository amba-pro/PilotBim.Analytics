# Dashboard Object Rows

Status: **BLOCKED_DB3_FULL_COVERAGE**  
Date: 2026-09-14  
No production ObjectRows implementation.

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

Until (2) is accepted as product cost, ObjectRows stay blocked.
