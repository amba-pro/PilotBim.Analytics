# Stage 9 Technical Debt Audit

Date: 2026-09-11
Scope: Stage 9.1 — **audit only**. No production code, test, XAML, resx, csproj or script was modified.

## Baseline

| Item | Value |
|------|-------|
| HEAD | `a833629` Stage 8.5: finalize low-risk UI localization |
| Remote | synced with `origin/main` |
| Working tree | clean |
| Build | PASS — 0 errors / 0 warnings |
| Tests | **118 PASS** (verified by counting `[Fact]` across 21 test classes: 118) |
| Stages complete | 0–8 (`STOP_STAGE_8`) |
| Target framework | `net472`, WPF, MEF plugin (`PilotBim.Analytics.ext2.dll`) |
| Audit method | Full read of every production file + PowerShell LOC/method-size metrics + grep sweeps per area A–V |

## Current Architecture Snapshot

### Requested file metrics (physical lines)

| File | LOC |
|------|----:|
| `Views/AnalyticsWindow.xaml` | 584 |
| `Views/AnalyticsWindow.xaml.cs` | 294 |
| `ViewModels/AnalyticsWindowViewModel.cs` | 637 |
| `Services/InventoryService.cs` | 366 |
| `Services/ObjectSamplingCoordinator.cs` | 306 |
| `ViewModels/AnalyticsScanComparePresenter.cs` | 271 |
| `ViewModels/AnalyticsDashboardPresenter.cs` | 272 |
| Production total (`src`, .cs + .xaml, incl. generated Designer) | **11 875** |
| Production .cs excluding `Resources.Designer.cs` | 10 122 |
| `Properties/Resources.Designer.cs` (generated, checked in) | 682 |
| XAML total (6 files) | 1 071 |
| Test code total | 2 007 |
| Test count | **118** |

### Other size hot spots

| File | LOC |
|------|----:|
| `Services/ProjectAnalyticsService.cs` | 559 |
| `Discovery/BimDiscoveryService.cs` | 459 |
| `Properties/Resources.resx` (124 keys) | 456 |
| `Discovery/BimIndexAnalyticsService.cs` | 425 |
| `Data/PilotObjectScanner.cs` | 422 |
| `Views/InventoryWindow.xaml` | 406 |
| `Views/ChartCanvasControl.xaml.cs` | 383 |

### Shape

- 49 production `.cs` files, 6 XAML files, 78 declared types (44 `public`, 34 `internal`).
- Composition root: `Plugin/Commands/AnalyticsCommandService` (`[Export]`, `[ImportingConstructor]`), MEF surface = `IDataPlugin`, `IMenu<MainViewContext>` ×2, `IMenu<ObjectsViewContext>`, `IToolbar<ObjectsViewContext>`.
- Two project-owned seams only: `IProjectAnalyticsService`, `IAnalyticsCsvExporter` (Stage 5). `IInventoryService` intentionally absent.
- SDK wait discipline centralised in `Data/AsyncCallbackGuard.cs` (`CallbackWaitSession`, 9 call sites).
- Presentation: root VM (637) + 2 presenters (271 / 272) + `DashboardSectionVm` (204); `ChartDataService` (195) is the only chart data source.
- Persistence: `%LOCALAPPDATA%\PilotBim.Analytics\` (`Logs\analytics.log`, `Snapshots\`, `dashboard-layout.json`, `state-mapping.json`, `Exports\`). Nothing is written into Pilot.
- Localization: 124 resx keys, 169 `x:Static` XAML references; **243 Russian string literals remain in C#** (top: `ProjectAnalyticsService` 50, `AnalyticsWindowViewModel` 45, `CapabilityMatrixService` 29, `ScanDiffService` 24).

## Executive Verdict

The codebase is in **good shape for its size and constraints**. Stages 4–8 removed the classes of defect that actually break a Pilot plugin: post-timeout callback writes, sticky sample buffers, collection aliasing, UI-owned service construction, unbounded index queries. `CallbackWaitSession` is a genuinely correct little primitive and is well tested (9 tests).

Findings: **49** total — **1 MUST_FIX_BEFORE_STAGE_10**, **13 SHOULD_FIX**, **24 DEFER**, **11 LEAVE_AS_IS**.

There is exactly one issue I consider a real runtime defect rather than a style preference: **neither window disposes or cancels its `CancellationTokenSource`, and neither cancels a running scan when the window closes** (TD-22). Because `AnalyticsCommandService` clears its window reference on `Closed`, a user can close a window mid-scan and immediately open a new one, producing two concurrent full scans hammering the Pilot SDK with no way to stop the orphaned one. Everything else is maintainability debt, documented deferral, or requires Pilot runtime to even observe.

Notably absent: no `TODO`/`FIXME`/`HACK` markers anywhere, no `async void` outside WPF event handlers, no cross-thread WPF collection mutation (all SDK→UI hops go through `Dispatcher.BeginInvoke`), no `int` narrowing on object counts, and CSV/snapshot formatting is already `InvariantCulture`.

Stage 9 is **not** a licence to rewrite: the 984→637 LOC VM, the 18-panel `ShowPanel`, the `new`-graph in `InventoryService.Run` and the sync-over-async BIM index bridge are all explicitly recorded as deferred by Stages 4–7 and should stay deferred.

## MUST_FIX_BEFORE_STAGE_10

| ID | Area | Severity | Title |
|----|------|----------|-------|
| TD-22 | I Disposal/lifetime | HIGH | Scan `CancellationTokenSource` never disposed and never cancelled on window close; orphaned scans can run concurrently |

Detail in [Disposal and Lifetime](#disposal-and-lifetime). This is the sole MUST and is the basis of the recommended Stage 9.2.

## SHOULD_FIX

| ID | Area | Severity | Title |
|----|------|----------|-------|
| TD-38 | N String contracts | MEDIUM | `IsRemarkType` heuristic duplicated verbatim in two services |
| TD-25 | J Thread safety | MEDIUM | Post-timeout callback can mutate `report` lists concurrently with the waiting thread |
| TD-14 | F InventoryService | MEDIUM | `LoadChildren` can invoke `onDone` more than once (no completion guard) |
| TD-08 | D ChartCanvasControl | MEDIUM | `420.0` / `220.0` divisors duplicate private `ChartDataService` constants |
| TD-06 | C UI navigation | MEDIUM | BIM filter visibility predicate duplicated in VM and Window |
| TD-11 | E ViewModel | MEDIUM | Snapshot reload re-evaluates chart builder ×4 and dashboard content ×3 |
| TD-41 | P Test coverage | MEDIUM | Store tests are non-hermetic; two parallel collections write the same `last-scan.json` |
| TD-40 | P Test coverage | MEDIUM | No tests for the VM, sampling coordinator, scanner, walk sampler or chart geometry |
| TD-03 | B Static state | MEDIUM | `analytics.log` grows without bound; one open/append/close per log line |
| TD-02 | A Sync-over-async | LOW | The "never call `Run` on the UI thread" invariant is undocumented in code |
| TD-30 | L Null safety | LOW | `AnalyticsWindow` ctor validates 2 of 3 dependencies |
| TD-37 | N String contracts | LOW | `"bimObjectId"` literal repeated in three files |
| TD-48 | U Hygiene | LOW | `csproj` `Description`/`Version` metadata stale (`Stage 1 …`, `0.9.0-rich-diff-to-dashboard`) |

## DEFER

| ID | Area | Severity | Title |
|----|------|----------|-------|
| TD-01 | A | MEDIUM | Four `.GetAwaiter().GetResult()` bridges over BIM index async API |
| TD-23 | I | MEDIUM | `IModelSearchService` instances never released |
| TD-24 | I | MEDIUM | `IDisposable` returned by `SubscribeObjects` discarded at every call site |
| TD-13 | E | MEDIUM | Root VM constructor performs disk I/O; no injectable seam, hence no VM tests |
| TD-28 | K | MEDIUM | Scan baseline persists *display* strings and re-parses them with a regex |
| TD-33 | M | MEDIUM | Russian `ScanDiff` `Area`/`Metric` values and `KpiLabels` are logic keys |
| TD-04 | B | LOW | Static `AnalyticsLogger` not injectable |
| TD-09 | D | LOW | Chart redraw reallocates all visuals on every `SizeChanged` |
| TD-10 | D | LOW | Russian literal inside `ChartCanvasControl` |
| TD-12 | E | LOW | Root VM still 637 LOC with inline nav/option lists |
| TD-15 | F | LOW | `Run` is 210 LOC and constructs ~12 services inline |
| TD-19 | G | LOW | Timeouts (30/20/10/8 s) hardcoded per call site, no mode scaling |
| TD-20 | H | LOW | Nine catch-all handlers, three of them silent |
| TD-27 | K | LOW | Display numbers formatted with current culture |
| TD-31 | L | LOW | `progress` / `onDone` callbacks invoked without null guards |
| TD-34 | M | LOW | 243 Russian literals remain in C# |
| TD-35 | M | LOW | Russian widget titles persisted into `dashboard-layout.json` |
| TD-36 | N | LOW | String ids round-tripped through `Enum.TryParse`; `CapabilityStatus` is string consts |
| TD-39 | O | LOW | No scan duration, no scan correlation id, no log rotation |
| TD-42 | Q | LOW | 36 methods ≥50 LOC, 11 ≥80, 5 ≥100 |
| TD-43 | R | LOW | Construction sprawl: `new` graph inside pipeline methods |
| TD-45 | T | LOW | `InventoryService` and the three Windows are `public` wider than needed |
| TD-46 | T | LOW | SDK references are `SpecificVersion=false` with no load-time version check |
| TD-47 | U | LOW | No CI workflow, no `Directory.Build.props`, no `.editorconfig`, no `TreatWarningsAsErrors` |

## LEAVE_AS_IS

Do **not** rewrite these. Each is either a deliberate, documented decision from Stages 4–8, or a change whose regression risk exceeds its value at this codebase size.

| ID | Item | Why it stays |
|----|------|--------------|
| TD-05 | `ShowPanel` toggling 18 (Analytics) + 13 (Inventory) sibling panels | Replacing it means a XAML architecture change (UserControl per section + navigation host) with ~150 bindings at risk. Stage 7 explicitly deferred it; it works and is trivially readable. |
| TD-07 | Click handlers + `MessageBox` in code-behind, no `ICommand` | An `ICommand` migration touches every XAML button and buys nothing for a 2-window plugin. Stage 7 deferred. |
| TD-16 | `InventoryService` as a public fat façade with no `IInventoryService` | Stages 5 and 6 both concluded an interface over a god-façade *hides* the problem. Introduce it only after the façade is genuinely thin. |
| TD-17 | Obsolete SDK members behind `#pragma warning disable 612` (3 sites) | `GetRootObject` / `GetCachedObject` have no non-obsolete replacement in this SDK; the pragma is scoped to a single statement each. |
| TD-18 | `Thread.Sleep(200)` × 20 poll of `IModelStorage.IsLoaded` | `IModelStorage` exposes no readiness signal. Documented in Stage 4 as a deliberate keep; the loop is bounded (4 s), respects the cancellation token, and degrades to `Partial`. |
| TD-21 | Outer `catch (Exception)` in `Run` → `FinalStatus = BLOCKED_BY_SDK` | Coarse by design: a read-only diagnostic plugin must never propagate an exception into Pilot's UI. The exception is logged with type and message first. |
| TD-26 | SDK→UI marshalling via `Dispatcher.BeginInvoke` | Audited and correct. All `ObservableCollection` mutation happens on the UI thread. Leave the pattern alone. |
| TD-29 | Numeric widths / overflow handling | `ObjectCount` is `long` end-to-end, `ResolveObjectCount` avoids narrowing, `maxHits ≤ 50 000`, the only `(int)` narrowing (`CreatorAggregationService:48`) is `Math.Min`-guarded. Nothing to fix. |
| TD-32 | `_allBimParts` / `_allBimTypes` passed live into `DashboardContentContext` | Same-thread, and `ChartDataService.ToSeries` materialises with `ToList()` before any reload can clear the source. No bug; adding defensive copies would only add allocation. |
| TD-44 | Deferred work tracked in `docs/` rather than `TODO` comments | Zero `TODO`/`FIXME`/`HACK` markers in 10k LOC is a feature, not a gap. The stage docs are the backlog. |
| TD-49 | Multiple types per file (`StateDiscoveryService.cs` → +`PersonDiscoveryService`, +`OrganisationDiscoveryService`; `TypeDiscoveryService.cs` → +`AttributeDiscoveryService`, +`SystemFieldDiscoveryService`; `PilotObjectScanner.cs` → 4 types) | Cohesive clusters. Splitting is pure churn across a dozen files and complicates diff archaeology against Stages 0–8. |

## Sync-over-Async

Sweep for `.GetAwaiter().GetResult()`, `.Result`, `.Wait(`, `Task.WaitAll/Any`, `Thread.Sleep`, `async void`:

| Pattern | Sites | Verdict |
|---------|------:|---------|
| `.GetAwaiter().GetResult()` | 4 | TD-01 |
| `.Result` on `Task` | 0 | `result.Result` (`PilotObjectScanner:104`) and `dlg.Result` (`AnalyticsWindow:195,217`) are SDK/dialog properties, not tasks |
| `session.Wait(timeout)` | 9 | Intended `CallbackWaitSession` API, not `Task.Wait` |
| `Thread.Sleep` | 1 | TD-18 (LEAVE_AS_IS) |
| `async void` | 2 | WPF event handlers `Refresh_Click`, `Scan_Click`, both with full `try/catch` — correct |
| `ConfigureAwait` | 0 | Only 2 `await`s exist, both on the UI thread awaiting `Task.Run`; `ConfigureAwait(false)` would be wrong there |

**TD-01 — Sync-over-async bridges over the BIM index API**
Area A. Severity MEDIUM.
Evidence: `Discovery/BimIndexAnalyticsService.cs:119-122` (`GetModelPartsSearchServiceAsync(...).GetAwaiter().GetResult()`), `:400-402` (`AddModelPartAsync`); `Discovery/RemarkAnalyticsService.cs:214` (`GetModelPartsSearchServiceAsync`), `:223` (`AddModelPartAsync`).
Risk: classic sync-over-async deadlock **if** ever reached from a thread with a `SynchronizationContext`. Today both paths are reached only from `InventoryService.Run`, which both windows invoke exclusively inside `Task.Run` (`AnalyticsWindow.xaml.cs:134-141`, `InventoryWindow.xaml.cs:79-86`) — i.e. no captured context, no deadlock. Wrapping the exception loses `AggregateException` detail, but both sites catch and log.
Classification: **DEFER** — matches Stage 4 rows 12–13 (`DEFERRED`, worker-only today).
Suggested minimal action: none now. If ever changed, the whole `Analyze` chain must go async up to `Task.Run`; a partial async-ification is worse than the current state.
Regression risk: HIGH if attempted (touches the entire BIM index pipeline).
Pilot runtime required: **YES**.

**TD-02 — The UI-thread invariant is undocumented**
Area A. Severity LOW.
Evidence: `Services/InventoryService.cs:35` `public ProjectInventoryReport Run(...)` has no XML doc or remark stating it must not be called on the UI thread, yet TD-01's safety depends entirely on that. `InventoryService` is `public`, so a future caller can violate it silently.
Risk: a later refactor calls `Run` directly from a click handler and deadlocks inside the BIM index open — a hang, not an exception, and invisible in tests.
Classification: **SHOULD_FIX**.
Suggested minimal action: one XML `<remarks>` block on `Run` (and on `BimIndexAnalyticsService.Analyze`) recording the worker-thread requirement. Documentation only.
Regression risk: NONE.
Pilot runtime required: NO.

## Static State

**TD-03 — Unbounded log file and per-line file I/O**
Area B. Severity MEDIUM.
Evidence: `Diagnostics/AnalyticsLogger.cs:57-86` — every `Info/Warning/Error/Blocked` call performs `Directory.CreateDirectory` + `File.AppendAllText` on `…\Logs\analytics.log` under a static `lock`. No size cap, no rotation, no retention. `InventoryService.Progress` (`:47-53`) logs *every* progress message, and `BimIndexAnalyticsService` emits one progress line per model part (`:173`), so a Full scan of a large project writes hundreds of individually-opened appends.
Risk: the log grows monotonically for the installation's lifetime (a plugin that lives in `%LOCALAPPDATA%` and is never cleaned); on a big project the open/append/close churn also adds avoidable I/O inside the scan loop.
Classification: **SHOULD_FIX**.
Suggested minimal action: size check before append — if `analytics.log` exceeds N MB, rename to `analytics.1.log` (single generation) and start fresh. ~10 LOC inside the existing `lock`, still inside the existing `try { } catch { }`.
Regression risk: LOW (logging is already fully exception-swallowed; worst case is a lost log line).
Pilot runtime required: NO.

**TD-04 — Static logger is not injectable**
Area B. Severity LOW.
Evidence: `AnalyticsLogger` is `internal static`; 32 files call it directly. `report.Diagnostics` is populated via `AnalyticsLogger.CreateEntry`, so the in-report diagnostics channel is coupled to the static type too.
Risk: no test can assert logging behaviour; no alternate sink (Pilot's own log, in-memory buffer) is possible without touching every call site.
Classification: **DEFER** — Stages 5 and 6 both examined and deferred this ("cross-cutting file log; no alternate sink").
Suggested minimal action: none until a second sink is actually needed.
Regression risk: MEDIUM if attempted (32-file mechanical change).
Pilot runtime required: NO.

## UI Navigation

**TD-05 — `ShowPanel` sibling-panel toggling** — LEAVE_AS_IS. Evidence: `Views/AnalyticsWindow.xaml.cs:46-71` sets `Visibility` on 18 named panels (`x:Name="Panel…"` ×18 in `AnalyticsWindow.xaml`); `Views/InventoryWindow.xaml.cs:38-53` does the same for 13. Adding a section means editing XAML, the nav list and `ShowPanel` — three places. Recorded as architecture debt by Stage 7; the cure (UserControl-per-section) is a UI redesign.

**TD-06 — BIM filter visibility predicate duplicated**
Area C. Severity MEDIUM.
Evidence: `ViewModels/AnalyticsWindowViewModel.cs:262-270` (`ShowBimModelFilter` → `BimParts | BimTypes | Charts | ChartBuilder | Summary`) and `Views/AnalyticsWindow.xaml.cs:67-70` (`BimFilterBar.Visibility` → the same five keys, hardcoded again). The VM property exists, is raised on `SelectedNavKey` change (`:280`), and is **not** the source of truth for the actual bar.
Risk: drift. Adding a sixth filter-aware section updates one list and silently not the other; the VM property and the visible bar then disagree.
Classification: **SHOULD_FIX**.
Suggested minimal action: `BimFilterBar.Visibility = _vm.ShowBimModelFilter ? Visibility.Visible : Visibility.Collapsed;` — one line, no XAML change. Safe because `SelectedNavKey` is assigned before `ShowPanel` runs in both entry paths (`Nav_SelectionChanged:41-44`, and ctor default `_selectedNavKey = "Summary"` matching `ShowPanel("Summary")` at `:38`).
Regression risk: LOW; covered by a new VM test on `ShowBimModelFilter`.
Pilot runtime required: NO (visual confirmation desirable).

**TD-07 — No `ICommand`; dialogs and `MessageBox` in code-behind** — LEAVE_AS_IS. Evidence: 14 `*_Click` handlers in `AnalyticsWindow.xaml.cs`, 8 in `InventoryWindow.xaml.cs`; `MessageBox.Show` at `:77,85,89,106,117,151,240,264,274,277`. Deliberate for a plugin this size; Stage 7 deferred.

## ViewModel

**TD-11 — Snapshot reload re-evaluates chart builder and dashboard content repeatedly**
Area E. Severity MEDIUM.
Evidence: in `ReloadCollections` (`ViewModels/AnalyticsWindowViewModel.cs:371-443`):
- `:433` `SelectedBimModelFilter = BimModelFilters[0]` — the setter (`:251-260`) already calls `ApplyBimModelFilter()`;
- `:434` calls `ApplyBimModelFilter()` **again**;
- `ApplyBimModelFilter` (`:519-541`) ends with `ReloadIfcChart()` → `RebuildChartBuilder()` (`:580`) and `_dashboard.RebuildWidgetContent()` (`:540`);
- `:435` `ReloadCharts()` (`:543-564`) calls `ReloadIfcChart()` (→ another `RebuildChartBuilder`) and then `RebuildChartBuilder()` once more (`:563`);
- `:442` `_dashboard.RebuildWidgetContent()` a third time.

Net effect per snapshot: `RebuildChartBuilder` ≈ 4×, `RebuildWidgetContent` ≈ 3×, `ApplyBimModelFilter` 2× — each of which re-filters `_allBimParts`/`_allBimTypes` and rebuilds every dashboard chart series via `ChartDataService.BuildSeries`.
Risk: wasted work proportional to snapshot size on every Refresh and on every BIM-filter change; also extra `PropertyChanged` storms on `ChartBuilderSeries`. No incorrect output — the operations are idempotent.
Classification: **SHOULD_FIX** (behaviour-preserving), not MUST — the visible result is identical.
Suggested minimal action: delete the redundant `ApplyBimModelFilter()` at `:434` (the setter already ran it) and drop the duplicate `RebuildChartBuilder()` at `:563`. Two deletions; do not restructure the cascade.
Regression risk: MEDIUM **today** — there is currently no `AnalyticsWindowViewModel` test at all (TD-40), so this must be paired with VM tests before touching it. That coupling is why it is not the recommended 9.2.
Pilot runtime required: NO.

**TD-12 — Root VM still large, with inline Russian nav/option lists** — DEFER. Evidence: 637 LOC after Stage 7's 984→637 reduction; `:40-60` 18 inline `NavItem` titles, `:70-95` 17 chart option items, `:45` Russian literals. The remaining responsibilities (snapshot→OC cascade, chart tab wiring, BIM filter cache, nav/progress/busy) are exactly what Stage 7 concluded should stay. A "chart presenter" extraction was assessed as `NOT_NEEDED_NOW`.

**TD-13 — VM constructor does disk I/O and cannot be substituted**
Area E. Severity MEDIUM.
Evidence: `:33-38` the public ctor news `AnalyticsScanComparePresenter(...)` and `AnalyticsDashboardPresenter(...)`, whose parameterless paths new a real `ScanSnapshotStore` / `ScanDiffService` / `DashboardLayoutStore`. Construction therefore reads `%LOCALAPPDATA%` history (`:118` `ReloadHistoryList`) and, via `DashboardLayoutStore.LoadOrDefault` (`:24-48`), **writes** a default layout file when none exists. The presenters do expose `internal` ctors taking stores (`AnalyticsScanComparePresenter:30`, `AnalyticsDashboardPresenter:40`) — the root VM just does not use them.
Risk: constructing the VM has filesystem side effects, which is why no VM test exists; also a small UI-thread I/O hit while the window opens.
Classification: **DEFER** (it is the prerequisite for TD-40's VM tests, not a defect by itself).
Suggested minimal action: add an `internal` VM ctor accepting the two presenters; keep the public ctor delegating to it. No behaviour change.
Regression risk: LOW, but it is the seam a future VM-test stage needs — do it *with* those tests, not before.
Pilot runtime required: NO.

## InventoryService

**TD-14 — `LoadChildren` can call `onDone` more than once**
Area F. Severity MEDIUM.
Evidence: `Services/InventoryService.cs:292-349`. Both branches subscribe with `obj => { … if (remaining.Count == 0) onDone(loaded); }` **and** `onCompleted: () => onDone(loaded)` **and** `onError: ex => onDone(loaded)` — with no `finished` latch. Contrast `Data/PilotObjectScanner.cs:281-288`, where `PilotObjectSampler.SampleType` guards exactly this with a `finished` flag, and `SearchByType` (`:87-93`) does the same. So the one-shot contract is enforced everywhere except here.
Risk: `onDone` fires on the last object *and* again on `OnCompleted`. Today's two consumers are accidentally idempotent (`InventoryWindow.xaml.cs:166-187` and `:217-228` both `Clear()` then re-add inside `Dispatcher.BeginInvoke`), so no visible bug — but the tree is rebuilt twice per expand, and any future non-idempotent consumer will duplicate children.
Classification: **SHOULD_FIX**.
Suggested minimal action: wrap `onDone` in the same `finished`-latch local `Action` already used by `PilotObjectSampler`. ~6 LOC, applied twice.
Regression risk: LOW; testable with a fake `IObjectsRepository` returning a synthetic `IObservable<IDataObject>`.
Pilot runtime required: NO for the unit test; YES to confirm tree behaviour.

**TD-15 — `Run` is 210 LOC and constructs ~12 services inline** — DEFER. Evidence: `:35-244`; `new PilotSdkDiscoveryService/TypeDiscoveryService/StateDiscoveryService/StateMappingService/OrganisationDiscoveryService/PersonDiscoveryService/PilotObjectScanner/HierarchyWalkSampler/ObjectSamplingCoordinator/DocumentDiscoveryService/HistoryDiscoveryService/BimDiscoveryService/RemarkAnalyticsService/SystemFieldDiscoveryService/CapabilityMatrixService`. It reads as a linear, ordered, cancellation-checked pipeline with per-zone `try/catch` — genuinely the clearest form for what it does. Stage 6 extracted the densest cluster and stopped deliberately; further extraction risks reordering zones.

**TD-16 — public fat façade, no `IInventoryService`** — LEAVE_AS_IS (see table).
**TD-17 — obsolete SDK members behind `#pragma 612`** — LEAVE_AS_IS. Evidence: `InventoryService.cs:355-357`, `PilotObjectScanner.cs:238-240`, `HierarchyWalkSampler.cs:155-157`.

## Callback and Timeout Safety

The Stage 4 primitive holds up under review. `CallbackWaitSession` (`Data/AsyncCallbackGuard.cs:32-138`) abandons before disposing (`:109-123`), swallows `ObjectDisposedException` on both `Set` and `Wait`, distinguishes `Completed/TimedOut/Failed/Cancelled`, and every one of the 9 call sites checks `ShouldAccept()` before mutating shared state. 9 dedicated tests. **LEAVE_AS_IS** — this is the most correct code in the repository.

**TD-18 — `Thread.Sleep(200)` × 20 `IsLoaded` poll** — LEAVE_AS_IS. Evidence: `Discovery/BimDiscoveryService.cs:293-308`, with the reason in a comment ("`IModelStorage` has no readiness wait API"), bounded at 4 s, token-aware, and degrading to `CapabilityStatus.Partial` plus a user-visible warning. Stage 4 row 4 = `KEPT`.

**TD-19 — Timeouts hardcoded per call site**
Area G. Severity LOW.
Evidence: 30 s (`ObjectSamplingCoordinator:128`, `BimPartCatalogBuilder:107`, `CreatorAggregationService:82`), 20 s (`RemarkAnalyticsService:111`, `HierarchyWalkSampler:143`), 10 s (`BimDiscoveryService:149,188,269`), 8 s (`DocumentDiscoveryService:227`, `HierarchyWalkSampler:54`).
Risk: no policy; `ScanMode` does not scale them, so Fast and Full wait identically, and tuning requires touching 9 files.
Classification: **DEFER** — values are sane and no timeout-related complaint exists in Stages 0–8.
Suggested minimal action (later): an internal `ScanTimeouts` static with named `TimeSpan`s.
Regression risk: LOW, but it changes real SDK wait behaviour — needs Pilot runtime to validate.
Pilot runtime required: YES.

## Exception Handling

**TD-20 — Catch-all handlers**
Area H. Severity LOW.
Evidence — 9 bare/blind sites: `Diagnostics/AnalyticsLogger.cs:82` (justified: logging must never throw into Pilot), `Plugin/Plugin.cs:24` (justified: a throwing `[ImportingConstructor]` breaks MEF load), `Data/PilotObjectScanner.cs:402` (justified: `SafeSampleString` returns `"<unreadable>"`), `Plugin/Commands/AnalyticsCommandService.cs:161` (returns `-1` as a probe sentinel), `Views/ChartCanvasControl.xaml.cs:370` (brush parse fallback), `Discovery/PilotSdkDiscoveryService.cs:136`, `Discovery/TypeDiscoveryService.cs:112`, `Data/HierarchyWalkSampler.cs:183,189` (`catch { return -1; }` / `return string.Empty`), `Services/ObjectSamplingCoordinator.cs:284` (`catch { continue; }` on attribute read).
Risk: the last three swallow without any log line, so a systematic SDK failure (e.g. every `obj.Type` access throwing) reads as "no data" rather than "broken". The rest are defensible.
Classification: **DEFER**.
Suggested minimal action (later): downgrade the three silent ones to `AnalyticsLogger.Warning` once, rate-limited.
Regression risk: LOW (could flood the log — pair with TD-03).
Pilot runtime required: NO.

Elsewhere the pattern is consistent and good: `Run` wraps each zone in `try/catch` → `FailZone` (`InventoryService.cs:275-280`) so one broken zone cannot abort the scan; `AnalyticsScanComparePresenter.RefreshOnSnapshot:139-153` surfaces failures as a diff row instead of throwing into the UI.

**TD-21 — Coarse outer catch → `BLOCKED_BY_SDK`** — LEAVE_AS_IS. Evidence: `InventoryService.cs:235-241`.

## Disposal and Lifetime

**TD-22 — Scan `CancellationTokenSource` never disposed; scan not cancelled on window close**
Area I. Severity **HIGH**.
Evidence:
- `Views/AnalyticsWindow.xaml.cs:20` `private CancellationTokenSource _cts;`; `:126` `_cts = new CancellationTokenSource();` on every Refresh — the previous instance is dropped, never `Dispose()`d; the `finally` at `:153-156` resets only `IsBusy`.
- `Views/InventoryWindow.xaml.cs:21,71,99-101` — identical shape.
- Neither window overrides `OnClosed`/handles `Closing`; grep for `OnClosed`/`Closing` in `Views` returns nothing. So closing the window while `IsBusy` leaves `_inventory.Run` executing on the thread-pool thread with a token nobody will ever cancel.
- `Plugin/Commands/AnalyticsCommandService.cs:53,83` sets `_catalogWindow = null` / `_analyticsWindow = null` on `Closed`, and `:46-50` / `:73-77` only reuse a window when `IsVisible`. Therefore: close mid-scan → reopen → `OpenAnalytics` builds a *second* `InventoryService` and a second scan can start while the orphan is still running.
- `Cancel_Click` (`AnalyticsWindow:287-291`, `InventoryWindow:104-108`) calls `Cancel()` on whatever `_cts` currently is, with no null-safety problem but also no disposal.

Risk (concrete, not theoretical): two concurrent Full scans issuing `ISearchService`/`IModelSearchService` queries and 30 s waits against the same Pilot session, with the orphan continuing to write `analytics.log` and to post `Dispatcher.BeginInvoke` callbacks that mutate a VM whose window is gone. Plus one leaked `CancellationTokenSource` per scan for the window's lifetime. The user has no way to stop the orphan — the only Cancel button went away with the window.
Classification: **MUST_FIX_BEFORE_STAGE_10**.
Suggested minimal action: (1) cancel + dispose the previous source before creating a new one; (2) dispose in the `finally` after `await`; (3) override `OnClosed` to `Cancel()` then `Dispose()`; (4) keep the existing `IsBusy` re-entry guard as the single-scan gate. No async rewrite, no XAML, no VM or service change.
Regression risk: LOW-MEDIUM. Cancel-on-close is an intentional observable change (`report.Cancelled = true` path already exists and is handled at `AnalyticsWindow:143-146`). Must be careful that `Cancel()` after `Dispose()` cannot throw `ObjectDisposedException` into `Cancel_Click`.
Pilot runtime required: **YES** for end-to-end confirmation; the lifetime rules themselves are unit-testable if extracted (see Stage 9.2).

**TD-23 — `IModelSearchService` instances are never released**
Area I. Severity MEDIUM.
Evidence: `Discovery/BimIndexAnalyticsService.cs:119-122` obtains one service **per model group** inside the loop and never disposes or unregisters it; `:394-409 TryRegisterParts` adds every part to it. `Discovery/RemarkAnalyticsService.cs:207-236 TryOpenIndexSearch` does the same once per scan and returns it for use at `:190`, again with no release. Neither is wrapped in `using`.
Risk: if the SDK type implements `IDisposable` or holds native/index handles, each rescan leaks one handle per model group. Cannot be confirmed from this repository — the SDK assemblies live outside it (`C:\Program Files\ASCON\Pilot-BIM\extensions\bim`).
Classification: **DEFER** pending SDK verification; do not add speculative `using` blocks around a type whose disposal semantics are unknown (disposing a shared/cached service could break subsequent queries).
Suggested minimal action: first, reflect over `Ascon.Pilot.Bim.SDK.Search.IModelSearchService` to check for `IDisposable`. Only then decide.
Regression risk: MEDIUM if a `using` is added blindly.
Pilot runtime required: **YES**.

**TD-24 — `IDisposable` subscriptions are discarded**
Area I. Severity MEDIUM.
Evidence: `Data/PilotObjectScanner.cs:125-133 SubscribeObjects` returns `IDisposable`, but every caller ignores the return value — `InventoryService.cs:306,321,332`, `PilotObjectScanner.cs:152,208,290`. Nothing ever unsubscribes; abandonment relies purely on the `ShouldAccept()` flag.
Risk: the Pilot repository may hold observer references for the session's lifetime, and post-timeout callbacks keep arriving (harmlessly, but they still execute). Stage 4's own SDK note says these subscriptions are not cancellable here, which is why abandonment was chosen.
Classification: **DEFER** — consistent with the documented Stage 4 model; changing it means verifying against real SDK semantics.
Suggested minimal action: capture the `IDisposable` in the `CallbackWaitSession` `using` scope at the three synchronous sites and dispose after `Wait` returns.
Regression risk: MEDIUM (disposing a subscription mid-delivery is SDK-behaviour-dependent).
Pilot runtime required: **YES**.

## Thread Safety

**TD-25 — Post-timeout callback races with the waiting thread over `report` collections**
Area J. Severity MEDIUM.
Evidence: `Services/ObjectSamplingCoordinator.cs:101-146`.
- `Exception sampleError = null;` (`:103`) is captured by two lambdas and read by the waiter at `:136` — a plain local, no `volatile`, no lock.
- `ShouldAccept()` is checked **once** at entry (`:108`). If the 30 s timeout fires while the callback is already inside `ApplySampledObjects` (`:110`), that callback keeps writing `typeRecord`, `report.CreatorSampleCounts`, `report.ResponsibleSampleCounts`, `_systemFieldSampleBuffer`, `_historySampleBuffer`, `_documentSampleBuffer` (`:193-240`) — while the waiter has already proceeded to `report.Warnings.Add(...)` (`:133`) and then to the next type, which mutates the same buffers.
- The error lambda (`:122-126`) writes `sampleError` with no abandon check at all.
- `report.Warnings`, `report.Diagnostics`, the sample buffers and the `Dictionary` counters are all plain `List<T>`/`Dictionary<,>`.

Risk: concurrent `List<T>.Add` / `Dictionary` insert from the SDK callback thread and the scan worker thread — the classic corruption/`IndexOutOfRangeException`/`InvalidOperationException` failure mode. Requires the timeout to coincide with an in-flight callback, so it is rare, but it is a genuine data race on shared mutable state, and a mid-scan crash surfaces as the coarse `BLOCKED_BY_SDK` (TD-21) with no diagnosis.
Classification: **SHOULD_FIX**.
Suggested minimal action: re-check `session.ShouldAccept()` inside `ApplySampledObjects`' per-object loop (cheap, matches the pattern `RemarkAnalyticsService:85-86` already uses), make `sampleError` an `Interlocked`/`volatile` field, and add the abandon check to the error lambda. No locking, no structural change.
Regression risk: LOW-MEDIUM — it can turn a "late data accepted" case into a "late data dropped" case, which is the documented intent but is a behaviour delta on the timeout path.
Pilot runtime required: **YES** to reproduce; the guard itself is unit-testable via a fake sampler.

**TD-26 — UI marshalling is correct** — LEAVE_AS_IS. Verified: `AnalyticsWindow.xaml.cs:137-139` and `InventoryWindow.xaml.cs:82-84` marshal progress via `Dispatcher.BeginInvoke`; `InventoryWindow.xaml.cs:168,219` marshal `LoadChildren` callbacks; `_vm.Snapshot = snapshot` (`:142`) runs on the UI thread after `await`. No `ObservableCollection` is touched off-thread anywhere.

## Numeric and Culture Safety

Good news first: `Diagnostics/AnalyticsFormats.cs:14,19,24`, `Export/AnalyticsCsvExporter.cs:152` and all of `ScanDiffService.FormatNum/FormatTimeDelta` (`:277-290`) already use `CultureInfo.InvariantCulture`, so **persisted and exported** numbers are culture-stable. `ObjectCount` is `long` end-to-end and `InventoryService.ResolveObjectCount` (`:285-290`) exists specifically to avoid narrowing.

**TD-27 — Display numbers use current culture**
Area K. Severity LOW.
Evidence: `ProjectAnalyticsService.cs:42` `avgFill.ToString("0.0")`, `:47,239` `RemarksPer1000Elements.ToString("0.0")`, `:231` fill-percent, `:508` `(bimAttr.FillRate * 100).ToString("0")`; `AnalyticsDashboardPresenter.cs:116` `row.SharePercent.ToString("0.0")`; `ChartCanvasControl.xaml.cs:296` `SharePercent.ToString("0.0")`.
Risk: decimal separator follows the operator's Windows locale. Cosmetic for display — and the UI is Russian anyway — but inconsistent with the invariant CSV/JSON paths.
Classification: **DEFER**.

**TD-28 — Scan baselines persist display strings and re-parse them**
Area K. Severity MEDIUM.
Evidence: `Services/ScanDiffService.cs:40-51` copies KPI rows into `StoredKpi { Value = row.Value, NumericValue = TryParseNumber(row.Value) }`; `:292-301 TryParseNumber` strips everything except digits/`-`/`,`/`.`, then replaces `','` with `'.'` and parses invariant. The inputs are formatted display strings from `ProjectAnalyticsService.FormatCount` (`:544-550`, e.g. `"1234 (~)"`) and `FormatIndexedCount` (`:552-557`, e.g. `"1234 ~"`).
Risk: today's KPI values are plain integers with no group separators, so parsing is correct. But the contract is implicit: introduce a thousands separator (`"1 234"` or `"1,234"`) and `1,234` silently becomes `1.234`; localise a KPI *label* and the `KpiLabels` match at `:42` stops finding the row, so the diff quietly loses metrics with no error. The diff feature depends on display formatting never changing.
Classification: **DEFER** — the real fix is stable metric codes on the snapshot (the same prerequisite Stage 8 identified for dual-use strings).
Suggested minimal action (later): carry numeric KPI values on `AnalyticsKpiRow` alongside the formatted string so `ScanDiffService` never parses text.
Regression risk: MEDIUM — changes persisted JSON shape; needs a migration path for existing `last-scan.json`/history files.
Pilot runtime required: NO.

**TD-29 — Overflow/width handling** — LEAVE_AS_IS (see table).

## Null and Collection Safety

**TD-30 — `AnalyticsWindow` ctor validates 2 of 3 dependencies**
Area L. Severity LOW.
Evidence: `Views/AnalyticsWindow.xaml.cs:27-30` throws `ArgumentNullException` for `projectAnalyticsService` and `analyticsCsvExporter`, but `inventory` (`:23`) is stored unchecked at `:33` and dereferenced later at `:136` (`_inventory.Run`). `InventoryWindow.xaml.cs:24-28` performs no validation at all.
Risk: a null `InventoryService` surfaces as an `NullReferenceException` inside the scan task rather than at construction — reported to the user as "Ошибка: Object reference not set…".
Classification: **SHOULD_FIX** (2 lines).
Regression risk: NONE — `AnalyticsCommandService.CreateInventoryService` (`:98-119`) always returns non-null.
Pilot runtime required: NO.

**TD-31 — Callbacks invoked without null guards** — DEFER. Evidence: `ObjectSamplingCoordinator.cs:68,153` call `progress(...)` directly (the `Action<string>` is documented as optional elsewhere — `InventoryService.cs:49` and `BimDiscoveryService.cs:43-48` both null-check theirs); `InventoryService.cs:300,312,325` call `onDone(...)` unchecked. Every current caller passes non-null.

**TD-32 — Live-list aliasing into `DashboardContentContext`** — LEAVE_AS_IS (see table).

Positive observations: `ChartDataService.ExtractRaw` (`:72-139`) null-coalesces every snapshot collection; `ToSeries` guards `max <= 0` and `total <= 0` (`:160-165`); `ChartCanvasControl.Redraw` returns early on empty (`:60-65`) so the `points.Count` divisions in `DrawColumns`/`DrawLine` cannot divide by zero; `DashboardLayoutStore.SanitizeWidget` (`:200-252`) defends every persisted field.

## Localization Dual-Use Debt

**TD-33 — Russian values used as logic keys**
Area M. Severity MEDIUM.
Evidence: `ViewModels/AnalyticsScanComparePresenter.cs:219-231 IsMeaningfulChange` compares `row.Area == "Скан"`, `row.Metric == "Время скана"`, `row.Area == "Ошибка"`; those exact literals are produced in `Services/ScanDiffService.cs:116-134,145,202-208`. `ScanDiffService.KpiLabels` (`:12-20`) matches KPI rows by Russian display label against `ProjectAnalyticsService.cs:35-41`. Diff areas `"Ответственные"`, `"OPEN/CLOSED"`, `"Типы"`, `"IFC"`, `"Замечания"` (`:189-196`) are both UI text and grouping keys.
Risk: localising any of these strings breaks the changes-only filter and the KPI diff **silently** — no exception, just missing rows. This is precisely the debt Stage 8 refused to migrate.
Classification: **DEFER** — needs a stable-code design stage (`Area`/`Metric` codes + display lookup), exactly as `REFACTOR_STAGE8_FINAL.md:55-66` records.
Regression risk: MEDIUM-HIGH if attempted piecemeal; `ScanDiffServiceTests` (9 tests) and `AnalyticsScanComparePresenterTests` (5) pin some of these literals.
Pilot runtime required: NO.

**TD-34 — 243 Russian literals remain in C#** — DEFER. Per-file: `ProjectAnalyticsService` 50, `AnalyticsWindowViewModel` 45, `CapabilityMatrixService` 29, `ScanDiffService` 24, `AnalyticsScanComparePresenter` 20, `AnalyticsWindow.xaml.cs` 19, `InventoryWindowViewModel` 14, `DashboardLayoutStore` 10, `DashboardWidgetEditorWindow.xaml.cs` 10, `InventoryWindow.xaml.cs` 9, `StateDiscoveryService` 7, others 5. These are the medium-risk dynamic strings (`ProgressText`, `ScanDiffHint`, `HeaderSubtitle`, branching `MessageBox`es, KPI labels) that Stage 8 deliberately left — see `REFACTOR_STAGE8_FINAL.md:51-53`.

**TD-35 — Russian titles persisted into layout JSON** — DEFER. Evidence: `DashboardLayoutStore.cs:83-85,156-158,190-194,227-233` write `"KPI / сводка"`, `"BIM"`, `"Ответственные"`, `"График"` into `dashboard-layout.json`. Localising them later leaves existing user files showing the old language, and `EnsureBuiltin`/`MapLegacyBlock` key off `Id` (safe) but re-seed titles by literal.

## String Contracts

**TD-36 — String ids round-tripped through `Enum.TryParse`; status as string constants** — DEFER. Evidence: chart ids (`AnalyticsWindowViewModel.cs:591,596`, `AnalyticsDashboardPresenter.cs:80,85`, `DashboardSectionVm.cs:141-144`) parse `"HorizontalBar"`/`"Types"` etc. back into enums, falling back silently on mismatch; `Models/CapabilityStatus.cs` is a `static class` of string consts rather than an enum, compared by `==` in ~40 places; nav keys are strings matched in `ShowPanel`. All internally consistent, all silently tolerant of typos.

**TD-37 — `"bimObjectId"` repeated in three files**
Area N. Severity LOW.
Evidence: `Discovery/RemarkAnalyticsService.cs:157` (attribute lookup), `Services/ProjectAnalyticsService.cs:454-455` (attribute lookup), `Discovery/BimDiscoveryService.cs:68` (capability description). It is an external Pilot config/attribute contract.
Classification: **SHOULD_FIX** — one `internal const string` in a shared place; explicitly excluded from localization by Stage 8.
Regression risk: NONE (identical literal).
Pilot runtime required: NO.

**TD-38 — `IsRemarkType` heuristic duplicated verbatim**
Area N. Severity MEDIUM.
Evidence: `Discovery/RemarkAnalyticsService.cs:239-248` and `Services/ProjectAnalyticsService.cs:471-480` are byte-identical: `name.Contains("issue") || name.Contains("remark") || title.Contains("замечан")`.
Risk: the inventory side decides *which types get sampled for remark links* and the analytics side decides *which types are counted as remarks in the KPIs*. If one copy is edited (e.g. adding `"замечание"` handling, or localisation touching `"замечан"`), the two disagree and KPI counts stop matching the sampled links — a silent data inconsistency in the headline "Замечаний к модели" metric.
Classification: **SHOULD_FIX**.
Suggested minimal action: move one copy into an `internal static` helper (e.g. next to `AnalyticsFormats`) and call it from both. Pure extraction.
Regression risk: LOW; `ProjectAnalyticsServiceTests` (9 tests) exercise the analytics side.
Pilot runtime required: NO.

## Logging and Diagnostics

**TD-39 — Diagnostic blind spots**
Area O. Severity LOW.
Evidence and gaps:
- No scan **duration**: `Run` logs progress text only; nothing records elapsed time per zone, so "the scan is slow" is undiagnosable. `report.PerformanceNotes` (`InventoryService.cs:214-229`) carries caps and sources but no timings.
- No **correlation id** per scan: with two windows (or the orphaned scan of TD-22) the single `analytics.log` interleaves lines with no way to attribute them.
- Timeouts are logged as warnings but **not counted** in the report summary, so a scan where 40 types timed out reports `PARTIAL_RUNTIME_INVENTORY` identically to a clean one.
- Log level is fixed; there is no way to quiet the per-progress-line `Info` writes (which feed TD-03).
- No rotation (TD-03).
Classification: **DEFER**.
Suggested minimal action (later): stopwatch around each zone into `PerformanceNotes`, plus a per-`Run` GUID prefix.
Regression risk: LOW.
Pilot runtime required: NO.

## Test Coverage Gaps

Current distribution (118 `[Fact]`, 0 `[Theory]`): `ChartDataServiceTests` 14, `CallbackWaitSessionTests` 9, `ProjectAnalyticsServiceTests` 9, `ReferenceResolverTests` 9, `ScanDiffServiceTests` 9, `DashboardLayoutStoreTests` 7, `LocalizationResourcesTests` 7, `ScanSnapshotStoreTests` 7, `BimIndexAnalyticsServiceTests` 6, `AnalyticsScanComparePresenterTests` 5, `BimDiscoveryServiceTests` 5, `CreatorAggregationServiceTests` 5, `DocumentDiscoveryServiceTests` 5, `AnalyticsDashboardPresenterTests` 4, `AnalyticsCsvExporterTests` 3, `AnalyticsFormatsTests` 3, `InventoryServiceObjectCountTests` 3, `StateMappingServiceTests` 3, `HistoryDiscoveryServiceTests` 2, `SampleBufferLifecycleTests` 2, `TypeDiscoveryServiceTests` 1.

**TD-40 — Untested behaviours (scenario-based)**
Area P. Severity MEDIUM.
| Untested unit | Scenario that would fail unnoticed |
|---------------|------------------------------------|
| `AnalyticsWindowViewModel` (0 tests) | Snapshot reload leaves a stale `BimModelFilters` selection; `ShowBimModelFilter` disagrees with the Window (TD-06); reload cascade change (TD-11) drops a collection |
| `ObjectSamplingCoordinator` (only buffer lifecycle, 2 tests) | Timeout marks the wrong `CapabilityStatus`; `FillPercent` averaging wrong when `SampledCount == 0`; walk-bucket path vs search path divergence |
| `PilotObjectScanner` / `PilotObjectSampler` | `SearchByType` firing `onDone` twice; `SubscribeObject` returning a non-`Loaded` object; cache-hit short-circuit |
| `InventoryService.LoadChildren` | Duplicate `onDone` (TD-14); empty-children path |
| `HierarchyWalkSampler` | `Truncated` not set at the visit budget; BFS revisiting a node; root resolution fallback |
| `InventoryReportService.BuildTextReport` (221 LOC, 0 tests) | Null section crashing the Copy/Save report buttons |
| `CapabilityMatrixService` (77 LOC, 0 tests) | Readiness computed from an empty report |
| `StateDiscoveryService` / `PersonDiscoveryService` / `OrganisationDiscoveryService` | `ApplyObservedUsage` mis-attributing counts; org→person linkage |
| `RemarkAnalyticsService` | `RemarksPer1000Elements` when `indexed == 0`; `bimObjectId` casing variants |
| `ChartCanvasControl` geometry | The `420`/`220` contract (TD-08) — service side is pinned by a test, the renderer side is not |
| `AnalyticsLogger` | Truncation at 500/800/400 chars; never-throw guarantee |
Classification: **SHOULD_FIX** (incrementally, alongside whichever area a later stage touches — not as a standalone coverage sprint).
Regression risk: NONE (tests only). Note `AnalyticsWindowViewModel` tests require the TD-13 seam first.
Pilot runtime required: NO.

**TD-41 — Store tests are not hermetic**
Area P. Severity MEDIUM.
Evidence: `ScanSnapshotStoreTests.cs:17` instantiates the real `ScanSnapshotStore` and its ctor/`Dispose` (`:23-45`) save and restore the developer's **real** `%LOCALAPPDATA%\PilotBim.Analytics\Snapshots\last-scan.json`; `AnalyticsScanComparePresenterTests.cs:57,71,92` also construct real `ScanSnapshotStore`s (and `RefreshOnSnapshot` calls `SaveLast` + `ArchiveToHistory`); `AnalyticsCsvExporterTests.cs:90` writes into the real `Exports` folder; `StateMappingServiceTests.cs:20` reads the real `state-mapping.json`. Paths are `private static` in the stores (`ScanSnapshotStore.cs:19-43`, `DashboardLayoutStore.cs:13-22`) with no injection point.
Risk: xUnit runs distinct test classes as parallel collections, and two of these classes write the same `last-scan.json` — an order-dependent flake that would look like a mysterious CI failure. The suite also mutates real user data on a developer machine (the test file even comments that it avoids filling the ring buffer "to not risk dropping user history").
Classification: **SHOULD_FIX**.
Suggested minimal action: put the filesystem-touching classes in a shared `[Collection("LocalAppDataStore")]` to serialise them (2 attributes, zero production change). A proper path seam is the larger, later option.
Regression risk: NONE.
Pilot runtime required: NO.

## Large Methods

Measured by brace-depth scan over `src` (excluding `Resources.Designer.cs`): **36 methods ≥50 LOC, 11 ≥80, 5 ≥100**.

| LOC | Method | File |
|----:|--------|------|
| 221 | `BuildTextReport` | `Services/InventoryReportService.cs:12` |
| 216 | `ProbeElements` | `Discovery/BimDiscoveryService.cs:217` |
| 210 | `Run` | `Services/InventoryService.cs:35` |
| 156 | `Analyze` | `Discovery/BimIndexAnalyticsService.cs:59` |
| 118 | `SampleAllTypes` | `Services/ObjectSamplingCoordinator.cs:40` |
| 110 | `Walk` | `Data/HierarchyWalkSampler.cs:26` |
| 107 | `Diff` | `Services/ScanDiffService.cs:106` |
| 104 | `Export` | `Export/AnalyticsCsvExporter.cs:18` |
| 102 | `SampleHistory` | `Discovery/DocumentDiscoveryService.cs:137` |
| 101 | `Analyze` | `Discovery/RemarkAnalyticsService.cs:38` |
| 101 | `CountBimTypes` | `Discovery/BimDiscoveryService.cs:102` |
| 80 | `Aggregate` | `Discovery/CreatorAggregationService.cs:17` |
| 77 | `Build` | `Services/CapabilityMatrixService.cs:10` |
| 76 | `PopulateStaticSdkInventory` | `Discovery/PilotSdkDiscoveryService.cs:12` |
| 75 | `Capture` | `Services/ScanDiffService.cs:22` |
| 73 | `ReloadCollections` | `ViewModels/AnalyticsWindowViewModel.cs:371` |
| 68 | `BuildResponsibleDistribution` | `Services/ProjectAnalyticsService.cs:263` |
| 66 | `BuildRow` | `Discovery/RemarkAnalyticsService.cs:140` |
| 65 | `Discover` | `Discovery/BimDiscoveryService.cs:36` |
| 64 | `DrawHorizontal` | `Views/ChartCanvasControl.xaml.cs:99` |
| 63 | `RefreshOnSnapshot` | `ViewModels/AnalyticsScanComparePresenter.cs:92` |
| 62 | `DrawPie` | `Views/ChartCanvasControl.xaml.cs:275` |
| 61 | `Build` | `Services/ProjectAnalyticsService.cs:15` |
| 60 | `RebuildWidgetContent` | `ViewModels/AnalyticsDashboardPresenter.cs:62` |

**TD-42 — Method length**
Area Q. Severity LOW.
Risk: `ProbeElements` (216) is the only one I would call genuinely hard to hold in your head — it mixes model-id resolution, storage readiness polling, root/part element sampling and property extraction, with 6 nested `try/catch` blocks. The rest are long but flat: `BuildTextReport` and `Export` are sequential emitters, `Run`/`Analyze`/`SampleAllTypes` are ordered pipelines with per-step guards, `Draw*` are geometry emitters.
Classification: **DEFER**.
Suggested minimal action (later, and only for `ProbeElements`): extract `ResolveProbeModelId`, `WaitForStorage`, `CollectSampleElements`, `ReadElementProperties` — four private methods, no behaviour change.
Regression risk: MEDIUM — `ProbeElements` is the least testable code in the repo (needs a loaded model in the viewer), so a refactor cannot be validated without Pilot.
Pilot runtime required: **YES**.

## Dependency Construction

**TD-43 — Construction sprawl**
Area R. Severity LOW.
Evidence: composition stops at `AnalyticsCommandService` (`:44,71,79-82,118`) as Stage 5 intended, but below it everything is `new`ed inline: ~15 discovery/service types in `InventoryService.Run`; `new PilotObjectScanner(...)` appears 4× inside `LoadChildren` alone (`:294,306,321,332`) plus `:120`, and again in `BimDiscoveryService:126,250`, `BimIndexAnalyticsService` (via parameter), `ObjectSamplingCoordinator:52`, `RemarkAnalyticsService:60`; root VM news its presenters (`:35-38`), which news their stores.
Risk: not substitutability (the concretes are pure or disk-only) but testability — the only seams are the two Stage 5 interfaces and the presenters' `internal` ctors.
Classification: **DEFER** — Stages 5 and 6 both examined this and concluded that per-type interfaces would be noise. `PilotObjectScanner` is stateless, so the repeated `new` is cosmetic.
Suggested minimal action (later): hoist a single scanner field in `InventoryService`; add the VM ctor seam of TD-13.
Regression risk: LOW-MEDIUM (Stage 6 flagged "scanner field reuse changes subtle identity" as unproven — respect that).
Pilot runtime required: NO.

## Deferred Items from Previous Stages

**TD-44 — Backlog lives in docs, not in code** — LEAVE_AS_IS. Zero `TODO`/`FIXME`/`HACK`/`XXX`/`NotImplementedException` markers in production code; the only suppressions are the three `#pragma warning disable 612` of TD-17.

Carried-forward register, with this audit's re-verdict:

| From | Deferred item | Still true? | Stage 9 verdict |
|------|---------------|-------------|-----------------|
| Stage 4 (rows 12–13) | Async rewrite of BIM index `.GetResult()` chain | Yes — 4 sites | DEFER (TD-01) |
| Stage 4 (row 4) | `Thread.Sleep` `IsLoaded` poll | Yes — 1 site | LEAVE_AS_IS (TD-18) |
| Stage 4 | Fake SDK cancellation | Yes — abandonment only | LEAVE_AS_IS |
| Stage 5 | `IInventoryService` after decompose | Yes — not introduced | LEAVE_AS_IS (TD-16) |
| Stage 5 | `PilotObjectScanner` instance reuse / `new` sprawl | Yes — 4× in `LoadChildren` | DEFER (TD-43) |
| Stage 5 | Static `AnalyticsLogger` abstraction | Yes | DEFER (TD-04) |
| Stage 5 | Deeper MVVM (Build/Export out of the View) | Yes — `Refresh_Click` still orchestrates | LEAVE_AS_IS (TD-07) |
| Stage 5 | Mirror injection for `InventoryWindow` report helpers | Yes — `new InventoryReportService()` at `:88,141`, `new InventoryReportExporter()` at `:133` | DEFER |
| Stage 6 | `InventoryPipeline` extract | Yes — `Run` 210 LOC | DEFER (TD-15) |
| Stage 6 | Structure-service split (`LoadChildren`/`GetRootObject` on the scan façade) | Yes | DEFER; but fix the callback latch (TD-14) |
| Stage 7 | Chart presenter extraction | Yes — chart wiring in root VM | LEAVE_AS_IS (`NOT_NEEDED_NOW`) |
| Stage 7 | `ShowBimModelFilter` vs Window visibility duplicate | Yes — verified both copies | **SHOULD_FIX** (TD-06) — smallest real cleanup remaining |
| Stage 7 | Section UserControl / navigation redesign | Yes — 18 panels | LEAVE_AS_IS (TD-05) |
| Stage 7 | `ICommand` migration | Yes | LEAVE_AS_IS (TD-07) |
| Stage 8 | Dual-use `ScanDiff` `Area`/`Metric`, `KpiLabels` | Yes — verified as logic keys | DEFER (TD-33) |
| Stage 8 | Nav keys / chart ids / widget kinds / block ids | Yes | DEFER (TD-36) |
| Stage 8 | Remark heuristic `замечан` | Yes — and duplicated | **SHOULD_FIX** the duplication (TD-38); keep the literal |
| Stage 8 | Runtime culture switching, extra languages | Not implemented | DEFER |
| Stage 8 | Medium-risk dynamic strings (`ProgressText`, hints, `HeaderSubtitle`) | Yes — 243 literals | DEFER (TD-34) |

## Pilot SDK / MEF Boundary

MEF surface (unchanged since Stage 1, correct): `[Export(typeof(IDataPlugin))] Extension` (`Plugin/Plugin.cs:9-28`), `AnalyticsMainMenuCommand` + `AnalyticsOverviewMenuCommand` (`IMenu<MainViewContext>`), `AnalyticsContextMenuCommand` (`IMenu<ObjectsViewContext>`), `AnalyticsToolbarCommand` (`IToolbar<ObjectsViewContext>`), `[Export] AnalyticsCommandService` with `[ImportingConstructor]` and `[Import(AllowDefault = true)] ISearchService` (`:27-38`). Optional BIM services are resolved defensively through `IPilotServiceProvider.GetServices<T>()` with per-service `try/catch` and logging (`:121-165`) — the right pattern for optionals that may be absent.

**TD-45 — Public surface wider than needed**
Area T. Severity LOW.
Evidence: 44 `public` types. Justified: MEF exports; `Models/*` (needed for `DataContractJsonSerializer` round-trips and XAML binding); the two Stage 5 interfaces. Questionable: `InventoryService` is `public` although only ever constructed by `AnalyticsCommandService` and consumed by two windows in the same assembly; `AnalyticsWindow`, `InventoryWindow`, `DashboardWidgetEditorWindow`, `SimplePromptWindow`, `ChartCanvasControl` are `public` (WPF default) though never referenced externally. `InternalsVisibleTo("PilotBim.Analytics.Tests")` already exists (`csproj:25-27`), so tests do not need `public`.
Risk: a wider-than-intended API for a plugin DLL; `InventoryService` being public is what makes TD-02's invariant reachable from outside.
Classification: **DEFER** — cosmetic; narrowing `x:Class` accessibility is fiddly for no functional gain.

**TD-46 — SDK references are unversioned**
Area T. Severity LOW.
Evidence: `csproj:37-51` — three `Reference`s with `SpecificVersion=false`, `Private=false`, `HintPath` under `$(PilotSdkPath)` (default `C:\Program Files\ASCON\Pilot-BIM`). `BimDiscoveryService.cs:53-57` reads the BIM SDK version at runtime **into the report**, but nothing validates it.
Risk: the plugin binds to whatever SDK the host installs; a breaking SDK change surfaces as a `MissingMethodException` inside a scan (caught → `BLOCKED_BY_SDK`) rather than a clear "unsupported Pilot version" message.
Classification: **DEFER**.
Suggested minimal action (later): log and report a minimum-supported-version comparison at plugin load, next to the existing version line in `Plugin.cs:21-22`.
Pilot runtime required: YES to test.

## Repository Hygiene

Solid: `.gitignore` covers `bin/ obj/ dist/ .vs/ TestResults/ *.dll *.user` etc.; two-project solution; `scripts/build.cmd` (Release + copy to `dist/`), `scripts/test.cmd`, `scripts/deploy-dev.ps1` (warns if Pilot is running, deploys to the `Development` extension folder, prints version and log path); `Resources.Designer.cs` checked in with the generator disabled and a comment explaining why (`csproj:28-34`); `InternalsVisibleTo` present; docs directory carries one audit + one final per stage; working tree clean, no stray build output tracked.

**TD-47 — Missing build/repo guardrails**
Area U. Severity LOW.
Evidence: no `.github/` or any CI definition (0 files); `TargetFramework`, `LangVersion`, `Nullable`, `PlatformTarget`, `AppendTargetFrameworkToOutputPath`, `PilotSdkPath` are duplicated across both `csproj`s with no `Directory.Build.props`; no `.editorconfig`; no `TreatWarningsAsErrors` (the 0-warning baseline is maintained by convention only); no analyzer package.
Risk: the "0 errors / 0 warnings, 118 PASS" gate is a human ritual, not enforced — a future PR can regress it silently. SDK-dependent tests make hosted CI awkward, which is presumably why none exists.
Classification: **DEFER**.
Suggested minimal action (later): `Directory.Build.props` for the shared properties plus `TreatWarningsAsErrors` in Release.
Regression risk: LOW, but flipping warnings-as-errors can block the build on a machine with a different SDK — verify before adopting.

**TD-48 — Stale assembly metadata**
Area U. Severity LOW.
Evidence: `csproj:10` `<Description>Stage 1 + BIM index analytics via IModelSearchService (no viewer required).</Description>` — eight stages out of date; `:13-15` `Version`/`FileVersion` `0.9.0` with `InformationalVersion` `0.9.0-rich-diff-to-dashboard`, which `Plugin.cs:18-22` logs as the plugin's identity and `deploy-dev.ps1:22-25` prints on deploy. Stages 2–8 did not bump it, so every build since is indistinguishable in the log.
Risk: field diagnostics cannot tell which build produced a log; the description misrepresents the plugin in the host's extension list.
Classification: **SHOULD_FIX** (metadata only).
Suggested minimal action: refresh `Description` and bump `InformationalVersion` to reflect the current stage. No code impact.
Regression risk: NONE — but it does change the `dist` artefact's version, so do it as its own commit, not folded into a behavioural change.
Pilot runtime required: NO.

**TD-49 — Multiple types per file** — LEAVE_AS_IS (see table).

## Prioritization Matrix

Sorted MUST → SHOULD → DEFER → LEAVE_AS_IS; within a band, by severity then blast radius.

| # | ID | Class | Sev | Area | Finding | Minimal action | Regr. risk | Pilot runtime |
|--:|----|-------|-----|------|---------|----------------|-----------|---------------|
| 1 | TD-22 | **MUST_FIX_BEFORE_STAGE_10** | HIGH | I | CTS never disposed / scan not cancelled on close / concurrent orphan scans | Cancel+dispose previous CTS, dispose in `finally`, `OnClosed` cancel | LOW-MED | YES |
| 2 | TD-38 | SHOULD_FIX | MED | N | `IsRemarkType` duplicated verbatim in 2 services | Extract one shared helper | LOW | NO |
| 3 | TD-25 | SHOULD_FIX | MED | J | Post-timeout callback races with waiter on `report` lists | Re-check `ShouldAccept` per object; volatile `sampleError` | LOW-MED | YES |
| 4 | TD-14 | SHOULD_FIX | MED | F | `LoadChildren` may call `onDone` twice | Add the `finished` latch used elsewhere | LOW | NO |
| 5 | TD-08 | SHOULD_FIX | MED | D | `420.0`/`220.0` duplicated from `ChartDataService` consts | Reference shared named constants | LOW | NO |
| 6 | TD-06 | SHOULD_FIX | MED | C | BIM filter visibility duplicated VM/Window | Window reads `_vm.ShowBimModelFilter` | LOW | NO |
| 7 | TD-11 | SHOULD_FIX | MED | E | Chart builder rebuilt ×4, dashboard ×3 per snapshot | Delete 2 redundant calls (needs VM tests first) | MED | NO |
| 8 | TD-41 | SHOULD_FIX | MED | P | Store tests share real `%LOCALAPPDATA%`, parallel-flake risk | Shared xUnit collection | NONE | NO |
| 9 | TD-40 | SHOULD_FIX | MED | P | No tests for VM / coordinator / scanner / walk / renderer | Add incrementally per touched area | NONE | NO |
| 10 | TD-03 | SHOULD_FIX | MED | B | `analytics.log` unbounded, per-line open/append | Single-generation size-based rotation | LOW | NO |
| 11 | TD-02 | SHOULD_FIX | LOW | A | UI-thread invariant for `Run` undocumented | XML `<remarks>` | NONE | NO |
| 12 | TD-30 | SHOULD_FIX | LOW | L | `AnalyticsWindow` ctor validates 2 of 3 deps | Add the third null check | NONE | NO |
| 13 | TD-37 | SHOULD_FIX | LOW | N | `"bimObjectId"` in 3 files | One `internal const` | NONE | NO |
| 14 | TD-48 | SHOULD_FIX | LOW | U | Stale `csproj` Description/Version | Refresh metadata | NONE | NO |
| 15 | TD-01 | DEFER | MED | A | 4 `.GetAwaiter().GetResult()` BIM index bridges | None (worker-only today) | HIGH if attempted | YES |
| 16 | TD-23 | DEFER | MED | I | `IModelSearchService` never released | Verify `IDisposable` via SDK first | MED | YES |
| 17 | TD-24 | DEFER | MED | I | `SubscribeObjects` `IDisposable` discarded | Dispose inside session scope | MED | YES |
| 18 | TD-13 | DEFER | MED | E | VM ctor does disk I/O; no seam → no VM tests | Add `internal` VM ctor with the tests | LOW | NO |
| 19 | TD-28 | DEFER | MED | K | Baseline stores display strings, regex re-parse | Carry numeric KPI values | MED (JSON shape) | NO |
| 20 | TD-33 | DEFER | MED | M | RU `ScanDiff` `Area`/`Metric` + `KpiLabels` are logic keys | Stable codes + display lookup (own stage) | MED-HIGH | NO |
| 21 | TD-42 | DEFER | LOW | Q | 36 methods ≥50 LOC; `ProbeElements` 216 | Extract 4 privates from `ProbeElements` only | MED | YES |
| 22 | TD-43 | DEFER | LOW | R | `new`-graph sprawl; 4× scanner in `LoadChildren` | Hoist one scanner field | LOW-MED | NO |
| 23 | TD-19 | DEFER | LOW | G | Timeouts hardcoded per site, no mode scaling | `ScanTimeouts` constants | LOW | YES |
| 24 | TD-20 | DEFER | LOW | H | 9 catch-alls, 3 silent | Log the 3, rate-limited | LOW | NO |
| 25 | TD-04 | DEFER | LOW | B | Static logger not injectable | None until a 2nd sink exists | MED | NO |
| 26 | TD-39 | DEFER | LOW | O | No scan timing / correlation id / rotation | Stopwatch per zone + run id | LOW | NO |
| 27 | TD-27 | DEFER | LOW | K | Display numbers use current culture | Invariant or explicit culture | LOW | NO |
| 28 | TD-31 | DEFER | LOW | L | `progress`/`onDone` unguarded | Null-check like sibling code | NONE | NO |
| 29 | TD-34 | DEFER | LOW | M | 243 RU literals in C# | Continue only with stable-code design | MED | NO |
| 30 | TD-35 | DEFER | LOW | M | RU widget titles persisted in JSON | Title-by-id lookup | MED (user files) | NO |
| 31 | TD-36 | DEFER | LOW | N | String ids via `Enum.TryParse`; string status consts | None | MED | NO |
| 32 | TD-12 | DEFER | LOW | E | VM 637 LOC, inline nav/option lists | None (Stage 7 stop) | MED | NO |
| 33 | TD-15 | DEFER | LOW | F | `Run` 210 LOC, ~12 inline `new` | None (Stage 6 stop) | MED | YES |
| 34 | TD-09 | DEFER | LOW | D | Full chart re-layout on every `SizeChanged` | Debounce redraw | LOW | NO |
| 35 | TD-10 | DEFER | LOW | D | RU literal in `ChartCanvasControl` | Move to resx | NONE | NO |
| 36 | TD-45 | DEFER | LOW | T | Public surface wider than needed | Narrow to `internal` | LOW | NO |
| 37 | TD-46 | DEFER | LOW | T | SDK refs unversioned, no load-time check | Min-version log/report | LOW | YES |
| 38 | TD-47 | DEFER | LOW | U | No CI, no `Directory.Build.props`/`.editorconfig`/warnings-as-errors | Shared props file | LOW | NO |
| 39 | TD-05 | LEAVE_AS_IS | LOW | C | 18+13 sibling-panel `ShowPanel` | — | — | — |
| 40 | TD-07 | LEAVE_AS_IS | LOW | C | No `ICommand`; `MessageBox` in views | — | — | — |
| 41 | TD-16 | LEAVE_AS_IS | LOW | F | Public fat `InventoryService`, no interface | — | — | — |
| 42 | TD-17 | LEAVE_AS_IS | LOW | F | Obsolete SDK members via `#pragma 612` | — | — | — |
| 43 | TD-18 | LEAVE_AS_IS | LOW | G | `Thread.Sleep(200)`×20 `IsLoaded` poll | — | — | — |
| 44 | TD-21 | LEAVE_AS_IS | LOW | H | Coarse outer catch → `BLOCKED_BY_SDK` | — | — | — |
| 45 | TD-26 | LEAVE_AS_IS | — | J | `Dispatcher.BeginInvoke` marshalling is correct | — | — | — |
| 46 | TD-29 | LEAVE_AS_IS | — | K | `long` counts, guarded narrowing, capped hits | — | — | — |
| 47 | TD-32 | LEAVE_AS_IS | LOW | L | Live-list aliasing into dashboard context | — | — | — |
| 48 | TD-44 | LEAVE_AS_IS | — | S | Backlog in docs, zero `TODO` markers | — | — | — |
| 49 | TD-49 | LEAVE_AS_IS | LOW | U | Multiple types per file | — | — | — |

Counts: **MUST_FIX_BEFORE_STAGE_10 = 1**, **SHOULD_FIX = 13**, **DEFER = 24**, **LEAVE_AS_IS = 11**.

## Recommended Stage 9.2

### Title

**Stage 9.2 — Scan session lifetime and cancellation hardening (TD-22)**

### Why this one

It is the only finding in this audit backed by evidence of a real runtime defect rather than a maintainability preference. Everything else either has no observable consequence today (TD-08, TD-11, TD-37), is explicitly deferred by an earlier stage's stop decision (TD-01, TD-05, TD-33), or cannot be acted on without first verifying SDK semantics (TD-23, TD-24).

TD-22 fails a criterion this plugin is otherwise careful about: *the user must always be able to stop work against the Pilot SDK.* Right now, closing the window during a scan removes the Cancel button while leaving the scan running, and `AnalyticsCommandService` will happily construct a second window and a second scan on top of it. The fix is small, local to two code-behind files, requires no async rewrite, no XAML, no VM or service change — and it closes the last lifetime gap left after Stage 4 fixed callback abandonment and Stage 6 fixed buffer lifetime.

### Scope

1. Extract a small `internal sealed class ScanSessionScope` (or equivalent) owning one `CancellationTokenSource`: `Begin()` → token, `Cancel()`, `Complete()`, `Dispose()`, with idempotent cancel-after-dispose and an `IsActive` flag. This exists so the rules are unit-testable without a WPF window.
2. `AnalyticsWindow`: use the scope in `Refresh_Click` (cancel + dispose any previous source before `Begin`), dispose in the existing `finally`, and override `OnClosed` to cancel the active scan. `Cancel_Click` delegates to the scope.
3. `InventoryWindow`: the same three changes in `Scan_Click` / `Cancel_Click` / `OnClosed`.
4. Keep the existing `if (_vm.IsBusy) return;` re-entry guard exactly as-is — it stays the single-scan gate.

### Files

- `src/PilotBim.Analytics/Views/AnalyticsWindow.xaml.cs` (modify: `_cts` field, `Refresh_Click`, `Cancel_Click`, new `OnClosed`)
- `src/PilotBim.Analytics/Views/InventoryWindow.xaml.cs` (modify: `_cts` field, `Scan_Click`, `Cancel_Click`, new `OnClosed`)
- `src/PilotBim.Analytics/Services/ScanSessionScope.cs` (new, ~60 LOC)
- `tests/PilotBim.Analytics.Tests/ScanSessionScopeTests.cs` (new)

### Tests

New `ScanSessionScopeTests` (~8 facts), expected total **118 → ~126 PASS**:

1. `Begin` returns a live, non-cancelled token.
2. `Cancel` cancels the active token.
3. A second `Begin` cancels and disposes the first source; the first token reports cancellation.
4. `Complete` disposes the source and clears `IsActive`.
5. `Cancel` after `Complete`/`Dispose` is a no-op — **does not** throw `ObjectDisposedException` (this is the regression the Cancel button would otherwise hit).
6. `Dispose` is idempotent.
7. `Dispose` while active cancels first (the window-close contract).
8. `IsActive` transitions Begin → true, Complete/Dispose → false.

No new tests touch the filesystem, the Pilot SDK, or WPF, so the suite stays hermetic and TD-41 is not made worse.

### Behavior classification

**CORRECTNESS_FIX.** Two intentional observable changes: (a) closing a window during a scan now cancels that scan — it takes the already-implemented `report.Cancelled` path (`AnalyticsWindow.xaml.cs:143-146`, `InventoryWindow.xaml.cs:89-91`); (b) starting a new scan cancels any still-running previous one instead of orphaning it. All non-cancelled scan behaviour, output, formatting, zone ordering, timeouts and SDK call patterns are unchanged.

### Exclusions (explicitly out of scope for 9.2)

- No async rewrite of `.GetAwaiter().GetResult()` (TD-01) and no change to `CallbackWaitSession`, timeouts, or the `IsLoaded` poll.
- No `ShowPanel` / navigation / panel-structure change, no XAML, no resx, no `csproj` edits.
- No ViewModel split, no presenter changes, no reload-cascade dedupe (TD-11) — that needs VM tests (TD-13/TD-40) first.
- No chart geometry change (TD-08), no localization work (TD-33/TD-34), no logging rotation (TD-03).
- No MEF/DI changes: `AnalyticsCommandService` window reuse and the `Closed` handlers stay as they are.
- No `IModelSearchService` or subscription disposal (TD-23/TD-24) — both require SDK verification against a Pilot runtime.
