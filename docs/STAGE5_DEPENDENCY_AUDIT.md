# Stage 5.1 — Dependency Audit

Date: 2026-09-08  
Baseline HEAD: `a67437c` (Stage 4 complete)  
Scope: production code under `src/PilotBim.Analytics`  
Production code changes on this sub-stage: **none**

## Baseline (Stage 5.0)

| Check | Result |
|-------|--------|
| Working tree | clean |
| `HEAD` / `origin/main` | `a67437c` (synced) |
| Build | PASS |
| Errors / Warnings | 0 / 0 |
| Tests | 100 passed, 0 failed, 0 skipped |

## Composition overview

Pilot host MEF loads plugin exports. **Below `AnalyticsCommandService`, composition is manual `new`.**  
Project-owned service interfaces after Stage 5: **`IProjectAnalyticsService`**, **`IAnalyticsCsvExporter`** only.

```
Pilot MEF
  └─ AnalyticsCommandService  [Export]  ← composition root (in-plugin)
       ├─ MEF: IObjectsRepository, IPilotServiceProvider, ISearchService?
       ├─ locator: GetServices<IModelStorageProvider / IModelSearchManager>()
       ├─ new InventoryService(...)
       ├─ new AnalyticsWindow(inventory)
       │    ├─ new AnalyticsWindowViewModel()
       │    │    └─ field-new: ChartDataService, DashboardLayoutStore,
       │    │                  ScanSnapshotStore, ScanDiffService
       │    ├─ inventory.Run(...)
       │    ├─ inject IProjectAnalyticsService.Build(report)
       │    └─ inject IAnalyticsCsvExporter.Export(snapshot)
       └─ new InventoryWindow(inventory)
            ├─ new InventoryWindowViewModel()
            ├─ new InventoryReportService()
            └─ new InventoryReportExporter()
```

### MEF map

| Export | Contract | Imports |
|--------|----------|---------|
| `Extension` | `IDataPlugin` | none |
| `AnalyticsCommandService` | concrete `[Export]` | `IObjectsRepository`, `IPilotServiceProvider`, optional `ISearchService` |
| `AnalyticsMainMenuCommand` / `AnalyticsOverviewMenuCommand` | `IMenu<MainViewContext>` | `AnalyticsCommandService` |
| `AnalyticsContextMenuCommand` | `IMenu<ObjectsViewContext>` | `AnalyticsCommandService` |
| `AnalyticsToolbarCommand` | `IToolbar<ObjectsViewContext>` | `AnalyticsCommandService` |

Service-locator pattern exists **only** in `AnalyticsCommandService.TryGetService<T>` / probe helpers for optional Pilot BIM services. Appropriate for host-provided optionals; do not expand.

## Audit table

| Consumer | Dependency | Current coupling | Risk | Proposed abstraction | Reason |
|----------|------------|------------------|------|----------------------|--------|
| Menu/Toolbar commands | `AnalyticsCommandService` | MEF import concrete | L | KEEP | Single host façade |
| `AnalyticsCommandService` | SDK `IObjectsRepository`, `IPilotServiceProvider`, `ISearchService` | MEF + SDK interfaces | L | KEEP | Already abstracted at SDK boundary |
| `AnalyticsCommandService` | `IModelStorageProvider`, `IModelSearchManager` | Service locator `GetServices<T>()` | M | KEEP (document) | Optional Pilot services; same AllowDefault pattern as search |
| `AnalyticsCommandService` | `InventoryService` | `new InventoryService(...)` | H | **Maybe** `IInventoryService` | Sole scan orchestration entry; UI typed to concrete |
| `AnalyticsCommandService` | `AnalyticsWindow` / `InventoryWindow` | `new` + singleton-ish fields | M | KEEP | WPF shell; composition root owns windows |
| `AnalyticsWindow` | `InventoryService` | ctor concrete parameter | M | optional `IInventoryService` | Same as above; enables fake Run for UI tests |
| `AnalyticsWindow` | `AnalyticsWindowViewModel` | `new` in ctor | M | KEEP for Stage 5 / defer inject | Avoids large VM rewrite (Stage 8 deferred) |
| `AnalyticsWindow` | `ProjectAnalyticsService` | injected `IProjectAnalyticsService` | L | **DECOUPLED — Stage 5.3** | Concrete created in `AnalyticsCommandService` |
| `AnalyticsWindow` | `AnalyticsCsvExporter` | injected `IAnalyticsCsvExporter` | L | **DECOUPLED — Stage 5.3** | Concrete created in `AnalyticsCommandService` |
| `AnalyticsWindowViewModel` | `ChartDataService` | field `new` | L | KEEP concrete | Pure snapshot→chart; no substitution need |
| `AnalyticsWindowViewModel` | `DashboardLayoutStore` | field `new` | L–M | KEEP; optional inject later | Disk under LocalAppData |
| `AnalyticsWindowViewModel` | `ScanSnapshotStore` | field `new` | L–M | KEEP; optional inject later | Shared scan history on disk |
| `AnalyticsWindowViewModel` | `ScanDiffService` | field `new` | L | KEEP concrete | Pure diff math |
| `InventoryWindow` | `InventoryService` | ctor concrete | M | same as AnalyticsWindow | |
| `InventoryWindow` | `InventoryWindowViewModel` | `new` | L | KEEP | No service fields |
| `InventoryWindow` | `InventoryReportService` / `InventoryReportExporter` | `new` | L | KEEP | Text/file helpers |
| `InventoryService` | ~15 discovery/data types | all `new` inside Run/Sample/LoadChildren | H | DEFER bulk DI | God-object factory; per-type interfaces = noise |
| `InventoryService` | `PilotObjectScanner` | many independent `new` (incl. LoadChildren ×4+) | H | DEFER (reuse instance later) | Not an interface problem; construction sprawl |
| `BimDiscoveryService` | `BimIndexAnalyticsService`, `PilotObjectScanner` | nested `new` | M | KEEP | Internal BIM pipeline |
| `BimIndexAnalyticsService` | `BimPartCatalogBuilder` | `new` | L | KEEP | Builder + existing scanner param |
| `RemarkAnalyticsService` | `PilotObjectScanner`, `PilotObjectSampler` | `new` | L | KEEP | Local helpers |
| `CreatorAggregationService` | `PilotObjectSampler` | `new` | L | KEEP | |
| `TypeDiscoveryService` / `SystemFieldDiscoveryService` | `PilotSdkDiscoveryService` | `new` | L | KEEP | Reflection helper |
| All Discovery/Data/Services | `AnalyticsLogger` | static | M | KEEP static | Cross-cutting file log; no alternate sink |
| Discovery / inventory | `ReferenceResolver` | static helpers | L | KEEP | Pure value parsing |
| Pure transform services | models only | no SDK | L | KEEP concrete | Chart/Diff/ProjectAnalytics/CapabilityMatrix/StateMapping/CSV/Stores |

## Direct constructions of interest

### Views create business services

| File | Construction |
|------|----------------|
| `Views/AnalyticsWindow.xaml.cs` | `new AnalyticsWindowViewModel()`, `new ProjectAnalyticsService().Build`, `new AnalyticsCsvExporter().Export` |
| `Views/InventoryWindow.xaml.cs` | `new InventoryWindowViewModel()`, `new InventoryReportService()`, `new InventoryReportExporter()` |

### ViewModel creates services

| File | Construction |
|------|----------------|
| `ViewModels/AnalyticsWindowViewModel.cs` | field-init `ChartDataService`, `DashboardLayoutStore`, `ScanSnapshotStore`, `ScanDiffService` |

### Composition root

| File | Construction |
|------|----------------|
| `Plugin/Commands/AnalyticsCommandService.cs` | `new InventoryService(...)`, `new AnalyticsWindow(...)`, `new InventoryWindow(...)` |

### Service creates service (hub)

`Services/InventoryService.cs` constructs: `PilotSdkDiscoveryService`, `TypeDiscoveryService`, `StateDiscoveryService`, `StateMappingService`, `OrganisationDiscoveryService`, `PersonDiscoveryService`, `PilotObjectScanner` (many sites), `HierarchyWalkSampler`, `DocumentDiscoveryService`, `HistoryDiscoveryService`, `BimDiscoveryService`, `RemarkAnalyticsService`, `SystemFieldDiscoveryService`, `CapabilityMatrixService`, `AttributeDiscoveryService`, `PilotObjectSampler`, `CreatorAggregationService`.

Nested: `BimDiscoveryService` → `BimIndexAnalyticsService` → `BimPartCatalogBuilder`; Remark/Creator → scanner/sampler.

## Layer boundary notes

| Layer | SDK coupling | Notes |
|-------|--------------|-------|
| Plugin shell | High | MEF + Pilot menus; resolve services |
| `InventoryService` + Discovery + Data | High | Correct place for `Ascon.Pilot.*` |
| Analytics transform (`ProjectAnalyticsService`, Chart, Diff, Stores, CSV, Analytics VM) | **None / minimal** | Already SDK-free once `ProjectInventoryReport` exists |
| Inventory UI structure tree | Uses `IDataObject` | Expected for tree expansion |

## Criteria applied (when NOT to add an interface)

Interface only if at least one holds:

- substitution / testing of a side-effecting collaborator;
- boundary isolation (UI ↔ business);
- multiple implementations planned;
- MEF composition need;
- dependency inversion that removes View ownership of pipeline.

**Not** sufficient alone: “class exists”, “feels cleaner”, “might need later”.

## Stage 5.2 — Interfaces introduced

Status: **INTRODUCED — Stage 5.2**; consumer wiring completed in Stage 5.3

| Interface | Implementation | Minimal contract | Production consumer today | Status |
|-----------|----------------|------------------|---------------------------|--------|
| `IProjectAnalyticsService` | `ProjectAnalyticsService` | `ProjectAnalyticsSnapshot Build(ProjectInventoryReport report)` | `AnalyticsWindow` via injected field | **DECOUPLED — Stage 5.3** |
| `IAnalyticsCsvExporter` | `AnalyticsCsvExporter` | `string Export(ProjectAnalyticsSnapshot snapshot)` | `AnalyticsWindow` via injected field | **DECOUPLED — Stage 5.3** |
| `IInventoryService` | — | — | — | **DEFERRED / NOT INTRODUCED** |

### Why these interfaces are justified

- Real UI ↔ business boundary: View previously constructed and called both types.
- Small contracts matching actual consumer usage (one method each).

### Why IInventoryService is NOT introduced

`InventoryService` is a large façade/god-factory. An interface would not reduce internal coupling and would only hide the architecture problem. Defer until decomposition (later stage).

## Stage 5.3 — AnalyticsWindow rewiring

Status: **DECOUPLED — Stage 5.3**

### Before

```
AnalyticsWindow business logic
  -> new ProjectAnalyticsService().Build(...)
  -> new AnalyticsCsvExporter().Export(...)
```

### After

```
AnalyticsCommandService (composition root)
  -> new ProjectAnalyticsService()
  -> new AnalyticsCsvExporter()
  -> new AnalyticsWindow(inventory, IProjectAnalyticsService, IAnalyticsCsvExporter)

AnalyticsWindow event/business handlers
  -> _projectAnalytics.Build(...)
  -> _csvExporter.Export(...)
```

Concrete creation lives only in `AnalyticsCommandService.OpenAnalytics` (existing composition root).  
MEF graph unchanged. No DI framework. Build/Export orchestration remains in the Window (not moved to ViewModel).

### Remaining technical debt (post 5.3)

- `AnalyticsWindow` still takes concrete `InventoryService`
- Window still constructs `AnalyticsWindowViewModel` directly
- Build/Export still orchestrated in View code-behind (MVVM cleanup later)
- Inventory god-factory / scanner sprawl
- InventoryWindow still `new`s report helpers

## Recommended minimal set (historical — Stage 5.1 proposal)

| Candidate | Introduce? | Consumers | Implementation | Why concrete is worse | MEF impact |
|-----------|------------|-----------|----------------|----------------------|------------|
| `IProjectAnalyticsService` (`Build` only) | **Done — Stage 5.2+5.3** | `AnalyticsWindow` | `ProjectAnalyticsService` | View no longer constructs | None |
| `IAnalyticsCsvExporter` (`Export` only) | **Done — Stage 5.2+5.3** | `AnalyticsWindow` | `AnalyticsCsvExporter` | View no longer constructs | None |
| `IInventoryService` | **NOT INTRODUCED** | Windows + command | `InventoryService` | Façade/god-factory; interface hides problem | — |

**Prefer not to add:** Chart/Diff/Stores/Discovery/logger interfaces; factories (`I*Factory`); MEF-export of entire Discovery layer.

## Deferred

| Item | Why not Stage 5 |
|------|-----------------|
| `IInventoryService` | God-factory; interface does not reduce internal coupling |
| InventoryService decomposition | Separate stage |
| DI for all `InventoryService` children | Largest fan-out; needs orchestrator extract first, not 15 interfaces |
| MEF-export Discovery services | Host only needs menus + command façade |
| Project ports wrapping all `Ascon.Pilot.*` | SDK already interface-based; multi-host not planned |
| `AnalyticsLogger` DI | No alternate sink |
| Abstract factories / DI framework | Explicitly forbidden |
| Split `AnalyticsWindowViewModel` / rewrite Window | Stage 6–8 deferred |
| Deeper View orchestration / MVVM cleanup | Build/Export still in Window by design for 5.3 |
| Localization | Deferred |
| `GetAwaiter().GetResult()` async rewrite | Stage 4 deferred; not dependency inversion |
| Reuse single `PilotObjectScanner` in LoadChildren | Mechanical cleanup / micro-opt; optional later, not interface work |
| Inject Chart/Stores into VM | Optional testability; not required for UI/business decoupling goal |

## Global / static state inventory

| Kind | Location | Notes |
|------|----------|-------|
| Static logger | `AnalyticsLogger` | Locked append to analytics.log |
| Static helpers | `ReferenceResolver`, `AnalyticsFormats`, `AsyncCallbackGuard` | Pure / sync primitives |
| Disk stores | `%LocalAppData%\PilotBim.Analytics\` via layout/snapshot/export paths | Shared across sessions |
| Window singletons | `_catalogWindow` / `_analyticsWindow` on command service | One visible instance |

## Stage 5.4 gate

Closed by Stage 5 Final Assessment below. No further Stage 5 production refactor.

## Stage 5 Final Assessment

Date: 2026-09-08  
HEAD: `5d8da52`  
Re-audit: production Views / ViewModels / Services / Discovery / Data (no production code changed on 5.4)

### Coupling re-check

| Coupling point | Current state | Harmful? | Refactor now? | Reason |
|----------------|---------------|----------|---------------|--------|
| `AnalyticsWindow` → Build/Export | injected interfaces; concretes in command service | No | **No** | Goal of Stage 5 achieved |
| `AnalyticsCommandService` → `new ProjectAnalyticsService` / `AnalyticsCsvExporter` | composition root | No | **No** | Legitimate boundary |
| `AnalyticsCommandService` → `new InventoryService` | composition root | Low | **No** | Façade entry; interface would hide god-factory |
| Windows → concrete `InventoryService` | ctor param | Low–Med | **No** | Needs decomposition first; Stage 6+ |
| `AnalyticsWindow` → `new AnalyticsWindowViewModel` | View constructs VM | Low | **No** | VM split is Stage 8 |
| VM → Chart / Diff / Stores field-`new` | pure + disk helpers | No | **No** | Interfaces add no test value today |
| `InventoryWindow` → ReportService/Exporter `new` | View helpers | Low | **No** | Parallel to already-solved Analytics path; catalog is secondary surface |
| `InventoryService` → ~15 discovery `new` | internal pipeline factory | Med (maintainability) | **No** | Decomposition = Stage 6+ scope |
| Multiple `new PilotObjectScanner` | construction sprawl | Med (noise, not coupling) | **No** | Reuse instance later; not interface |
| Static `AnalyticsLogger` | cross-cutting | Low | **No** | No alternate sink planned |
| `.GetAwaiter().GetResult()` | BIM index open | Known | **No** | Async rewrite deferred Stage 4 |
| MEF → command service locator for optional BIM | host optionals | Low | **No** | Correct for AllowDefault Pilot services |

### Achieved

- View business handlers no longer construct `ProjectAnalyticsService` / `AnalyticsCsvExporter`
- Minimal seams: `IProjectAnalyticsService`, `IAnalyticsCsvExporter`
- Composition root clarified: `AnalyticsCommandService`
- MEF preserved; no DI framework; behavior preserved
- Audit + final docs committed

### Kept Concrete

| Type | Why |
|------|-----|
| `ChartDataService`, `ScanDiffService` | Pure transforms; tested via characterization |
| `DashboardLayoutStore`, `ScanSnapshotStore` | Simple disk I/O; no alternate impl |
| `InventoryReportService` / `Exporter` | Catalog helpers; low risk |
| Discovery / Data pipeline types | Owned only by `InventoryService`; no substitution |
| `InventoryService` | Façade; interface without decompose is harmful theatre |
| `AnalyticsLogger` | Static file sink is fine |

### Deferred

- InventoryService decomposition
- `IInventoryService` decision (after decompose)
- PilotObjectScanner sprawl/reuse
- Static AnalyticsLogger abstraction
- Async GetResult rewrite
- ViewModel split
- Localization
- Deeper MVVM (move Build/Export out of Window)
- InventoryWindow report helper injection (optional mirror of 5.3; not blocking)

### Recommendation

**STOP_STAGE_5**

Further decoupling now would expand into Inventory decomposition, VM/window splits, or async rewrite — those are Stage 6+ and lack an immediate Stage-5 benefit beyond “architecture could be cleaner.”

