# Stage 9 Final

Date: 2026-09-11  
HEAD: Stage 9.3 harden final callback races

## Goal

Close Stage 9 by auditing remaining technical debt (9.1), fixing the only MUST_FIX lifetime defect (9.2), and hardening the last two production callback-race SHOULD_FIX findings (9.3) — without starting Stage 10 or expanding into maintenance/defer items.

## Baseline

| Item | Value |
|------|-------|
| After Stage 8.5 | 118 PASS, 0 errors / 0 warnings |
| After Stage 9.1 | Audit only — no code change |
| After Stage 9.2 | 127 PASS (TD-22) |
| After Stage 9.3 | 138 PASS (TD-25 + TD-14) |
| Stage 10 blockers | **0** |

## Fixed Findings

### TD-22 — Scan CancellationTokenSource lifetime

**FIXED_IN_STAGE_9_2**

- `ScanSessionScope` with versioned `ScanSession`
- Analytics/Inventory windows: Begin / Complete / Cancel / OnClosed Dispose
- Closing the window or starting a new scan cancels prior work

### TD-25 — Post-timeout sample callback mutates report lists

**FIXED_IN_STAGE_9_3**

- Evidence: `ObjectSamplingCoordinator` checked `ShouldAccept()` once, then `ApplySampledObjects` could keep mutating after timeout abandon
- Fix: session passed into `ApplySampledObjects`; per-object / pre-finalize re-checks; error/catch paths gated
- Tests: `SampleCallbackRaceTests`

### TD-14 — `LoadChildren` completion not one-shot

**FIXED_IN_STAGE_9_3**

- Evidence: last-child + `OnCompleted` + `OnError` could each call `onDone`
- Fix: `OneShotCallback.Wrap` at `LoadChildren` entry
- Tests: `OneShotCallbackTests`

## Remaining SHOULD_FIX

| ID | Title | Classification |
|----|-------|----------------|
| TD-38 | Duplicated `IsRemarkType` | OPTIONAL_MAINTENANCE |
| TD-08 | Chart geometry constants duplicated | OPTIONAL_MAINTENANCE |
| TD-06 | BIM filter visibility duplication | OPTIONAL_MAINTENANCE |
| TD-11 | Snapshot reload chart/dashboard churn | DEFER_TO_POST_RUNTIME (needs VM seam/tests) |
| TD-41 | Non-hermetic store tests / parallel flake | OPTIONAL_MAINTENANCE |
| TD-40 | Missing VM/coordinator/scanner/geometry tests | DEFER_TO_POST_RUNTIME |
| TD-03 | Unbounded `analytics.log` / per-line open | OPTIONAL_MAINTENANCE |
| TD-02 | Undocumented UI-thread invariant for `Run` | OPTIONAL_MAINTENANCE |
| TD-30 | AnalyticsWindow third null-check | OPTIONAL_MAINTENANCE |
| TD-37 | `"bimObjectId"` literal ×3 | OPTIONAL_MAINTENANCE |
| TD-48 | Stale csproj Description/Version | OPTIONAL_MAINTENANCE |

## Remaining DEFER

Representative (full list in `STAGE9_TECHNICAL_DEBT_AUDIT.md`):

- TD-01 sync-over-async BIM bridges  
- TD-23 / TD-24 SDK disposable release (needs Pilot runtime verification)  
- TD-13 VM ctor disk I/O seam  
- TD-28 / TD-33 dual-use display/logic strings  
- TD-05 ShowPanel sibling panels, localization remainder, scanner architecture, InventoryService composition  

## Stage 10 Blockers

**0**

## Runtime Risks Remaining

Evidence-based only:

1. **SDK subscription lifetime (TD-24)** — `SubscribeObjects` disposables still discarded; late callbacks rely on `ShouldAccept` / one-shot latches. Harmless when guards hold; residual observer retention until host session ends is unverified against Pilot.
2. **`IModelSearchService` not released (TD-23)** — same class of SDK lifetime uncertainty; no crash evidence in Stages 0–9.
3. **Sticky sample buffers** — Stage 6 documented sticky semantics remain intentional; not a Stage 9 regression.
4. **Timeout + partial type records** — after TD-25, late mutations stop; Partial/timeout status path unchanged. Rare timeout coinciding with a long callback still drops *remaining* late writes by design.

No remaining MUST_FIX correctness defects identified in the Stage 9 audit after 9.2/9.3.

## Recommendation

**STOP_STAGE_9**  
**READY_FOR_STAGE_10**

Do not start Stage 10 until explicitly confirmed. Remaining SHOULD_FIX items are maintenance or post-runtime optional work — none block a Stage 10 planning pass.
