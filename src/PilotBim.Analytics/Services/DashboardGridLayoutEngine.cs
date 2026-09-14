using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Pure 12-column layout engine. No WPF, SDK, filesystem, or query execution.
    /// Collision: the active widget keeps its requested valid rect; others are pushed down.
    /// No automatic vertical compaction.
    /// </summary>
    internal static class DashboardGridLayoutEngine
    {
        public const int Columns = DashboardPersistenceV2.GridColumns;
        public const int MinWidth = 3;
        public const int MinHeight = 2;
        public const int MaxWidth = Columns;
        public const int MaxHeight = 24;
        public const int DefaultWidth = 6;
        public const int DefaultHeight = 3;
        public const int CompactWidth = 6;
        public const int CompactHeight = 2;

        public static DashboardGridRect FromLayout(DashboardWidgetLayoutDefinition layout)
        {
            if (layout == null)
                return new DashboardGridRect(0, 0, DefaultWidth, DefaultHeight);
            return new DashboardGridRect(layout.X, layout.Y, layout.Width, layout.Height);
        }

        public static void ApplyRect(DashboardWidgetLayoutDefinition layout, DashboardGridRect rect)
        {
            if (layout == null)
                return;
            layout.X = rect.X;
            layout.Y = rect.Y;
            layout.Width = rect.Width;
            layout.Height = rect.Height;
            layout.Order = rect.Y * Columns + rect.X;
            layout.ColumnSpan = rect.Width <= 6 ? 1 : 2;
        }

        public static bool TryValidate(DashboardGridRect rect, out string error)
        {
            error = null;
            if (rect.X < 0)
            {
                error = "X must be >= 0";
                return false;
            }
            if (rect.X >= Columns)
            {
                error = "X must be < " + Columns;
                return false;
            }
            if (rect.Y < 0)
            {
                error = "Y must be >= 0";
                return false;
            }
            if (rect.Width < 1)
            {
                error = "Width must be >= 1";
                return false;
            }
            if (rect.Height < 1)
            {
                error = "Height must be >= 1";
                return false;
            }
            if (rect.Width > Columns)
            {
                error = "Width must be <= " + Columns;
                return false;
            }
            if (rect.X + rect.Width > Columns)
            {
                error = "X + Width must be <= " + Columns;
                return false;
            }
            return true;
        }

        public static DashboardGridRect Clamp(DashboardGridRect rect)
        {
            var width = rect.Width;
            if (width < MinWidth)
                width = MinWidth;
            if (width > MaxWidth)
                width = MaxWidth;

            var height = rect.Height;
            if (height < MinHeight)
                height = MinHeight;
            if (height > MaxHeight)
                height = MaxHeight;

            var x = rect.X;
            if (x < 0)
                x = 0;
            if (x > Columns - width)
                x = Columns - width;

            var y = rect.Y;
            if (y < 0)
                y = 0;

            return new DashboardGridRect(x, y, width, height);
        }

        /// <summary>Inclusive edge-touch is not overlap.</summary>
        public static bool Overlaps(DashboardGridRect a, DashboardGridRect b)
        {
            return a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
        }

        public static bool AnyVisibleOverlap(IEnumerable<DashboardWidgetDefinition> widgets)
        {
            var visible = Visible(widgets).ToList();
            for (var i = 0; i < visible.Count; i++)
            {
                var a = FromLayout(visible[i].Layout);
                for (var j = i + 1; j < visible.Count; j++)
                {
                    if (Overlaps(a, FromLayout(visible[j].Layout)))
                        return true;
                }
            }
            return false;
        }

        public static DashboardGridRect FirstFit(
            IEnumerable<DashboardWidgetDefinition> widgets,
            int width,
            int height)
        {
            var size = Clamp(new DashboardGridRect(0, 0, width, height));
            var occupied = VisibleRects(widgets).ToList();
            for (var y = 0; y < 512; y++)
            {
                for (var x = 0; x <= Columns - size.Width; x++)
                {
                    var candidate = new DashboardGridRect(x, y, size.Width, size.Height);
                    if (!occupied.Any(o => Overlaps(candidate, o)))
                        return candidate;
                }
            }

            var maxBottom = occupied.Count == 0 ? 0 : occupied.Max(r => r.Bottom);
            return new DashboardGridRect(0, maxBottom, size.Width, size.Height);
        }

        public static DashboardGridRect DefaultSize(DashboardWidgetDefinition widget)
        {
            if (IsCompact(widget))
                return new DashboardGridRect(0, 0, CompactWidth, CompactHeight);
            return new DashboardGridRect(0, 0, DefaultWidth, DefaultHeight);
        }

        public static void PlaceNew(DashboardDefinition definition, DashboardWidgetDefinition widget)
        {
            if (definition == null || widget == null)
                return;
            if (widget.Layout == null)
                widget.Layout = new DashboardWidgetLayoutDefinition { IsVisible = true };
            widget.Layout.IsVisible = true;
            var size = DefaultSize(widget);
            if (widget.Layout.Width >= MinWidth && widget.Layout.Height >= MinHeight)
                size = new DashboardGridRect(0, 0, widget.Layout.Width, widget.Layout.Height);
            var others = definition.Widgets == null
                ? Enumerable.Empty<DashboardWidgetDefinition>()
                : definition.Widgets.Where(w => w != null && w != widget && w.Id != widget.Id);
            var placed = FirstFit(others, size.Width, size.Height);
            ApplyRect(widget.Layout, placed);
        }

        public static void Move(DashboardDefinition definition, string id, DashboardGridRect requested)
        {
            ApplyActive(definition, id, requested, keepSize: true);
        }

        public static void Resize(DashboardDefinition definition, string id, DashboardGridRect requested)
        {
            ApplyActive(definition, id, requested, keepSize: false);
        }

        public static void Unhide(DashboardDefinition definition, string id)
        {
            if (definition == null)
                return;
            var widget = Find(definition, id);
            if (widget == null || widget.Layout == null)
                return;
            widget.Layout.IsVisible = true;
            var rect = Clamp(FromLayout(widget.Layout));
            if (!TryValidate(rect, out _))
                rect = FirstFit(definition.Widgets.Where(w => w != null && w.Id != id), rect.Width, rect.Height);
            ApplyRect(widget.Layout, rect);
            ResolveCollisions(definition, id);
        }

        private static void ApplyActive(
            DashboardDefinition definition,
            string id,
            DashboardGridRect requested,
            bool keepSize)
        {
            if (definition == null)
                return;
            var widget = Find(definition, id);
            if (widget == null || widget.Layout == null)
                return;

            var current = FromLayout(widget.Layout);
            var next = requested;
            if (keepSize)
                next = new DashboardGridRect(requested.X, requested.Y, current.Width, current.Height);
            next = Clamp(next);
            ApplyRect(widget.Layout, next);
            ResolveCollisions(definition, id);
        }

        /// <summary>
        /// Active widget stays. Other visible widgets are processed in (Y, X, Id) order
        /// and pushed down to the first non-overlapping Y, keeping X/Width/Height.
        /// </summary>
        public static void ResolveCollisions(DashboardDefinition definition, string activeId)
        {
            if (definition == null || definition.Widgets == null)
                return;

            var active = Find(definition, activeId);
            var committed = new List<DashboardWidgetDefinition>();
            if (active != null && active.Layout != null && active.Layout.IsVisible)
                committed.Add(active);

            var others = definition.Widgets
                .Where(w => w != null && w.Id != activeId && w.Layout != null && w.Layout.IsVisible)
                .OrderBy(w => w.Layout.Y)
                .ThenBy(w => w.Layout.X)
                .ThenBy(w => w.Id, StringComparer.Ordinal)
                .ToList();

            foreach (var other in others)
            {
                var x = other.Layout.X;
                var width = other.Layout.Width;
                var height = other.Layout.Height;
                var y = other.Layout.Y;
                if (y < 0)
                    y = 0;
                while (OverlapsCommitted(x, y, width, height, committed))
                    y++;
                ApplyRect(other.Layout, new DashboardGridRect(x, y, width, height));
                committed.Add(other);
            }
        }

        private static bool OverlapsCommitted(
            int x,
            int y,
            int width,
            int height,
            List<DashboardWidgetDefinition> committed)
        {
            var rect = new DashboardGridRect(x, y, width, height);
            for (var i = 0; i < committed.Count; i++)
            {
                if (Overlaps(rect, FromLayout(committed[i].Layout)))
                    return true;
            }
            return false;
        }

        private static IEnumerable<DashboardWidgetDefinition> Visible(IEnumerable<DashboardWidgetDefinition> widgets)
        {
            if (widgets == null)
                yield break;
            foreach (var widget in widgets)
            {
                if (widget != null && widget.Layout != null && widget.Layout.IsVisible)
                    yield return widget;
            }
        }

        private static IEnumerable<DashboardGridRect> VisibleRects(IEnumerable<DashboardWidgetDefinition> widgets)
        {
            foreach (var widget in Visible(widgets))
                yield return FromLayout(widget.Layout);
        }

        private static DashboardWidgetDefinition Find(DashboardDefinition definition, string id)
        {
            if (definition == null || definition.Widgets == null || string.IsNullOrWhiteSpace(id))
                return null;
            return definition.Widgets.FirstOrDefault(w => w != null && w.Id == id);
        }

        private static bool IsCompact(DashboardWidgetDefinition widget)
        {
            if (widget == null)
                return false;
            if (widget.ContentKind == DashboardPersistenceV2.ContentLegacy && widget.Legacy != null)
            {
                var kind = widget.Legacy.WidgetKind;
                return kind == DashboardWidgetKinds.Kpi
                    || kind == DashboardWidgetKinds.Bim
                    || kind == DashboardWidgetKinds.Responsible;
            }
            if (widget.ContentKind == DashboardPersistenceV2.ContentQuery)
            {
                var viz = widget.Visualization != null ? widget.Visualization.Type : "Auto";
                var scalar = widget.Query == null || string.IsNullOrWhiteSpace(widget.Query.DimensionFieldId);
                return scalar || string.Equals(viz, "Kpi", StringComparison.Ordinal);
            }
            return false;
        }
    }
}
