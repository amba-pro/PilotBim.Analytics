using System.Collections.Generic;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Builds an effective widget query: base filters first, then applicable dashboard overlays.
    /// Never mutates the persisted base query.
    /// </summary>
    internal static class DashboardEffectiveQueryBuilder
    {
        public static DashboardWidgetQuery Build(
            DashboardWidgetQuery baseQuery,
            IList<DashboardLevelFilterDefinition> dashboardFilters,
            DashboardWidgetDefinition widget,
            DashboardFieldCatalog catalog)
        {
            if (baseQuery == null)
                return null;

            var extras = Collect(dashboardFilters, widget, catalog);
            if (extras.Count == 0)
                return baseQuery;

            var merged = new List<DashboardFilterDefinition>();
            if (baseQuery.Filters != null)
            {
                for (var i = 0; i < baseQuery.Filters.Count; i++)
                    merged.Add(baseQuery.Filters[i]);
            }
            merged.AddRange(extras);

            return new DashboardWidgetQuery(
                baseQuery.Scope,
                baseQuery.DimensionFieldId,
                baseQuery.Measure,
                baseQuery.Sort,
                baseQuery.Limit,
                baseQuery.EntityTypeId,
                merged);
        }

        public static IReadOnlyList<DashboardFilterDefinition> Collect(
            IList<DashboardLevelFilterDefinition> dashboardFilters,
            DashboardWidgetDefinition widget,
            DashboardFieldCatalog catalog)
        {
            var extras = new List<DashboardFilterDefinition>();
            if (dashboardFilters == null || widget == null)
                return extras;

            for (var i = 0; i < dashboardFilters.Count; i++)
            {
                var filter = dashboardFilters[i];
                if (filter == null || filter.Disabled)
                    continue;
                if (!DashboardFilterCompatibility.Targets(filter, widget.Id))
                    continue;
                if (DashboardFilterCompatibility.Evaluate(filter, widget, catalog)
                    != DashboardFilterCompatibilityStatus.Compatible)
                    continue;

                DashboardFilterDefinition runtime;
                string error;
                if (!DashboardFilterCompatibility.TryToRuntimeFilter(filter, out runtime, out error) || runtime == null)
                    continue;
                extras.Add(runtime);
            }

            return extras;
        }
    }
}
