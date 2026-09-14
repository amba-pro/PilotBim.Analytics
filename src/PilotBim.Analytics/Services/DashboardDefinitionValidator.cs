using System;
using System.Collections.Generic;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Structural integrity for V2 dashboard documents. Not query-engine semantics.
    /// Malformed documents are rejected as a whole.
    /// </summary>
    internal static class DashboardDefinitionValidator
    {
        private static readonly HashSet<string> VisualizationTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "Auto",
            "Kpi",
            "Bar",
            "HorizontalBar",
            "Pie",
            "Line",
            "Table"
        };

        public static bool TryValidate(DashboardDefinition definition, out string error)
        {
            error = null;
            if (definition == null)
            {
                error = "dashboard definition is required";
                return false;
            }

            if (definition.SchemaVersion != DashboardPersistenceV2.SchemaVersion
                && definition.SchemaVersion != DashboardPersistenceV2.CurrentSchemaVersion)
            {
                error = "schema version is not " + DashboardPersistenceV2.SchemaVersion
                    + " or " + DashboardPersistenceV2.CurrentSchemaVersion;
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                error = "dashboard id is required";
                return false;
            }

            Guid projectKey;
            if (!DashboardProjectKey.TryParse(definition.ProjectKey, out projectKey))
            {
                error = "project key is not a database guid";
                return false;
            }

            var widgets = definition.Widgets ?? new List<DashboardWidgetDefinition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < widgets.Count; i++)
            {
                var widget = widgets[i];
                if (widget == null)
                {
                    error = "widget is required";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(widget.Id))
                {
                    error = "widget id is required";
                    return false;
                }
                if (!seen.Add(widget.Id))
                {
                    error = "duplicate widget id: " + widget.Id;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(widget.Title))
                {
                    error = "widget title is required: " + widget.Id;
                    return false;
                }
                if (!ValidateLayout(widget.Layout, widget.Id, definition.SchemaVersion, out error))
                    return false;
                if (!ValidateContent(widget, out error))
                    return false;
            }

            if (definition.SchemaVersion == DashboardPersistenceV2.CurrentSchemaVersion
                && DashboardGridLayoutEngine.AnyVisibleOverlap(widgets))
            {
                error = "visible widgets overlap";
                return false;
            }

            return true;
        }

        private static bool ValidateLayout(
            DashboardWidgetLayoutDefinition layout,
            string widgetId,
            int schemaVersion,
            out string error)
        {
            error = null;
            if (layout == null)
            {
                error = "widget layout is required: " + widgetId;
                return false;
            }
            if (schemaVersion == DashboardPersistenceV2.CurrentSchemaVersion)
            {
                string gridError;
                if (!DashboardGridLayoutEngine.TryValidate(DashboardGridLayoutEngine.FromLayout(layout), out gridError))
                {
                    error = "widget grid layout is invalid: " + widgetId + " " + gridError;
                    return false;
                }
                return true;
            }
            if (layout.Order < 0)
            {
                error = "widget order is invalid: " + widgetId;
                return false;
            }
            if (layout.ColumnSpan != 1 && layout.ColumnSpan != 2)
            {
                error = "widget column span must be 1 or 2: " + widgetId;
                return false;
            }
            return true;
        }

        private static bool ValidateContent(DashboardWidgetDefinition widget, out string error)
        {
            error = null;
            var kind = widget.ContentKind;
            var isLegacy = string.Equals(kind, DashboardPersistenceV2.ContentLegacy, StringComparison.Ordinal);
            var isQuery = string.Equals(kind, DashboardPersistenceV2.ContentQuery, StringComparison.Ordinal);
            if (!isLegacy && !isQuery)
            {
                error = "widget content kind is invalid: " + widget.Id;
                return false;
            }

            if (isLegacy)
            {
                if (widget.Legacy == null || widget.Query != null || widget.Visualization != null)
                {
                    error = "legacy widget must contain only Legacy content: " + widget.Id;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(widget.Legacy.WidgetKind))
                {
                    error = "legacy widget kind is required: " + widget.Id;
                    return false;
                }
                if (widget.Legacy.TopN < 0)
                {
                    error = "legacy TopN is invalid: " + widget.Id;
                    return false;
                }
                return true;
            }

            if (widget.Query == null || widget.Visualization == null || widget.Legacy != null)
            {
                error = "query widget must contain Query and Visualization only: " + widget.Id;
                return false;
            }
            if (!ValidateQuery(widget.Query, widget.Id, out error))
                return false;
            if (string.IsNullOrWhiteSpace(widget.Visualization.Type) || !VisualizationTypes.Contains(widget.Visualization.Type))
            {
                error = "visualization type is invalid: " + widget.Id;
                return false;
            }
            return true;
        }

        private static bool ValidateQuery(DashboardWidgetQueryDocument query, string widgetId, out string error)
        {
            DashboardWidgetQuery unused;
            if (!DashboardQueryPersistence.TryToQuery(query, out unused, out error))
            {
                error = (error ?? "query is invalid") + ": " + widgetId;
                return false;
            }
            if (query.Limit.HasValue && query.Limit.Value <= 0)
            {
                error = "query limit must be null or a positive integer: " + widgetId;
                return false;
            }
            return true;
        }
    }
}
