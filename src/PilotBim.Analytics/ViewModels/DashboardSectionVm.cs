using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PilotBim.Analytics.Models;

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
            Rows = new ObservableCollection<AnalyticsKpiRow>();
            AnalyticsChartKind parsed;
            if (EnumTryParseChart(state.ChartKind, out parsed))
                ChartKind = parsed;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; private set; }
        public string WidgetKind { get; private set; }
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
            get { return ColumnSpan == 1; }
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
            CanDelete = widgetKind == DashboardWidgetKinds.Chart;
            CanEdit = widgetKind == DashboardWidgetKinds.Chart;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; private set; }
        public string WidgetKind { get; private set; }
        public bool CanDelete { get; private set; }
        public bool CanEdit { get; private set; }

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

    // Kept for any leftover references during transition
    internal sealed class DashboardSectionVm : INotifyPropertyChanged
    {
        private bool _isVisible;

        public DashboardSectionVm(string id, string title)
        {
            Id = id;
            Title = title;
            Rows = new ObservableCollection<AnalyticsKpiRow>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; private set; }
        public string Title { get; private set; }

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

        public ObservableCollection<AnalyticsKpiRow> Rows { get; private set; }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
