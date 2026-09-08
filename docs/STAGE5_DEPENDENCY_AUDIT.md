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
There are **zero project-owned service interfaces** today.

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
       │    ├─ new ProjectAnalyticsService().Build(report)
       │    └─ new AnalyticsCsvExporter().Export(snapshot)
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
| `AnalyticsWindow` | `ProjectAnalyticsService` | `new …().Build(report)` | M | **Maybe** `IProjectAnalyticsService` **or** move Build out of View | View owns analytics pipeline step |
| `AnalyticsWindow` | `AnalyticsCsvExporter` | `new …().Export(...)` | M | **Maybe** `IAnalyticsCsvExporter` **or** move export out of View | View owns file I/O |
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

Status: **INTRODUCED — Stage 5.2** (seam only; consumer rewiring deferred to Stage 5.3)

| Interface | Implementation | Minimal contract | Production consumer today | Status |
|-----------|----------------|------------------|---------------------------|--------|
| `IProjectAnalyticsService` | `ProjectAnalyticsService` | `ProjectAnalyticsSnapshot Build(ProjectInventoryReport report)` | `AnalyticsWindow` still `new ProjectAnalyticsService()` | **INTRODUCED** |
| `IAnalyticsCsvExporter` | `AnalyticsCsvExporter` | `string Export(ProjectAnalyticsSnapshot snapshot)` | `AnalyticsWindow` still `new AnalyticsCsvExporter()` | **INTRODUCED** |
| `IInventoryService` | — | — | — | **DEFERRED / NOT INTRODUCED** |

### Why these interfaces are justified

- Real UI ↔ business boundary: View currently constructs and calls both types.
- Small contracts matching actual consumer usage (one method each).
- Enables Stage 5.3 injection without changing Build/Export behavior.

### Why IInventoryService is NOT introduced

`InventoryService` is a large façade/god-factory. An interface would not reduce internal coupling and would only hide the architecture problem. Defer until decomposition (later stage).

### Orchestration

`AnalyticsWindow` rewiring / dependency injection → **Stage 5.3**. Stage 5.2 intentionally leaves `new Concrete()` call sites unchanged.

## Recommended minimal set (historical — Stage 5.1 proposal)

| Candidate | Introduce? | Consumers | Implementation | Why concrete is worse | MEF impact |
|-----------|------------|-----------|----------------|----------------------|------------|
| `IProjectAnalyticsService` (`Build` only) | **Done — Stage 5.2** | `AnalyticsWindow` | `ProjectAnalyticsService` | View constructs and orchestrates transform | None |
| `IAnalyticsCsvExporter` (`Export` only) | **Done — Stage 5.2** | `AnalyticsWindow` | `AnalyticsCsvExporter` | View owns export I/O | None |
| `IInventoryService` | **NOT INTRODUCED** | Windows + command | `InventoryService` | Façade/god-factory; interface hides problem | — |

**Prefer not to add:** Chart/Diff/Stores/Discovery/logger interfaces; factories (`I*Factory`); MEF-export of entire Discovery layer.

## Deferred

| Item | Why not Stage 5 |
|------|-----------------|
| `AnalyticsWindow` inject `IProjectAnalyticsService` / `IAnalyticsCsvExporter` | **Stage 5.3** — seam first, rewiring second |
| `IInventoryService` | God-factory; interface does not reduce internal coupling |
| DI for all `InventoryService` children | Largest fan-out; needs orchestrator extract first, not 15 interfaces |
| MEF-export Discovery services | Host only needs menus + command façade |
| Project ports wrapping all `Ascon.Pilot.*` | SDK already interface-based; multi-host not planned |
| `AnalyticsLogger` DI | No alternate sink |
| Abstract factories / DI framework | Explicitly forbidden |
| Split `AnalyticsWindowViewModel` / rewrite Window | Stage 6–8 deferred |
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

## Stage 5.3 gate

Await confirmation before rewiring `AnalyticsWindow` to consume the new interfaces (injection / composition-root construction).

