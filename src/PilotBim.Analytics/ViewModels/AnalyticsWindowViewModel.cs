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
    internal sealed class AnalyticsWindowViewModel : INotifyPropertyChanged
    {
        private readonly ChartDataService _charts = new ChartDataService();
        private string _selectedNavKey = "Summary";
        private string _progressText = "Нажмите «Обновить» для сканирования и построения аналитики.";
        private bool _isBusy;
        private ScanMode _scanMode = ScanMode.Standard;
        private ProjectAnalyticsSnapshot _snapshot;
        private string _limitationsText;
        private BimModelFilterItem _selectedBimModelFilter;
        private List<BimPartAnalyticsRow> _allBimParts = new List<BimPartAnalyticsRow>();
        private List<BimElementTypeCountRow> _allBimTypes = new List<BimElementTypeCountRow>();
        private readonly DashboardLayoutStore _dashboardLayoutStore = new DashboardLayoutStore();
        private DashboardLayoutState _dashboardLayout;
        private ChartOptionItem _selectedChartKind;
        private ChartOptionItem _selectedChartSource;
        private ChartOptionItem _selectedChartTopN;
        private AnalyticsChartKind _builderChartKind = AnalyticsChartKind.HorizontalBar;
        private string _chartBuilderHint;
        private IList<ChartSeriesPoint> _chartBuilderSeries = new List<ChartSeriesPoint>();
        private readonly ScanSnapshotStore _scanStore = new ScanSnapshotStore();
        private readonly ScanDiffService _scanDiff = new ScanDiffService();
        private StoredScanBaseline _currentScanBaseline;
        private ScanHistoryEntry _selectedScanHistory;
        private string _scanDiffHint = "Выполните «Обновить», затем можно сохранять именованные снимки и сравнивать с ними.";
        private bool _scanDiffChangesOnly;
        private readonly List<ScanDiffRow> _scanDiffAll = new List<ScanDiffRow>();

        public AnalyticsWindowViewModel()
        {
            Navigation = new ObservableCollection<NavItem>
            {
                new NavItem { Key = "Summary", Title = "Сводка" },
                new NavItem { Key = "Charts", Title = "Графики" },
                new NavItem { Key = "ChartBuilder", Title = "Конструктор" },
                new NavItem { Key = "Types", Title = "По типам" },
                new NavItem { Key = "Creators", Title = "По создателям" },
                new NavItem { Key = "Created", Title = "По дате создания" },
                new NavItem { Key = "States", Title = "По статусам" },
                new NavItem { Key = "StateSemantic", Title = "Статусы OPEN/CLOSED" },
                new NavItem { Key = "Responsible", Title = "По ответственным" },
                new NavItem { Key = "Documents", Title = "Версии документов" },
                new NavItem { Key = "Bim", Title = "BIM — сводка" },
                new NavItem { Key = "BimModels", Title = "BIM — модели" },
                new NavItem { Key = "BimParts", Title = "BIM — части" },
                new NavItem { Key = "BimTypes", Title = "BIM — типы IFC" },
                new NavItem { Key = "Quality", Title = "Качество данных" },
                new NavItem { Key = "Remarks", Title = "Замечания к модели" },
                new NavItem { Key = "RemarkLinks", Title = "Замечания → BIM" },
                new NavItem { Key = "ScanDiff", Title = "Сравнение сканов" }
            };

            Summary = new ObservableCollection<AnalyticsKpiRow>();
            ChartTypesTop = new ObservableCollection<ChartBarRow>();
            ChartCreatorsTop = new ObservableCollection<ChartBarRow>();
            ChartCreatedTimeline = new ObservableCollection<ChartBarRow>();
            ChartIfcTypesTop = new ObservableCollection<ChartBarRow>();
            ChartStateSemantic = new ObservableCollection<ChartBarRow>();
            ChartResponsibleTop = new ObservableCollection<ChartBarRow>();
            ChartBuilderPoints = new ObservableCollection<ChartSeriesPoint>();
            ChartKindOptions = new ObservableCollection<ChartOptionItem>
            {
                new ChartOptionItem { Id = "HorizontalBar", Title = "Горизонтальные столбцы" },
                new ChartOptionItem { Id = "VerticalBar", Title = "Вертикальные столбцы" },
                new ChartOptionItem { Id = "Pie", Title = "Круговая" },
                new ChartOptionItem { Id = "Line", Title = "Линейная" }
            };
            ChartSourceOptions = new ObservableCollection<ChartOptionItem>
            {
                new ChartOptionItem { Id = "Types", Title = "Типы объектов" },
                new ChartOptionItem { Id = "Creators", Title = "Создатели" },
                new ChartOptionItem { Id = "CreatedMonth", Title = "Месяцы создания" },
                new ChartOptionItem { Id = "UserStates", Title = "Статусы" },
                new ChartOptionItem { Id = "StateSemantic", Title = "OPEN / CLOSED" },
                new ChartOptionItem { Id = "Responsible", Title = "Ответственные" },
                new ChartOptionItem { Id = "IfcTypes", Title = "IFC типы" },
                new ChartOptionItem { Id = "BimModels", Title = "BIM модели (элементы)" },
                new ChartOptionItem { Id = "Remarks", Title = "Замечания по типам" }
            };
            ChartTopNOptions = new ObservableCollection<ChartOptionItem>
            {
                new ChartOptionItem { Id = "8", Title = "Топ 8" },
                new ChartOptionItem { Id = "12", Title = "Топ 12" },
                new ChartOptionItem { Id = "20", Title = "Топ 20" },
                new ChartOptionItem { Id = "0", Title = "Все" }
            };
            _selectedChartKind = ChartKindOptions[0];
            _selectedChartSource = ChartSourceOptions[0];
            _selectedChartTopN = ChartTopNOptions[1];
            BimModelFilters = new ObservableCollection<BimModelFilterItem>();
            ObjectsByType = new ObservableCollection<TypeCountRow>();
            ObjectsByCreator = new ObservableCollection<CreatorCountRow>();
            ObjectsByCreatedMonth = new ObservableCollection<PeriodCountRow>();
            ObjectsByUserState = new ObservableCollection<StateCountRow>();
            ObjectsByUserStateSemantic = new ObservableCollection<StateSemanticCountRow>();
            ObjectsByResponsible = new ObservableCollection<ResponsibleCountRow>();
            DocumentVersions = new ObservableCollection<DocumentVersionRow>();
            BimSummary = new ObservableCollection<AnalyticsKpiRow>();
            BimModelAnalytics = new ObservableCollection<BimModelAnalyticsRow>();
            BimPartAnalytics = new ObservableCollection<BimPartAnalyticsRow>();
            BimElementTypeCounts = new ObservableCollection<BimElementTypeCountRow>();
            DataQuality = new ObservableCollection<AttributeQualityRow>();
            ModelRemarks = new ObservableCollection<RemarkTypeRow>();
            RemarkLinks = new ObservableCollection<RemarkLinkRow>();
            ScanDiff = new ObservableCollection<ScanDiffRow>();
            ScanHistory = new ObservableCollection<ScanHistoryEntry>();

            BimModelFilters.Add(new BimModelFilterItem { ModelId = Guid.Empty, DisplayName = "Все модели", IsAll = true });
            SelectedBimModelFilter = BimModelFilters[0];

            DashboardLayoutItems = new ObservableCollection<DashboardLayoutItemVm>();
            DashboardWidgets = new ObservableCollection<DashboardWidgetVm>();
            InitDashboardLayout();
            ReloadScanHistoryList();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<NavItem> Navigation { get; }
        public ObservableCollection<AnalyticsKpiRow> Summary { get; }
        public ObservableCollection<ChartBarRow> ChartTypesTop { get; }
        public ObservableCollection<ChartBarRow> ChartCreatorsTop { get; }
        public ObservableCollection<ChartBarRow> ChartCreatedTimeline { get; }
        public ObservableCollection<ChartBarRow> ChartIfcTypesTop { get; }
        public ObservableCollection<ChartBarRow> ChartStateSemantic { get; }
        public ObservableCollection<ChartBarRow> ChartResponsibleTop { get; }
        public ObservableCollection<ChartSeriesPoint> ChartBuilderPoints { get; }
        public IList<ChartSeriesPoint> ChartBuilderSeries
        {
            get { return _chartBuilderSeries; }
            private set
            {
                _chartBuilderSeries = value ?? new List<ChartSeriesPoint>();
                OnPropertyChanged();
            }
        }
        public ObservableCollection<ChartOptionItem> ChartKindOptions { get; }
        public ObservableCollection<ChartOptionItem> ChartSourceOptions { get; }
        public ObservableCollection<ChartOptionItem> ChartTopNOptions { get; }
        public ObservableCollection<BimModelFilterItem> BimModelFilters { get; }
        public ObservableCollection<DashboardLayoutItemVm> DashboardLayoutItems { get; private set; }
        public ObservableCollection<DashboardWidgetVm> DashboardWidgets { get; private set; }

        public ChartOptionItem SelectedChartKind
        {
            get { return _selectedChartKind; }
            set
            {
                _selectedChartKind = value;
                OnPropertyChanged();
                RebuildChartBuilder();
            }
        }

        public ChartOptionItem SelectedChartSource
        {
            get { return _selectedChartSource; }
            set
            {
                _selectedChartSource = value;
                OnPropertyChanged();
                RebuildChartBuilder();
            }
        }

        public ChartOptionItem SelectedChartTopN
        {
            get { return _selectedChartTopN; }
            set
            {
                _selectedChartTopN = value;
                OnPropertyChanged();
                RebuildChartBuilder();
            }
        }

        public AnalyticsChartKind BuilderChartKind
        {
            get { return _builderChartKind; }
            private set
            {
                _builderChartKind = value;
                OnPropertyChanged();
            }
        }

        public string ChartBuilderHint
        {
            get { return _chartBuilderHint; }
            private set
            {
                _chartBuilderHint = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<TypeCountRow> ObjectsByType { get; }
        public ObservableCollection<CreatorCountRow> ObjectsByCreator { get; }
        public ObservableCollection<PeriodCountRow> ObjectsByCreatedMonth { get; }
        public ObservableCollection<StateCountRow> ObjectsByUserState { get; }
        public ObservableCollection<StateSemanticCountRow> ObjectsByUserStateSemantic { get; }
        public ObservableCollection<ResponsibleCountRow> ObjectsByResponsible { get; }
        public ObservableCollection<DocumentVersionRow> DocumentVersions { get; }
        public ObservableCollection<AnalyticsKpiRow> BimSummary { get; }
        public ObservableCollection<BimModelAnalyticsRow> BimModelAnalytics { get; }
        public ObservableCollection<BimPartAnalyticsRow> BimPartAnalytics { get; }
        public ObservableCollection<BimElementTypeCountRow> BimElementTypeCounts { get; }
        public ObservableCollection<AttributeQualityRow> DataQuality { get; }
        public ObservableCollection<RemarkTypeRow> ModelRemarks { get; }
        public ObservableCollection<RemarkLinkRow> RemarkLinks { get; }
        public ObservableCollection<ScanDiffRow> ScanDiff { get; }
        public ObservableCollection<ScanHistoryEntry> ScanHistory { get; }

        public ScanHistoryEntry SelectedScanHistory
        {
            get { return _selectedScanHistory; }
            set
            {
                _selectedScanHistory = value;
                OnPropertyChanged();
            }
        }

        public string ScanDiffHint
        {
            get { return _scanDiffHint; }
            private set
            {
                _scanDiffHint = value;
                OnPropertyChanged();
            }
        }

        public bool ScanDiffChangesOnly
        {
            get { return _scanDiffChangesOnly; }
            set
            {
                _scanDiffChangesOnly = value;
                OnPropertyChanged();
                PublishScanDiffRows();
            }
        }

        public BimModelFilterItem SelectedBimModelFilter
        {
            get { return _selectedBimModelFilter; }
            set
            {
                _selectedBimModelFilter = value;
                OnPropertyChanged();
                ApplyBimModelFilter();
            }
        }

        public bool ShowBimModelFilter
        {
            get
            {
                return SelectedNavKey == "BimParts" || SelectedNavKey == "BimTypes"
                    || SelectedNavKey == "Charts" || SelectedNavKey == "ChartBuilder"
                    || SelectedNavKey == "Summary";
            }
        }

        public string SelectedNavKey
        {
            get { return _selectedNavKey; }
            set
            {
                _selectedNavKey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedNavTitle));
                OnPropertyChanged(nameof(ShowBimModelFilter));
            }
        }

        public string SelectedNavTitle
        {
            get
            {
                foreach (var n in Navigation)
                    if (n.Key == SelectedNavKey)
                        return n.Title;
                return SelectedNavKey;
            }
        }

        public string ProgressText
        {
            get { return _progressText; }
            set
            {
                _progressText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HeaderSubtitle));
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HeaderSubtitle));
            }
        }

        public ScanMode ScanMode
        {
            get { return _scanMode; }
            set { _scanMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFast)); OnPropertyChanged(nameof(IsStandard)); OnPropertyChanged(nameof(IsFull)); }
        }

        public bool IsFast
        {
            get { return ScanMode == ScanMode.Fast; }
            set { if (value) ScanMode = ScanMode.Fast; }
        }

        public bool IsStandard
        {
            get { return ScanMode == ScanMode.Standard; }
            set { if (value) ScanMode = ScanMode.Standard; }
        }

        public bool IsFull
        {
            get { return ScanMode == ScanMode.Full; }
            set { if (value) ScanMode = ScanMode.Full; }
        }

        public string LimitationsText
        {
            get { return _limitationsText; }
            set { _limitationsText = value; OnPropertyChanged(); }
        }

        public string HeaderSubtitle
        {
            get
            {
                if (_isBusy)
                    return "идёт сканирование — таблицы заполнятся после завершения · " + (_progressText ?? "");
                if (_snapshot == null)
                    return "данные не загружены — нажмите «Обновить»";
                return _snapshot.ScanMode + " · " + _snapshot.DataSource + " · " + _snapshot.GeneratedAt.ToString("g");
            }
        }

        public ProjectAnalyticsSnapshot Snapshot
        {
            get { return _snapshot; }
            set
            {
                _snapshot = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HeaderSubtitle));
                ReloadCollections();
            }
        }

        public void ReloadCollections()
        {
            Summary.Clear();
            ChartTypesTop.Clear();
            ChartCreatorsTop.Clear();
            ChartCreatedTimeline.Clear();
            ChartIfcTypesTop.Clear();
            ChartStateSemantic.Clear();
            ChartResponsibleTop.Clear();
            ObjectsByType.Clear();
            ObjectsByCreator.Clear();
            ObjectsByCreatedMonth.Clear();
            ObjectsByUserState.Clear();
            ObjectsByUserStateSemantic.Clear();
            ObjectsByResponsible.Clear();
            DocumentVersions.Clear();
            BimSummary.Clear();
            BimModelAnalytics.Clear();
            BimPartAnalytics.Clear();
            BimElementTypeCounts.Clear();
            DataQuality.Clear();
            ModelRemarks.Clear();
            RemarkLinks.Clear();
            ScanDiff.Clear();
            BimModelFilters.Clear();
            BimModelFilters.Add(new BimModelFilterItem { ModelId = Guid.Empty, DisplayName = "Все модели", IsAll = true });

            _allBimParts.Clear();
            _allBimTypes.Clear();

            if (_snapshot == null)
            {
                LimitationsText = null;
                SelectedBimModelFilter = BimModelFilters[0];
                return;
            }

            foreach (var row in _snapshot.Summary) Summary.Add(row);
            foreach (var row in _snapshot.ObjectsByType) ObjectsByType.Add(row);
            foreach (var row in _snapshot.ObjectsByCreator) ObjectsByCreator.Add(row);
            foreach (var row in _snapshot.ObjectsByCreatedMonth) ObjectsByCreatedMonth.Add(row);
            foreach (var row in _snapshot.ObjectsByUserState) ObjectsByUserState.Add(row);
            foreach (var row in _snapshot.ObjectsByUserStateSemantic) ObjectsByUserStateSemantic.Add(row);
            foreach (var row in _snapshot.ObjectsByResponsible) ObjectsByResponsible.Add(row);
            foreach (var row in _snapshot.DocumentVersions) DocumentVersions.Add(row);
            foreach (var row in _snapshot.BimSummary) BimSummary.Add(row);
            foreach (var row in _snapshot.BimModelAnalytics) BimModelAnalytics.Add(row);
            foreach (var row in _snapshot.BimPartAnalytics) _allBimParts.Add(row);
            foreach (var row in _snapshot.BimElementTypeCounts) _allBimTypes.Add(row);
            foreach (var row in _snapshot.DataQuality) DataQuality.Add(row);
            foreach (var row in _snapshot.ModelRemarks) ModelRemarks.Add(row);
            foreach (var row in _snapshot.RemarkLinks) RemarkLinks.Add(row);

            foreach (var model in _snapshot.BimModelAnalytics.OrderBy(m => m.ModelName ?? string.Empty))
            {
                BimModelFilters.Add(new BimModelFilterItem
                {
                    ModelId = model.ModelId,
                    DisplayName = model.ModelName ?? model.ModelId.ToString()
                });
            }

            SelectedBimModelFilter = BimModelFilters[0];
            ApplyBimModelFilter();
            ReloadCharts();

            LimitationsText = _snapshot.Limitations != null
                ? string.Join(Environment.NewLine, _snapshot.Limitations)
                : null;

            RefreshScanDiff(_snapshot);
            RebuildDashboardWidgets();
        }

        private void RefreshScanDiff(ProjectAnalyticsSnapshot snapshot)
        {
            _scanDiffAll.Clear();
            ScanDiff.Clear();
            if (snapshot != null)
                snapshot.ScanDiffRows = new List<ScanDiffRow>();
            try
            {
                var current = _scanDiff.Capture(snapshot);
                _currentScanBaseline = current;

                var previousLast = _scanStore.TryLoadLast();
                StoredScanBaseline previous = null;
                string previousLabel = "предыдущий last-scan";

                if (_selectedScanHistory != null && !string.IsNullOrWhiteSpace(_selectedScanHistory.Id))
                {
                    previous = _scanStore.TryLoadById(_selectedScanHistory.Id);
                    if (previous != null)
                        previousLabel = _selectedScanHistory.DisplayTitle;
                }
                if (previous == null)
                {
                    previous = previousLast;
                    previousLabel = "предыдущий last-scan";
                }

                var rows = _scanDiff.Diff(previous, current);
                SetScanDiffRows(rows, snapshot);

                _scanStore.SaveLast(current);

                if (previousLast != null
                    && previousLast.GeneratedAt != current.GeneratedAt
                    && !HistoryHasGeneratedAt(previousLast.GeneratedAt))
                {
                    _scanStore.ArchiveToHistory(
                        previousLast,
                        "Авто · " + previousLast.GeneratedAt.ToString("yyyy-MM-dd HH:mm"));
                }

                ReloadScanHistoryList();
                ScanDiffHint = previous == null
                    ? "База сохранена. Следующее «Обновить» покажет diff. Можно сохранить именованный снимок."
                    : "Сравнение с: " + previousLabel;
            }
            catch (Exception ex)
            {
                SetScanDiffRows(new List<ScanDiffRow>
                {
                    new ScanDiffRow
                    {
                        Area = "Ошибка",
                        Metric = "Сравнение сканов",
                        Previous = "—",
                        Current = "—",
                        Delta = "n/a",
                        Notes = ex.Message
                    }
                }, snapshot);
                ScanDiffHint = "Ошибка сравнения: " + ex.Message;
            }
        }

        private void SetScanDiffRows(List<ScanDiffRow> rows, ProjectAnalyticsSnapshot snapshot)
        {
            _scanDiffAll.Clear();
            if (rows != null)
                _scanDiffAll.AddRange(rows);

            if (snapshot != null)
            {
                snapshot.ScanDiffRows = new List<ScanDiffRow>();
                foreach (var row in _scanDiffAll)
                    snapshot.ScanDiffRows.Add(row);
            }

            PublishScanDiffRows();
        }

        private void PublishScanDiffRows()
        {
            ScanDiff.Clear();
            foreach (var row in _scanDiffAll)
            {
                if (_scanDiffChangesOnly && !IsMeaningfulChange(row))
                    continue;
                ScanDiff.Add(row);
            }
        }

        private static bool IsMeaningfulChange(ScanDiffRow row)
        {
            if (row == null)
                return false;
            if (row.Area == "Скан" && row.Metric == "Время скана")
                return true;
            if (row.Area == "Ошибка")
                return true;
            var d = row.Delta;
            if (string.IsNullOrWhiteSpace(d) || d == "0" || d == "n/a" || d == "+0")
                return false;
            return true;
        }

        private bool HistoryHasGeneratedAt(DateTime generatedAt)
        {
            return _scanStore.ListHistory()
                .Any(e => e != null && e.GeneratedAt == generatedAt);
        }

        public void ReloadScanHistoryList()
        {
            var selectedId = _selectedScanHistory != null ? _selectedScanHistory.Id : null;
            ScanHistory.Clear();
            foreach (var e in _scanStore.ListHistory())
                ScanHistory.Add(e);

            if (selectedId != null)
                SelectedScanHistory = ScanHistory.FirstOrDefault(e => e.Id == selectedId);
        }

        public void CompareWithSelectedHistory()
        {
            if (_currentScanBaseline == null && _snapshot != null)
                _currentScanBaseline = _scanDiff.Capture(_snapshot);

            if (_currentScanBaseline == null)
            {
                ScanDiffHint = "Нет текущего скана — нажмите «Обновить».";
                return;
            }

            if (_selectedScanHistory == null)
            {
                ScanDiffHint = "Выберите снимок в списке истории.";
                return;
            }

            var previous = _scanStore.TryLoadById(_selectedScanHistory.Id);
            var rows = _scanDiff.Diff(previous, _currentScanBaseline);
            SetScanDiffRows(rows, _snapshot);

            ScanDiffHint = previous == null
                ? "Снимок не найден на диске."
                : "Сравнение с: " + _selectedScanHistory.DisplayTitle;
        }

        public string AddChartBuilderToDashboard()
        {
            var sourceId = _selectedChartSource != null ? _selectedChartSource.Id : "Types";
            var kindId = _selectedChartKind != null ? _selectedChartKind.Id : "HorizontalBar";
            var sourceTitle = _selectedChartSource != null ? _selectedChartSource.Title : "Данные";
            var kindTitle = _selectedChartKind != null ? _selectedChartKind.Title : "График";
            int topN = 12;
            if (_selectedChartTopN != null)
                int.TryParse(_selectedChartTopN.Id, out topN);

            var title = sourceTitle + " · " + kindTitle;
            AddDashboardWidget(new DashboardWidgetState
            {
                WidgetKind = DashboardWidgetKinds.Chart,
                Title = title,
                ChartSource = sourceId,
                ChartKind = kindId,
                TopN = topN,
                ColumnSpan = 2,
                IsVisible = true
            });
            return title;
        }

        public bool SaveNamedScan(string name)
        {
            if (_currentScanBaseline == null && _snapshot != null)
                _currentScanBaseline = _scanDiff.Capture(_snapshot);

            if (_currentScanBaseline == null)
            {
                ScanDiffHint = "Нет текущего скана — нажмите «Обновить».";
                return false;
            }

            var id = _scanStore.SaveNamed(_currentScanBaseline, name);
            ReloadScanHistoryList();
            if (id != null)
            {
                SelectedScanHistory = ScanHistory.FirstOrDefault(e => e.Id == id);
                ScanDiffHint = "Сохранён снимок: " + (SelectedScanHistory != null ? SelectedScanHistory.DisplayTitle : name);
                return true;
            }

            ScanDiffHint = "Не удалось сохранить снимок.";
            return false;
        }

        public void DeleteSelectedHistory()
        {
            if (_selectedScanHistory == null)
                return;
            var id = _selectedScanHistory.Id;
            if (_scanStore.DeleteHistory(id))
            {
                SelectedScanHistory = null;
                ReloadScanHistoryList();
                ScanDiffHint = "Снимок удалён из истории.";
            }
        }

        private void InitDashboardLayout()
        {
            _dashboardLayout = _dashboardLayoutStore.LoadOrDefault();
            RebuildDashboardUi();
        }

        private void RebuildDashboardUi()
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

            RebuildDashboardWidgets();
        }

        public void RebuildDashboardWidgets()
        {
            if (DashboardWidgets == null)
                return;

            IEnumerable<BimElementTypeCountRow> ifc = _allBimTypes;
            var filter = _selectedBimModelFilter;
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

                    widget.ChartSeries = _snapshot == null
                        ? new List<ChartSeriesPoint>()
                        : _charts.BuildSeries(_snapshot, ifc, source, kind, widget.TopN);
                    continue;
                }

                widget.Rows.Clear();
                if (_snapshot == null)
                    continue;

                if (widget.WidgetKind == DashboardWidgetKinds.Kpi || widget.Id == DashboardBlockIds.Kpi)
                {
                    foreach (var row in _snapshot.Summary ?? Enumerable.Empty<AnalyticsKpiRow>())
                        widget.Rows.Add(row);
                }
                else if (widget.WidgetKind == DashboardWidgetKinds.Bim || widget.Id == DashboardBlockIds.Bim)
                {
                    foreach (var row in _snapshot.BimSummary ?? Enumerable.Empty<AnalyticsKpiRow>())
                        widget.Rows.Add(row);
                }
                else if (widget.WidgetKind == DashboardWidgetKinds.Responsible || widget.Id == DashboardBlockIds.Responsible)
                {
                    foreach (var row in (_snapshot.ObjectsByResponsible ?? Enumerable.Empty<ResponsibleCountRow>()).Take(10))
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

        public void SetDashboardBlockVisible(string id, bool visible)
        {
            var block = (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>())
                .FirstOrDefault(b => b.Id == id);
            if (block == null)
                return;
            block.IsVisible = visible;
            PersistAndRebuildDashboard();
        }

        public void MoveDashboardBlock(string id, int delta)
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
            PersistAndRebuildDashboard();
        }

        public void AddDashboardWidget(DashboardWidgetState draft)
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
                    PersistAndRebuildDashboard();
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
            PersistAndRebuildDashboard();
        }

        public void UpdateDashboardWidget(string id, DashboardWidgetState draft)
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

            PersistAndRebuildDashboard();
        }

        public void RemoveDashboardWidget(string id)
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
            PersistAndRebuildDashboard();
        }

        public DashboardWidgetState GetWidgetState(string id)
        {
            return (_dashboardLayout.Widgets ?? new List<DashboardWidgetState>())
                .FirstOrDefault(w => w.Id == id);
        }

        private void PersistAndRebuildDashboard()
        {
            _dashboardLayoutStore.Save(_dashboardLayout);
            _dashboardLayout = _dashboardLayoutStore.LoadOrDefault();
            RebuildDashboardUi();
        }

        private void ApplyBimModelFilter()
        {
            BimPartAnalytics.Clear();
            BimElementTypeCounts.Clear();

            var filter = _selectedBimModelFilter;
            IEnumerable<BimPartAnalyticsRow> parts = _allBimParts;
            IEnumerable<BimElementTypeCountRow> types = _allBimTypes;

            if (filter != null && !filter.IsAll && filter.ModelId != Guid.Empty)
            {
                parts = parts.Where(p => p.ModelId == filter.ModelId);
                types = types.Where(t => t.ModelId == filter.ModelId);
            }

            foreach (var row in parts)
                BimPartAnalytics.Add(row);
            foreach (var row in types)
                BimElementTypeCounts.Add(row);

            ReloadIfcChart();
            RebuildDashboardWidgets();
        }

        private void ReloadCharts()
        {
            ChartTypesTop.Clear();
            ChartCreatorsTop.Clear();
            ChartCreatedTimeline.Clear();
            ChartStateSemantic.Clear();
            ChartResponsibleTop.Clear();

            if (_snapshot == null)
            {
                ChartIfcTypesTop.Clear();
                return;
            }

            foreach (var row in _charts.BuildTopTypes(_snapshot, 12)) ChartTypesTop.Add(row);
            foreach (var row in _charts.BuildTopCreators(_snapshot, 10)) ChartCreatorsTop.Add(row);
            foreach (var row in _charts.BuildCreatedTimeline(_snapshot)) ChartCreatedTimeline.Add(row);
            foreach (var row in _charts.BuildStateSemantic(_snapshot)) ChartStateSemantic.Add(row);
            foreach (var row in _charts.BuildTopResponsible(_snapshot, 10)) ChartResponsibleTop.Add(row);
            ReloadIfcChart();
            RebuildChartBuilder();
        }

        private void ReloadIfcChart()
        {
            ChartIfcTypesTop.Clear();
            if (_snapshot == null)
                return;

            IEnumerable<BimElementTypeCountRow> types = _allBimTypes;
            var filter = _selectedBimModelFilter;
            if (filter != null && !filter.IsAll && filter.ModelId != Guid.Empty)
                types = types.Where(t => t.ModelId == filter.ModelId);

            foreach (var row in _charts.BuildTopIfcTypes(types, 12))
                ChartIfcTypesTop.Add(row);

            RebuildChartBuilder();
        }

        public void RebuildChartBuilder()
        {
            if (ChartBuilderPoints == null)
                return;

            ChartBuilderPoints.Clear();

            AnalyticsChartKind kind;
            if (_selectedChartKind == null || !Enum.TryParse(_selectedChartKind.Id, out kind))
                kind = AnalyticsChartKind.HorizontalBar;
            BuilderChartKind = kind;

            AnalyticsChartSource source;
            if (_selectedChartSource == null || !Enum.TryParse(_selectedChartSource.Id, out source))
                source = AnalyticsChartSource.Types;

            int take = 12;
            if (_selectedChartTopN != null)
                int.TryParse(_selectedChartTopN.Id, out take);

            if (_snapshot == null)
            {
                ChartBuilderSeries = new List<ChartSeriesPoint>();
                ChartBuilderHint = "Нет данных — нажмите «Обновить».";
                return;
            }

            IEnumerable<BimElementTypeCountRow> ifc = _allBimTypes;
            var filter = _selectedBimModelFilter;
            if (filter != null && !filter.IsAll && filter.ModelId != Guid.Empty)
                ifc = ifc.Where(t => t.ModelId == filter.ModelId);

            var series = _charts.BuildSeries(_snapshot, ifc, source, kind, take);
            ChartBuilderSeries = series;
            foreach (var p in series)
                ChartBuilderPoints.Add(p);

            var srcTitle = _selectedChartSource != null ? _selectedChartSource.Title : source.ToString();
            var kindTitle = _selectedChartKind != null ? _selectedChartKind.Title : kind.ToString();
            ChartBuilderHint = ChartBuilderPoints.Count == 0
                ? "Для выбранного источника нет точек. Смените источник или выполните полное сканирование."
                : srcTitle + " · " + kindTitle + " · точек: " + ChartBuilderPoints.Count
                  + (source == AnalyticsChartSource.IfcTypes && filter != null && !filter.IsAll
                      ? " · модель: " + filter.DisplayName
                      : "");
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
