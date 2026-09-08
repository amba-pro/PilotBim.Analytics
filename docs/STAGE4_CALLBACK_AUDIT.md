# Stage 4 — Callback / Timeout Audit

Date: 2026-09-08  
Baseline HEAD: `497d1eb` (post Stage 3)

SDK note: Pilot `ISearchService.Search` / `SubscribeObjects` / `GetHistoryItems` do **not** expose cancellable subscriptions in this plugin. After timeout we can only **abandon** results (stop accepting writes), not stop the SDK work.

| # | Location | Gate creator | SDK call | Callbacks / Set | Shared mutable state | Wait handled? | Dispose? | Abandon? | Late write risk | Stage 4 action |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `BimDiscoveryService.CountBimTypes` models | local | `SearchByType` | success+error → Signal | `report.BimModelsCount` | TimedOut/Failed → status | **yes** (session) | **yes** | mitigated | **DONE** CallbackWaitSession |
| 2 | `BimDiscoveryService.CountBimTypes` parts | local | `SearchByType` | success+error → Signal | `report.BimModelPartsCount` | TimedOut/Failed → status | **yes** | **yes** | mitigated | **DONE** |
| 3 | `BimDiscoveryService.ProbeElements` search | local | `SearchByType` | success+error → Signal | `modelId` | TimedOut logged | **yes** | **yes** | mitigated | **DONE** |
| 4 | `BimDiscoveryService` IsLoaded poll | n/a | `storage.IsLoaded` | n/a | n/a | Sleep×20 | n/a | n/a | n/a | **KEPT** + comment (no readiness wait API) |
| 5 | `BimPartCatalogBuilder.TryAddPartsFromSearch` | local | `SearchByType` | success+error → Signal | `map` | TimedOut/Failed | **yes** | **yes** | mitigated | **DONE** |
| 6 | `RemarkAnalyticsService.Analyze` | local | `SampleType` | SignalCompleted / SignalFailed | `report.RemarkLinks`, counters | TimedOut/Failed logged | **yes** | **yes** | mitigated | **DONE** (error no longer swallowed) |
| 7 | `CreatorAggregationService.Aggregate` | local | `SampleType` | SignalCompleted / SignalFailed | `report` counts | TimedOut/Failed | **yes** | **yes** | mitigated | **DONE** |
| 8 | `HistoryDiscoveryService.SampleHistory` | local | `GetHistoryItems` | OnNext/OnError/OnCompleted | `target`, `pending` | TimedOut/Failed | **yes** | **yes** | mitigated | **DONE** |
| 9 | `InventoryService.SampleAllTypes` | local | `SampleType` | SignalCompleted / SignalFailed | `typeRecord`, buffers | TimedOut → Partial | **yes** | **yes** | mitigated | **DONE** |
| 10 | `PilotObjectScanner.SubscribeObject` | session | `SubscribeObjects` | OnNext/Completed/Error | `loaded` | Wait; abandon on exit | **yes** | **yes** | mitigated | **DONE** |
| 11 | `PilotObjectScanner.SubscribeObjects(batch)` | session | `SubscribeObjects` | OnNext/Completed/Error | `remaining` | Wait; abandon on exit | **yes** | **yes** | mitigated | **DONE** |
| 12 | `BimIndexAnalyticsService` | n/a | `AddModelPartAsync` | sync `.GetResult()` | n/a | n/a | n/a | n/a | deadlock if UI sync-ctx | **DEFERRED** (worker-only today) |
| 13 | `RemarkAnalyticsService` index open | n/a | `GetModelPartsSearchServiceAsync` + `AddModelPartAsync` | `.GetResult()` | n/a | n/a | n/a | n/a | same | **DEFERRED** |

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
