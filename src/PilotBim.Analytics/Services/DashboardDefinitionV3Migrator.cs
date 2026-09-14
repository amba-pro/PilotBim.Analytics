using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Deterministic V2 Order/ColumnSpan → V3 X/Y/Width/Height. Pure. Does not write files.
    /// Query and Legacy content are copied unchanged.
    /// </summary>
    internal static class DashboardDefinitionV3Migrator
    {
        public static DashboardDefinition FromV2(DashboardDefinition source)
        {
            var clone = DashboardDefinitionCopy.Clone(source) ?? new DashboardDefinition
            {
                Widgets = new List<DashboardWidgetDefinition>()
            };

            clone.SchemaVersion = DashboardPersistenceV2.CurrentSchemaVersion;
            if (clone.Widgets == null)
                clone.Widgets = new List<DashboardWidgetDefinition>();

            var ordered = clone.Widgets
                .Where(w => w != null)
                .OrderBy(w => w.Layout != null ? w.Layout.Order : 0)
                .ThenBy(w => w.Id ?? string.Empty)
                .ToList();

            var cursorX = 0;
            var cursorY = 0;
            var rowHeight = 0;
            var maxBottom = 0;

            foreach (var widget in ordered)
            {
                if (widget.Layout == null)
                    widget.Layout = new DashboardWidgetLayoutDefinition { IsVisible = true };
                if (!widget.Layout.IsVisible)
                    continue;

                int width;
                int height;
                SizeFor(widget, out width, out height);

                if (cursorX + width > DashboardGridLayoutEngine.Columns)
                {
                    cursorX = 0;
                    cursorY += rowHeight;
                    rowHeight = height;
                }

                if (height > rowHeight)
                    rowHeight = height;

                DashboardGridLayoutEngine.ApplyRect(
                    widget.Layout,
                    new DashboardGridRect(cursorX, cursorY, width, height));
                cursorX += width;
                if (widget.Layout.Y + widget.Layout.Height > maxBottom)
                    maxBottom = widget.Layout.Y + widget.Layout.Height;
            }

            var hiddenY = maxBottom;
            foreach (var widget in ordered)
            {
                if (widget.Layout == null || widget.Layout.IsVisible)
                    continue;
                int width;
                int height;
                SizeFor(widget, out width, out height);
                DashboardGridLayoutEngine.ApplyRect(
                    widget.Layout,
                    new DashboardGridRect(0, hiddenY, width, height));
                hiddenY += height;
            }

            clone.Widgets = ordered;
            return clone;
        }

        private static void SizeFor(DashboardWidgetDefinition widget, out int width, out int height)
        {
            width = widget.Layout != null && widget.Layout.ColumnSpan <= 1
                ? DashboardGridLayoutEngine.DefaultWidth
                : DashboardGridLayoutEngine.Columns;
            height = IsCompact(widget)
                ? DashboardGridLayoutEngine.CompactHeight
                : DashboardGridLayoutEngine.DefaultHeight;
        }

        public static DashboardDefinition FromLegacy(DashboardLayoutState state, System.Guid projectKey)
        {
            var v2 = DashboardDefinitionV2Migrator.FromLegacy(state, projectKey);
            return FromV2(v2);
        }

        private static bool IsCompact(DashboardWidgetDefinition widget)
        {
            if (widget.ContentKind == DashboardPersistenceV2.ContentLegacy && widget.Legacy != null)
            {
                var kind = widget.Legacy.WidgetKind;
                return kind == DashboardWidgetKinds.Kpi
                    || kind == DashboardWidgetKinds.Bim
                    || kind == DashboardWidgetKinds.Responsible;
            }
            if (widget.ContentKind == DashboardPersistenceV2.ContentQuery)
            {
                var scalar = widget.Query == null || string.IsNullOrWhiteSpace(widget.Query.DimensionFieldId);
                return scalar;
            }
            return false;
        }
    }
}
