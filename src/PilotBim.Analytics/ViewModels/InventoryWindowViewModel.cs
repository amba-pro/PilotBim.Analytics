using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.ViewModels
{
    internal sealed class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }

    internal sealed class NavItem
    {
        public string Key { get; set; }
        public string Title { get; set; }
    }

    internal sealed class InventoryWindowViewModel : INotifyPropertyChanged
    {
        private string _selectedNavKey = "Overview";
        private string _progressText = "Готово к сканированию. Выберите режим и нажмите «Сканировать».";
        private bool _isBusy;
        private ScanMode _scanMode = ScanMode.Standard;
        private ProjectInventoryReport _report;
        private TypeInventoryRecord _selectedType;
        private string _attributeTypeFilter;
        private string _attributeNameFilter;
        private BimElementSample _selectedBimElement;

        public InventoryWindowViewModel()
        {
            Navigation = new ObservableCollection<NavItem>
            {
                new NavItem { Key = "Overview", Title = "Обзор" },
                new NavItem { Key = "Types", Title = "Карточки" },
                new NavItem { Key = "Attributes", Title = "Атрибуты" },
                new NavItem { Key = "SystemFields", Title = "Системные поля" },
                new NavItem { Key = "States", Title = "Статусы" },
                new NavItem { Key = "Persons", Title = "Пользователи" },
                new NavItem { Key = "Organisations", Title = "Организации" },
                new NavItem { Key = "Structure", Title = "Структура" },
                new NavItem { Key = "Documents", Title = "Документы / версии" },
                new NavItem { Key = "History", Title = "История" },
                new NavItem { Key = "Bim", Title = "BIM" },
                new NavItem { Key = "Capabilities", Title = "Возможности аналитики" },
                new NavItem { Key = "Diagnostics", Title = "Диагностика" }
            };

            Types = new ObservableCollection<TypeInventoryRecord>();
            Attributes = new ObservableCollection<AttributeInventoryRecord>();
            SystemFields = new ObservableCollection<SystemFieldCapability>();
            States = new ObservableCollection<StateInventoryRecord>();
            Persons = new ObservableCollection<PersonInventoryRecord>();
            Organisations = new ObservableCollection<OrganisationInventoryRecord>();
            DocumentCapabilities = new ObservableCollection<DocumentCapabilityRecord>();
            DocumentSamples = new ObservableCollection<DocumentSampleRow>();
            HistoryCapabilities = new ObservableCollection<HistoryCapabilityRecord>();
            HistorySamples = new ObservableCollection<HistorySampleEvent>();
            BimCapabilities = new ObservableCollection<BimCapability>();
            BimElements = new ObservableCollection<BimElementSample>();
            BimModelAnalytics = new ObservableCollection<BimModelAnalyticsRow>();
            BimPartAnalytics = new ObservableCollection<BimPartAnalyticsRow>();
            BimElementTypeCounts = new ObservableCollection<BimElementTypeCountRow>();
            RemarkLinks = new ObservableCollection<RemarkLinkRow>();
            AnalyticsCapabilities = new ObservableCollection<AnalyticsCapability>();
            DataQualityCapabilities = new ObservableCollection<AnalyticsCapability>();
            Diagnostics = new ObservableCollection<DiagnosticEntry>();
            StructureRoots = new ObservableCollection<StructureNodeModel>();
            Zones = new ObservableCollection<ZoneResult>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<NavItem> Navigation { get; }
        public ObservableCollection<TypeInventoryRecord> Types { get; }
        public ObservableCollection<AttributeInventoryRecord> Attributes { get; }
        public ObservableCollection<SystemFieldCapability> SystemFields { get; }
        public ObservableCollection<StateInventoryRecord> States { get; }
        public ObservableCollection<PersonInventoryRecord> Persons { get; }
        public ObservableCollection<OrganisationInventoryRecord> Organisations { get; }
        public ObservableCollection<DocumentCapabilityRecord> DocumentCapabilities { get; }
        public ObservableCollection<DocumentSampleRow> DocumentSamples { get; }
        public ObservableCollection<HistoryCapabilityRecord> HistoryCapabilities { get; }
        public ObservableCollection<HistorySampleEvent> HistorySamples { get; }
        public ObservableCollection<BimCapability> BimCapabilities { get; }
        public ObservableCollection<BimElementSample> BimElements { get; }
        public ObservableCollection<BimModelAnalyticsRow> BimModelAnalytics { get; }
        public ObservableCollection<BimPartAnalyticsRow> BimPartAnalytics { get; }
        public ObservableCollection<BimElementTypeCountRow> BimElementTypeCounts { get; }
        public ObservableCollection<RemarkLinkRow> RemarkLinks { get; }
        public ObservableCollection<AnalyticsCapability> AnalyticsCapabilities { get; }
        public ObservableCollection<AnalyticsCapability> DataQualityCapabilities { get; }
        public ObservableCollection<DiagnosticEntry> Diagnostics { get; }
        public ObservableCollection<StructureNodeModel> StructureRoots { get; }
        public ObservableCollection<ZoneResult> Zones { get; }

        public string SelectedNavKey
        {
            get { return _selectedNavKey; }
            set { _selectedNavKey = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedNavTitle)); }
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
            set { _progressText = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set { _isBusy = value; OnPropertyChanged(); }
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

        public ProjectInventoryReport Report
        {
            get { return _report; }
            set
            {
                _report = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OverviewSummary));
                ReloadCollections();
            }
        }

        public TypeInventoryRecord SelectedType
        {
            get { return _selectedType; }
            set { _selectedType = value; OnPropertyChanged(); }
        }

        public string AttributeTypeFilter
        {
            get { return _attributeTypeFilter; }
            set { _attributeTypeFilter = value; OnPropertyChanged(); ApplyAttributeFilter(); }
        }

        public string AttributeNameFilter
        {
            get { return _attributeNameFilter; }
            set { _attributeNameFilter = value; OnPropertyChanged(); ApplyAttributeFilter(); }
        }

        public BimElementSample SelectedBimElement
        {
            get { return _selectedBimElement; }
            set { _selectedBimElement = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedBimPropertiesText)); }
        }

        public string SelectedBimPropertiesText
        {
            get
            {
                if (SelectedBimElement == null || SelectedBimElement.PropertyPreview == null)
                    return "";
                return string.Join(Environment.NewLine, SelectedBimElement.PropertyPreview);
            }
        }

        public string OverviewSummary
        {
            get
            {
                if (Report == null)
                    return "Отчёт ещё не сформирован.";
                return
                    "Types discovered: " + Report.TypesDiscovered + Environment.NewLine +
                    "Objects found / estimated: " + Report.ObjectsFound + Environment.NewLine +
                    "Attributes discovered: " + Report.AttributesDiscovered + Environment.NewLine +
                    "States discovered: " + Report.StatesDiscovered + Environment.NewLine +
                    "Persons resolved: " + Report.PersonsResolved + Environment.NewLine +
                    "Organisations resolved: " + Report.OrganisationsResolved + Environment.NewLine +
                    "BIM models discovered: " + Report.BimModelsCount + Environment.NewLine +
                    "BIM model parts: " + Report.BimModelPartsCount + Environment.NewLine +
                    "BIM indexed elements: " + (Report.BimPartAnalytics != null ? Report.BimPartAnalytics.Sum(p => p.ElementCount) : 0) + Environment.NewLine +
                    "Remark links sampled: " + (Report.RemarkLinks != null ? Report.RemarkLinks.Count : 0) + Environment.NewLine +
                    "Warnings: " + Report.Warnings.Count + Environment.NewLine +
                    "Errors: " + Report.Errors.Count + Environment.NewLine +
                    "Analytics readiness: " + Report.AnalyticsReadiness + Environment.NewLine +
                    "Final status: " + Report.FinalStatus;
            }
        }

        public void ReloadCollections()
        {
            Types.Clear();
            Attributes.Clear();
            SystemFields.Clear();
            States.Clear();
            Persons.Clear();
            Organisations.Clear();
            DocumentCapabilities.Clear();
            DocumentSamples.Clear();
            HistoryCapabilities.Clear();
            HistorySamples.Clear();
            BimCapabilities.Clear();
            BimElements.Clear();
            BimModelAnalytics.Clear();
            BimPartAnalytics.Clear();
            BimElementTypeCounts.Clear();
            RemarkLinks.Clear();
            AnalyticsCapabilities.Clear();
            DataQualityCapabilities.Clear();
            Diagnostics.Clear();
            Zones.Clear();

            if (Report == null)
                return;

            foreach (var t in Report.Types) Types.Add(t);
            foreach (var a in Report.AllAttributes) Attributes.Add(a);
            foreach (var f in Report.SystemFields) SystemFields.Add(f);
            foreach (var s in Report.States) States.Add(s);
            foreach (var p in Report.Persons) Persons.Add(p);
            foreach (var o in Report.Organisations) Organisations.Add(o);
            foreach (var d in Report.DocumentCapabilities) DocumentCapabilities.Add(d);
            foreach (var d in Report.DocumentSamples) DocumentSamples.Add(d);
            foreach (var h in Report.HistoryCapabilities) HistoryCapabilities.Add(h);
            foreach (var h in Report.HistorySamples) HistorySamples.Add(h);
            foreach (var b in Report.BimCapabilities) BimCapabilities.Add(b);
            foreach (var e in Report.BimElementSamples) BimElements.Add(e);
            if (Report.BimModelAnalytics != null)
                foreach (var r in Report.BimModelAnalytics) BimModelAnalytics.Add(r);
            if (Report.BimPartAnalytics != null)
                foreach (var r in Report.BimPartAnalytics) BimPartAnalytics.Add(r);
            if (Report.BimElementTypeCounts != null)
                foreach (var r in Report.BimElementTypeCounts) BimElementTypeCounts.Add(r);
            if (Report.RemarkLinks != null)
                foreach (var r in Report.RemarkLinks) RemarkLinks.Add(r);
            foreach (var m in Report.AnalyticsCapabilities) AnalyticsCapabilities.Add(m);
            foreach (var m in Report.DataQualityCapabilities) DataQualityCapabilities.Add(m);
            foreach (var d in Report.Diagnostics) Diagnostics.Add(d);
            foreach (var z in Report.ZoneResults) Zones.Add(z);

            if (Types.Count > 0)
                SelectedType = Types[0];
        }

        private void ApplyAttributeFilter()
        {
            Attributes.Clear();
            if (Report == null)
                return;
            foreach (var a in Report.AllAttributes)
            {
                if (!string.IsNullOrWhiteSpace(AttributeTypeFilter)
                    && (a.TypeName == null || a.TypeName.IndexOf(AttributeTypeFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    && (a.TypeId.ToString().IndexOf(AttributeTypeFilter, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                if (!string.IsNullOrWhiteSpace(AttributeNameFilter)
                    && (a.Name == null || a.Name.IndexOf(AttributeNameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    && (a.Title == null || a.Title.IndexOf(AttributeNameFilter, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                Attributes.Add(a);
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
