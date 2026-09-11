# PilotBim.Analytics Refactor Final Report

Date: 2026-09-11  
Repository HEAD at Stage 10 docs commit (builds on `489a151` Stage 9.3)  
Scope of Stage 10: **verification and documentation only** — no architecture refactor, no SHOULD_FIX fixes, no production code changes.

## Executive Summary

Stages 0–9 delivered correctness hardening, characterization tests, controlled extractions seams (services, presenters, resources), and callback/cancellation lifetime fixes while preserving Pilot MEF/`Private=false` host-SDK loading. Stage 10 audited the Release artifact, MEF/SDK boundary, embedded resources, repository hygiene, diagnostics readiness, and residual debt. Clean rebuild + full test suite: **138 PASS**, **0** errors, **0** warnings. The build is ready for **manual Pilot-BIM runtime validation**, not for an unsupervised production declaration.

## Final Verdict

**READY_FOR_PILOT_RUNTIME_VALIDATION**

Not claimed: **PRODUCTION_READY** (requires real Pilot-BIM execution per `docs/PILOT_RUNTIME_VALIDATION_CHECKLIST.md`).

## Project Baseline

| Item | Value |
|------|-------|
| Branch | `main` |
| Stage 9.3 HEAD | `489a151` — Stage 9.3: harden final callback races |
| Working tree at Stage 10 start | clean |
| Origin sync note | Local was **ahead 2** (9.2 + 9.3) at Stage 10 start; Stage 10 instructions forbid push — sync is an operator action |
| Target framework | .NET Framework **4.7.2** |
| Assembly name | `PilotBim.Analytics.ext2` |
| PlatformTarget | **AnyCPU** (MSIL); `Prefer32Bit` not set → default **false** |
| Tests | **138** Fact / 0 Theory / 0 failed / 0 skipped |
| Stage 10 blockers | **0** |
| MUST_FIX remaining | **0** |

## Stages Completed

| Stage | Focus | Outcome |
|-------|-------|---------|
| 0–1 | Dead code / baseline inventory | Clean start for characterization |
| 2 | Characterization tests | ~61 PASS foundation |
| 3 | Correctness fixes (budgets, continue, counts, clamp, GlobalId, …) | ~93 PASS |
| 4 | `CallbackWaitSession` / timeout / late-callback safety | ~100 PASS |
| 5 | Minimal service abstractions; AnalyticsWindow inject | Interfaces kept thin; MEF unchanged |
| 6 | `ObjectSamplingCoordinator`; sample buffer lifecycle | Sticky-buffer bug fixed; STOP_STAGE_6 |
| 7 | Scan + dashboard presenters | VM reduced; STOP_STAGE_7 |
| 8 | Resources.resx chrome localization | Display-only; STOP_STAGE_8 |
| 9.1 | Tech-debt audit | Docs only |
| 9.2 | TD-22 scan CTS / window-close cancel | 127 PASS |
| 9.3 | TD-25 late mutation + TD-14 one-shot `LoadChildren` | 138 PASS; Stage 9 closed |
| 10 | Release audit + runtime checklist + this report | Docs only |

## Architecture After Refactor

- **Host plugin:** MEF `IDataPlugin` + menu/toolbar exports; SDK refs `Private=false`.
- **Scan façade:** `InventoryService.Run` pipeline with zone try/catch; sampling in `ObjectSamplingCoordinator`.
- **Analytics UI:** `AnalyticsWindow` + ViewModel; scan-compare and dashboard presenters; chart canvas.
- **Persistence:** snapshot/history/diff stores under LocalAppData; dashboard layout JSON.
- **Localization:** checked-in `Properties/Resources.resx` + Designer; generator disabled; no satellites / no runtime culture switch.
- **Sync-over-async BIM bridges:** retained (deferred TD-01); worker-thread usage assumed.

Architecture is **ACCEPTABLE** for runtime validation. Further extraction is optional maintenance, not a gate.

## Correctness Fixes

Major fixes retained through Stage 9 (non-exhaustive):

| Area | Fix |
|------|-----|
| Creator sampling | Budget / aggregation correctness |
| Document discovery | Continue / sample population paths |
| Callbacks | `CallbackWaitSession`; abandon on timeout; `ShouldAccept` before mutate |
| Capability status | Timeout ≠ Available (Partial + warnings) |
| Counts | `long` object counts; `ResolveObjectCount` |
| Percents | Clamp / safe formatting |
| CSV / diagnostics numbers | Invariant culture on persisted/export paths |
| BIM identity | GlobalId priority where applicable |
| Sample buffers | Stage 6.3 lifecycle — no stale cross-scan aliasing |
| Scan CTS | Stage 9.2 `ScanSessionScope` — cancel on close / re-entry |
| Late sample mutation | Stage 9.3 re-check inside `ApplySampledObjects` |
| LoadChildren completion | Stage 9.3 `OneShotCallback` at most once |

## Test Evolution

| Milestone | Tests (PASS) |
|-----------|-------------:|
| Initial | 0 |
| Stage 2 | 61 |
| Stage 3 | 93 |
| Stage 4 | 100 |
| Stage 5 | 100 |
| Stage 6 | 102 |
| Stage 7 | 111 |
| Stage 8 | 118 |
| Stage 9.2 | 127 |
| Stage 9.3 / Stage 10 | **138** |

Test projects: **1** (`PilotBim.Analytics.Tests`). Test files: **24**.

Scenario coverage (not line coverage): ChartDataService, ScanDiffService, DashboardLayoutStore, ScanSnapshotStore, AnalyticsCsvExporter, StateMappingService, ProjectAnalyticsService, ReferenceResolver, CallbackWaitSession, sample buffer lifecycle, ScanSessionScope lifetime, OneShotCallback / sample-callback race characterization, scan-compare + dashboard presenters, localization resources, BIM/document/creator/history discovery helpers, formats, object-count resolver.

## Final Build

| Check | Result |
|-------|--------|
| `scripts\build.cmd` | PASS — 0 errors / 0 warnings |
| Clean rebuild (delete bin/obj/dist → build) | PASS — 0 errors / 0 warnings |
| `scripts\test.cmd` after clean | **138 PASS**, 0 failed, 0 skipped |
| `dist` equals `bin\Release` hash after successful copy | PASS |

**Operator note:** loading the DLL via reflection in the same process can lock `dist\*.dll` and make `copy` fail silently (`>nul` in `build.cmd`). Close Pilot / avoid in-process LoadFile when verifying dist; confirm `dist` timestamp matches `bin\Release`.

## Final Artifact

| Field | Value |
|-------|-------|
| Path | `dist\PilotBim.Analytics.ext2.dll` |
| Dist contents | **Only** that DLL (EXPECTED) |
| Size (clean Release) | **361984** bytes |
| Assembly name | `PilotBim.Analytics.ext2` |
| Assembly version | `0.9.0.0` |
| File version | `0.9.0` |
| Informational / product version | `0.9.0-rich-diff-to-dashboard` |
| Target framework (attribute) | `.NETFramework,Version=v4.7.2` |
| Processor architecture | **MSIL** (AnyCPU) |
| Prefer32Bit | unset → **false** |
| Embedded resources | `PilotBim.Analytics.Properties.Resources.resources` **YES**; also WPF `PilotBim.Analytics.ext2.g.resources` |
| Satellite assemblies | **None** |
| PDB in dist | **No** (pdb only under bin — EXPECTED) |

### Version consistency

| Source | Value | Note |
|--------|-------|------|
| csproj Version / FileVersion | 0.9.0 | Matches assembly |
| InformationalVersion | `0.9.0-rich-diff-to-dashboard` | Logged on plugin load; **stale label** (TD-48) — not a runtime blocker |
| Description | “Stage 1 + …” | Stale metadata (TD-48) — OPTIONAL post-runtime |

**No version bump in Stage 10** (not a confirmed blocker).

## MEF / SDK Boundary

### MEF exports (PASS)

| Export | Type |
|--------|------|
| `IDataPlugin` | `Extension` (single plugin entry) |
| `IToolbar<ObjectsViewContext>` | `AnalyticsToolbarCommand` |
| `IMenu<ObjectsViewContext>` | `AnalyticsContextMenuCommand` |
| `IMenu<MainViewContext>` | `AnalyticsMainMenuCommand`, `AnalyticsOverviewMenuCommand` (two intentional menu commands) |
| `[Export]` concrete | `AnalyticsCommandService` (MEF injection target) |

Importing constructors intact. No duplicate `IDataPlugin`. Contracts not renamed in Stage 10.

### Pilot SDK references (PASS)

| Assembly | HintPath | Private |
|----------|----------|---------|
| `Ascon.Pilot.SDK` | `$(PilotSdkPath)\Ascon.Pilot.SDK.dll` | **false** |
| `Ascon.Pilot.Bim.SDK` | `...\extensions\bim\Ascon.Pilot.Bim.SDK.dll` | **false** |
| `Ascon.Pilot.Bim.Search.SDK` | `...\extensions\bim\Ascon.Pilot.Bim.Search.SDK.dll` | **false** |

Host machine sample versions observed at audit time: SDK `25.9.0.55929`, Bim.SDK `25.9.0.18606`, Bim.Search.SDK `25.7.52.0` (unversioned refs / `SpecificVersion=false` — TD-46 deferred).

**Unexpected SDK DLLs in dist:** **NO**.

### Public API (intentional)

- Stage 5: `IProjectAnalyticsService`, `IAnalyticsCsvExporter`
- MEF command/plugin types, WPF windows/controls, `InventoryService`, `Properties.Resources`
- Large `Models/*` surface for JSON/XAML (known; TD-45 DEFER)
- `InternalsVisibleTo`: **only** `PilotBim.Analytics.Tests`

No Stage 10 public API growth.

### Resources (PASS)

- `Resources.resx` + checked-in `Resources.Designer.cs`
- Generator disabled in csproj (empty Generator / LastGenOutput)
- Embedded name confirmed in DLL
- No duplicate Designer generation path observed

## Localization State

Display chrome partially migrated to resx (Stage 8). No runtime culture switching. Dual-use Russian logic keys (ScanDiff Area/Metric, KPI labels, etc.) intentionally **not** localized. Additional languages: not added.

## Remaining Technical Debt

### MUST_FIX

**0**

### SHOULD_FIX → Stage 10 classification

| ID | Classification |
|----|----------------|
| TD-38 IsRemarkType duplication | OPTIONAL |
| TD-08 chart 420/220 constants | OPTIONAL |
| TD-06 BIM filter visibility duplication | OPTIONAL |
| TD-11 snapshot rebuild churn | POST_RUNTIME_MAINTENANCE |
| TD-41 store test hermeticity | OPTIONAL |
| TD-40 VM/coordinator/scanner test gaps | POST_RUNTIME_MAINTENANCE |
| TD-03 unbounded analytics.log | WATCH_IN_RUNTIME |
| TD-02 undocumented UI-thread invariant | OPTIONAL |
| TD-30 third null-check | OPTIONAL |
| TD-37 bimObjectId literal | OPTIONAL |
| TD-48 stale Description/Version label | OPTIONAL |

None promoted to Stage 10 blockers without new evidence.

## Deferred Items

Representative: TD-01 sync-over-async; TD-23/24 SDK disposable release; TD-13 VM ctor seam; TD-28/33 dual-use strings; TD-05 ShowPanel; further InventoryService/scanner architecture; CI / Directory.Build.props. Full list: `docs/STAGE9_TECHNICAL_DEBT_AUDIT.md`, `docs/REFACTOR_STAGE9_FINAL.md`.

## Runtime Validation Required

| Gap | Classification |
|-----|----------------|
| Actual Pilot SDK callbacks / observables | RUNTIME_REQUIRED |
| Real project object graph / DB | RUNTIME_REQUIRED |
| BIM discovery on real indexed models | RUNTIME_REQUIRED |
| MEF load inside Pilot-BIM host | RUNTIME_REQUIRED |
| Toolbar/menu visibility in host chrome | RUNTIME_REQUIRED |
| Full inventory + analytics scans | RUNTIME_REQUIRED |
| Cancellation against live SDK | RUNTIME_REQUIRED |
| Window close during live scan (TD-22) | RUNTIME_REQUIRED |
| Late callback after real timeout (TD-25) | RUNTIME_REQUIRED (may be NOT_REPRODUCED) |
| CSV export from live snapshot | RUNTIME_REQUIRED |
| Repeated scan same window (buffer lifecycle) | RUNTIME_REQUIRED |
| CallbackWaitSession / OneShot / ScanSessionScope unit behavior | ALREADY_AUTOMATED |
| Chart/diff/store/presenter pure logic | ALREADY_AUTOMATED |
| Cosmetic UI polish beyond smoke | OPTIONAL_RUNTIME |

## Runtime Checklist

See: [`docs/PILOT_RUNTIME_VALIDATION_CHECKLIST.md`](PILOT_RUNTIME_VALIDATION_CHECKLIST.md)

## Diagnostics Readiness

| Item | Detail |
|------|--------|
| Log directory | `%LOCALAPPDATA%\PilotBim.Analytics\Logs\` |
| Log file | `analytics.log` (UTF-8 append) |
| Levels | INFO / WARNING / ERROR / BLOCKED |
| Context | `area=` + message; exceptions include type + truncated message |
| Plugin identity | InformationalVersion on load |
| Timeouts | Logged under areas such as `sample-type`, `remark-sample`, … |
| Known limit | TD-03 unbounded growth; per-line open/append |

**Verdict: SUFFICIENT_FOR_RUNTIME** — enough to investigate load failures, open/scan errors, and timeout warnings during checklist execution.

## Release Limitations

1. Not validated inside Pilot-BIM in Stage 10.
2. SDK versions are host-provided and unpinned.
3. InformationalVersion / Description metadata are stale labels (TD-48).
4. Log file can grow without rotation (TD-03) — monitor during long sessions.
5. Some dual-use RU strings remain logic keys — do not “fix” during runtime triage without a dedicated stage.
6. Binary hash of incremental vs clean builds may differ; verify by version + tests + functional checklist, not bit-identity alone.

## Dist / Repository Hygiene

| Check | Result |
|-------|--------|
| Dist file set | EXPECTED: plugin DLL only |
| Unexpected SDK / test / pdb / xml in dist | NO |
| `.gitignore` covers bin/obj/dist/TestResults/*.user/*.dll/pdb | YES |
| Tracked build outputs / backups | NONE found |

## Recommended Next Action

**RUN_PILOT_RUNTIME_VALIDATION**

1. Push local Stage 9 (+ Stage 10 docs) to origin when approved.  
2. Deploy with `scripts\deploy-dev.ps1`.  
3. Execute `docs/PILOT_RUNTIME_VALIDATION_CHECKLIST.md`.  
4. Only after checklist sign-off, consider production packaging / version bump (TD-48) as a separate release chore.
