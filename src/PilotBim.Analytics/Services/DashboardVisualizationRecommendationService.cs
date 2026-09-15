using PilotBim.Analytics.Properties;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Deterministic Auto visualization. No AI, no localized field-name heuristics, no SDK.
    /// </summary>
    internal static class DashboardVisualizationRecommendationService
    {
        public const int HorizontalBarMinRows = 6;
        public const int TableMinRows = 16;

        public static string Recommend(bool hasDimension, int rowCount)
        {
            if (!hasDimension)
                return "Kpi";
            if (rowCount >= TableMinRows)
                return "Table";
            if (rowCount >= HorizontalBarMinRows)
                return "HorizontalBar";
            return "Bar";
        }

        public static string DisplayName(string type)
        {
            switch (type)
            {
                case "Kpi":
                    return Resources.QueryEditor_VizKpi;
                case "Bar":
                    return Resources.QueryEditor_VizBar;
                case "HorizontalBar":
                    return Resources.QueryEditor_VizHorizontalBar;
                case "Pie":
                    return Resources.QueryEditor_VizPie;
                case "Table":
                    return Resources.QueryEditor_VizTable;
                case "Line":
                    return Resources.QueryEditor_VizLine;
                default:
                    return Resources.QueryEditor_VizAuto;
            }
        }
    }
}
