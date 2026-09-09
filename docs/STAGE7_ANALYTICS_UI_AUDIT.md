# Stage 7 Analytics UI Audit

Date: 2026-09-09  
Scope: Stage 7.0 baseline + Stage 7.1 analysis only  
Production / XAML changes: **none**

## Baseline

| Check | Result |
|-------|--------|
| HEAD | `97c2697` |
| origin/main | `b8df8f3` (**local ahead by 5** Stage 6 commits; not pushed) |
| Working tree | clean |
| Build | PASS |
| Errors / Warnings | 0 / 0 |
| Tests | 102 passed, 0 failed, 0 skipped |

## Window Size

| File | LOC |
|------|-----|
| `AnalyticsWindow.xaml` | **583** |
| `AnalyticsWindow.xaml.cs` | **293** |

### XAML structure

- Single Window; DockPanel header/status + 2-column body  
- **~18** named `Panel*` siblings + `BimFilterBar`  
- **~150+** bindings to one `DataContext` (`AnalyticsWindowViewModel`)  
- **0** `ICommand` bindings; **14** `Click=` handlers  
- `ChartCanvasControl` used for chart tabs / builder / dashboard widgets  

### Code-behind structure

| Aspect | Detail |
|--------|--------|
| Fields | `_inventory`, `_projectAnalytics`, `_csvExporter`, `_vm`, `_cts` |
| Ctor | Injects inventory + Stage 5 analytics/CSV abstractions; `new AnalyticsWindowViewModel()` |
| Heaviest method | `Refresh_Click` (~48 LOC) — scan orchestration |

## ViewModel Size

| Metric | Value |
|--------|-------|
| LOC | **984** |
| Field-new services | `ChartDataService`, `DashboardLayoutStore`, `ScanSnapshotStore`, `ScanDiffService` |
| Public properties | **~53** (many `ObservableCollection`s + selected/current) |
| Commands | **0** |
| Methods >60 LOC | ctor (~87), `ReloadCollections` (~73), `RefreshScanDiff` (~63) |
| Methods >30 LOC | also `RebuildDashboardWidgets` (~57), `RebuildChartBuilder` (~46), `AddDashboardWidget` (~43) |

## Responsibilities

| Responsibility | Owner today | Methods/Properties | UI-bound? | Candidate extraction? |
|----------------|-------------|-------------------|-----------|------------------------|
| Nav key + title | VM | `Navigation`, `SelectedNavKey`, `SelectedNavTitle` | Yes | **NO** (thin) |
| Panel visibility | **Window** `ShowPanel` | string-key Visibility on 18 panels | Code-behind | **MAYBE** later (bindings) |
| Scan mode radios | VM | `ScanMode`, `IsFast/IsStandard/IsFull` | Yes | **NO** |
| Run inventory scan | **Window** | `Refresh_Click` | Click | **MAYBE** (async runner later) |
| Build snapshot | Window → `IProjectAnalyticsService` | already abstracted (Stage 5) | Indirect | **NO** |
| Snapshot → UI collections | VM | `Snapshot` → `ReloadCollections` | Yes | **YES** (cascade owner) |
| BIM model filter | VM | filters + `_allBimParts/_allBimTypes` | Yes | **MAYBE** |
| Fixed chart tabs | VM + `ChartDataService` | `ReloadCharts` / chart OCs | Yes | **YES** (presenter; data already in service) |
| Chart builder | VM + charts | `SelectedChart*`, `RebuildChartBuilder` | Yes | **YES** |
| Dashboard layout CRUD + persist | VM + `DashboardLayoutStore` | Set/Move/Add/Update/Remove + persist | Partial | **YES** |
| Dashboard widget fill | VM + charts | `RebuildDashboardWidgets` | Yes | **YES** |
| Scan capture / diff / history | VM + `ScanDiffService` + `ScanSnapshotStore` | `RefreshScanDiff`, Compare/Save/Delete | Yes | **YES** |
| Diff “changes only” | VM | `ScanDiffChangesOnly`, publish/filter | Yes | **YES** (with scan) |
| CSV export / open folder | Window | Export/Open handlers | Click | **NO** |
| Cancel scan | Window | `Cancel_Click` + `_cts` | Click | **NO** |
| Dialog UX | Window | editor/prompt/confirm | Click | **NO** (keep UI) |
| Progress / busy / limitations | VM | `ProgressText`, `IsBusy`, `LimitationsText` | Yes | **NO** |

## Window Code-Behind

| Handler | Responsibility | UI-only? | Business logic? | Move now? |
|---------|----------------|----------|-----------------|-----------|
| `Nav_SelectionChanged` | Call `ShowPanel` | Mostly | No | No |
| `ShowPanel` | Toggle 18 panels + BIM filter by string key | Yes | No | Later → bindings |
| `ExportCsv_Click` | Guard + export + MessageBox | Partial | Thin (exporter) | No |
| `OpenExports_Click` | Open Exports folder | Yes | Path policy | No |
| `Refresh_Click` | Confirm Full, CTS, `Inventory.Run`, `Build`, set Snapshot | No | **Yes** | Later (not 7.2) |
| `DashboardVisible/Move*` | Tag → VM layout methods | Thin glue | No | No |
| `DashboardAdd/Edit/Remove` | Dialogs → VM | Dialog | No | No |
| `ChartBuilderToDashboard_Click` | VM + MessageBox | Thin | No | No |
| `ScanCompare_Click` | VM compare | Glue | No | No |
| `ScanSaveNamed_Click` | Prompt → VM save | Dialog | No | No |
| `ScanDeleteHistory_Click` | Confirm → VM delete | Dialog | No | No |
| `Cancel_Click` | Cancel CTS | Yes | No | No |

**Business-heavy handlers:** primarily `Refresh_Click`. Most others are UI glue/dialogs.

## Dependency Graph

```
AnalyticsCommandService [MEF]
  → CreateInventoryService / new ProjectAnalyticsService / new AnalyticsCsvExporter
  → new AnalyticsWindow(inventory, IProjectAnalyticsService, IAnalyticsCsvExporter)
       → new AnalyticsWindowViewModel()
            → field-new ChartDataService
            → field-new DashboardLayoutStore
            → field-new ScanSnapshotStore
            → field-new ScanDiffService
       → Refresh: Inventory.Run → ProjectAnalytics.Build → VM.Snapshot
       → Export: IAnalyticsCsvExporter.Export(VM.Snapshot)
```

MEF stops at command service. Chart/scan/dashboard stores are **manual field-new** inside the VM.

## Field-New Audit

| Type | Created where | Stateful? | Harmful coupling? | Should inject? |
|------|---------------|-----------|-------------------|----------------|
| `AnalyticsWindowViewModel` | Window ctor | Yes | Medium (blocks VM ctor injection) | MAYBE later |
| `ChartDataService` | VM field | No (pure) | Low | **No** (keep concrete) |
| `DashboardLayoutStore` | VM field | Disk | Medium | Optional later |
| `ScanSnapshotStore` | VM field | Disk | Medium | Optional with scan extract |
| `ScanDiffService` | VM field | No (pure) | Low | **No** |
| Dialog windows | Window handlers | Transient | Low | **No** |
| `CancellationTokenSource` | Refresh | Per scan | Low | **No** |

Could inject ≠ should inject. Stage 7.2 should not introduce DI/MEF for these.

## State Ownership

| State | Owner | Lifetime | Mutation points | Candidate move? |
|-------|-------|----------|-----------------|-----------------|
| Selected nav key | VM | Window lifetime | Nav selection | Keep |
| Panel Visibility | **Window** named controls | Window lifetime | `ShowPanel` | Later bindings |
| Snapshot | VM | Until next scan | `Refresh_Click` → setter | Keep on VM façade |
| BIM filter + caches | VM | Per snapshot | Reload / filter change | MAYBE |
| Chart option selections | VM | Session | UI | Keep / with chart presenter |
| Dashboard layout | VM + layout store | Disk + memory | CRUD handlers | YES (after scan) |
| Scan baseline / history / diff | VM + scan store/diff | Disk + memory | Snapshot reload / compare / save | **YES — 7.2** |
| CTS | Window | Per refresh | Refresh/Cancel | Keep |

**Split ownership note:** Window drives Visibility via string keys; VM has `ShowBimModelFilter` that XAML does **not** use for the BIM bar (Window duplicates the rule).

## XAML Coupling

- High density: one VM as `DataContext` for all panels  
- Sibling-panel architecture: mutually exclusive Visibility by string keys (`Summary`, `Charts`, `ChartBuilder`, `Types`, … `ScanDiff`)  
- Nested templates for dashboard widgets / chart bars  
- Renaming bound property paths = **HIGH** cost  
- Extracting a presenter while **keeping façade property names** = **NONE** XAML impact  

## Section Architecture

1. Identity: hardcoded `NavItem.Key` strings in VM ctor  
2. Selection: `ListBox` → `SelectedNavKey` + `SelectionChanged` → `ShowPanel`  
3. Visibility: code-behind only (not VM booleans)  
4. BIM filter bar: separate Visibility rule in Window  

No section descriptor framework today — do not invent one in 7.2.

## Chart Area

| Question | Answer |
|----------|--------|
| Series construction | Already in **`ChartDataService`** (tested) |
| VM role | Wire options, copy into OCs, builder hints, IFC filter |
| Canvas | `ChartCanvasControl` DPs `ChartKind` + `Points`; magic normalize vs 420/220 |
| First extract? | **Partial** — data already extracted; remaining is wiring. Lower urgency than scan/compare |

## Scan / Compare Area

Owned by VM orchestration over existing pure services:

- `ScanDiffService.Capture` / diff  
- `ScanSnapshotStore` last + named history  
- `RefreshScanDiff` (~63 LOC): capture → compare → save last → archive → history → filter  
- UI binds `ScanHistory`, `SelectedScanHistory`, `ScanDiff`, hints, changes-only  

**Can extract without XAML rewrite: YES** — keep same public property names on VM; move orchestration to a presenter/state object.

## Testability

| Area | Today |
|------|--------|
| `ChartDataService` / `ScanDiffService` / stores | Already unit-testable (characterization tests exist) |
| `AnalyticsWindowViewModel` as-is | Hard (field-new, disk, many OCs, no seams) |
| Window | WPF; do not unit-test in 7.x |

**Best testability gain:** extract scan/compare presenter and unit-test capture/compare/filter/save/delete without WPF.

## Decomposition Options

| Option | Scope | Benefit | XAML impact | Behavior risk | Testability gain | Recommended? |
|--------|-------|---------|-------------|---------------|------------------|--------------|
| **A — Scan/compare presenter** | Move store/diff/baseline/history/diff rows/filter/hints + Refresh/Compare/Save/Delete; VM façades | Cuts heaviest orchestration; leverages existing service tests | **NONE** | Low if reload order kept | **High** | **YES — Stage 7.2** |
| **B — Dashboard presentation state** | Layout store + CRUD + rebuild widgets | Large LOC cut | NONE if façades | Medium | Medium–high | After A |
| **C — Split XAML UserControls** | One control per Panel | File readability | **LARGE** | Medium | Low alone | **No** for 7.2 |

Prefer a **pure presentation/state object** over inventing DashboardViewModel/ChartViewModel/ScanViewModel explosion.

## Risks

| Risk | Severity | Implication |
|------|----------|-------------|
| Mass XAML rewrite / sibling panels | High if pursued | Defer UserControl split |
| Dual nav visibility (Window vs unused VM prop) | Medium | Do not “fix” in 7.2 without tests |
| `RefreshScanDiff` side effects (disk + snapshot.ScanDiffRows) | Medium | Preserve call order inside `ReloadCollections` |
| Renaming bound props | High | Keep façade names |
| MEF/DI for VM stores | Out of scope | Manual `new` at extract boundary OK |

No STOP that blocks Stage 7.2 option A.

## Deferred

- XAML UserControl / section framework  
- ICommand migration  
- Sub-ViewModel explosion  
- Moving `Refresh_Click` orchestration out of Window  
- Dashboard extract (Stage 7.3+)  
- Chart wiring extract (optional later)  
- Localization / MEF / DI / async rewrite / InventoryService changes  

## Recommended Stage 7.2

See dedicated section below.

---

## Stage 7.2 Recommendation

### Change

Extract **`AnalyticsScanComparePresenter`** (name may be `ScanCompareState` — prefer accurate role over “Manager/Engine”):

Owns:

- `ScanSnapshotStore`, `ScanDiffService`  
- current baseline, history collection, full/filtered diff rows, hints, changes-only flag  
- methods equivalent to: reload-on-snapshot, compare selected, save named, delete selected, reload history  

`AnalyticsWindowViewModel` **delegates** and **re-exposes the same public property names** bound by XAML.

### Why first

- Self-contained boundary already backed by pure services + tests  
- Heaviest orchestration cluster after `ReloadCollections`  
- **No XAML rewrite**  
- Improves VM testability without MEF/DI/framework  
- Chart data already extracted; dashboard is larger/riskier  

### Expected files

- New: `Services/AnalyticsScanComparePresenter.cs` (or `ViewModels/` if team prefers presentation placement)  
- Edit: `ViewModels/AnalyticsWindowViewModel.cs`  
- Optional: new unit tests for presenter  
- **Not** Window XAML / code-behind  

### Expected XAML impact

**NONE**

### Expected behavior

**UNCHANGED** (same auto last-scan, archive, hint strings, filter rules, property names)

### Test strategy

Unit-test presenter with temp store directory + `ScanDiffService`:

- first scan (no previous)  
- compare named history  
- changes-only filter  
- delete history  
- error/empty paths  

Keep existing `ScanDiffService` / store characterization tests.

### Explicit non-goals

- No UserControl / XAML split  
- No ICommand migration  
- No MEF registration of presenter  
- No dashboard or chart extract in the same commit  
- No changing `ShowPanel` keys  
- No moving `Refresh_Click`  
- No localization / DI framework  
