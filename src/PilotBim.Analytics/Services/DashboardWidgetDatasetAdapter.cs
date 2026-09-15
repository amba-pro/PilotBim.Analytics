using System;
using System.Collections.Generic;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Properties;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Converts <see cref="WidgetDataset"/> + visualization into WPF chart/KPI/table models.
    /// Auto is resolved at runtime and never written back.
    /// </summary>
    internal static class DashboardWidgetDatasetAdapter
    {
        public const int PieComfortableMaxSlices = 8;

        public static string ResolveVisualization(string requested, bool hasDimension, int rowCount)
        {
            var type = string.IsNullOrWhiteSpace(requested) ? "Auto" : requested.Trim();
            if (!string.Equals(type, "Auto", StringComparison.Ordinal))
                return type;
            return DashboardVisualizationRecommendationService.Recommend(hasDimension, rowCount);
        }

        public static DashboardQueryRenderModel TryRender(
            WidgetDataset dataset,
            string visualization,
            bool hasDimension)
        {
            var rows = dataset != null && dataset.Rows != null
                ? dataset.Rows
                : (IReadOnlyList<WidgetDataRow>)new WidgetDataRow[0];
            var requested = string.IsNullOrWhiteSpace(visualization) ? "Auto" : visualization.Trim();

            if (string.Equals(requested, "Line", StringComparison.Ordinal)
                || (!string.Equals(requested, "Auto", StringComparison.Ordinal)
                    && !DashboardVisualizationCompatibility.IsAllowed(requested, hasDimension)))
                return UnsupportedOrInvalid(requested);

            var resolved = ResolveVisualization(requested, hasDimension, rows.Count);

            if (string.Equals(resolved, "Kpi", StringComparison.Ordinal))
                return RenderKpi(rows, resolved, hasDimension);

            if (string.Equals(resolved, "Table", StringComparison.Ordinal))
                return RenderTable(rows, resolved, hasDimension);

            if (string.Equals(resolved, "Bar", StringComparison.Ordinal)
                || string.Equals(resolved, "HorizontalBar", StringComparison.Ordinal)
                || string.Equals(resolved, "Pie", StringComparison.Ordinal))
                return RenderChart(rows, resolved);

            return Invalid(resolved, Resources.QueryWidget_Invalid);
        }

        private static DashboardQueryRenderModel UnsupportedOrInvalid(string requested)
        {
            if (string.Equals(requested, "Line", StringComparison.Ordinal))
                return Unsupported("Line", Resources.QueryWidget_Unsupported);
            return Invalid(requested, Resources.QueryWidget_Invalid);
        }

        private static DashboardQueryRenderModel RenderKpi(
            IReadOnlyList<WidgetDataRow> rows,
            string resolved,
            bool hasDimension)
        {
            if (hasDimension || rows == null || rows.Count != 1)
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
                        Value = DashboardVisualizationFormat.Count(row.Value),
                        Detail = string.Empty
                    }
                }
            };
        }

        private static DashboardQueryRenderModel RenderTable(
            IReadOnlyList<WidgetDataRow> rows,
            string resolved,
            bool hasDimension)
        {
            var table = new List<DashboardQueryTableRow>();
            if (rows != null)
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var category = hasDimension
                        ? DashboardVisualizationFormat.DisplayLabel(row.Label)
                        : Resources.QueryTable_Indicator;
                    table.Add(new DashboardQueryTableRow(category, DashboardVisualizationFormat.Count(row.Value)));
                }
            }

            return new DashboardQueryRenderModel
            {
                Status = DashboardQueryWidgetRuntimeStatus.Success,
                ResolvedVisualization = resolved,
                ShowTable = true,
                TableIsScalar = !hasDimension,
                TableRows = table
            };
        }

        private static DashboardQueryRenderModel RenderChart(IReadOnlyList<WidgetDataRow> rows, string resolved)
        {
            AnalyticsChartKind kind;
            if (string.Equals(resolved, "Pie", StringComparison.Ordinal))
                kind = AnalyticsChartKind.Pie;
            else if (string.Equals(resolved, "HorizontalBar", StringComparison.Ordinal))
                kind = AnalyticsChartKind.HorizontalBar;
            else
                kind = AnalyticsChartKind.VerticalBar;

            string warning = null;
            if (kind == AnalyticsChartKind.Pie && rows != null && rows.Count > PieComfortableMaxSlices)
                warning = Resources.QueryViz_PieTooMany;

            var charts = new ChartDataService();
            return new DashboardQueryRenderModel
            {
                Status = DashboardQueryWidgetRuntimeStatus.Success,
                ResolvedVisualization = resolved,
                ShowChart = true,
                ChartKind = kind,
                Points = charts.FromWidgetRows(rows),
                Warning = warning
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
    }
}
