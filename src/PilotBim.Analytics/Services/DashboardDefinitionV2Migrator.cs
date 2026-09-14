using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Pure in-memory V1 layout → V2 definition. Does not write files.
    /// Does not convert specialized widgets into generic queries.
    /// </summary>
    internal static class DashboardDefinitionV2Migrator
    {
        public static DashboardDefinition FromLegacy(DashboardLayoutState state, Guid projectKey)
        {
            if (projectKey == Guid.Empty)
                throw new ArgumentException("database id is required.", "projectKey");

            var widgets = new List<DashboardWidgetDefinition>();
            if (state != null && state.Widgets != null && state.Widgets.Count > 0)
            {
                for (var i = 0; i < state.Widgets.Count; i++)
                    TryAdd(widgets, FromWidget(state.Widgets[i], i));
            }
            else if (state != null && state.Blocks != null && state.Blocks.Count > 0)
            {
                foreach (var block in state.Blocks.OrderBy(b => b == null ? int.MaxValue : b.Order))
                    TryAdd(widgets, FromBlock(block));
            }

            widgets = widgets
                .GroupBy(w => w.Id, StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderBy(w => w.Layout.Order)
                .Select((w, i) =>
                {
                    w.Layout.Order = i;
                    return w;
                })
                .ToList();

            return new DashboardDefinition
            {
                SchemaVersion = DashboardPersistenceV2.SchemaVersion,
                Id = DashboardPersistenceV2.DefaultDashboardId,
                Title = DashboardPersistenceV2.DefaultTitle,
                ProjectKey = DashboardProjectKey.ToFolderName(projectKey),
                Widgets = widgets
            };
        }

        private static void TryAdd(List<DashboardWidgetDefinition> widgets, DashboardWidgetDefinition widget)
        {
            if (widget != null)
                widgets.Add(widget);
        }

        private static DashboardWidgetDefinition FromWidget(DashboardWidgetState widget, int index)
        {
            if (widget == null)
                return null;

            var id = widget.Id;
            if (string.IsNullOrWhiteSpace(id))
                id = "legacy-" + index;
            else
                id = id.Trim();

            var kind = widget.WidgetKind;
            if (string.IsNullOrWhiteSpace(kind))
            {
                if (id == DashboardBlockIds.Kpi)
                    kind = DashboardWidgetKinds.Kpi;
                else if (id == DashboardBlockIds.Bim)
                    kind = DashboardWidgetKinds.Bim;
                else if (id == DashboardBlockIds.Responsible)
                    kind = DashboardWidgetKinds.Responsible;
                else
                    kind = DashboardWidgetKinds.Chart;
            }

            var title = widget.Title;
            if (string.IsNullOrWhiteSpace(title))
            {
                if (kind == DashboardWidgetKinds.Kpi)
                    title = "KPI / сводка";
                else if (kind == DashboardWidgetKinds.Bim)
                    title = "BIM";
                else if (kind == DashboardWidgetKinds.Responsible)
                    title = "Ответственные";
                else
                    title = "График";
            }

            var span = widget.ColumnSpan <= 1 ? 1 : 2;
            var topN = widget.TopN < 0 ? 12 : widget.TopN;

            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = title.Trim(),
                ContentKind = DashboardPersistenceV2.ContentLegacy,
                Layout = new DashboardWidgetLayoutDefinition
                {
                    Order = widget.Order,
                    ColumnSpan = span,
                    IsVisible = widget.IsVisible
                },
                Legacy = new DashboardLegacyWidgetContent
                {
                    WidgetKind = kind,
                    ChartSource = string.IsNullOrWhiteSpace(widget.ChartSource) ? "Types" : widget.ChartSource,
                    ChartKind = string.IsNullOrWhiteSpace(widget.ChartKind) ? "HorizontalBar" : widget.ChartKind,
                    TopN = topN
                }
            };
        }

        private static DashboardWidgetDefinition FromBlock(DashboardBlockState block)
        {
            if (block == null || string.IsNullOrWhiteSpace(block.Id))
                return null;

            string title;
            string kind;
            if (block.Id == DashboardBlockIds.Kpi)
            {
                title = "KPI / сводка";
                kind = DashboardWidgetKinds.Kpi;
            }
            else if (block.Id == DashboardBlockIds.Bim)
            {
                title = "BIM";
                kind = DashboardWidgetKinds.Bim;
            }
            else if (block.Id == DashboardBlockIds.Responsible)
            {
                title = "Ответственные";
                kind = DashboardWidgetKinds.Responsible;
            }
            else
            {
                return null;
            }

            return new DashboardWidgetDefinition
            {
                Id = block.Id,
                Title = title,
                ContentKind = DashboardPersistenceV2.ContentLegacy,
                Layout = new DashboardWidgetLayoutDefinition
                {
                    Order = block.Order,
                    ColumnSpan = 2,
                    IsVisible = block.IsVisible
                },
                Legacy = new DashboardLegacyWidgetContent
                {
                    WidgetKind = kind,
                    ChartSource = "Types",
                    ChartKind = "HorizontalBar",
                    TopN = 12
                }
            };
        }
    }
}
