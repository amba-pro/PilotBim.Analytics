using System;
using System.Globalization;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Object-row filter predicates. DisplayText never participates.
    /// Missing uses the same definition as <see cref="DashboardGroupValue"/>.
    /// </summary>
    internal static class DashboardFieldPredicate
    {
        public static bool IsEmpty(DashboardFieldValue field)
        {
            return DashboardGroupValue.FromField(field).IsMissing;
        }

        public static bool IsEmpty(DashboardObjectRow row, string fieldId)
        {
            return IsEmpty(GetField(row, fieldId));
        }

        public static bool EqualsCriterion(DashboardFieldValue field, DashboardFilterValue criterion)
        {
            if (criterion == null || criterion.Value == null)
                return false;
            if (IsEmpty(field))
                return false;

            switch (criterion.Kind)
            {
                case DashboardFieldType.Text:
                    return TextEquals(field, criterion.Value);
                case DashboardFieldType.Integer:
                case DashboardFieldType.Number:
                    return NumericEquals(field.Value, criterion.Value);
                case DashboardFieldType.Boolean:
                    return BooleanEquals(field.Value, criterion.Value);
                case DashboardFieldType.Guid:
                    return GuidEquals(field, criterion.Value);
                case DashboardFieldType.Enum:
                case DashboardFieldType.User:
                case DashboardFieldType.Reference:
                    return IdentityEquals(field, criterion.Value);
                default:
                    return false;
            }
        }

        public static bool Matches(DashboardObjectRow row, DashboardFilterDefinition filter)
        {
            if (filter == null)
                return false;
            var field = GetField(row, filter.FieldId);
            switch (filter.Operator)
            {
                case DashboardFilterOperator.IsEmpty:
                    return IsEmpty(field);
                case DashboardFilterOperator.IsNotEmpty:
                    return !IsEmpty(field);
                case DashboardFilterOperator.Equals:
                    return EqualsCriterion(field, filter.Value);
                case DashboardFilterOperator.NotEquals:
                    return !EqualsCriterion(field, filter.Value);
                default:
                    return false;
            }
        }

        public static bool AreKindsCompatible(DashboardFieldType fieldKind, DashboardFieldType criterionKind)
        {
            if (fieldKind == criterionKind)
                return true;
            if ((fieldKind == DashboardFieldType.Integer || fieldKind == DashboardFieldType.Number)
                && (criterionKind == DashboardFieldType.Integer || criterionKind == DashboardFieldType.Number))
                return true;
            return false;
        }

        private static DashboardFieldValue GetField(DashboardObjectRow row, string fieldId)
        {
            if (row == null || row.Fields == null || string.IsNullOrEmpty(fieldId))
                return null;
            DashboardFieldValue field;
            if (!row.Fields.TryGetValue(fieldId, out field))
                return null;
            return field;
        }

        private static bool TextEquals(DashboardFieldValue field, object criterion)
        {
            var left = field.Value as string;
            if (left == null)
                left = Convert.ToString(field.Value, CultureInfo.InvariantCulture);
            var right = criterion as string;
            if (right == null)
                right = Convert.ToString(criterion, CultureInfo.InvariantCulture);
            if (left == null || right == null)
                return false;
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static bool BooleanEquals(object left, object right)
        {
            if (!(left is bool) || !(right is bool))
                return false;
            return (bool)left == (bool)right;
        }

        private static bool GuidEquals(DashboardFieldValue field, object criterion)
        {
            Guid left;
            Guid right;
            if (!TryGuid(field.Value, out left) && !TryGuid(field.StableKey, out left))
                return false;
            if (!TryGuid(criterion, out right))
                return false;
            return left == right;
        }

        private static bool IdentityEquals(DashboardFieldValue field, object criterion)
        {
            var left = DashboardGroupValue.FromField(field);
            if (left.IsMissing)
                return false;
            string right;
            if (criterion is Guid)
                right = ((Guid)criterion).ToString("D");
            else if (TryInt64(criterion))
                right = ToInt64(criterion).ToString(CultureInfo.InvariantCulture);
            else
                right = Convert.ToString(criterion, CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(right))
                return false;
            return string.Equals(left.Key, right, StringComparison.Ordinal);
        }

        internal static bool NumericEquals(object left, object right)
        {
            decimal da;
            decimal db;
            if (TryDecimal(left, out da) && TryDecimal(right, out db))
                return da == db;
            double xa;
            double xb;
            if (DashboardGroupValueTryDouble(left, out xa) && DashboardGroupValueTryDouble(right, out xb))
                return xa == xb;
            return false;
        }

        private static bool TryDecimal(object value, out decimal d)
        {
            d = 0;
            if (value == null)
                return false;
            if (value is decimal)
            {
                d = (decimal)value;
                return true;
            }
            if (value is int)
            {
                d = (int)value;
                return true;
            }
            if (value is long)
            {
                d = (long)value;
                return true;
            }
            if (value is short)
            {
                d = (short)value;
                return true;
            }
            if (value is byte)
            {
                d = (byte)value;
                return true;
            }
            try
            {
                if (value is double)
                {
                    var x = (double)value;
                    if (double.IsNaN(x) || double.IsInfinity(x))
                        return false;
                    d = Convert.ToDecimal(x);
                    return true;
                }
                if (value is float)
                {
                    var x = (float)value;
                    if (float.IsNaN(x) || float.IsInfinity(x))
                        return false;
                    d = Convert.ToDecimal(x);
                    return true;
                }
            }
            catch (OverflowException)
            {
                return false;
            }
            return false;
        }

        private static bool TryGuid(object value, out Guid guid)
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

        private static bool TryInt64(object value)
        {
            return value is int || value is long || value is short;
        }

        private static long ToInt64(object value)
        {
            if (value is int)
                return (int)value;
            if (value is long)
                return (long)value;
            return (short)value;
        }

        private static bool DashboardGroupValueTryDouble(object value, out double d)
        {
            d = 0;
            if (value is double)
            {
                d = (double)value;
                return !double.IsNaN(d) && !double.IsInfinity(d);
            }
            if (value is float)
            {
                d = (float)value;
                return !double.IsNaN(d) && !double.IsInfinity(d);
            }
            decimal dec;
            if (TryDecimal(value, out dec))
            {
                d = (double)dec;
                return true;
            }
            return false;
        }
    }
}
