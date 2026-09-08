using System;
using System.Globalization;

namespace PilotBim.Analytics.Diagnostics
{
    /// <summary>
    /// Machine/data formats that must not depend on UI culture.
    /// Display strings in ViewModels remain culture-sensitive by design.
    /// </summary>
    internal static class AnalyticsFormats
    {
        public static string MonthBucketKey(DateTime value)
        {
            return value.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        public static string FileTimestamp(DateTime value)
        {
            return value.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        }

        public static string InvariantOneDecimal(double value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
