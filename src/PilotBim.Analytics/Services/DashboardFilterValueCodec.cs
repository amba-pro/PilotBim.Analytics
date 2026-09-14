using System;
using System.Globalization;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Deterministic, culture-invariant encoding of <see cref="DashboardFilterValue"/>.
    /// Does not persist DisplayText. Does not use boxed JSON object.
    /// </summary>
    internal static class DashboardFilterValueCodec
    {
        public static bool TryEncode(DashboardFilterValue value, out string kind, out string encoded, out string error)
        {
            kind = null;
            encoded = null;
            error = null;
            if (value == null)
            {
                error = "filter value is required";
                return false;
            }

            kind = value.Kind.ToString();
            switch (value.Kind)
            {
                case DashboardFieldType.Text:
                    encoded = value.Value as string;
                    if (encoded == null)
                        encoded = Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                    return true;
                case DashboardFieldType.Integer:
                    long n;
                    if (!TryInt64(value.Value, out n))
                    {
                        error = "integer filter value is not an integer";
                        return false;
                    }
                    encoded = n.ToString(CultureInfo.InvariantCulture);
                    return true;
                case DashboardFieldType.Number:
                    double d;
                    if (!TryDouble(value.Value, out d) || double.IsNaN(d) || double.IsInfinity(d))
                    {
                        error = "number filter value is not a finite number";
                        return false;
                    }
                    encoded = d.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                case DashboardFieldType.Boolean:
                    if (!(value.Value is bool))
                    {
                        error = "boolean filter value is not a boolean";
                        return false;
                    }
                    encoded = (bool)value.Value ? "true" : "false";
                    return true;
                case DashboardFieldType.Guid:
                    Guid g;
                    if (!TryGuid(value.Value, out g))
                    {
                        error = "guid filter value is not a guid";
                        return false;
                    }
                    encoded = g.ToString("D");
                    return true;
                case DashboardFieldType.Enum:
                case DashboardFieldType.User:
                case DashboardFieldType.Reference:
                    encoded = EncodeStableKey(value.Value);
                    if (string.IsNullOrEmpty(encoded))
                    {
                        error = "identity filter value is empty";
                        return false;
                    }
                    return true;
                case DashboardFieldType.DateTime:
                    DateTime dt;
                    if (!TryDateTime(value.Value, out dt))
                    {
                        error = "datetime filter value is not a datetime";
                        return false;
                    }
                    encoded = dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                    return true;
                default:
                    error = "unsupported filter value kind";
                    return false;
            }
        }

        public static bool TryDecode(string kind, string encoded, out DashboardFilterValue value, out string error)
        {
            value = null;
            error = null;
            DashboardFieldType fieldType;
            if (string.IsNullOrWhiteSpace(kind) || !Enum.TryParse(kind, true, out fieldType)
                || !string.Equals(fieldType.ToString(), kind, StringComparison.Ordinal))
            {
                error = "unknown filter value kind";
                return false;
            }

            switch (fieldType)
            {
                case DashboardFieldType.Text:
                    value = DashboardFilterValue.Text(encoded ?? string.Empty);
                    return true;
                case DashboardFieldType.Integer:
                    long n;
                    if (!long.TryParse(encoded, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                    {
                        error = "integer filter value is not invariant integer text";
                        return false;
                    }
                    value = DashboardFilterValue.Integer(n);
                    return true;
                case DashboardFieldType.Number:
                    double d;
                    if (!double.TryParse(encoded, NumberStyles.Float, CultureInfo.InvariantCulture, out d)
                        || double.IsNaN(d) || double.IsInfinity(d))
                    {
                        error = "number filter value is not invariant number text";
                        return false;
                    }
                    value = DashboardFilterValue.Number(d);
                    return true;
                case DashboardFieldType.Boolean:
                    if (encoded == "true")
                    {
                        value = DashboardFilterValue.Boolean(true);
                        return true;
                    }
                    if (encoded == "false")
                    {
                        value = DashboardFilterValue.Boolean(false);
                        return true;
                    }
                    error = "boolean filter value must be true or false";
                    return false;
                case DashboardFieldType.Guid:
                    Guid g;
                    if (!Guid.TryParse(encoded, out g))
                    {
                        error = "guid filter value is not a guid";
                        return false;
                    }
                    value = DashboardFilterValue.Guid(g);
                    return true;
                case DashboardFieldType.Enum:
                case DashboardFieldType.User:
                case DashboardFieldType.Reference:
                    if (string.IsNullOrEmpty(encoded))
                    {
                        error = "identity filter value is empty";
                        return false;
                    }
                    value = DashboardFilterValue.Identity(fieldType, encoded);
                    return true;
                case DashboardFieldType.DateTime:
                    DateTime dt;
                    if (!DateTime.TryParse(encoded, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt))
                    {
                        error = "datetime filter value is not round-trip text";
                        return false;
                    }
                    value = new DashboardFilterValue(DashboardFieldType.DateTime, dt);
                    return true;
                default:
                    error = "unsupported filter value kind";
                    return false;
            }
        }

        public static bool AreSemanticallyEqual(DashboardFilterValue left, DashboardFilterValue right)
        {
            if (left == null && right == null)
                return true;
            if (left == null || right == null)
                return false;
            string k1, v1, e1, k2, v2, e2;
            if (!TryEncode(left, out k1, out v1, out e1) || !TryEncode(right, out k2, out v2, out e2))
                return false;
            return string.Equals(k1, k2, StringComparison.Ordinal)
                && string.Equals(v1, v2, StringComparison.Ordinal);
        }

        private static string EncodeStableKey(object value)
        {
            if (value == null)
                return null;
            if (value is Guid)
                return ((Guid)value).ToString("D");
            long n;
            if (TryInt64(value, out n))
                return n.ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static bool TryInt64(object value, out long n)
        {
            n = 0;
            if (value == null)
                return false;
            if (value is long)
            {
                n = (long)value;
                return true;
            }
            if (value is int)
            {
                n = (int)value;
                return true;
            }
            if (value is short)
            {
                n = (short)value;
                return true;
            }
            if (value is byte)
            {
                n = (byte)value;
                return true;
            }
            return long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
        }

        private static bool TryDouble(object value, out double d)
        {
            d = 0;
            if (value == null)
                return false;
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
            if (TryInt64(value, out n))
            {
                d = n;
                return true;
            }
            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out d);
        }

        private static bool TryGuid(object value, out Guid g)
        {
            g = Guid.Empty;
            if (value == null)
                return false;
            if (value is Guid)
            {
                g = (Guid)value;
                return true;
            }
            return Guid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out g);
        }

        private static bool TryDateTime(object value, out DateTime dt)
        {
            dt = default(DateTime);
            if (value == null)
                return false;
            if (value is DateTime)
            {
                dt = (DateTime)value;
                return true;
            }
            return DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt);
        }
    }
}
