using System.Globalization;
using PilotBim.Analytics.Properties;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Display-only formatting. Does not change query identity or dataset rows.
    /// </summary>
    internal static class DashboardVisualizationFormat
    {
        private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("ru-RU");

        public static string Count(long value)
        {
            return value.ToString("N0", DisplayCulture);
        }

        public static string DisplayLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return Resources.QueryViz_NotSet;
            return label;
        }
    }
}
