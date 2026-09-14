using System.Collections.Generic;
using PilotBim.Analytics.Services;

namespace PilotBim.Analytics.Models
{
    /// <summary>
    /// Presenter/runtime status for a Query widget. Not persisted.
    /// </summary>
    internal enum DashboardQueryWidgetRuntimeStatus
    {
        Idle = 0,
        Loading = 1,
        Success = 2,
        Empty = 3,
        Incomplete = 4,
        Unsupported = 5,
        Invalid = 6,
        Error = 7
    }

    internal sealed class DashboardQueryTableRow
    {
        public DashboardQueryTableRow(string category, string value)
        {
            Category = category ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Category { get; private set; }
        public string Value { get; private set; }
    }

    /// <summary>
    /// Renderer-neutral output of <see cref="DashboardWidgetDatasetAdapter"/>.
    /// No SDK. No query execution.
    /// </summary>
    internal sealed class DashboardQueryRenderModel
    {
        public DashboardQueryWidgetRuntimeStatus Status { get; set; }
        public string Message { get; set; }
        public string ResolvedVisualization { get; set; }
        public bool ShowChart { get; set; }
        public bool ShowKpi { get; set; }
        public bool ShowTable { get; set; }
        public AnalyticsChartKind ChartKind { get; set; }
        public IList<ChartSeriesPoint> Points { get; set; }
        public IList<AnalyticsKpiRow> KpiRows { get; set; }
        public IList<DashboardQueryTableRow> TableRows { get; set; }
    }

    internal sealed class DashboardQueryRuntimeCache
    {
        public DashboardQueryWidgetRuntimeStatus Status { get; set; }
        public string Message { get; set; }
        public DashboardQueryRenderModel Render { get; set; }
    }
}
