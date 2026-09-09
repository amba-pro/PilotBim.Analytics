# Stage 7 Final

Date: 2026-09-09  
Code HEAD: `6aa529b`  
Docs: Stage 7.4 finalize analytics UI review

## Goal

Reduce AnalyticsWindowViewModel responsibility via safe presentation extractions without XAML rewrite, Window changes, MEF/DI, or behavior changes.

## Baseline

| Check | Result |
|-------|--------|
| HEAD | `6aa529b` |
| origin/main | `b8df8f3` (local ahead by Stage 6–7 commits) |
| Working tree | clean |
| Build | PASS, 0 errors / 0 warnings |
| Tests | 111 PASS |

## Before

- `AnalyticsWindowViewModel` ~984 LOC god-object  
- Owned scan/compare/history, dashboard CRUD/persist, charts, BIM filter, nav/progress  
- Field-new: ChartDataService, DashboardLayoutStore, ScanSnapshotStore, ScanDiffService  
- XAML ~583 LOC / Window ~293 LOC with ~150+ bindings to root VM  

## After

- `AnalyticsWindowViewModel` **~637 LOC** (−~35%)  
- Scan/compare → `AnalyticsScanComparePresenter` (~271)  
- Dashboard → `AnalyticsDashboardPresenter` (~272)  
- Root VM retains façades; XAML and Window **unchanged**  
- Tests **111** PASS  
- Structural behavior **unchanged**  

## AnalyticsScanComparePresenter

Owns scan stores/services, baseline, history/diff OCs, Refresh/Compare/Save/Delete, changes-only filter.  
PropertyChanged via `Action<string>` to root VM.

## AnalyticsDashboardPresenter

Owns layout store/state, layout/widget OCs, CRUD/persist, widget content fill.  
Chart/snapshot/filter supplied via `DashboardContentContext` from root VM (ChartDataService stays on VM).

## Root ViewModel

Still owns:

- Snapshot and table OC reload cascade  
- Chart tab/builder wiring over `ChartDataService`  
- BIM model filter caches  
- Navigation / progress / busy / scan-mode radios  
- Thin façades to scan + dashboard presenters  

## XAML Contract

**Preserved.** Binding paths for scan and dashboard properties still exposed on root VM.

## Window Code-Behind

**Unchanged** through Stage 7 (ShowPanel sibling panels, Refresh/Export/dialogs intact).

## Tests

| | |
|--|--|
| Before Stage 7 | 102 |
| After Stage 7 | **111** |
| Added | ScanCompare + Dashboard presenter tests |

## Build

PASS — 0 errors, 0 warnings

## Behavioral Changes

Structural stages 7.2–7.3: **UNCHANGED** observable plugin behavior.  
Stage 7.4: docs only — **no** production behavior change.

## Remaining Coupling

| Item | Notes |
|------|--------|
| Chart wiring in root VM | Thin over tested `ChartDataService`; shared with dashboard context |
| BIM filter | Shared by charts + dashboard; visibility quirk deferred |
| ShowPanel / 18 panels | Architecture debt; XAML-heavy to change |
| Refresh_Click orchestration | Stays in Window by design for Stage 7 |

## Deferred

- Chart presenter  
- BIM filter extract / `ShowBimModelFilter` vs Window visibility cleanup  
- Section UserControl / navigation redesign  
- ICommand migration  
- Localization / MEF expansion / DI  

## Risks

| Risk | Mitigation |
|------|------------|
| Further chart extract becomes plumbing | Documented NOT_NEEDED_NOW |
| Accidental XAML rewrite for sections | Explicitly deferred |
| Visibility duplicate | Deferred; not treated as Stage 7 fix |

## Recommendation

**STOP_STAGE_7**

Largest coherent presentation domains (scan/compare, dashboard) are extracted with zero XAML impact. Remaining chart/BIM/nav work is either already service-backed, shared-context-heavy, or requires UI architecture changes outside Stage 7 scope.
