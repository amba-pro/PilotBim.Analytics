using System;
using System.Collections.Generic;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    internal static class DashboardLevelFilterBindings
    {
        public static void UnbindWidget(DashboardDefinition definition, string widgetId)
        {
            if (definition == null || definition.DashboardFilters == null || string.IsNullOrWhiteSpace(widgetId))
                return;
            for (var i = 0; i < definition.DashboardFilters.Count; i++)
            {
                var filter = definition.DashboardFilters[i];
                if (filter == null || filter.TargetWidgetIds == null)
                    continue;
                filter.TargetWidgetIds.RemoveAll(id => string.Equals(id, widgetId, StringComparison.Ordinal));
            }
        }

        public static void UnbindIncompatible(DashboardDefinition definition, DashboardWidgetDefinition widget)
        {
            if (definition == null || definition.DashboardFilters == null || widget == null)
                return;
            for (var i = 0; i < definition.DashboardFilters.Count; i++)
            {
                var filter = definition.DashboardFilters[i];
                if (filter == null || !DashboardFilterCompatibility.Targets(filter, widget.Id))
                    continue;
                if (DashboardFilterCompatibility.IsBindingStructurallyValid(filter, widget))
                    continue;
                if (filter.TargetWidgetIds != null)
                    filter.TargetWidgetIds.RemoveAll(id => string.Equals(id, widget.Id, StringComparison.Ordinal));
            }
        }

        public static IList<string> TargetIds(DashboardLevelFilterDefinition filter)
        {
            if (filter == null || filter.TargetWidgetIds == null)
                return new string[0];
            return filter.TargetWidgetIds;
        }

        public static HashSet<string> UnionTargets(DashboardLevelFilterDefinition left, DashboardLevelFilterDefinition right)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            Add(set, left);
            Add(set, right);
            return set;
        }

        private static void Add(HashSet<string> set, DashboardLevelFilterDefinition filter)
        {
            if (filter == null || filter.TargetWidgetIds == null)
                return;
            for (var i = 0; i < filter.TargetWidgetIds.Count; i++)
            {
                var id = filter.TargetWidgetIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                    set.Add(id);
            }
        }
    }
}
