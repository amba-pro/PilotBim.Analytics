using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;

namespace PilotBim.Analytics.ViewModels
{
    internal sealed class DashboardWidgetVm : INotifyPropertyChanged
    {
        private string _title;
        private bool _isVisible;
        private AnalyticsChartKind _chartKind = AnalyticsChartKind.HorizontalBar;
        private IList<ChartSeriesPoint> _chartSeries = new List<ChartSeriesPoint>();
        private bool _isChart;
        private bool _isKpi;
        private int _columnSpan = 2;
        private int _gridX;
        private int _gridY;
        private int _gridWidth = 6;
        private int _gridHeight = 3;
        private bool _isEditMode;
        private DashboardQueryWidgetRuntimeStatus _queryStatus = DashboardQueryWidgetRuntimeStatus.Idle;
        private string _queryStatusText;
        private string _resolvedVisualization;
        private bool _showQueryTable;

        public DashboardWidgetVm(DashboardWidgetState state)
        {
            Id = state.Id;
            WidgetKind = state.WidgetKind;
            Title = state.Title;
            IsVisible = state.IsVisible;
            ColumnSpan = state.ColumnSpan <= 1 ? 1 : 2;
            ChartSource = state.ChartSource;
            ChartKindName = state.ChartKind;
            TopN = state.TopN;
            IsChart = state.WidgetKind == DashboardWidgetKinds.Chart;
            IsKpi = !IsChart;
            IsQueryWidget = false;
            Rows = new ObservableCollection<AnalyticsKpiRow>();
            TableRows = new ObservableCollection<DashboardQueryTableRow>();
            AnalyticsChartKind parsed;
            if (EnumTryParseChart(state.ChartKind, out parsed))
                ChartKind = parsed;
        }

        public DashboardWidgetVm(DashboardWidgetDefinition queryWidget)
        {
            if (queryWidget == null)
                throw new ArgumentNullException("queryWidget");
            Id = queryWidget.Id;
            WidgetKind = DashboardPersistenceV2.ContentQuery;
            Title = queryWidget.Title;
            var layout = queryWidget.Layout ?? new DashboardWidgetLayoutDefinition();
            IsVisible = layout.IsVisible;
            ApplyGrid(layout);
            IsQueryWidget = true;
            IsChart = false;
            IsKpi = false;
            Rows = new ObservableCollection<AnalyticsKpiRow>();
            TableRows = new ObservableCollection<DashboardQueryTableRow>();
            QueryStatus = DashboardQueryWidgetRuntimeStatus.Idle;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; private set; }
        public string WidgetKind { get; private set; }
        public bool IsQueryWidget { get; private set; }
        public string ChartSource { get; set; }
        public string ChartKindName { get; set; }
        public int TopN { get; set; }

        public string Title
        {
            get { return _title; }
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                _isVisible = value;
                OnPropertyChanged();
            }
        }

        public bool IsChart
        {
            get { return _isChart; }
            private set
            {
                _isChart = value;
                OnPropertyChanged();
            }
        }

        public bool IsKpi
        {
            get { return _isKpi; }
            private set
            {
                _isKpi = value;
                OnPropertyChanged();
            }
        }

        public int ColumnSpan
        {
            get { return _columnSpan; }
            set
            {
                _columnSpan = value <= 1 ? 1 : 2;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsHalfWidth));
            }
        }

        public bool IsHalfWidth
        {
            get { return GridWidth <= 6; }
        }

        public int GridX
        {
            get { return _gridX; }
            private set
            {
                _gridX = value;
                OnPropertyChanged();
            }
        }

        public int GridY
        {
            get { return _gridY; }
            private set
            {
                _gridY = value;
                OnPropertyChanged();
            }
        }

        public int GridWidth
        {
            get { return _gridWidth; }
            private set
            {
                _gridWidth = value;
                OnPropertyChanged();
                OnPropertyChanged("IsHalfWidth");
            }
        }

        public int GridHeight
        {
            get { return _gridHeight; }
            private set
            {
                _gridHeight = value;
                OnPropertyChanged();
            }
        }

        public bool IsEditMode
        {
            get { return _isEditMode; }
            set
            {
                if (_isEditMode == value)
                    return;
                _isEditMode = value;
                OnPropertyChanged();
            }
        }

        public void ApplyGrid(DashboardWidgetLayoutDefinition layout)
        {
            var rect = DashboardGridLayoutEngine.Clamp(DashboardGridLayoutEngine.FromLayout(layout));
            GridX = rect.X;
            GridY = rect.Y;
            GridWidth = rect.Width;
            GridHeight = rect.Height;
            ColumnSpan = rect.Width <= 6 ? 1 : 2;
            if (layout != null)
                IsVisible = layout.IsVisible;
        }

        public DashboardQueryRuntimeCache CaptureQueryRuntime()
        {
            if (!IsQueryWidget)
                return null;
            return new DashboardQueryRuntimeCache
            {
                Status = QueryStatus,
                Message = QueryStatusText,
                Render = QueryStatus == DashboardQueryWidgetRuntimeStatus.Success
                    ? new DashboardQueryRenderModel
                    {
                        Status = QueryStatus,
                        ResolvedVisualization = ResolvedVisualization,
                        ShowChart = IsChart,
                        ShowKpi = IsKpi,
                        ShowTable = ShowQueryTable,
                        ChartKind = ChartKind,
                        Points = ChartSeries,
                        KpiRows = Rows.ToList(),
                        TableRows = TableRows.ToList()
                    }
                    : null
            };
        }

        public AnalyticsChartKind ChartKind
        {
            get { return _chartKind; }
            set
            {
                _chartKind = value;
                OnPropertyChanged();
            }
        }

        public IList<ChartSeriesPoint> ChartSeries
        {
            get { return _chartSeries; }
            set
            {
                _chartSeries = value ?? new List<ChartSeriesPoint>();
                OnPropertyChanged();
            }
        }

        public ObservableCollection<AnalyticsKpiRow> Rows { get; private set; }
        public ObservableCollection<DashboardQueryTableRow> TableRows { get; private set; }

        public DashboardQueryWidgetRuntimeStatus QueryStatus
        {
            get { return _queryStatus; }
            private set
            {
                _queryStatus = value;
                OnPropertyChanged();
                OnPropertyChanged("ShowQueryMessage");
            }
        }

        public string QueryStatusText
        {
            get { return _queryStatusText; }
            private set
            {
                _queryStatusText = value;
                OnPropertyChanged();
            }
        }

        public string ResolvedVisualization
        {
            get { return _resolvedVisualization; }
            private set
            {
                _resolvedVisualization = value;
                OnPropertyChanged();
            }
        }

        public bool ShowQueryTable
        {
            get { return _showQueryTable; }
            private set
            {
                _showQueryTable = value;
                OnPropertyChanged();
            }
        }

        public bool ShowQueryMessage
        {
            get { return IsQueryWidget && QueryStatus != DashboardQueryWidgetRuntimeStatus.Success; }
        }

        public void ApplyQueryRuntime(
            DashboardQueryWidgetRuntimeStatus status,
            string message,
            DashboardQueryRenderModel render)
        {
            if (status == DashboardQueryWidgetRuntimeStatus.Success && render != null
                && render.Status != DashboardQueryWidgetRuntimeStatus.Success)
            {
                status = render.Status;
                if (string.IsNullOrEmpty(message))
                    message = render.Message;
            }

            QueryStatus = status;
            QueryStatusText = message ?? string.Empty;
            ResolvedVisualization = render != null ? render.ResolvedVisualization : null;

            var success = status == DashboardQueryWidgetRuntimeStatus.Success && render != null;
            IsChart = success && render.ShowChart;
            IsKpi = success && render.ShowKpi;
            ShowQueryTable = success && render.ShowTable;

            ChartSeries = success && render.ShowChart
                ? (render.Points ?? new List<ChartSeriesPoint>())
                : new List<ChartSeriesPoint>();
            if (success && render.ShowChart)
                ChartKind = render.ChartKind;

            Rows.Clear();
            if (success && render.ShowKpi && render.KpiRows != null)
            {
                foreach (var row in render.KpiRows)
                    Rows.Add(row);
            }

            TableRows.Clear();
            if (success && render.ShowTable && render.TableRows != null)
            {
                foreach (var row in render.TableRows)
                    TableRows.Add(row);
            }
        }

        public void ApplyState(DashboardWidgetState state)
        {
            if (state == null)
                return;
            Title = state.Title;
            IsVisible = state.IsVisible;
            ColumnSpan = state.ColumnSpan;
            ChartSource = state.ChartSource;
            ChartKindName = state.ChartKind;
            TopN = state.TopN;
            WidgetKind = state.WidgetKind;
            IsChart = state.WidgetKind == DashboardWidgetKinds.Chart;
            IsKpi = !IsChart;
            AnalyticsChartKind parsed;
            if (EnumTryParseChart(state.ChartKind, out parsed))
                ChartKind = parsed;
        }

        private static bool EnumTryParseChart(string name, out AnalyticsChartKind kind)
        {
            return System.Enum.TryParse(name ?? "", out kind);
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }

    internal sealed class DashboardLayoutItemVm : INotifyPropertyChanged
    {
        private bool _isVisible;
        private string _title;

        public DashboardLayoutItemVm(string id, string title, string widgetKind)
        {
            Id = id;
            Title = title;
            WidgetKind = widgetKind;
            var query = widgetKind == DashboardPersistenceV2.ContentQuery;
            CanDelete = widgetKind == DashboardWidgetKinds.Chart || query;
            CanEdit = widgetKind == DashboardWidgetKinds.Chart || query;
            MutationsEnabled = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; private set; }
        public string WidgetKind { get; private set; }
        public bool CanDelete { get; private set; }
        public bool CanEdit { get; private set; }
        public bool MutationsEnabled { get; set; }

        public string Title
        {
            get { return _title; }
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                if (_isVisible == value)
                    return;
                _isVisible = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
