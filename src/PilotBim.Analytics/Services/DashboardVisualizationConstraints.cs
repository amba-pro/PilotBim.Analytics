using System;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Visualization-aware grid minimums. Not persisted. Grid engine stays generic.
    /// Auto uses query shape, not current row count.
    /// </summary>
    internal static class DashboardVisualizationConstraints
    {
        public const int KpiMinWidth = 3;
        public const int KpiMinHeight = 2;
        public const int ChartMinWidth = 4;
        public const int ChartMinHeight = 3;

        public struct Size
        {
            public Size(int minWidth, int minHeight)
            {
                MinWidth = minWidth;
                MinHeight = minHeight;
            }

            public int MinWidth { get; private set; }
            public int MinHeight { get; private set; }
        }

        public static Size ForWidget(DashboardWidgetDefinition widget)
        {
            if (widget == null)
                return new Size(DashboardGridLayoutEngine.MinWidth, DashboardGridLayoutEngine.MinHeight);

            if (widget.ContentKind == DashboardPersistenceV2.ContentLegacy)
            {
                var kind = widget.Legacy != null ? widget.Legacy.WidgetKind : null;
                if (kind == DashboardWidgetKinds.Chart)
                    return new Size(ChartMinWidth, ChartMinHeight);
                return new Size(KpiMinWidth, KpiMinHeight);
            }

            var scalar = widget.Query == null || string.IsNullOrWhiteSpace(widget.Query.DimensionFieldId);
            var type = widget.Visualization != null ? widget.Visualization.Type : "Auto";
            return ForQuery(type, scalar);
        }

        public static Size ForQuery(string visualizationType, bool scalar)
        {
            var type = string.IsNullOrWhiteSpace(visualizationType) ? "Auto" : visualizationType.Trim();
            if (string.Equals(type, "Auto", StringComparison.Ordinal))
                return scalar
                    ? new Size(KpiMinWidth, KpiMinHeight)
                    : new Size(ChartMinWidth, ChartMinHeight);
            if (string.Equals(type, "Kpi", StringComparison.Ordinal))
                return new Size(KpiMinWidth, KpiMinHeight);
            return new Size(ChartMinWidth, ChartMinHeight);
        }

        public static void ExpandToMin(DashboardDefinition definition, DashboardWidgetDefinition widget)
        {
            if (definition == null || widget == null || widget.Layout == null)
                return;
            var limits = ForWidget(widget);
            var rect = DashboardGridLayoutEngine.FromLayout(widget.Layout);
            if (rect.Width >= limits.MinWidth && rect.Height >= limits.MinHeight)
                return;
            var next = DashboardGridLayoutEngine.Clamp(
                new DashboardGridRect(
                    rect.X,
                    rect.Y,
                    Math.Max(rect.Width, limits.MinWidth),
                    Math.Max(rect.Height, limits.MinHeight)),
                limits.MinWidth,
                limits.MinHeight);
            DashboardGridLayoutEngine.ApplyRect(widget.Layout, next);
            DashboardGridLayoutEngine.ResolveCollisions(definition, widget.Id);
        }
    }
}
