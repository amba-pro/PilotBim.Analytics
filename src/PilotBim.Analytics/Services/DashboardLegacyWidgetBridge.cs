using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Maps V1 <see cref="DashboardWidgetState"/> to V2 Legacy content and back.
    /// Does not convert specialized widgets into Query widgets.
    /// </summary>
    internal static class DashboardLegacyWidgetBridge
    {
        public static DashboardWidgetState ToState(DashboardWidgetDefinition widget)
        {
            if (widget == null || widget.Legacy == null)
                return null;

            var layout = widget.Layout ?? new DashboardWidgetLayoutDefinition();
            return new DashboardWidgetState
            {
                Id = widget.Id,
                Title = widget.Title,
                WidgetKind = widget.Legacy.WidgetKind,
                IsVisible = layout.IsVisible,
                Order = layout.Order,
                ColumnSpan = layout.ColumnSpan <= 1 ? 1 : 2,
                ChartSource = string.IsNullOrWhiteSpace(widget.Legacy.ChartSource) ? "Types" : widget.Legacy.ChartSource,
                ChartKind = string.IsNullOrWhiteSpace(widget.Legacy.ChartKind) ? "HorizontalBar" : widget.Legacy.ChartKind,
                TopN = widget.Legacy.TopN < 0 ? 12 : widget.Legacy.TopN
            };
        }

        public static DashboardWidgetDefinition ToDefinition(DashboardWidgetState state, int order)
        {
            if (state == null)
                return null;

            var kind = state.WidgetKind;
            if (string.IsNullOrWhiteSpace(kind))
                kind = DashboardWidgetKinds.Chart;

            var title = state.Title;
            if (string.IsNullOrWhiteSpace(title))
                title = kind == DashboardWidgetKinds.Chart ? "График" : kind;

            var span = state.ColumnSpan <= 1 ? 1 : 2;
            if (kind != DashboardWidgetKinds.Chart)
                span = 2;

            return new DashboardWidgetDefinition
            {
                Id = state.Id,
                Title = title.Trim(),
                ContentKind = DashboardPersistenceV2.ContentLegacy,
                Layout = new DashboardWidgetLayoutDefinition
                {
                    Order = order,
                    ColumnSpan = span,
                    IsVisible = state.IsVisible
                },
                Legacy = new DashboardLegacyWidgetContent
                {
                    WidgetKind = kind,
                    ChartSource = string.IsNullOrWhiteSpace(state.ChartSource) ? "Types" : state.ChartSource,
                    ChartKind = string.IsNullOrWhiteSpace(state.ChartKind) ? "HorizontalBar" : state.ChartKind,
                    TopN = state.TopN < 0 ? 12 : state.TopN
                }
            };
        }
    }
}
