using System.Collections.Generic;
using System.Globalization;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Properties;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Converts <see cref="WidgetDataset"/> + visualization into existing WPF chart/KPI/table models.
    /// TEMPORARY_V1_AUTO_RULE: scalar → KPI; grouped row count ≥ 6 → HorizontalBar; else Bar. Never auto-Pie.
    /// </summary>
    internal static class DashboardWidgetDatasetAdapter
    {
        public const int AutoHorizontalBarMinRows = 6;

        public static string ResolveVisualization(string requested, bool hasDimension, int rowCount)
        {
            var type = string.IsNullOrWhiteSpace(requested) ? "Auto" : requested.Trim();
            if (!string.Equals(type, "Auto", System.StringComparison.Ordinal))
                return type;
            if (!hasDimension)
                return "Kpi";
            if (rowCount >= AutoHorizontalBarMinRows)
                return "HorizontalBar";
            return "Bar";
        }

        public static DashboardQueryRenderModel TryRender(
            WidgetDataset dataset,
            string visualization,
            bool hasDimension)
        {
            var rows = dataset != null && dataset.Rows != null
                ? dataset.Rows
                : (IReadOnlyList<WidgetDataRow>)new WidgetDataRow[0];
            var resolved = ResolveVisualization(visualization, hasDimension, rows.Count);

            if (string.Equals(resolved, "Line", System.StringComparison.Ordinal))
                return Unsupported(resolved, Resources.QueryWidget_Unsupported);

            if (string.Equals(resolved, "Kpi", System.StringComparison.Ordinal))
                return RenderKpi(rows, resolved);

            if (string.Equals(resolved, "Table", System.StringComparison.Ordinal))
                return RenderTable(rows, resolved);

            if (string.Equals(resolved, "Bar", System.StringComparison.Ordinal)
                || string.Equals(resolved, "HorizontalBar", System.StringComparison.Ordinal)
                || string.Equals(resolved, "Pie", System.StringComparison.Ordinal))
                return RenderChart(rows, resolved);

            return Invalid(resolved, Resources.QueryWidget_Invalid);
        }

        private static DashboardQueryRenderModel RenderKpi(IReadOnlyList<WidgetDataRow> rows, string resolved)
        {
            if (rows == null || rows.Count != 1)
                return Invalid(resolved, Resources.QueryWidget_Invalid);

            var row = rows[0];
            return new DashboardQueryRenderModel
            {
                Status = DashboardQueryWidgetRuntimeStatus.Success,
                ResolvedVisualization = resolved,
                ShowKpi = true,
                KpiRows = new List<AnalyticsKpiRow>
                {
                    new AnalyticsKpiRow
                    {
                        Label = string.Empty,
                        Value = FormatValue(row.Value),
                        Detail = string.Empty
                    }
                }
            };
        }

        private static DashboardQueryRenderModel RenderTable(IReadOnlyList<WidgetDataRow> rows, string resolved)
        {
            var table = new List<DashboardQueryTableRow>();
            if (rows != null)
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    table.Add(new DashboardQueryTableRow(row.Label, FormatValue(row.Value)));
                }
            }

            return new DashboardQueryRenderModel
            {
                Status = DashboardQueryWidgetRuntimeStatus.Success,
                ResolvedVisualization = resolved,
                ShowTable = true,
                TableRows = table
            };
        }

        private static DashboardQueryRenderModel RenderChart(IReadOnlyList<WidgetDataRow> rows, string resolved)
        {
            AnalyticsChartKind kind;
            if (string.Equals(resolved, "Pie", System.StringComparison.Ordinal))
                kind = AnalyticsChartKind.Pie;
            else if (string.Equals(resolved, "HorizontalBar", System.StringComparison.Ordinal))
                kind = AnalyticsChartKind.HorizontalBar;
            else
                kind = AnalyticsChartKind.VerticalBar;

            var charts = new ChartDataService();
            return new DashboardQueryRenderModel
            {
                Status = DashboardQueryWidgetRuntimeStatus.Success,
                ResolvedVisualization = resolved,
                ShowChart = true,
                ChartKind = kind,
                Points = charts.FromWidgetRows(rows)
            };
        }

        private static DashboardQueryRenderModel Unsupported(string resolved, string message)
        {
            return new DashboardQueryRenderModel
            {
                Status = DashboardQueryWidgetRuntimeStatus.Unsupported,
                ResolvedVisualization = resolved,
                Message = message
            };
        }

        private static DashboardQueryRenderModel Invalid(string resolved, string message)
        {
            return new DashboardQueryRenderModel
            {
                Status = DashboardQueryWidgetRuntimeStatus.Invalid,
                ResolvedVisualization = resolved,
                Message = message
            };
        }

        private static string FormatValue(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
