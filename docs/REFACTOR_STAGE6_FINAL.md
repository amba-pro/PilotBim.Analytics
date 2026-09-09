# Stage 6 Final

Date: 2026-09-09  
Code HEAD: `88a3a82` (Stage 6.3b)  
Docs: Stage 6.4 finalize InventoryService review

## Goal

Reduce InventoryService responsibility safely: understand first, extract the densest sampling cluster, then fix confirmed sample-buffer lifecycle bugs — without MEF/DI/UI/async rewrites.

## Before

- `InventoryService` ~605 LOC god-factory / orchestrator  
- Sampling, discovery composition, structure APIs, and report assembly in one type  
- Instance sample buffers never cleared across Rescans  
- `DocumentSamples` aliased the live working list  
- No `ObjectSamplingCoordinator`  
- Tests: 100 PASS  

## After

- `InventoryService` ~366 LOC — primarily ordered `Run` orchestration + structure helpers  
- Sampling in `ObjectSamplingCoordinator`  
- Buffers cleared at each `SampleAllTypes` start; DocumentSamples published via `ToList()` snapshot  
- Tests: **102** PASS  
- MEF / DI / UI / timeouts / CallbackWaitSession: unchanged architecture  

## ObjectSamplingCoordinator

`internal sealed` type owning:

- type walk vs search sampling  
- per-type `CallbackWaitSession` (30s)  
- attribute/document profiling into caller-owned buffers  
- optional creator aggregation  

Buffers remain owned by `InventoryService`; coordinator receives references.

## Correctness Fixes

| Issue | Status |
|-------|--------|
| Sticky sample buffers across Runs | **Fixed** (Clear at sampling cycle start) |
| DocumentSamples collection alias | **Fixed** (`PublishDocumentSamples` → `ToList`) |
| Late callback vs Clear | **Safe** (`ShouldAccept` after Abandon/Dispose) |

## InventoryService Remaining Responsibilities

- SDK composition (ctor-injected Pilot/BIM services)  
- Ordered discovery pipeline in `Run`  
- Zone / cancel / FailZone bookkeeping  
- Call sampling coordinator  
- History/system/doc consume after sampling  
- BIM / Remark / CapabilityMatrix delegation  
- `LoadChildren` / `GetRootObject` for InventoryWindow  
- `ResolveObjectCount` / `PublishDocumentSamples` helpers  

## Remaining Coupling

| Item | Notes |
|------|--------|
| Many `new` discovery services in Run | Composition noise; stages already separate types |
| PilotObjectScanner multi-`new` | Stateless façade; cosmetic |
| Structure APIs on scan façade | Acceptable for plugin size |
| Static AnalyticsLogger | Cross-cutting; defer |
| No IInventoryService | Intentional |

## Deferred

- InventoryPipeline extract  
- IInventoryService  
- Structure service split  
- Scanner reuse cleanup  
- Logger abstraction  
- Async GetResult / UI / VM / localization / MEF expansion  

## Tests

| | |
|--|--|
| Before Stage 6 | 100 PASS |
| After Stage 6 | **102** PASS |
| Failed / skipped | 0 / 0 |

New: `SampleBufferLifecycleTests` (sticky + alias).

## Build

PASS — 0 errors, 0 warnings

## Behavioral Changes

| Path | Result |
|------|--------|
| First / single Run | Unchanged fill/timeout/SDK semantics |
| Repeated Run same instance | **Intentional correctness:** no stale samples |
| Finished report DocumentSamples | **Intentional correctness:** snapshot, not alias |

## Risks

| Risk | Mitigation |
|------|------------|
| Further pipeline extract reorders zones | Deferred — STOP Stage 6 |
| Scanner field reuse changes subtle identity | Deferred — no proven bug |
| IInventoryService freezes fat façade | Deferred until façade is thinner |

## Recommendation

**STOP_STAGE_6**

Stage 6 acceptance met: sampling extracted, buffer lifecycle correct, InventoryService readable enough. No further Stage 6 change has high immediate value without expanding into Stage 7+ work.
