using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Properties;
using PilotBim.Analytics.Services;

namespace PilotBim.Analytics.ViewModels
{
    internal sealed class DashboardFilterEditorViewModel : INotifyPropertyChanged
    {
        private readonly DashboardFieldCatalog _catalog;
        private readonly IReadOnlyList<DashboardWidgetDefinition> _widgets;
        private readonly string _filterId;
        private string _title;
        private DashboardObjectTypeOption _selectedType;
        private DashboardFieldOption _selectedField;
        private DashboardEditorChoice _selectedOperator;
        private DashboardEditorChoice _selectedBoolean;
        private string _valueText = string.Empty;
        private bool _enabled = true;
        private bool _loading;

        public DashboardFilterEditorViewModel(
            DashboardFieldCatalog catalog,
            IReadOnlyList<DashboardObjectTypeOption> typeOptions,
            IReadOnlyList<DashboardWidgetDefinition> widgets,
            DashboardLevelFilterDefinition existing)
        {
            _catalog = catalog ?? new PilotFieldCatalogBuilder().Build(Enumerable.Empty<TypeInventoryRecord>());
            _widgets = widgets ?? new DashboardWidgetDefinition[0];
            TypeOptions = new ObservableCollection<DashboardObjectTypeOption>();
            Fields = new ObservableCollection<DashboardFieldOption>();
            Targets = new ObservableCollection<DashboardFilterTargetVm>();
            Operators = DashboardQueryWidgetEditorViewModel.CreateOperators();
            BooleanChoices = DashboardQueryWidgetEditorViewModel.CreateBooleanChoices();
            _selectedOperator = Operators[0];
            _selectedBoolean = BooleanChoices[0];
            _filterId = existing != null && !string.IsNullOrWhiteSpace(existing.Id)
                ? existing.Id
                : Guid.NewGuid().ToString("D");

            BuildTypeOptions(typeOptions, existing);
            if (existing != null)
                LoadExisting(existing);
            else if (TypeOptions.Count > 0)
                SelectedType = TypeOptions[0];
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<DashboardObjectTypeOption> TypeOptions { get; private set; }
        public ObservableCollection<DashboardFieldOption> Fields { get; private set; }
        public ObservableCollection<DashboardFilterTargetVm> Targets { get; private set; }
        public IList<DashboardEditorChoice> Operators { get; private set; }
        public IList<DashboardEditorChoice> BooleanChoices { get; private set; }

        public string Title
        {
            get { return _title ?? string.Empty; }
            set
            {
                _title = value ?? string.Empty;
                Raise();
                Raise("IsValid");
                Raise("ValidationMessage");
            }
        }

        public DashboardObjectTypeOption SelectedType
        {
            get { return _selectedType; }
            set
            {
                if (ReferenceEquals(_selectedType, value))
                    return;
                _selectedType = value;
                Raise();
                if (!_loading)
                    OnTypeChanged();
                Raise("IsValid");
                Raise("ValidationMessage");
            }
        }

        public DashboardFieldOption SelectedField
        {
            get { return _selectedField; }
            set
            {
                if (ReferenceEquals(_selectedField, value))
                    return;
                _selectedField = value;
                Raise();
                Raise("ValueEditorKind");
                Raise("ShowTextValue");
                Raise("ShowBooleanValue");
                Raise("ShowStableKeyHint");
                Raise("IsValid");
                Raise("ValidationMessage");
            }
        }

        public DashboardEditorChoice SelectedOperator
        {
            get { return _selectedOperator; }
            set
            {
                if (ReferenceEquals(_selectedOperator, value))
                    return;
                _selectedOperator = value;
                Raise();
                Raise("NeedsValue");
                Raise("ValueEditorKind");
                Raise("ShowTextValue");
                Raise("ShowBooleanValue");
                Raise("ShowStableKeyHint");
                Raise("IsValid");
                Raise("ValidationMessage");
            }
        }

        public string ValueText
        {
            get { return _valueText; }
            set
            {
                _valueText = value ?? string.Empty;
                Raise();
                Raise("IsValid");
                Raise("ValidationMessage");
            }
        }

        public DashboardEditorChoice SelectedBoolean
        {
            get { return _selectedBoolean; }
            set
            {
                _selectedBoolean = value;
                Raise();
                Raise("IsValid");
                Raise("ValidationMessage");
            }
        }

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                Raise();
            }
        }

        public bool NeedsValue
        {
            get
            {
                var op = Operator;
                return op == DashboardFilterOperator.Equals || op == DashboardFilterOperator.NotEquals;
            }
        }

        public DashboardFilterOperator Operator
        {
            get
            {
                DashboardFilterOperator parsed;
                if (_selectedOperator != null && Enum.TryParse(_selectedOperator.Id, out parsed))
                    return parsed;
                return DashboardFilterOperator.Equals;
            }
        }

        public DashboardFilterValueEditorKind ValueEditorKind
        {
            get
            {
                if (!NeedsValue || _selectedField == null || _selectedField.IsNone)
                    return DashboardFilterValueEditorKind.None;
                switch (_selectedField.FieldType)
                {
                    case DashboardFieldType.Boolean:
                        return DashboardFilterValueEditorKind.Boolean;
                    case DashboardFieldType.Integer:
                        return DashboardFilterValueEditorKind.Integer;
                    case DashboardFieldType.Number:
                        return DashboardFilterValueEditorKind.Number;
                    case DashboardFieldType.Guid:
                        return DashboardFilterValueEditorKind.Guid;
                    case DashboardFieldType.Enum:
                    case DashboardFieldType.User:
                    case DashboardFieldType.Reference:
                        return DashboardFilterValueEditorKind.StableKey;
                    default:
                        return DashboardFilterValueEditorKind.Text;
                }
            }
        }

        public bool ShowTextValue
        {
            get
            {
                var kind = ValueEditorKind;
                return kind == DashboardFilterValueEditorKind.Text
                    || kind == DashboardFilterValueEditorKind.Integer
                    || kind == DashboardFilterValueEditorKind.Number
                    || kind == DashboardFilterValueEditorKind.Guid
                    || kind == DashboardFilterValueEditorKind.StableKey;
            }
        }

        public bool ShowBooleanValue
        {
            get { return ValueEditorKind == DashboardFilterValueEditorKind.Boolean; }
        }

        public bool ShowStableKeyHint
        {
            get { return ValueEditorKind == DashboardFilterValueEditorKind.StableKey; }
        }

        public bool HasCompatibleWidgets
        {
            get { return Targets.Count > 0; }
        }

        public bool ShowNoCompatibleWidgets
        {
            get { return !HasCompatibleWidgets; }
        }

        public bool IsValid
        {
            get
            {
                string unused;
                return TryBuild(out unused) != null;
            }
        }

        public string ValidationMessage
        {
            get
            {
                string error;
                TryBuild(out error);
                return error ?? string.Empty;
            }
        }

        public DashboardLevelFilterDefinition TryBuild(out string error)
        {
            error = null;
            if (_selectedType == null)
            {
                error = Resources.DashboardFilters_NoCompatibleWidgets;
                return null;
            }
            if (_selectedField == null || string.IsNullOrWhiteSpace(_selectedField.FieldId) || _selectedField.IsUnavailable)
            {
                error = Resources.QueryEditor_UnavailableMetadata;
                return null;
            }

            var op = Operator;
            DashboardFilterDefinition runtime;
            if (op == DashboardFilterOperator.IsEmpty || op == DashboardFilterOperator.IsNotEmpty)
                runtime = new DashboardFilterDefinition(_selectedField.FieldId, op, null);
            else
            {
                DashboardFilterValue value;
                if (!TryParseValue(out value, out error))
                    return null;
                runtime = new DashboardFilterDefinition(_selectedField.FieldId, op, value);
            }

            DashboardFilterDocument document;
            if (!DashboardQueryPersistence.TryToFilterDocument(runtime, out document, out error))
                return null;

            var targets = new List<string>();
            foreach (var target in Targets)
            {
                if (target != null && target.IsSelected)
                    targets.Add(target.Id);
            }
            if (targets.Count == 0)
            {
                error = Resources.DashboardFilters_NeedTarget;
                return null;
            }

            var title = string.IsNullOrWhiteSpace(_title) ? _selectedField.DisplayName : _title.Trim();
            return new DashboardLevelFilterDefinition
            {
                Id = _filterId,
                Title = title,
                EntityTypeId = _selectedType.TypeId,
                FieldId = _selectedField.FieldId,
                Operator = document.Operator,
                ValueKind = document.ValueKind,
                Value = document.Value,
                Disabled = !_enabled,
                TargetWidgetIds = targets
            };
        }

        private void BuildTypeOptions(
            IReadOnlyList<DashboardObjectTypeOption> typeOptions,
            DashboardLevelFilterDefinition existing)
        {
            var present = new HashSet<int>();
            for (var i = 0; i < _widgets.Count; i++)
            {
                var widget = _widgets[i];
                if (widget == null || widget.ContentKind != DashboardPersistenceV2.ContentQuery)
                    continue;
                if (widget.Query == null || !widget.Query.EntityTypeId.HasValue)
                    continue;
                present.Add(widget.Query.EntityTypeId.Value);
            }

            if (typeOptions != null)
            {
                for (var i = 0; i < typeOptions.Count; i++)
                {
                    var option = typeOptions[i];
                    if (option != null && present.Contains(option.TypeId))
                        TypeOptions.Add(option);
                }
            }

            foreach (var typeId in present.OrderBy(id => id))
            {
                var found = false;
                foreach (var option in TypeOptions)
                {
                    if (option.TypeId == typeId)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    TypeOptions.Add(new DashboardObjectTypeOption(typeId, null, true));
            }

            if (existing != null)
            {
                var exists = false;
                foreach (var option in TypeOptions)
                {
                    if (option.TypeId == existing.EntityTypeId)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    TypeOptions.Add(new DashboardObjectTypeOption(existing.EntityTypeId, null, true));
            }
        }

        private void LoadExisting(DashboardLevelFilterDefinition existing)
        {
            _loading = true;
            _title = existing.Title ?? string.Empty;
            _enabled = existing.IsEnabled;
            foreach (var option in TypeOptions)
            {
                if (option.TypeId == existing.EntityTypeId)
                {
                    _selectedType = option;
                    break;
                }
            }
            RefreshFieldsAndTargets(existing);
            DashboardFilterDefinition runtime;
            string error;
            if (DashboardFilterCompatibility.TryToRuntimeFilter(existing, out runtime, out error) && runtime != null)
            {
                foreach (var choice in Operators)
                {
                    if (choice.Id == runtime.Operator.ToString())
                    {
                        _selectedOperator = choice;
                        break;
                    }
                }
                if (runtime.Value != null && NeedsValue)
                {
                    if (ValueEditorKind == DashboardFilterValueEditorKind.Boolean)
                    {
                        var yes = runtime.Value.Value is bool && (bool)runtime.Value.Value;
                        _selectedBoolean = yes ? BooleanChoices[0] : BooleanChoices[1];
                    }
                    else
                    {
                        string kind;
                        string encoded;
                        string encodeError;
                        if (DashboardFilterValueCodec.TryEncode(runtime.Value, out kind, out encoded, out encodeError))
                            _valueText = encoded ?? string.Empty;
                    }
                }
            }
            _loading = false;
            Raise("Title");
            Raise("SelectedType");
            Raise("SelectedField");
            Raise("SelectedOperator");
            Raise("ValueText");
            Raise("SelectedBoolean");
            Raise("Enabled");
            Raise("NeedsValue");
            Raise("ValueEditorKind");
            Raise("ShowTextValue");
            Raise("ShowBooleanValue");
            Raise("ShowStableKeyHint");
            Raise("HasCompatibleWidgets");
            Raise("ShowNoCompatibleWidgets");
        }

        private void OnTypeChanged()
        {
            RefreshFieldsAndTargets(null);
        }

        private void RefreshFieldsAndTargets(DashboardLevelFilterDefinition existing)
        {
            Fields.Clear();
            Targets.Clear();
            if (_selectedType == null)
            {
                Raise("HasCompatibleWidgets");
                Raise("ShowNoCompatibleWidgets");
                return;
            }

            var fields = _catalog.ForObjectType(_selectedType.TypeId);
            DashboardFieldOption selected = null;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field == null
                    || field.Capabilities == null
                    || !field.Capabilities.CanFilter
                    || !DashboardFilterCompatibility.IsObjectRowsFilterable(field))
                    continue;
                var option = DashboardFieldOption.FromDescriptor(field);
                Fields.Add(option);
                if (existing != null && string.Equals(existing.FieldId, field.Id, StringComparison.Ordinal))
                    selected = option;
            }

            if (existing != null && selected == null && !string.IsNullOrWhiteSpace(existing.FieldId))
            {
                selected = DashboardFieldOption.Unavailable(existing.FieldId);
                Fields.Add(selected);
            }

            _selectedField = selected ?? (Fields.Count > 0 ? Fields[0] : null);

            var selectedIds = new HashSet<string>(StringComparer.Ordinal);
            if (existing != null && existing.TargetWidgetIds != null)
            {
                for (var i = 0; i < existing.TargetWidgetIds.Count; i++)
                    selectedIds.Add(existing.TargetWidgetIds[i]);
            }

            var draft = new DashboardLevelFilterDefinition
            {
                Id = _filterId,
                EntityTypeId = _selectedType.TypeId,
                FieldId = _selectedField != null ? _selectedField.FieldId : existing != null ? existing.FieldId : null,
                Operator = existing != null ? existing.Operator : "Equals",
                ValueKind = existing != null ? existing.ValueKind : null,
                Value = existing != null ? existing.Value : null,
                Disabled = existing != null && existing.Disabled
            };

            for (var i = 0; i < _widgets.Count; i++)
            {
                var widget = _widgets[i];
                if (widget == null)
                    continue;
                if (!DashboardFilterCompatibility.IsBindingStructurallyValid(draft, widget))
                    continue;
                var status = DashboardFilterCompatibility.EvaluateField(draft, widget, _catalog);
                if (status == DashboardFilterCompatibilityStatus.Incompatible)
                    continue;
                var checkedState = selectedIds.Contains(widget.Id) || (existing == null && status != DashboardFilterCompatibilityStatus.Incompatible);
                Targets.Add(new DashboardFilterTargetVm(widget.Id, widget.Title, checkedState));
            }

            Raise("SelectedField");
            Raise("HasCompatibleWidgets");
            Raise("ShowNoCompatibleWidgets");
            Raise("ValueEditorKind");
            Raise("ShowTextValue");
            Raise("ShowBooleanValue");
            Raise("ShowStableKeyHint");
        }

        private bool TryParseValue(out DashboardFilterValue value, out string error)
        {
            value = null;
            error = null;
            var kind = ValueEditorKind;
            if (kind == DashboardFilterValueEditorKind.Boolean)
            {
                var id = _selectedBoolean != null ? _selectedBoolean.Id : "true";
                value = DashboardFilterValue.Boolean(id == "true");
                return true;
            }

            var text = (_valueText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                error = Resources.QueryEditor_NeedEqualsValue;
                return false;
            }

            switch (kind)
            {
                case DashboardFilterValueEditorKind.Integer:
                    long n;
                    if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                    {
                        error = Resources.QueryEditor_InvalidNumber;
                        return false;
                    }
                    value = DashboardFilterValue.Integer(n);
                    return true;
                case DashboardFilterValueEditorKind.Number:
                    double d;
                    if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d)
                        || double.IsNaN(d) || double.IsInfinity(d))
                    {
                        error = Resources.QueryEditor_InvalidNumber;
                        return false;
                    }
                    value = DashboardFilterValue.Number(d);
                    return true;
                case DashboardFilterValueEditorKind.Guid:
                    Guid g;
                    if (!Guid.TryParse(text, out g))
                    {
                        error = Resources.QueryEditor_InvalidGuid;
                        return false;
                    }
                    value = DashboardFilterValue.Guid(g);
                    return true;
                case DashboardFilterValueEditorKind.StableKey:
                    value = DashboardFilterValue.Identity(_selectedField.FieldType, text);
                    return true;
                default:
                    value = DashboardFilterValue.Text(_valueText ?? string.Empty);
                    return true;
            }
        }

        private void Raise([CallerMemberName] string name = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
