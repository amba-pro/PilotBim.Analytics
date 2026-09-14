using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Properties;
using PilotBim.Analytics.Services;

namespace PilotBim.Analytics.ViewModels
{
    /// <summary>
    /// Snapshot/chart inputs needed to fill dashboard widget content.
    /// Owned by root VM; passed into presenter at rebuild time.
    /// </summary>
    internal sealed class DashboardContentContext
    {
        public ProjectAnalyticsSnapshot Snapshot { get; set; }
        public ChartDataService Charts { get; set; }
        public IList<BimElementTypeCountRow> AllBimTypes { get; set; }
        public BimModelFilterItem Filter { get; set; }
    }

    /// <summary>
    /// Dashboard layout / CRUD / persist orchestration. DB-9: V2 definition owner,
    /// coordinator session, sequential Query refresh.
    /// </summary>
    internal sealed class AnalyticsDashboardPresenter : IDisposable
    {
        private readonly DashboardLayoutStore _dashboardLayoutStore;
        private readonly Func<DashboardContentContext> _content;
        private readonly Action<string> _notify;
        private readonly Guid _projectKey;
        private readonly DashboardDefinitionStore _definitionStore;
        private IDashboardTypeDatasetProvider _typeProvider;
        private readonly Action<Action> _postToUi;
        private readonly bool _v2;

        private DashboardLayoutState _dashboardLayout;
        private DashboardDefinition _definition;
        private DashboardFieldCatalog _catalog;
        private IReadOnlyList<DashboardObjectTypeOption> _typeOptions;
        private DashboardQueryCoordinator _coordinator;
        private int _applyGeneration;
        private int _disposed;
        private Task _refreshTask = Task.CompletedTask;
        private bool _mutationsEnabled = true;
        private string _dashboardWarning;
        private bool _lastSaveFailed;

        public AnalyticsDashboardPresenter(
            Action<string> notifyPropertyChanged,
            Func<DashboardContentContext> contentProvider)
            : this(notifyPropertyChanged, contentProvider, null)
        {
        }

        internal AnalyticsDashboardPresenter(
            Action<string> notifyPropertyChanged,
            Func<DashboardContentContext> contentProvider,
            AnalyticsDashboardRuntimeOptions runtime)
            : this(
                runtime != null && runtime.LayoutStore != null ? runtime.LayoutStore : new DashboardLayoutStore(),
                notifyPropertyChanged,
                contentProvider,
                runtime)
        {
        }

        internal AnalyticsDashboardPresenter(
            DashboardLayoutStore store,
            Action<string> notifyPropertyChanged,
            Func<DashboardContentContext> contentProvider)
            : this(store, notifyPropertyChanged, contentProvider, null)
        {
        }

        internal AnalyticsDashboardPresenter(
            DashboardLayoutStore store,
            Action<string> notifyPropertyChanged,
            Func<DashboardContentContext> contentProvider,
            AnalyticsDashboardRuntimeOptions runtime)
        {
            _notify = notifyPropertyChanged ?? (_ => { });
            _content = contentProvider ?? (() => null);
            DashboardLayoutItems = new ObservableCollection<DashboardLayoutItemVm>();
            DashboardWidgets = new ObservableCollection<DashboardWidgetVm>();
            _catalog = new PilotFieldCatalogBuilder().Build(Enumerable.Empty<TypeInventoryRecord>());
            _typeOptions = new List<DashboardObjectTypeOption>();

            if (runtime != null && runtime.DefinitionStore != null && runtime.ProjectKey != Guid.Empty)
            {
                _v2 = true;
                _projectKey = runtime.ProjectKey;
                _definitionStore = runtime.DefinitionStore;
                _dashboardLayoutStore = runtime.LayoutStore ?? store ?? new DashboardLayoutStore();
                _typeProvider = runtime.TypeDatasetProvider ?? new NullTypeDatasetProvider();
                _postToUi = runtime.PostToUi ?? (action => { if (action != null) action(); });
                LoadV2();
            }
            else
            {
                _v2 = false;
                _dashboardLayoutStore = store ?? new DashboardLayoutStore();
                _postToUi = action => { if (action != null) action(); };
                InitLayout();
            }
        }

        public ObservableCollection<DashboardLayoutItemVm> DashboardLayoutItems { get; private set; }
        public ObservableCollection<DashboardWidgetVm> DashboardWidgets { get; private set; }

        internal bool IsV2Runtime { get { return _v2; } }
        internal bool MutationsEnabled { get { return _mutationsEnabled; } }
        internal string DashboardWarning { get { return _dashboardWarning; } }
        internal DashboardDefinition CurrentDefinition { get { return _definition; } }
        internal DashboardQueryCoordinator Coordinator { get { return _coordinator; } }
        internal DashboardFieldCatalog Catalog { get { return _catalog; } }
        internal IReadOnlyList<DashboardObjectTypeOption> TypeOptions { get { return _typeOptions; } }
        internal Task RefreshTask { get { return _refreshTask ?? Task.CompletedTask; } }
        internal bool LastSaveFailed { get { return _lastSaveFailed; } }
        internal int ApplyGeneration { get { return Volatile.Read(ref _applyGeneration); } }

        public void InitLayout()
        {
            if (_v2)
                return;
            _dashboardLayout = _dashboardLayoutStore.LoadOrDefault();
            RebuildUi();
        }

        public void RebuildWidgetContent()
        {
            if (DashboardWidgets == null)
                return;

            var ctx = _content() ?? new DashboardContentContext();
            var snapshot = ctx.Snapshot;
            var charts = ctx.Charts;
            IEnumerable<BimElementTypeCountRow> ifc = ctx.AllBimTypes ?? new List<BimElementTypeCountRow>();
            var filter = ctx.Filter;
            if (filter != null && !filter.IsAll && filter.ModelId != Guid.Empty)
                ifc = ifc.Where(t => t.ModelId == filter.ModelId);

            foreach (var widget in DashboardWidgets)
            {
                if (widget.IsQueryWidget)
                    continue;

                if (widget.IsChart)
                {
                    AnalyticsChartKind kind;
                    if (!Enum.TryParse(widget.ChartKindName ?? "", out kind))
                        kind = AnalyticsChartKind.HorizontalBar;
                    widget.ChartKind = kind;

                    AnalyticsChartSource source;
                    if (!Enum.TryParse(widget.ChartSource ?? "", out source))
                        source = AnalyticsChartSource.Types;

                    widget.ChartSeries = snapshot == null || charts == null
                        ? new List<ChartSeriesPoint>()
                        : charts.BuildSeries(snapshot, ifc, source, kind, widget.TopN);
                    continue;
                }

                widget.Rows.Clear();
                if (snapshot == null)
                    continue;

                if (widget.WidgetKind == DashboardWidgetKinds.Kpi || widget.Id == DashboardBlockIds.Kpi)
                {
                    foreach (var row in snapshot.Summary ?? Enumerable.Empty<AnalyticsKpiRow>())
                        widget.Rows.Add(row);
                }
                else if (widget.WidgetKind == DashboardWidgetKinds.Bim || widget.Id == DashboardBlockIds.Bim)
                {
                    foreach (var row in snapshot.BimSummary ?? Enumerable.Empty<AnalyticsKpiRow>())
                        widget.Rows.Add(row);
                }
                else if (widget.WidgetKind == DashboardWidgetKinds.Responsible || widget.Id == DashboardBlockIds.Responsible)
                {
                    foreach (var row in (snapshot.ObjectsByResponsible ?? Enumerable.Empty<ResponsibleCountRow>()).Take(10))
                    {
                        widget.Rows.Add(new AnalyticsKpiRow
                        {
                            Label = row.DisplayName,
                            Value = row.SampledCount.ToString(),
                            Detail = row.SharePercent.ToString("0.0") + "% · " + (row.Scope ?? "")
                        });
                    }
                }
            }
        }

        public void SetBlockVisible(string id, bool visible)
        {
            if (_v2)
            {
                TryMutate(copy =>
                {
                    var widget = FindWidget(copy, id);
                    if (widget == null || widget.Layout == null)
                        return false;
                    widget.Layout.IsVisible = visible;
                    return true;
                }, out _);
                return;
            }

            var block = (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>())
                .FirstOrDefault(b => b.Id == id);
            if (block == null)
                return;
            block.IsVisible = visible;
            PersistAndRebuild();
        }

        public void MoveBlock(string id, int delta)
        {
            if (_v2)
            {
                TryMutate(copy =>
                {
                    var ordered = Ordered(copy);
                    var idx = ordered.FindIndex(b => b.Id == id);
                    if (idx < 0)
                        return false;
                    var target = idx + delta;
                    if (target < 0 || target >= ordered.Count)
                        return false;
                    var tmp = ordered[idx];
                    ordered[idx] = ordered[target];
                    ordered[target] = tmp;
                    for (var i = 0; i < ordered.Count; i++)
                    {
                        if (ordered[i].Layout == null)
                            ordered[i].Layout = new DashboardWidgetLayoutDefinition();
                        ordered[i].Layout.Order = i;
                    }
                    copy.Widgets = ordered;
                    return true;
                }, out _);
                return;
            }

            var v1 = (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>()).OrderBy(b => b.Order).ToList();
            var v1idx = v1.FindIndex(b => b.Id == id);
            if (v1idx < 0)
                return;
            var v1target = v1idx + delta;
            if (v1target < 0 || v1target >= v1.Count)
                return;
            var swap = v1[v1idx];
            v1[v1idx] = v1[v1target];
            v1[v1target] = swap;
            for (var i = 0; i < v1.Count; i++)
                v1[i].Order = i;
            _dashboardLayout.Widgets = v1;
            PersistAndRebuild();
        }

        public void AddWidget(DashboardWidgetState draft)
        {
            if (draft == null)
                return;

            if (_v2)
            {
                TryMutate(copy =>
                {
                    EnsureWidgets(copy);
                    if (draft.WidgetKind == DashboardWidgetKinds.Kpi
                        || draft.WidgetKind == DashboardWidgetKinds.Bim
                        || draft.WidgetKind == DashboardWidgetKinds.Responsible)
                    {
                        var id = draft.WidgetKind == DashboardWidgetKinds.Kpi ? DashboardBlockIds.Kpi
                            : draft.WidgetKind == DashboardWidgetKinds.Bim ? DashboardBlockIds.Bim
                            : DashboardBlockIds.Responsible;
                        var existing = FindWidget(copy, id);
                        if (existing != null)
                        {
                            existing.Title = draft.Title;
                            if (existing.Layout != null)
                                existing.Layout.IsVisible = true;
                            return true;
                        }
                        draft.Id = id;
                    }

                    if (string.IsNullOrWhiteSpace(draft.Id) || draft.WidgetKind == DashboardWidgetKinds.Chart)
                    {
                        draft = DashboardLayoutStore.CreateChartWidget(
                            draft.Title,
                            draft.ChartSource,
                            draft.ChartKind,
                            draft.TopN,
                            draft.ColumnSpan,
                            copy.Widgets.Count);
                    }

                    draft.Order = copy.Widgets.Count;
                    draft.IsVisible = true;
                    copy.Widgets.Add(DashboardLegacyWidgetBridge.ToDefinition(draft, copy.Widgets.Count));
                    Reindex(copy);
                    return true;
                }, out _);
                return;
            }

            if (_dashboardLayout == null)
                return;

            if (_dashboardLayout.Widgets == null)
                _dashboardLayout.Widgets = new List<DashboardWidgetState>();

            if (draft.WidgetKind == DashboardWidgetKinds.Kpi
                || draft.WidgetKind == DashboardWidgetKinds.Bim
                || draft.WidgetKind == DashboardWidgetKinds.Responsible)
            {
                var id = draft.WidgetKind == DashboardWidgetKinds.Kpi ? DashboardBlockIds.Kpi
                    : draft.WidgetKind == DashboardWidgetKinds.Bim ? DashboardBlockIds.Bim
                    : DashboardBlockIds.Responsible;
                var existing = _dashboardLayout.Widgets.FirstOrDefault(w => w.Id == id);
                if (existing != null)
                {
                    existing.IsVisible = true;
                    existing.Title = draft.Title;
                    PersistAndRebuild();
                    return;
                }
                draft.Id = id;
            }

            if (string.IsNullOrWhiteSpace(draft.Id) || draft.WidgetKind == DashboardWidgetKinds.Chart)
            {
                draft = DashboardLayoutStore.CreateChartWidget(
                    draft.Title,
                    draft.ChartSource,
                    draft.ChartKind,
                    draft.TopN,
                    draft.ColumnSpan,
                    _dashboardLayout.Widgets.Count);
            }

            draft.Order = _dashboardLayout.Widgets.Count;
            draft.IsVisible = true;
            _dashboardLayout.Widgets.Add(draft);
            PersistAndRebuild();
        }

        public void UpdateWidget(string id, DashboardWidgetState draft)
        {
            if (string.IsNullOrWhiteSpace(id) || draft == null)
                return;

            if (_v2)
            {
                TryMutate(copy =>
                {
                    var existing = FindWidget(copy, id);
                    if (existing == null || existing.Legacy == null)
                        return false;
                    existing.Title = draft.Title;
                    if (existing.Legacy.WidgetKind == DashboardWidgetKinds.Chart)
                    {
                        existing.Legacy.ChartSource = draft.ChartSource;
                        existing.Legacy.ChartKind = draft.ChartKind;
                        existing.Legacy.TopN = draft.TopN;
                        if (existing.Layout == null)
                            existing.Layout = new DashboardWidgetLayoutDefinition();
                        existing.Layout.ColumnSpan = draft.ColumnSpan <= 1 ? 1 : 2;
                    }
                    return true;
                }, out _);
                return;
            }

            if (_dashboardLayout == null)
                return;
            var v1 = (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>())
                .FirstOrDefault(w => w.Id == id);
            if (v1 == null)
                return;

            v1.Title = draft.Title;
            if (v1.WidgetKind == DashboardWidgetKinds.Chart)
            {
                v1.ChartSource = draft.ChartSource;
                v1.ChartKind = draft.ChartKind;
                v1.TopN = draft.TopN;
                v1.ColumnSpan = draft.ColumnSpan <= 1 ? 1 : 2;
            }

            PersistAndRebuild();
        }

        public void RemoveWidget(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            if (_v2)
            {
                TryMutate(copy =>
                {
                    var target = FindWidget(copy, id);
                    if (target == null)
                        return false;
                    var kind = target.Legacy != null ? target.Legacy.WidgetKind : target.ContentKind;
                    if (target.ContentKind != DashboardPersistenceV2.ContentQuery
                        && kind != DashboardWidgetKinds.Chart)
                        return false;
                    copy.Widgets.Remove(target);
                    Reindex(copy);
                    return true;
                }, out _);
                return;
            }

            if (_dashboardLayout == null)
                return;
            var list = _dashboardLayout.Widgets ?? new List<DashboardWidgetState>();
            var v1target = list.FirstOrDefault(w => w.Id == id);
            if (v1target == null)
                return;
            if (v1target.WidgetKind != DashboardWidgetKinds.Chart)
                return;
            list.Remove(v1target);
            for (var i = 0; i < list.Count; i++)
                list[i].Order = i;
            _dashboardLayout.Widgets = list;
            PersistAndRebuild();
        }

        public DashboardWidgetState GetWidgetState(string id)
        {
            if (_v2)
            {
                var widget = FindWidget(_definition, id);
                if (widget == null || widget.ContentKind == DashboardPersistenceV2.ContentQuery)
                    return null;
                return DashboardLegacyWidgetBridge.ToState(widget);
            }

            return (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>())
                .FirstOrDefault(w => w.Id == id);
        }

        public bool IsQueryWidget(string id)
        {
            if (!_v2 || _definition == null)
                return false;
            var widget = FindWidget(_definition, id);
            return widget != null && widget.ContentKind == DashboardPersistenceV2.ContentQuery;
        }

        public DashboardWidgetDefinition GetWidgetDefinition(string id)
        {
            return FindWidget(_definition, id);
        }

        public bool TrySaveQueryWidget(DashboardWidgetDefinition widget, out string error)
        {
            error = null;
            if (widget == null)
            {
                error = Resources.Dashboard_SaveFailed;
                return false;
            }

            return TryMutate(copy =>
            {
                EnsureWidgets(copy);
                var existing = FindWidget(copy, widget.Id);
                if (existing != null)
                {
                    var index = copy.Widgets.IndexOf(existing);
                    if (widget.Layout == null)
                        widget.Layout = existing.Layout;
                    copy.Widgets[index] = widget;
                }
                else
                {
                    if (widget.Layout == null)
                        widget.Layout = new DashboardWidgetLayoutDefinition();
                    widget.Layout.Order = copy.Widgets.Count;
                    widget.Layout.IsVisible = true;
                    copy.Widgets.Add(widget);
                }
                Reindex(copy);
                return true;
            }, out error);
        }

        public void ReplaceDataSession(
            ProjectAnalyticsSnapshot snapshot,
            DashboardFieldCatalog catalog,
            IReadOnlyList<DashboardObjectTypeOption> types)
        {
            ReplaceDataSession(snapshot, catalog, types, null);
        }

        internal void ReplaceDataSession(
            ProjectAnalyticsSnapshot snapshot,
            DashboardFieldCatalog catalog,
            IReadOnlyList<DashboardObjectTypeOption> types,
            IDashboardTypeDatasetProvider provider)
        {
            if (!_v2 || Volatile.Read(ref _disposed) != 0)
                return;

            Interlocked.Increment(ref _applyGeneration);
            var old = _coordinator;
            _catalog = catalog ?? new PilotFieldCatalogBuilder().Build(Enumerable.Empty<TypeInventoryRecord>());
            _typeOptions = types ?? new List<DashboardObjectTypeOption>();
            if (provider != null)
                _typeProvider = provider;
            _coordinator = new DashboardQueryCoordinator(snapshot, _catalog, _typeProvider);
            if (old != null)
                old.Dispose();
            RebuildWidgetContent();
            ScheduleQueryRefresh();
        }

        internal Task ScheduleQueryRefresh()
        {
            if (!_v2 || Volatile.Read(ref _disposed) != 0)
                return Task.CompletedTask;

            var generation = Interlocked.Increment(ref _applyGeneration);
            var coordinator = _coordinator;
            var definition = _definition;
            var task = RefreshQueryWidgetsAsync(generation, coordinator, definition);
            _refreshTask = task;
            return task;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            Interlocked.Increment(ref _applyGeneration);
            var coordinator = _coordinator;
            _coordinator = null;
            if (coordinator != null)
                coordinator.Dispose();
        }

        private void LoadV2()
        {
            var loaded = _definitionStore.Load(_projectKey);
            _mutationsEnabled = true;
            _dashboardWarning = null;

            switch (loaded.Status)
            {
                case DashboardDefinitionLoadStatus.Success:
                    _definition = loaded.Definition;
                    AnalyticsLogger.Info("Dashboard", "Loaded V2 path=" + loaded.Path);
                    break;
                case DashboardDefinitionLoadStatus.Missing:
                    var v1 = _dashboardLayoutStore.TryLoad() ?? DashboardLayoutStore.Default();
                    _definition = DashboardDefinitionV2Migrator.FromLegacy(v1, _projectKey);
                    AnalyticsLogger.Info("Dashboard", "Migrated V1 in memory (V2 not written)");
                    break;
                default:
                    _mutationsEnabled = false;
                    _dashboardWarning = WarningFor(loaded.Status);
                    var fallback = _dashboardLayoutStore.TryLoad() ?? DashboardLayoutStore.Default();
                    _definition = DashboardDefinitionV2Migrator.FromLegacy(fallback, _projectKey);
                    AnalyticsLogger.Warning("Dashboard", "Degraded status=" + loaded.Status + " path=" + loaded.Path);
                    break;
            }

            EnsureCoordinator();
            RebuildUi();
            NotifyDashboardChrome();
        }

        private static string WarningFor(DashboardDefinitionLoadStatus status)
        {
            switch (status)
            {
                case DashboardDefinitionLoadStatus.UnsupportedVersion:
                    return Resources.Dashboard_UnsupportedVersion;
                case DashboardDefinitionLoadStatus.ProjectMismatch:
                    return Resources.Dashboard_ProjectMismatch;
                default:
                    return Resources.Dashboard_Corrupt;
            }
        }

        private void EnsureCoordinator()
        {
            if (_coordinator != null)
                return;
            var ctx = _content();
            var snapshot = ctx != null ? ctx.Snapshot : null;
            _coordinator = new DashboardQueryCoordinator(snapshot, _catalog, _typeProvider);
        }

        private bool TryMutate(Func<DashboardDefinition, bool> apply, out string error)
        {
            error = null;
            _lastSaveFailed = false;
            if (!_v2 || _definition == null)
            {
                error = Resources.Dashboard_SaveFailed;
                return false;
            }
            if (!_mutationsEnabled)
            {
                error = _dashboardWarning ?? Resources.Dashboard_Corrupt;
                return false;
            }

            var copy = DashboardDefinitionCopy.Clone(_definition);
            if (copy == null || !apply(copy))
                return false;

            var save = _definitionStore.Save(copy);
            if (save.Status != DashboardDefinitionSaveStatus.Success)
            {
                _lastSaveFailed = true;
                error = string.IsNullOrWhiteSpace(save.Reason)
                    ? Resources.Dashboard_SaveFailed
                    : Resources.Dashboard_SaveFailed + " " + save.Reason;
                AnalyticsLogger.Warning("Dashboard", "V2 save failed: " + save.Reason);
                return false;
            }

            _definition = copy;
            RebuildUi();
            ScheduleQueryRefresh();
            return true;
        }

        private async Task RefreshQueryWidgetsAsync(
            int generation,
            DashboardQueryCoordinator coordinator,
            DashboardDefinition definition)
        {
            if (coordinator == null || definition == null)
                return;

            var items = Ordered(definition)
                .Where(w => w != null && w.ContentKind == DashboardPersistenceV2.ContentQuery)
                .ToList();

            foreach (var widget in items)
            {
                if (generation != Volatile.Read(ref _applyGeneration) || Volatile.Read(ref _disposed) != 0)
                    return;

                Post(() => ApplyLoading(widget.Id, generation));

                WidgetQueryResult result;
                var sw = Stopwatch.StartNew();
                try
                {
                    result = await ExecuteWidgetAsync(coordinator, widget).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    AnalyticsLogger.Error("DashboardQuery", "Widget id=" + widget.Id, ex);
                    result = null;
                    Post(() => ApplyRuntime(
                        widget.Id,
                        generation,
                        DashboardQueryWidgetRuntimeStatus.Error,
                        Resources.QueryWidget_Error,
                        null));
                    continue;
                }

                sw.Stop();
                if (generation != Volatile.Read(ref _applyGeneration) || Volatile.Read(ref _disposed) != 0)
                    return;

                var captured = result;
                var capturedWidget = widget;
                    var elapsed = (int)sw.Elapsed.TotalMilliseconds;
                Post(() =>
                {
                    ApplyQueryResult(capturedWidget, captured, generation);
                    var typeId = capturedWidget.Query != null ? capturedWidget.Query.EntityTypeId : null;
                    AnalyticsLogger.Info(
                        "DashboardQuery",
                        "Widget id=" + capturedWidget.Id
                        + " status=" + (captured != null ? captured.Status.ToString() : "Error")
                        + (typeId.HasValue ? " TypeId=" + typeId.Value : string.Empty)
                        + " ElapsedMs=" + elapsed);
                });
            }
        }

        private static async Task<WidgetQueryResult> ExecuteWidgetAsync(
            DashboardQueryCoordinator coordinator,
            DashboardWidgetDefinition widget)
        {
            var viz = widget.Visualization != null ? widget.Visualization.Type : "Auto";
            if (string.Equals(viz, "Line", StringComparison.Ordinal))
                return WidgetQueryResult.Unsupported("Line is not supported for TypeId queries");

            DashboardWidgetQuery query;
            string error;
            if (!DashboardQueryPersistence.TryToQuery(widget.Query, out query, out error))
                return WidgetQueryResult.Invalid(error ?? "query is invalid");

            return await coordinator.ExecuteAsync(query, CancellationToken.None).ConfigureAwait(false);
        }

        private void ApplyLoading(string id, int generation)
        {
            if (generation != Volatile.Read(ref _applyGeneration))
                return;
            var vm = FindVm(id);
            if (vm == null || !vm.IsQueryWidget)
                return;
            vm.ApplyQueryRuntime(
                DashboardQueryWidgetRuntimeStatus.Loading,
                Resources.QueryWidget_Loading,
                null);
        }

        private void ApplyQueryResult(DashboardWidgetDefinition widget, WidgetQueryResult result, int generation)
        {
            if (result == null)
            {
                ApplyRuntime(widget.Id, generation, DashboardQueryWidgetRuntimeStatus.Error, Resources.QueryWidget_Error, null);
                return;
            }

            DashboardQueryWidgetRuntimeStatus status;
            string message;
            DashboardQueryRenderModel render = null;
            switch (result.Status)
            {
                case WidgetQueryStatus.Success:
                    status = DashboardQueryWidgetRuntimeStatus.Success;
                    message = null;
                    var hasDimension = widget.Query != null && !string.IsNullOrWhiteSpace(widget.Query.DimensionFieldId);
                    var viz = widget.Visualization != null ? widget.Visualization.Type : "Auto";
                    render = DashboardWidgetDatasetAdapter.TryRender(result.Dataset, viz, hasDimension);
                    if (render.Status != DashboardQueryWidgetRuntimeStatus.Success)
                    {
                        status = render.Status;
                        message = render.Message;
                        render = null;
                    }
                    break;
                case WidgetQueryStatus.Empty:
                    status = DashboardQueryWidgetRuntimeStatus.Empty;
                    message = Resources.QueryWidget_Empty;
                    break;
                case WidgetQueryStatus.IncompleteData:
                    status = DashboardQueryWidgetRuntimeStatus.Incomplete;
                    message = Resources.QueryWidget_Incomplete;
                    break;
                case WidgetQueryStatus.UnsupportedQuery:
                    status = DashboardQueryWidgetRuntimeStatus.Unsupported;
                    message = Resources.QueryWidget_Unsupported;
                    break;
                case WidgetQueryStatus.InvalidQuery:
                    status = DashboardQueryWidgetRuntimeStatus.Invalid;
                    message = Resources.QueryWidget_Invalid;
                    break;
                default:
                    status = DashboardQueryWidgetRuntimeStatus.Error;
                    message = Resources.QueryWidget_Error;
                    break;
            }

            ApplyRuntime(widget.Id, generation, status, message, render);
        }

        private void ApplyRuntime(
            string id,
            int generation,
            DashboardQueryWidgetRuntimeStatus status,
            string message,
            DashboardQueryRenderModel render)
        {
            if (generation != Volatile.Read(ref _applyGeneration) || Volatile.Read(ref _disposed) != 0)
                return;
            var vm = FindVm(id);
            if (vm == null || !vm.IsQueryWidget)
                return;
            vm.ApplyQueryRuntime(status, message, render);
        }

        private DashboardWidgetVm FindVm(string id)
        {
            foreach (var widget in DashboardWidgets)
            {
                if (widget.Id == id)
                    return widget;
            }
            return null;
        }

        private void Post(Action action)
        {
            if (action == null)
                return;
            _postToUi(action);
        }

        internal void InvokeOnUi(Action action)
        {
            Post(action);
        }

        private void PersistAndRebuild()
        {
            _dashboardLayoutStore.Save(_dashboardLayout);
            _dashboardLayout = _dashboardLayoutStore.LoadOrDefault();
            RebuildUi();
        }

        private void RebuildUi()
        {
            if (DashboardLayoutItems == null || DashboardWidgets == null)
                return;

            DashboardLayoutItems.Clear();
            DashboardWidgets.Clear();

            if (_v2)
            {
                if (_definition == null)
                    return;
                foreach (var widget in Ordered(_definition))
                {
                    var kind = widget.ContentKind == DashboardPersistenceV2.ContentQuery
                        ? DashboardPersistenceV2.ContentQuery
                        : (widget.Legacy != null ? widget.Legacy.WidgetKind : DashboardWidgetKinds.Chart);
                    var title = widget.Title;
                    var visible = widget.Layout == null || widget.Layout.IsVisible;
                    var item = new DashboardLayoutItemVm(widget.Id, title, kind)
                    {
                        IsVisible = visible,
                        MutationsEnabled = _mutationsEnabled
                    };
                    DashboardLayoutItems.Add(item);
                    if (!visible)
                        continue;
                    if (widget.ContentKind == DashboardPersistenceV2.ContentQuery)
                        DashboardWidgets.Add(new DashboardWidgetVm(widget));
                    else
                    {
                        var state = DashboardLegacyWidgetBridge.ToState(widget);
                        if (state != null)
                            DashboardWidgets.Add(new DashboardWidgetVm(state));
                    }
                }
            }
            else
            {
                if (_dashboardLayout == null)
                    return;
                foreach (var widget in (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>()).OrderBy(b => b.Order))
                {
                    DashboardLayoutItems.Add(new DashboardLayoutItemVm(widget.Id, widget.Title, widget.WidgetKind)
                    {
                        IsVisible = widget.IsVisible
                    });
                    if (widget.IsVisible)
                        DashboardWidgets.Add(new DashboardWidgetVm(widget));
                }
            }

            RebuildWidgetContent();
        }

        private void NotifyDashboardChrome()
        {
            Notify("DashboardWarning");
            Notify("HasDashboardWarning");
            Notify("DashboardMutationsEnabled");
        }

        private static void EnsureWidgets(DashboardDefinition definition)
        {
            if (definition.Widgets == null)
                definition.Widgets = new List<DashboardWidgetDefinition>();
        }

        private static List<DashboardWidgetDefinition> Ordered(DashboardDefinition definition)
        {
            EnsureWidgets(definition);
            return definition.Widgets
                .Where(w => w != null)
                .OrderBy(w => w.Layout != null ? w.Layout.Order : 0)
                .ToList();
        }

        private static DashboardWidgetDefinition FindWidget(DashboardDefinition definition, string id)
        {
            if (definition == null || definition.Widgets == null || string.IsNullOrWhiteSpace(id))
                return null;
            return definition.Widgets.FirstOrDefault(w => w != null && w.Id == id);
        }

        private static void Reindex(DashboardDefinition definition)
        {
            var ordered = Ordered(definition);
            for (var i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Layout == null)
                    ordered[i].Layout = new DashboardWidgetLayoutDefinition();
                ordered[i].Layout.Order = i;
            }
            definition.Widgets = ordered;
        }

        private void Notify(string propertyName)
        {
            _notify(propertyName);
        }

        private sealed class NullTypeDatasetProvider : IDashboardTypeDatasetProvider
        {
            public DashboardTypeDataset Materialize(int typeId, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Type dataset provider is not configured.");
            }
        }
    }
}
