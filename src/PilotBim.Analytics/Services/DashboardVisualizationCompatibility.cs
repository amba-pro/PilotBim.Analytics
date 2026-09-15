using System;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Query visualization compatibility for TypeId V1. Legacy charts are out of scope.
    /// </summary>
    internal static class DashboardVisualizationCompatibility
    {
        public static bool IsAllowed(string type, bool hasDimension)
        {
            if (string.IsNullOrWhiteSpace(type) || string.Equals(type, "Auto", StringComparison.Ordinal))
                return true;
            if (string.Equals(type, "Line", StringComparison.Ordinal))
                return false;
            if (!hasDimension)
                return string.Equals(type, "Kpi", StringComparison.Ordinal)
                    || string.Equals(type, "Table", StringComparison.Ordinal);
            return string.Equals(type, "Bar", StringComparison.Ordinal)
                || string.Equals(type, "HorizontalBar", StringComparison.Ordinal)
                || string.Equals(type, "Pie", StringComparison.Ordinal)
                || string.Equals(type, "Table", StringComparison.Ordinal);
        }
    }
}
