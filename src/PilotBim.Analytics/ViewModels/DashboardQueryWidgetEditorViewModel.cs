using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Properties;
using PilotBim.Analytics.Services;

namespace PilotBim.Analytics.ViewModels
{
    /// <summary>
    /// Query-widget editor. Metadata + explicit Preview execution. Does not persist.
    /// </summary>
    internal sealed class DashboardQueryWidgetEditorViewModel : INotifyPropertyChanged
    {
        private readonly DashboardFieldCatalog _catalog;
        private readonly DashboardWidgetLayoutDefinition _layout;
        private readonly string _widgetId;
        private readonly bool _isNew;
        private bool _loading;
        private bool _titleTouched;
        private string _title;
        private string _limitText = string.Empty;
        private DashboardObjectTypeOption _selectedType;
        private DashboardFieldOption _selectedDimension;
        private DashboardEditorChoice _selectedSort;
        private DashboardEditorChoice _selectedVisualization;
        private DashboardQueryCoordinator _coordinator;
        private Action<Action> _postToUi;
        private int _previewGeneration;
        private DashboardQueryWidgetRuntimeStatus _previewStatus = DashboardQueryWidgetRuntimeStatus.Idle;
        private string _previewMessage;
        private bool _previewShowChart;
        private bool _previewShowKpi;
        private bool _previewShowTable;
        private bool _previewTableIsScalar;
        private string _previewKpiValue;
        private string _previewAutoResolved;
        private string _previewWarning;
        private AnalyticsChartKind _previewChartKind = AnalyticsChartKind.VerticalBar;
        private IList<ChartSeriesPoint> _previewPoints = new List<ChartSeriesPoint>();

        public DashboardQueryWidgetEditorViewModel(
            DashboardFieldCatalog catalog,
            IEnumerable<DashboardObjectTypeOption> types,
            DashboardWidgetDefinition existing)
        {
            if (catalog == null)
                throw new ArgumentNullException("catalog");
            _catalog = catalog;

            TypeOptions = new ObservableCollection<DashboardObjectTypeOption>();
            foreach (var type in types ?? new DashboardObjectTypeOption[0])
            {
                if (type != null && !type.IsUnavailable)
                    TypeOptions.Add(type);
            }

            GroupByFields = new ObservableCollection<DashboardFieldOption>();
            FilterFields = new ObservableCollection<DashboardFieldOption>();
            FilterRows = new ObservableCollection<DashboardQueryFilterRowViewModel>();
            VisualizationChoices = new ObservableCollection<DashboardEditorChoice>();
            SortChoices = CreateSortChoices();
            _selectedSort = SortChoices[0];

            if (existing != null && string.Equals(existing.ContentKind, DashboardPersistenceV2.ContentQuery, StringComparison.Ordinal))
            {
                _isNew = false;
                _widgetId = existing.Id;
                _titleTouched = true;
                _title = existing.Title ?? string.Empty;
                _layout = CloneLayout(existing.Layout);
                LoadExisting(existing);
            }
            else
            {
                _isNew = true;
                _widgetId = "query-" + Guid.NewGuid().ToString("N");
                _layout = new DashboardWidgetLayoutDefinition { Order = 0, ColumnSpan = 2, IsVisible = true };
                _titleTouched = false;
                if (TypeOptions.Count > 0)
                    SelectedType = TypeOptions[0];
                else
                    RefreshFields();
                SelectedDimension = FindDimension(null);
                SelectedVisualization = FindVisualization("Auto");
                RefreshGeneratedTitle();
            }

            PreviewKpiRows = new ObservableCollection<AnalyticsKpiRow>();
            PreviewTableRows = new ObservableCollection<DashboardQueryTableRow>();
            SetPreview(
                DashboardQueryWidgetRuntimeStatus.Idle,
                Resources.QueryEditor_PreviewIdle,
                null);

            Revalidate();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<DashboardObjectTypeOption> TypeOptions { get; private set; }
        public ObservableCollection<DashboardFieldOption> GroupByFields { get; private set; }
        public ObservableCollection<DashboardFieldOption> FilterFields { get; private set; }
        public ObservableCollection<DashboardQueryFilterRowViewModel> FilterRows { get; private set; }
        public ObservableCollection<DashboardEditorChoice> VisualizationChoices { get; private set; }
        public IList<DashboardEditorChoice> SortChoices { get; private set; }

        public string ScopeDisplay
        {
            get { return Resources.QueryEditor_ScopeCurrentProject; }
        }

        public string MeasureDisplay
        {
            get { return Resources.QueryEditor_MeasureCount; }
        }

        public string Title
        {
            get { return _title; }
            set
            {
                var next = value ?? string.Empty;
                if (_title == next)
                    return;
                _title = next;
                if (!_loading)
                    _titleTouched = true;
                Raise();
                Revalidate();
            }
        }

        public DashboardObjectTypeOption SelectedType
        {
            get { return _selectedType; }
            set
            {
                if (ReferenceEquals(_selectedType, value))
                    return;
                var previousId = _selectedType != null ? (int?)_selectedType.TypeId : null;
                _selectedType = value;
                Raise();
                if (!_loading && previousId != (value != null ? (int?)value.TypeId : null))
                    OnTypeChanged();
                Revalidate();
            }
        }

        public DashboardFieldOption SelectedDimension
        {
            get { return _selectedDimension; }
            set
            {
                if (ReferenceEquals(_selectedDimension, value))
                    return;
                var hadGroup = HasDimension;
                _selectedDimension = value;
                Raise();
                Raise("HasDimension");
                RefreshVisualizations(hadGroup);
                if (!_loading && !hadGroup && HasDimension && string.IsNullOrWhiteSpace(_limitText))
                    LimitText = "10";
                if (!_loading)
                    RefreshGeneratedTitle();
                Revalidate();
            }
        }

        public DashboardEditorChoice SelectedSort
        {
            get { return _selectedSort; }
            set
            {
                if (ReferenceEquals(_selectedSort, value))
                    return;
                _selectedSort = value;
                Raise();
                Revalidate();
            }
        }

        public string LimitText
        {
            get { return _limitText; }
            set
            {
                var next = value ?? string.Empty;
                if (_limitText == next)
                    return;
                _limitText = next;
                Raise();
                Revalidate();
            }
        }

        public DashboardEditorChoice SelectedVisualization
        {
            get { return _selectedVisualization; }
            set
            {
                if (ReferenceEquals(_selectedVisualization, value))
                    return;
                _selectedVisualization = value;
                Raise();
                Raise("ShowAutoHint");
                Revalidate();
            }
        }

        public bool HasDimension
        {
            get { return _selectedDimension != null && !_selectedDimension.IsNone && !string.IsNullOrEmpty(_selectedDimension.FieldId); }
        }

        public bool ShowAutoHint
        {
            get { return _selectedVisualization != null && _selectedVisualization.Id == "Auto"; }
        }

        public bool IsValid { get; private set; }
        public string ValidationMessage { get; private set; }
        public string SummaryText { get; private set; }

        public DashboardQueryWidgetRuntimeStatus PreviewStatus
        {
            get { return _previewStatus; }
            private set
            {
                _previewStatus = value;
                Raise();
                Raise("PreviewShowMessage");
            }
        }

        public string PreviewMessage
        {
            get { return _previewMessage; }
            private set
            {
                _previewMessage = value;
                Raise();
            }
        }

        public bool PreviewShowMessage
        {
            get { return PreviewStatus != DashboardQueryWidgetRuntimeStatus.Success; }
        }

        public bool PreviewShowChart
        {
            get { return _previewShowChart; }
            private set
            {
                _previewShowChart = value;
                Raise();
            }
        }

        public bool PreviewShowKpi
        {
            get { return _previewShowKpi; }
            private set
            {
                _previewShowKpi = value;
                Raise();
            }
        }

        public bool PreviewShowTable
        {
            get { return _previewShowTable; }
            private set
            {
                _previewShowTable = value;
                Raise();
                Raise("PreviewShowTableScalar");
                Raise("PreviewShowTableGrouped");
            }
        }

        public bool PreviewShowTableScalar
        {
            get { return PreviewShowTable && _previewTableIsScalar; }
        }

        public bool PreviewShowTableGrouped
        {
            get { return PreviewShowTable && !_previewTableIsScalar; }
        }

        public string PreviewKpiValue
        {
            get { return _previewKpiValue; }
            private set
            {
                _previewKpiValue = value;
                Raise();
            }
        }

        public string PreviewAutoResolved
        {
            get { return _previewAutoResolved; }
            private set
            {
                _previewAutoResolved = value;
                Raise();
                Raise("ShowPreviewAutoResolved");
            }
        }

        public bool ShowPreviewAutoResolved
        {
            get { return !string.IsNullOrEmpty(_previewAutoResolved); }
        }

        public string PreviewWarning
        {
            get { return _previewWarning; }
            private set
            {
                _previewWarning = value;
                Raise();
                Raise("ShowPreviewWarning");
            }
        }

        public bool ShowPreviewWarning
        {
            get { return !string.IsNullOrEmpty(_previewWarning); }
        }

        public AnalyticsChartKind PreviewChartKind
        {
            get { return _previewChartKind; }
            private set
            {
                _previewChartKind = value;
                Raise();
            }
        }

        public IList<ChartSeriesPoint> PreviewPoints
        {
            get { return _previewPoints; }
            private set
            {
                _previewPoints = value ?? new List<ChartSeriesPoint>();
                Raise();
            }
        }

        public ObservableCollection<AnalyticsKpiRow> PreviewKpiRows { get; private set; }
        public ObservableCollection<DashboardQueryTableRow> PreviewTableRows { get; private set; }
        public int PreviewGeneration { get { return Volatile.Read(ref _previewGeneration); } }

        public void AttachSession(DashboardQueryCoordinator coordinator, Action<Action> postToUi)
        {
            _coordinator = coordinator;
            _postToUi = postToUi ?? (action => { if (action != null) action(); });
        }

        public void AddFilter()
        {
            var row = new DashboardQueryFilterRowViewModel(this);
            if (FilterFields.Count > 0)
                row.SelectedField = FilterFields[0];
            FilterRows.Add(row);
            Revalidate();
        }

        public void RemoveFilter(DashboardQueryFilterRowViewModel row)
        {
            if (row == null)
                return;
            FilterRows.Remove(row);
            Revalidate();
        }

        public void OnFilterChanged()
        {
            Revalidate();
        }

        public DashboardWidgetDefinition TrySave()
        {
            Revalidate();
            if (!IsValid)
                return null;

            int? limit;
            TryParseLimit(out limit);

            var filters = new List<DashboardFilterDefinition>();
            foreach (var row in FilterRows)
            {
                DashboardFilterDefinition filter;
                string error;
                if (!row.TryBuild(out filter, out error))
                    return null;
                filters.Add(filter);
            }

            var dimension = HasDimension ? _selectedDimension.FieldId : null;
            DashboardQuerySort sort;
            if (_selectedSort == null || !Enum.TryParse(_selectedSort.Id, out sort))
                sort = DashboardQuerySort.ValueDescending;

            var query = new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                dimension,
                DashboardQueryMeasure.Count,
                sort,
                limit,
                _selectedType.TypeId,
                filters);

            var span = _isNew ? (HasDimension ? 1 : 2) : _layout.ColumnSpan;
            return new DashboardWidgetDefinition
            {
                Id = _widgetId,
                Title = (_title ?? string.Empty).Trim(),
                ContentKind = DashboardPersistenceV2.ContentQuery,
                Layout = new DashboardWidgetLayoutDefinition
                {
                    Order = _layout.Order,
                    ColumnSpan = span <= 1 ? 1 : 2,
                    IsVisible = _layout.IsVisible,
                    X = _layout.X,
                    Y = _layout.Y,
                    Width = _layout.Width,
                    Height = _layout.Height
                },
                Query = DashboardQueryPersistence.ToDocument(query),
                Visualization = new DashboardVisualizationDefinition
                {
                    Type = _selectedVisualization != null ? _selectedVisualization.Id : "Auto"
                }
            };
        }

        internal async Task RunPreviewAsync()
        {
            var generation = Interlocked.Increment(ref _previewGeneration);
            PostPreview(() => SetPreview(
                DashboardQueryWidgetRuntimeStatus.Loading,
                Resources.QueryWidget_Loading,
                null));

            string validation;
            if (!TryValidate(out validation))
            {
                if (generation != Volatile.Read(ref _previewGeneration))
                    return;
                PostPreview(() => SetPreview(
                    DashboardQueryWidgetRuntimeStatus.Invalid,
                    validation ?? Resources.QueryWidget_Invalid,
                    null));
                return;
            }

            var candidate = TrySave();
            if (candidate == null)
            {
                if (generation != Volatile.Read(ref _previewGeneration))
                    return;
                PostPreview(() => SetPreview(
                    DashboardQueryWidgetRuntimeStatus.Invalid,
                    Resources.QueryWidget_Invalid,
                    null));
                return;
            }

            if (_coordinator == null)
            {
                if (generation != Volatile.Read(ref _previewGeneration))
                    return;
                PostPreview(() => SetPreview(
                    DashboardQueryWidgetRuntimeStatus.Error,
                    Resources.QueryWidget_Error,
                    null));
                return;
            }

            WidgetQueryResult result;
            try
            {
                DashboardWidgetQuery query;
                string error;
                if (!DashboardQueryPersistence.TryToQuery(candidate.Query, out query, out error))
                    result = WidgetQueryResult.Invalid(error);
                else
                    result = await _coordinator.ExecuteAsync(query, CancellationToken.None).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("DashboardQuery", "Preview", ex);
                result = null;
            }

            if (generation != Volatile.Read(ref _previewGeneration))
                return;

            var captured = result;
            var capturedCandidate = candidate;
            PostPreview(() => ApplyPreviewResult(capturedCandidate, captured, generation));
        }

        private void ApplyPreviewResult(DashboardWidgetDefinition candidate, WidgetQueryResult result, int generation)
        {
            if (generation != Volatile.Read(ref _previewGeneration))
                return;
            if (result == null)
            {
                SetPreview(DashboardQueryWidgetRuntimeStatus.Error, Resources.QueryWidget_Error, null);
                return;
            }

            switch (result.Status)
            {
                case WidgetQueryStatus.Success:
                    var hasDimension = candidate.Query != null && !string.IsNullOrWhiteSpace(candidate.Query.DimensionFieldId);
                    var viz = candidate.Visualization != null ? candidate.Visualization.Type : "Auto";
                    var render = DashboardWidgetDatasetAdapter.TryRender(result.Dataset, viz, hasDimension);
                    if (render.Status != DashboardQueryWidgetRuntimeStatus.Success)
                        SetPreview(render.Status, render.Message ?? Resources.QueryWidget_Invalid, null);
                    else
                        SetPreview(DashboardQueryWidgetRuntimeStatus.Success, null, render);
                    break;
                case WidgetQueryStatus.Empty:
                    SetPreview(DashboardQueryWidgetRuntimeStatus.Empty, Resources.QueryWidget_Empty, null);
                    break;
                case WidgetQueryStatus.IncompleteData:
                    SetPreview(DashboardQueryWidgetRuntimeStatus.Incomplete, Resources.QueryWidget_Incomplete, null);
                    break;
                case WidgetQueryStatus.UnsupportedQuery:
                    SetPreview(DashboardQueryWidgetRuntimeStatus.Unsupported, Resources.QueryWidget_Unsupported, null);
                    break;
                case WidgetQueryStatus.InvalidQuery:
                    SetPreview(DashboardQueryWidgetRuntimeStatus.Invalid, Resources.QueryWidget_Invalid, null);
                    break;
                default:
                    SetPreview(DashboardQueryWidgetRuntimeStatus.Error, Resources.QueryWidget_Error, null);
                    break;
            }
        }

        private void SetPreview(
            DashboardQueryWidgetRuntimeStatus status,
            string message,
            DashboardQueryRenderModel render)
        {
            PreviewStatus = status;
            PreviewMessage = message ?? string.Empty;
            var success = status == DashboardQueryWidgetRuntimeStatus.Success && render != null;
            PreviewShowChart = success && render.ShowChart;
            PreviewShowKpi = success && render.ShowKpi;
            PreviewShowTable = success && render.ShowTable;
            _previewTableIsScalar = success && render.TableIsScalar;
            Raise("PreviewShowTableScalar");
            Raise("PreviewShowTableGrouped");
            PreviewPoints = success && render.ShowChart ? render.Points : new List<ChartSeriesPoint>();
            if (success && render.ShowChart)
                PreviewChartKind = render.ChartKind;
            PreviewKpiValue = success && render.ShowKpi && render.KpiRows != null && render.KpiRows.Count > 0
                ? render.KpiRows[0].Value
                : string.Empty;
            PreviewWarning = success ? render.Warning : null;
            if (success && ShowAutoHint && render.ResolvedVisualization != null)
                PreviewAutoResolved = string.Format(Resources.QueryEditor_AutoResolved, DashboardVisualizationRecommendationService.DisplayName(render.ResolvedVisualization));
            else
                PreviewAutoResolved = null;

            PreviewKpiRows.Clear();
            if (success && render.ShowKpi && render.KpiRows != null)
            {
                foreach (var row in render.KpiRows)
                    PreviewKpiRows.Add(row);
            }

            PreviewTableRows.Clear();
            if (success && render.ShowTable && render.TableRows != null)
            {
                foreach (var row in render.TableRows)
                    PreviewTableRows.Add(row);
            }
        }

        private void PostPreview(Action action)
        {
            if (action == null)
                return;
            if (_postToUi != null)
                _postToUi(action);
            else
                action();
        }

        internal static bool IsObjectRowsExecutable(DashboardFieldDescriptor descriptor)
        {
            if (descriptor == null)
                return false;
            if (descriptor.FieldType == DashboardFieldType.DateTime || descriptor.FieldType == DashboardFieldType.Unknown)
                return false;
            if (descriptor.Id == DashboardFieldIds.SystemCreatedMonth)
                return false;
            return true;
        }

        internal static List<DashboardEditorChoice> CreateOperators()
        {
            return new List<DashboardEditorChoice>
            {
                new DashboardEditorChoice("Equals", Resources.QueryEditor_OpEquals),
                new DashboardEditorChoice("NotEquals", Resources.QueryEditor_OpNotEquals),
                new DashboardEditorChoice("IsEmpty", Resources.QueryEditor_OpIsEmpty),
                new DashboardEditorChoice("IsNotEmpty", Resources.QueryEditor_OpIsNotEmpty)
            };
        }

        internal static List<DashboardEditorChoice> CreateBooleanChoices()
        {
            return new List<DashboardEditorChoice>
            {
                new DashboardEditorChoice("true", Resources.QueryEditor_BoolYes),
                new DashboardEditorChoice("false", Resources.QueryEditor_BoolNo)
            };
        }

        private void LoadExisting(DashboardWidgetDefinition existing)
        {
            _loading = true;
            var query = existing.Query;
            int? typeId = query != null ? query.EntityTypeId : null;
            if (typeId.HasValue)
            {
                var match = FindType(typeId.Value);
                if (match == null)
                {
                    match = new DashboardObjectTypeOption(typeId.Value, null, true);
                    TypeOptions.Insert(0, match);
                }
                _selectedType = match;
            }

            RefreshFields();

            var dimensionId = query != null ? query.DimensionFieldId : null;
            _selectedDimension = FindDimension(dimensionId);
            if (!string.IsNullOrWhiteSpace(dimensionId) && (_selectedDimension == null || _selectedDimension.IsNone))
            {
                var missing = DashboardFieldOption.Unavailable(dimensionId);
                GroupByFields.Add(missing);
                _selectedDimension = missing;
            }

            if (query != null && !string.IsNullOrWhiteSpace(query.Sort))
            {
                foreach (var choice in SortChoices)
                {
                    if (choice.Id == query.Sort)
                    {
                        _selectedSort = choice;
                        break;
                    }
                }
            }

            if (query != null && query.Limit.HasValue)
                _limitText = query.Limit.Value.ToString(CultureInfo.InvariantCulture);

            var viz = existing.Visualization != null ? existing.Visualization.Type : "Auto";
            RefreshVisualizations(false);
            _selectedVisualization = FindVisualization(viz) ?? EnsureVisualization(viz);

            if (query != null && query.Filters != null)
            {
                foreach (var doc in query.Filters)
                {
                    DashboardFilterDefinition filter;
                    string error;
                    if (!DashboardQueryPersistence.TryToFilter(doc, out filter, out error))
                        continue;
                    var row = new DashboardQueryFilterRowViewModel(this);
                    var field = FindFilterField(filter.FieldId);
                    var mismatch = false;
                    if (field == null)
                    {
                        field = DashboardFieldOption.Unavailable(filter.FieldId);
                        FilterFields.Add(field);
                    }
                    else if (filter.Value != null
                        && (filter.Operator == DashboardFilterOperator.Equals || filter.Operator == DashboardFilterOperator.NotEquals)
                        && !DashboardFieldPredicate.AreKindsCompatible(field.FieldType, filter.Value.Kind))
                    {
                        mismatch = true;
                    }
                    row.Load(filter, field, mismatch);
                    FilterRows.Add(row);
                }
            }

            _loading = false;
            Raise("SelectedType");
            Raise("SelectedDimension");
            Raise("SelectedSort");
            Raise("LimitText");
            Raise("SelectedVisualization");
            Raise("Title");
            Raise("HasDimension");
            Raise("ShowAutoHint");
        }

        private void OnTypeChanged()
        {
            RefreshFields();
            FilterRows.Clear();
            SelectedDimension = FindDimension(null);
            RefreshGeneratedTitle();
        }

        private void RefreshFields()
        {
            GroupByFields.Clear();
            FilterFields.Clear();
            GroupByFields.Add(DashboardFieldOption.None());

            if (_selectedType == null || _selectedType.IsUnavailable)
            {
                NotifyFilterFieldChoices();
                return;
            }

            var fields = _catalog.ForObjectType(_selectedType.TypeId);
            foreach (var field in fields)
            {
                if (field == null || !IsObjectRowsExecutable(field))
                    continue;
                var option = DashboardFieldOption.FromDescriptor(field);
                if (field.Capabilities != null && field.Capabilities.CanGroup)
                    GroupByFields.Add(option);
                if (field.Capabilities != null && field.Capabilities.CanFilter)
                    FilterFields.Add(option);
            }

            NotifyFilterFieldChoices();
        }

        private void NotifyFilterFieldChoices()
        {
            foreach (var row in FilterRows)
                row.NotifyFieldChoicesChanged();
        }

        private void RefreshVisualizations(bool keepCurrentIfAllowed)
        {
            var current = _selectedVisualization != null ? _selectedVisualization.Id : "Auto";
            VisualizationChoices.Clear();
            foreach (var id in AllowedVisualizations())
                VisualizationChoices.Add(VisualizationChoice(id));

            DashboardEditorChoice next = null;
            foreach (var choice in VisualizationChoices)
            {
                if (choice.Id == current)
                {
                    next = choice;
                    break;
                }
            }
            if (next == null)
                next = FindVisualization("Auto");
            _selectedVisualization = next;
            Raise("SelectedVisualization");
            Raise("ShowAutoHint");
        }

        private IEnumerable<string> AllowedVisualizations()
        {
            if (HasDimension)
            {
                yield return "Auto";
                yield return "Bar";
                yield return "HorizontalBar";
                yield return "Pie";
                yield return "Table";
            }
            else
            {
                yield return "Auto";
                yield return "Kpi";
                yield return "Table";
            }
        }

        private bool IsVisualizationAllowed(string type)
        {
            foreach (var id in AllowedVisualizations())
            {
                if (id == type)
                    return true;
            }
            return false;
        }

        private DashboardEditorChoice EnsureVisualization(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                type = "Auto";
            foreach (var choice in VisualizationChoices)
            {
                if (choice.Id == type)
                    return choice;
            }
            var extra = VisualizationChoice(type);
            VisualizationChoices.Add(extra);
            return extra;
        }

        private DashboardEditorChoice VisualizationChoice(string id)
        {
            switch (id)
            {
                case "Kpi":
                    return new DashboardEditorChoice(id, Resources.QueryEditor_VizKpi);
                case "Bar":
                    return new DashboardEditorChoice(id, Resources.QueryEditor_VizBar);
                case "HorizontalBar":
                    return new DashboardEditorChoice(id, Resources.QueryEditor_VizHorizontalBar);
                case "Pie":
                    return new DashboardEditorChoice(id, Resources.QueryEditor_VizPie);
                case "Line":
                    return new DashboardEditorChoice(id, Resources.QueryEditor_VizLine);
                case "Table":
                    return new DashboardEditorChoice(id, Resources.QueryEditor_VizTable);
                default:
                    return new DashboardEditorChoice("Auto", Resources.QueryEditor_VizAuto);
            }
        }

        private void RefreshGeneratedTitle()
        {
            if (_titleTouched)
                return;
            _title = GenerateTitle();
            Raise("Title");
        }

        private string GenerateTitle()
        {
            var typeName = _selectedType != null ? _selectedType.Label : Resources.QueryEditor_MeasureCount;
            if (!HasDimension)
                return typeName + " — " + Resources.QueryEditor_MeasureCount;
            return typeName + " по " + _selectedDimension.Label;
        }

        private void Revalidate()
        {
            string message;
            var valid = TryValidate(out message);
            if (IsValid != valid)
            {
                IsValid = valid;
                Raise("IsValid");
            }
            if (ValidationMessage != message)
            {
                ValidationMessage = message;
                Raise("ValidationMessage");
            }
            var summary = BuildSummary();
            if (SummaryText != summary)
            {
                SummaryText = summary;
                Raise("SummaryText");
            }
        }

        private bool TryValidate(out string message)
        {
            message = null;
            if (_selectedType == null)
            {
                message = Resources.QueryEditor_NeedType;
                return false;
            }
            if (_selectedType.IsUnavailable)
            {
                message = Resources.QueryEditor_UnavailableMetadata;
                return false;
            }
            if (string.IsNullOrWhiteSpace(_title))
            {
                message = Resources.QueryEditor_NeedTitle;
                return false;
            }
            if (_selectedDimension != null && _selectedDimension.IsUnavailable)
            {
                message = Resources.QueryEditor_UnavailableMetadata;
                return false;
            }

            int? limit;
            if (!TryParseLimit(out limit))
            {
                message = Resources.QueryEditor_InvalidLimit;
                return false;
            }

            var viz = _selectedVisualization != null ? _selectedVisualization.Id : "Auto";
            if (!IsVisualizationAllowed(viz))
            {
                message = Resources.QueryEditor_InvalidVisualization;
                return false;
            }

            foreach (var row in FilterRows)
            {
                string error;
                if (!row.TryBuild(out error))
                {
                    message = error;
                    return false;
                }
            }

            return true;
        }

        private bool TryParseLimit(out int? limit)
        {
            limit = null;
            var text = (_limitText ?? string.Empty).Trim();
            if (text.Length == 0)
                return true;
            int n;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0)
                return false;
            limit = n;
            return true;
        }

        private string BuildSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Resources.QueryEditor_SummaryType + ": " + (_selectedType != null ? _selectedType.Label : "—"));
            sb.AppendLine(Resources.QueryEditor_SummaryFilters + ": " + FilterRows.Count);
            sb.AppendLine(Resources.QueryEditor_SummaryMeasure + ": " + Resources.QueryEditor_MeasureCount);
            sb.AppendLine(Resources.QueryEditor_SummaryGroup + ": " + (_selectedDimension != null ? _selectedDimension.Label : Resources.QueryEditor_GroupByNone));
            sb.AppendLine(Resources.QueryEditor_SummaryViz + ": " + (_selectedVisualization != null ? _selectedVisualization.Title : "Auto"));
            return sb.ToString();
        }

        private DashboardObjectTypeOption FindType(int typeId)
        {
            foreach (var type in TypeOptions)
            {
                if (type.TypeId == typeId)
                    return type;
            }
            return null;
        }

        private DashboardFieldOption FindDimension(string fieldId)
        {
            if (string.IsNullOrWhiteSpace(fieldId))
            {
                foreach (var option in GroupByFields)
                {
                    if (option.IsNone)
                        return option;
                }
                return DashboardFieldOption.None();
            }
            foreach (var option in GroupByFields)
            {
                if (string.Equals(option.FieldId, fieldId, StringComparison.Ordinal))
                    return option;
            }
            return null;
        }

        private DashboardFieldOption FindFilterField(string fieldId)
        {
            foreach (var option in FilterFields)
            {
                if (string.Equals(option.FieldId, fieldId, StringComparison.Ordinal))
                    return option;
            }
            return null;
        }

        private DashboardEditorChoice FindVisualization(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                id = "Auto";
            foreach (var choice in VisualizationChoices)
            {
                if (choice.Id == id)
                    return choice;
            }
            return null;
        }

        private static List<DashboardEditorChoice> CreateSortChoices()
        {
            return new List<DashboardEditorChoice>
            {
                new DashboardEditorChoice("ValueDescending", Resources.QueryEditor_SortValueDesc),
                new DashboardEditorChoice("ValueAscending", Resources.QueryEditor_SortValueAsc),
                new DashboardEditorChoice("LabelAscending", Resources.QueryEditor_SortLabelAsc),
                new DashboardEditorChoice("LabelDescending", Resources.QueryEditor_SortLabelDesc)
            };
        }

        private static DashboardWidgetLayoutDefinition CloneLayout(DashboardWidgetLayoutDefinition layout)
        {
            if (layout == null)
                return new DashboardWidgetLayoutDefinition { Order = 0, ColumnSpan = 2, IsVisible = true };
            return new DashboardWidgetLayoutDefinition
            {
                Order = layout.Order,
                ColumnSpan = layout.ColumnSpan <= 1 ? 1 : 2,
                IsVisible = layout.IsVisible,
                X = layout.X,
                Y = layout.Y,
                Width = layout.Width,
                Height = layout.Height
            };
        }

        private void Raise([CallerMemberName] string name = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
