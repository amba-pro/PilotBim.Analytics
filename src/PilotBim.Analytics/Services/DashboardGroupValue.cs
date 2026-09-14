using System;
using System.Globalization;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Machine group identity + display label derived from a typed field value.
    /// Display text never controls identity when a stable key exists.
    /// </summary>
    internal sealed class DashboardGroupValue
    {
        /// <summary>Reserved missing bucket. Not a localized placeholder. Cannot match prefixed typed keys.</summary>
        public const string MissingKey = "\u0001missing";

        private DashboardGroupValue(string key, string label)
        {
            Key = key ?? string.Empty;
            Label = label ?? string.Empty;
        }

        public string Key { get; private set; }
        public string Label { get; private set; }
        public bool IsMissing
        {
            get { return Key == MissingKey; }
        }

        public static DashboardGroupValue Missing
        {
            get { return new DashboardGroupValue(MissingKey, string.Empty); }
        }

        public static DashboardGroupValue FromField(DashboardFieldValue field)
        {
            if (field == null)
                return Missing;

            switch (field.Kind)
            {
                case DashboardFieldType.Text:
                    return FromText(field.Value);
                case DashboardFieldType.Integer:
                    return FromInteger(field);
                case DashboardFieldType.Number:
                    return FromNumber(field);
                case DashboardFieldType.Boolean:
                    return FromBoolean(field);
                case DashboardFieldType.Guid:
                    return FromGuid(field);
                case DashboardFieldType.Enum:
                    return FromStableOrText(field);
                case DashboardFieldType.User:
                case DashboardFieldType.Reference:
                    return FromStableOrText(field);
                default:
                    return Missing;
            }
        }

        private static DashboardGroupValue FromText(object value)
        {
            var text = value as string;
            if (text == null)
                text = value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text))
                return Missing;
            return new DashboardGroupValue(text, text);
        }

        private static DashboardGroupValue FromInteger(DashboardFieldValue field)
        {
            long n;
            if (!TryInt64(field.Value, out n))
                return Missing;
            var key = n.ToString(CultureInfo.InvariantCulture);
            return new DashboardGroupValue(key, FirstLabel(field.DisplayText, key));
        }

        private static DashboardGroupValue FromNumber(DashboardFieldValue field)
        {
            double d;
            if (!TryDouble(field.Value, out d) || double.IsNaN(d) || double.IsInfinity(d))
                return Missing;
            var key = d.ToString("R", CultureInfo.InvariantCulture);
            return new DashboardGroupValue(key, FirstLabel(field.DisplayText, key));
        }

        private static DashboardGroupValue FromBoolean(DashboardFieldValue field)
        {
            if (!(field.Value is bool))
                return Missing;
            var key = (bool)field.Value ? "1" : "0";
            return new DashboardGroupValue(key, FirstLabel(field.DisplayText, key));
        }

        private static DashboardGroupValue FromGuid(DashboardFieldValue field)
        {
            Guid g;
            if (field.Value is Guid)
                g = (Guid)field.Value;
            else if (!Guid.TryParse(Convert.ToString(field.Value, CultureInfo.InvariantCulture), out g) || g == Guid.Empty)
                return Missing;
            if (g == Guid.Empty)
                return Missing;
            var key = g.ToString("D");
            return new DashboardGroupValue(key, FirstLabel(field.DisplayText, key));
        }

        private static DashboardGroupValue FromStableOrText(DashboardFieldValue field)
        {
            if (!string.IsNullOrEmpty(field.StableKey))
            {
                if (string.IsNullOrWhiteSpace(field.StableKey))
                    return Missing;
                return new DashboardGroupValue(field.StableKey, FirstLabel(field.DisplayText, field.StableKey));
            }

            if (field.Value == null)
                return Missing;

            if (field.Value is Guid)
            {
                var g = (Guid)field.Value;
                if (g == Guid.Empty)
                    return Missing;
                var guidKey = g.ToString("D");
                return new DashboardGroupValue(guidKey, FirstLabel(field.DisplayText, guidKey));
            }

            int id;
            if (TryInt32(field.Value, out id))
            {
                var key = id.ToString(CultureInfo.InvariantCulture);
                return new DashboardGroupValue(key, FirstLabel(field.DisplayText, key));
            }

            var text = Convert.ToString(field.Value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text))
                return Missing;
            return new DashboardGroupValue(text, FirstLabel(field.DisplayText, text));
        }

        private static string FirstLabel(string displayText, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(displayText))
                return displayText;
            return fallback ?? string.Empty;
        }

        private static bool TryInt64(object value, out long n)
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

        private static bool TryInt32(object value, out int n)
        {
            n = 0;
            long l;
            if (!TryInt64(value, out l) || l > int.MaxValue || l < int.MinValue)
                return false;
            n = (int)l;
            return true;
        }

        private static bool TryDouble(object value, out double d)
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
            if (TryInt64(value, out n))
            {
                d = n;
                return true;
            }
            return false;
        }
    }
}
