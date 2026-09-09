using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PilotBim.Analytics.Models;
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
    /// Dashboard layout / CRUD / persist orchestration extracted from AnalyticsWindowViewModel.
    /// Root VM keeps façade property names for XAML bindings.
    /// </summary>
    internal sealed class AnalyticsDashboardPresenter
    {
        private readonly DashboardLayoutStore _dashboardLayoutStore;
        private readonly Func<DashboardContentContext> _content;
        private readonly Action<string> _notify;
        private DashboardLayoutState _dashboardLayout;

        public AnalyticsDashboardPresenter(
            Action<string> notifyPropertyChanged,
            Func<DashboardContentContext> contentProvider)
            : this(new DashboardLayoutStore(), notifyPropertyChanged, contentProvider)
        {
        }

        internal AnalyticsDashboardPresenter(
            DashboardLayoutStore store,
            Action<string> notifyPropertyChanged,
            Func<DashboardContentContext> contentProvider)
        {
            _dashboardLayoutStore = store ?? new DashboardLayoutStore();
            _notify = notifyPropertyChanged ?? (_ => { });
            _content = contentProvider ?? (() => null);
            DashboardLayoutItems = new ObservableCollection<DashboardLayoutItemVm>();
            DashboardWidgets = new ObservableCollection<DashboardWidgetVm>();
            InitLayout();
        }

        public ObservableCollection<DashboardLayoutItemVm> DashboardLayoutItems { get; private set; }
        public ObservableCollection<DashboardWidgetVm> DashboardWidgets { get; private set; }

        public void InitLayout()
        {
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
            var block = (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>())
                .FirstOrDefault(b => b.Id == id);
            if (block == null)
                return;
            block.IsVisible = visible;
            PersistAndRebuild();
        }

        public void MoveBlock(string id, int delta)
        {
            var ordered = (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>()).OrderBy(b => b.Order).ToList();
            var idx = ordered.FindIndex(b => b.Id == id);
            if (idx < 0)
                return;
            var target = idx + delta;
            if (target < 0 || target >= ordered.Count)
                return;
            var tmp = ordered[idx];
            ordered[idx] = ordered[target];
            ordered[target] = tmp;
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].Order = i;
            _dashboardLayout.Widgets = ordered;
            PersistAndRebuild();
        }

        public void AddWidget(DashboardWidgetState draft)
        {
            if (draft == null || _dashboardLayout == null)
                return;

            if (_dashboardLayout.Widgets == null)
                _dashboardLayout.Widgets = new List<DashboardWidgetState>();

            // Built-in kinds: ensure single instance (unhide / update)
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
            if (string.IsNullOrWhiteSpace(id) || draft == null || _dashboardLayout == null)
                return;
            var existing = (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>())
                .FirstOrDefault(w => w.Id == id);
            if (existing == null)
                return;

            existing.Title = draft.Title;
            if (existing.WidgetKind == DashboardWidgetKinds.Chart)
            {
                existing.ChartSource = draft.ChartSource;
                existing.ChartKind = draft.ChartKind;
                existing.TopN = draft.TopN;
                existing.ColumnSpan = draft.ColumnSpan <= 1 ? 1 : 2;
            }

            PersistAndRebuild();
        }

        public void RemoveWidget(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || _dashboardLayout == null)
                return;
            var list = _dashboardLayout.Widgets ?? new List<DashboardWidgetState>();
            var target = list.FirstOrDefault(w => w.Id == id);
            if (target == null)
                return;
            if (target.WidgetKind != DashboardWidgetKinds.Chart)
                return; // built-ins: hide instead
            list.Remove(target);
            for (var i = 0; i < list.Count; i++)
                list[i].Order = i;
            _dashboardLayout.Widgets = list;
            PersistAndRebuild();
        }

        public DashboardWidgetState GetWidgetState(string id)
        {
            return (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>())
                .FirstOrDefault(w => w.Id == id);
        }

        private void PersistAndRebuild()
        {
            _dashboardLayoutStore.Save(_dashboardLayout);
            _dashboardLayout = _dashboardLayoutStore.LoadOrDefault();
            RebuildUi();
        }

        private void RebuildUi()
        {
            if (DashboardLayoutItems == null || DashboardWidgets == null || _dashboardLayout == null)
                return;

            DashboardLayoutItems.Clear();
            DashboardWidgets.Clear();

            foreach (var widget in (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>()).OrderBy(b => b.Order))
            {
                DashboardLayoutItems.Add(new DashboardLayoutItemVm(widget.Id, widget.Title, widget.WidgetKind)
                {
                    IsVisible = widget.IsVisible
                });
                if (widget.IsVisible)
                    DashboardWidgets.Add(new DashboardWidgetVm(widget));
            }

            RebuildWidgetContent();
        }

        private void Notify(string propertyName)
        {
            _notify(propertyName);
        }
    }
}
