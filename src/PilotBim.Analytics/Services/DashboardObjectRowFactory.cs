using System;
using System.Collections.Generic;
using System.Globalization;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Maps a loaded object snapshot to <see cref="DashboardObjectRow"/> using DB-1 field ids.
    /// No Pilot SDK types.
    /// </summary>
    internal sealed class DashboardObjectRowFactory
    {
        public int SkippedUnsupportedValues { get; private set; }
        private readonly List<string> _skippedFieldIds = new List<string>();

        public IReadOnlyList<string> SkippedUnsupportedFieldIds
        {
            get { return _skippedFieldIds; }
        }

        public DashboardObjectRow Create(DashboardObjectSource source)
        {
            SkippedUnsupportedValues = 0;
            _skippedFieldIds.Clear();
            if (source == null || source.Id == Guid.Empty)
                return null;

            var fields = new Dictionary<string, DashboardFieldValue>(StringComparer.Ordinal);
            Add(fields, DashboardFieldIds.SystemObjectId, GuidValue(source.Id));
            Add(fields, DashboardFieldIds.SystemTypeId, IntegerValue(source.TypeId));
            if (source.ParentId != Guid.Empty)
                Add(fields, DashboardFieldIds.SystemParentId, GuidValue(source.ParentId));
            if (source.CreatorId.HasValue)
                Add(fields, DashboardFieldIds.SystemCreatorId, IntegerValue(source.CreatorId.Value));
            if (source.Created != default(DateTime))
                Add(fields, DashboardFieldIds.SystemCreated, DateValue(source.Created));
            if (!string.IsNullOrWhiteSpace(source.ObjectState))
            {
                Add(fields, DashboardFieldIds.SystemObjectState, new DashboardFieldValue(
                    DashboardFieldType.Enum,
                    source.ObjectState,
                    source.ObjectState,
                    null));
            }

            DashboardFieldValue userState = null;
            DashboardFieldValue responsible = null;
            if (source.Attributes != null)
            {
                foreach (var attr in source.Attributes)
                {
                    if (attr == null || string.IsNullOrWhiteSpace(attr.Name))
                        continue;
                    if (!attr.Present)
                        continue;

                    var fieldId = DashboardFieldIds.Attribute(source.TypeId, attr.Name.Trim());
                    DashboardFieldValue mapped;
                    if (!TryMapAttribute(attr, out mapped))
                    {
                        SkippedUnsupportedValues++;
                        _skippedFieldIds.Add(fieldId);
                        continue;
                    }
                    if (!fields.ContainsKey(fieldId))
                        fields.Add(fieldId, mapped);
                    if (userState == null && string.Equals(attr.ValueType, "UserState", StringComparison.Ordinal))
                        userState = mapped;
                    if (responsible == null && string.Equals(attr.ValueType, "OrgUnit", StringComparison.Ordinal))
                        responsible = mapped;
                }
            }

            Add(fields, DashboardFieldIds.SystemUserState, userState);
            Add(fields, DashboardFieldIds.SystemResponsible, responsible);

            Guid? parent = source.ParentId == Guid.Empty ? (Guid?)null : source.ParentId;
            return new DashboardObjectRow(source.Id, source.TypeId, parent, fields);
        }

        private static void Add(Dictionary<string, DashboardFieldValue> fields, string id, DashboardFieldValue value)
        {
            if (value == null || fields.ContainsKey(id))
                return;
            fields.Add(id, value);
        }

        internal static bool TryMapAttribute(DashboardAttributeSource attr, out DashboardFieldValue mapped)
        {
            mapped = null;
            if (attr == null)
                return false;

            var kind = PilotFieldCatalogBuilder.MapValueType(attr.ValueType);
            if (kind == DashboardFieldType.Unknown)
                return false;

            var value = attr.Value;
            if (value == null)
            {
                mapped = new DashboardFieldValue(kind, null, null, null);
                return true;
            }

            switch (kind)
            {
                case DashboardFieldType.Text:
                    var text = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
                    mapped = new DashboardFieldValue(DashboardFieldType.Text, text ?? string.Empty, null, null);
                    return true;
                case DashboardFieldType.Integer:
                    long n;
                    if (!TryToInt64(value, out n))
                        return false;
                    mapped = new DashboardFieldValue(DashboardFieldType.Integer, n, n.ToString(CultureInfo.InvariantCulture), null);
                    return true;
                case DashboardFieldType.Number:
                    double d;
                    if (!TryToDouble(value, out d))
                        return false;
                    mapped = new DashboardFieldValue(DashboardFieldType.Number, d, d.ToString("R", CultureInfo.InvariantCulture), null);
                    return true;
                case DashboardFieldType.Boolean:
                    if (!(value is bool))
                        return false;
                    var b = (bool)value;
                    mapped = new DashboardFieldValue(DashboardFieldType.Boolean, b, b ? "1" : "0", null);
                    return true;
                case DashboardFieldType.DateTime:
                    if (!(value is DateTime))
                        return false;
                    var dt = (DateTime)value;
                    mapped = new DashboardFieldValue(DashboardFieldType.DateTime, dt, dt.ToString("o", CultureInfo.InvariantCulture), null);
                    return true;
                case DashboardFieldType.Guid:
                    Guid guidValue;
                    if (!TryToGuid(value, out guidValue))
                        return false;
                    mapped = new DashboardFieldValue(DashboardFieldType.Guid, guidValue, guidValue.ToString("D"), null);
                    return true;
                case DashboardFieldType.Enum:
                    Guid enumGuid;
                    if (TryToGuid(value, out enumGuid))
                    {
                        mapped = new DashboardFieldValue(DashboardFieldType.Enum, enumGuid, enumGuid.ToString("D"), null);
                        return true;
                    }
                    var enumText = Convert.ToString(value, CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(enumText))
                        return false;
                    mapped = new DashboardFieldValue(DashboardFieldType.Enum, enumText, enumText, null);
                    return true;
                case DashboardFieldType.User:
                case DashboardFieldType.Reference:
                    var ids = CollectIntIds(value);
                    if (ids.Count == 0)
                    {
                        mapped = new DashboardFieldValue(kind, null, null, null);
                        return true;
                    }
                    var key = string.Join(",", ids.ConvertAll(i => i.ToString(CultureInfo.InvariantCulture)).ToArray());
                    object boxed = ids.Count == 1 ? (object)ids[0] : ids.ToArray();
                    mapped = new DashboardFieldValue(kind, boxed, key, null);
                    return true;
                default:
                    return false;
            }
        }

        private static DashboardFieldValue GuidValue(Guid id)
        {
            return new DashboardFieldValue(DashboardFieldType.Guid, id, id.ToString("D"), null);
        }

        private static DashboardFieldValue IntegerValue(int value)
        {
            long n = value;
            return new DashboardFieldValue(DashboardFieldType.Integer, n, n.ToString(CultureInfo.InvariantCulture), null);
        }

        private static DashboardFieldValue DateValue(DateTime value)
        {
            return new DashboardFieldValue(DashboardFieldType.DateTime, value, value.ToString("o", CultureInfo.InvariantCulture), null);
        }

        private static bool TryToGuid(object value, out Guid guid)
        {
            guid = Guid.Empty;
            if (value is Guid)
            {
                guid = (Guid)value;
                return guid != Guid.Empty;
            }
            return Guid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out guid)
                && guid != Guid.Empty;
        }

        private static bool TryToInt64(object value, out long n)
        {
            n = 0;
            if (value is int)
            {
                n = (int)value;
                return true;
            }
            if (value is long)
            {
                n = (long)value;
                return true;
            }
            if (value is short)
            {
                n = (short)value;
                return true;
            }
            return false;
        }

        private static bool TryToDouble(object value, out double d)
        {
            d = 0;
            if (value is double)
            {
                d = (double)value;
                return true;
            }
            if (value is float)
            {
                d = (float)value;
                return true;
            }
            if (value is decimal)
            {
                d = (double)(decimal)value;
                return true;
            }
            long n;
            if (TryToInt64(value, out n))
            {
                d = n;
                return true;
            }
            return false;
        }

        private static List<int> CollectIntIds(object value)
        {
            var list = new List<int>();
            CollectIntIds(value, list);
            return list;
        }

        private static void CollectIntIds(object value, List<int> list)
        {
            if (value == null)
                return;
            if (value is int)
            {
                list.Add((int)value);
                return;
            }
            if (value is long)
            {
                var l = (long)value;
                if (l <= int.MaxValue && l >= int.MinValue)
                    list.Add((int)l);
                return;
            }
            var arr = value as Array;
            if (arr != null)
            {
                foreach (var item in arr)
                    CollectIntIds(item, list);
                return;
            }
            var enumerable = value as System.Collections.IEnumerable;
            if (enumerable != null && !(value is string))
            {
                foreach (var item in enumerable)
                    CollectIntIds(item, list);
            }
        }
    }
}
