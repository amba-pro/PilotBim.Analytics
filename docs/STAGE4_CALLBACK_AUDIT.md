# Stage 4 — Callback / Timeout Audit

Date: 2026-09-08  
Baseline HEAD: `497d1eb` (post Stage 3)

SDK note: Pilot `ISearchService.Search` / `SubscribeObjects` / `GetHistoryItems` do **not** expose cancellable subscriptions in this plugin. After timeout we can only **abandon** results (stop accepting writes), not stop the SDK work.

| # | Location | Gate creator | SDK call | Callbacks / Set | Shared mutable state | Wait handled? | Dispose? | Abandon? | Late write risk | Stage 4 action |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `BimDiscoveryService.CountBimTypes` models | local | `SearchByType` | success+error → Set | `report.BimModelsCount`, `hadError` | yes → status | **no** | **no** | yes (count after timeout) | migrate + abandon |
| 2 | `BimDiscoveryService.CountBimTypes` parts | local | `SearchByType` | success+error → Set | `report.BimModelPartsCount` | yes → status | **no** | **no** | yes | migrate + abandon |
| 3 | `BimDiscoveryService.ProbeElements` search | local | `SearchByType` | success+error → Set | `modelId` | **ignored** | **no** | **no** | yes | migrate + abandon |
| 4 | `BimDiscoveryService` IsLoaded poll | n/a | `storage.IsLoaded` | n/a | n/a | Sleep×20 | n/a | n/a | n/a | **keep Sleep**; document (no readiness wait API) |
| 5 | `BimPartCatalogBuilder.TryAddPartsFromSearch` | local | `SearchByType` | success+error → Set | `map` | yes + abandon | **no** | yes | mitigated | migrate to session |
| 6 | `RemarkAnalyticsService.Analyze` | local | `SampleType` | success; error `_=>Set` **swallows** | `report.RemarkLinks`, counters | yes + abandon | **no** | yes | mitigated; **error lost** | migrate + log error |
| 7 | `CreatorAggregationService.Aggregate` | local | `SampleType` | success+error → Set | `report` counts, `returned` | yes + abandon | **no** | yes | mitigated | migrate to session |
| 8 | `HistoryDiscoveryService.SampleHistory` | local | `GetHistoryItems` | OnNext/OnError/OnCompleted | `target`, `pending` | yes + abandon | **no** | yes (custom) | mitigated | migrate to session |
| 9 | `InventoryService.SampleAllTypes` | local | `SampleType` | success+error → Set | `typeRecord`, buffers | yes Partial | **no** | **no** | **high** | migrate + abandon |
| 10 | `PilotObjectScanner.SubscribeObject` | using | `SubscribeObjects` | OnNext/Completed/Error | `loaded` | ignored bool | **yes** | **no** | yes (write after dispose race) | abandon + safe Set |
| 11 | `PilotObjectScanner.SubscribeObjects(batch)` | using | `SubscribeObjects` | OnNext/Completed/Error | `remaining` | ignored bool | **yes** | **no** | yes | abandon + safe Set |
| 12 | `BimIndexAnalyticsService` | n/a | `AddModelPartAsync` | sync `.GetResult()` | n/a | n/a | n/a | n/a | deadlock if UI sync-ctx | **DEFER** (worker-only today) |
| 13 | `RemarkAnalyticsService` index open | n/a | `GetModelPartsSearchServiceAsync` + `AddModelPartAsync` | `.GetResult()` | n/a | n/a | n/a | n/a | same | **DEFER** |

## Shared semantics (sites 1–9, SampleType/SearchByType pattern)

Identical enough for one helper:

1. Create gate  
2. Start SDK async  
3. Callback mutates owned state then Set  
4. Wait(timeout)  
5. On timeout → abandon further mutation  
6. Distinguish Completed / TimedOut / Failed  

## Helper decision

Introduce `CallbackWaitSession` (+ status enum) wrapping:

- `ManualResetEventSlim` with safe Signal after dispose  
- abandon flag (`AsyncCallbackGuard`)  
- `Wait` → `Completed | TimedOut | Failed | Cancelled`  
- `using` disposal **after** Wait returns (abandon first)

## Explicit non-goals / deferred

- Full async rewrite of BIM index `.GetAwaiter().GetResult()` chain  
- Replacing `Thread.Sleep` IsLoaded poll without a better SDK signal (documented keep)  
- Fake SDK cancellation
