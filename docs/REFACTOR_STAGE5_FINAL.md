# Stage 5 Final

Date: 2026-09-08  
Final HEAD (code): `5d8da52`  
Docs commit: Stage 5.4 finalize dependency review

## Goal

Controlled mechanical decoupling of UI from the two highest-risk analytics business services, without rewriting architecture, MEF, DI, ViewModel, or analytics behavior.

## Before

```
Pilot MEF → AnalyticsCommandService
  → new AnalyticsWindow(inventory)
       handlers: new ProjectAnalyticsService().Build(...)
                 new AnalyticsCsvExporter().Export(...)
```

- Zero project-owned service interfaces
- Analytics View owned concrete construction of transform + CSV export
- Inventory / Discovery remained a large manual `new` graph (unchanged by Stage 5)

## After

```
Pilot MEF → AnalyticsCommandService  (composition root)
  → new ProjectAnalyticsService()
  → new AnalyticsCsvExporter()
  → new AnalyticsWindow(inventory, IProjectAnalyticsService, IAnalyticsCsvExporter)
       handlers: _projectAnalytics.Build(...)
                 _csvExporter.Export(...)
```

- Two minimal public seams; concretes stay `internal`
- MEF graph unchanged
- No DI container
- UI/XAML unchanged
- Observable plugin behavior unchanged

## Interfaces Added

| Interface | Members | Implementation | Consumer |
|-----------|---------|----------------|----------|
| `IProjectAnalyticsService` | `Build(ProjectInventoryReport)` | `ProjectAnalyticsService` | `AnalyticsWindow` |
| `IAnalyticsCsvExporter` | `Export(ProjectAnalyticsSnapshot)` | `AnalyticsCsvExporter` | `AnalyticsWindow` |

`IInventoryService`: **not** introduced.

## Composition Root

`AnalyticsCommandService` — creates inventory, analytics/CSV concretes, and windows. MEF stops here.

## Direct Dependencies Removed

| From | Removed |
|------|---------|
| `AnalyticsWindow` Refresh handler | `new ProjectAnalyticsService()` |
| `AnalyticsWindow` ExportCsv handler | `new AnalyticsCsvExporter()` |

## Dependencies Intentionally Kept

| Dependency | Reason |
|------------|--------|
| Windows → `InventoryService` concrete | God-factory; interface without decompose hides the problem |
| VM field-`new` Chart/Diff/Stores | Pure / disk helpers; no substitution need |
| InventoryWindow → report helpers `new` | Secondary surface; low risk |
| InventoryService → discovery/`PilotObjectScanner` `new` | Internal pipeline; Stage 6+ |
| Static `AnalyticsLogger` | Cross-cutting file log; no alternate sink |
| Optional Pilot `GetServices<T>` at command service | Legitimate host locator for BIM optionals |

## Deferred

- InventoryService decomposition
- `IInventoryService` (after decompose)
- PilotObjectScanner instance reuse / sprawl cleanup
- Static AnalyticsLogger abstraction
- Async `.GetAwaiter().GetResult()` rewrite
- AnalyticsWindowViewModel / Window split
- Localization
- Deeper MVVM (move Build/Export orchestration out of View)
- Mirror injection for InventoryWindow report helpers

## Tests

| | |
|--|--|
| Before Stage 5 | 100 PASS |
| After Stage 5.3 / 5.4 | 100 PASS |
| Failed / skipped | 0 / 0 |

## Build

PASS — 0 errors, 0 warnings

## Behavior Compatibility

**UNCHANGED** — Build/Export implementations untouched; only construction/injection moved.

## Risks

| Risk | Mitigation |
|------|------------|
| Public interfaces widen API surface | Concretes remain internal; contracts are one method each |
| Inventory still concrete in Windows | Documented; do not fake-interface |
| Scanner multi-`new` | Deferred mechanical cleanup, not Stage 5 |

## Recommendation

**STOP_STAGE_5**

Stage 5 acceptance criteria met. Next work (if any) requires separate confirmation for Stage 6+ (dedup / inventory structure / VM), not more Stage 5 interface churn.
