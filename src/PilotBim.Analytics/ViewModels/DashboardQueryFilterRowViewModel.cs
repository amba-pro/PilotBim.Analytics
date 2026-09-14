using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Properties;
using PilotBim.Analytics.Services;

namespace PilotBim.Analytics.ViewModels
{
    internal enum DashboardFilterValueEditorKind
    {
        None = 0,
        Text = 1,
        Integer = 2,
        Number = 3,
        Boolean = 4,
        Guid = 5,
        StableKey = 6
    }

    internal sealed class DashboardQueryFilterRowViewModel : INotifyPropertyChanged
    {
        private readonly DashboardQueryWidgetEditorViewModel _owner;
        private DashboardFieldOption _selectedField;
        private DashboardEditorChoice _selectedOperator;
        private string _valueText = string.Empty;
        private DashboardEditorChoice _selectedBoolean;
        private bool _kindMismatch;

        public DashboardQueryFilterRowViewModel(DashboardQueryWidgetEditorViewModel owner)
        {
            if (owner == null)
                throw new ArgumentNullException("owner");
            _owner = owner;
            Operators = DashboardQueryWidgetEditorViewModel.CreateOperators();
            BooleanChoices = DashboardQueryWidgetEditorViewModel.CreateBooleanChoices();
            _selectedOperator = Operators[2]; // IsEmpty — new rows do not immediately invalidate Save
            _selectedBoolean = BooleanChoices[0];
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public IList<DashboardFieldOption> FieldChoices
        {
            get { return _owner.FilterFields; }
        }

        public IList<DashboardEditorChoice> Operators { get; private set; }
        public IList<DashboardEditorChoice> BooleanChoices { get; private set; }

        public DashboardFieldOption SelectedField
        {
            get { return _selectedField; }
            set
            {
                if (ReferenceEquals(_selectedField, value))
                    return;
                _selectedField = value;
                _kindMismatch = false;
                Raise("SelectedField");
                Raise("ValueEditorKind");
                Raise("ShowTextValue");
                Raise("ShowBooleanValue");
                Raise("ShowStableKeyHint");
                _owner.OnFilterChanged();
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
                Raise("SelectedOperator");
                Raise("NeedsValue");
                Raise("ValueEditorKind");
                Raise("ShowTextValue");
                Raise("ShowBooleanValue");
                Raise("ShowStableKeyHint");
                _owner.OnFilterChanged();
            }
        }

        public string ValueText
        {
            get { return _valueText; }
            set
            {
                var next = value ?? string.Empty;
                if (_valueText == next)
                    return;
                _valueText = next;
                Raise("ValueText");
                _owner.OnFilterChanged();
            }
        }

        public DashboardEditorChoice SelectedBoolean
        {
            get { return _selectedBoolean; }
            set
            {
                if (ReferenceEquals(_selectedBoolean, value))
                    return;
                _selectedBoolean = value;
                Raise("SelectedBoolean");
                _owner.OnFilterChanged();
            }
        }

        public bool KindMismatch
        {
            get { return _kindMismatch; }
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
                return DashboardFilterOperator.IsEmpty;
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

        public string ValidationMessage
        {
            get
            {
                string unused;
                TryBuild(out unused);
                return unused;
            }
        }

        public void MarkKindMismatch()
        {
            _kindMismatch = true;
            Raise("KindMismatch");
            _owner.OnFilterChanged();
        }

        public void NotifyFieldChoicesChanged()
        {
            Raise("FieldChoices");
        }

        public bool TryBuild(out string error)
        {
            error = null;
            DashboardFilterDefinition unused;
            return TryBuild(out unused, out error);
        }

        public bool TryBuild(out DashboardFilterDefinition filter, out string error)
        {
            filter = null;
            error = null;
            if (_selectedField == null || _selectedField.IsNone || string.IsNullOrWhiteSpace(_selectedField.FieldId))
            {
                error = Resources.QueryEditor_UnavailableMetadata;
                return false;
            }
            if (_selectedField.IsUnavailable)
            {
                error = Resources.QueryEditor_UnavailableMetadata;
                return false;
            }
            if (_kindMismatch)
            {
                error = Resources.QueryEditor_KindMismatch;
                return false;
            }

            var op = Operator;
            if (!NeedsValue)
            {
                filter = new DashboardFilterDefinition(_selectedField.FieldId, op, null);
                return true;
            }

            DashboardFilterValue value;
            if (!TryParseValue(out value, out error))
                return false;
            filter = new DashboardFilterDefinition(_selectedField.FieldId, op, value);
            return true;
        }

        public void Load(DashboardFilterDefinition filter, DashboardFieldOption field, bool kindMismatch)
        {
            _selectedField = field;
            _kindMismatch = kindMismatch;
            var op = filter == null ? DashboardFilterOperator.IsEmpty : filter.Operator;
            _selectedOperator = FindOperator(op);
            if (filter != null && filter.Value != null && NeedsValue)
            {
                if (ValueEditorKind == DashboardFilterValueEditorKind.Boolean)
                {
                    var encoded = filter.Value.Value is bool && (bool)filter.Value.Value ? "true" : "false";
                    _selectedBoolean = encoded == "true" ? BooleanChoices[0] : BooleanChoices[1];
                }
                else
                {
                    string kind;
                    string encoded;
                    string encodeError;
                    if (DashboardFilterValueCodec.TryEncode(filter.Value, out kind, out encoded, out encodeError))
                        _valueText = encoded ?? string.Empty;
                }
            }
            Raise("SelectedField");
            Raise("SelectedOperator");
            Raise("ValueText");
            Raise("SelectedBoolean");
            Raise("NeedsValue");
            Raise("ValueEditorKind");
            Raise("ShowTextValue");
            Raise("ShowBooleanValue");
            Raise("ShowStableKeyHint");
            Raise("KindMismatch");
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

        private DashboardEditorChoice FindOperator(DashboardFilterOperator op)
        {
            var id = op.ToString();
            for (var i = 0; i < Operators.Count; i++)
            {
                if (Operators[i].Id == id)
                    return Operators[i];
            }
            return Operators[2];
        }

        private void Raise([CallerMemberName] string name = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
