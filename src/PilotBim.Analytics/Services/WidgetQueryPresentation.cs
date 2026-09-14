using System;
using System.Collections.Generic;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Shared sort + limit for widget category rows. Exact DB-2 semantics.
    /// </summary>
    internal static class WidgetQueryPresentation
    {
        public static IReadOnlyList<WidgetDataRow> SortAndLimit(
            IReadOnlyList<WidgetDataRow> rows,
            DashboardQuerySort sort,
            int? limit)
        {
            var sorted = SortRows(rows, sort);
            if (limit.HasValue && limit.Value < sorted.Count)
            {
                var limited = new List<WidgetDataRow>(limit.Value);
                for (var i = 0; i < limit.Value; i++)
                    limited.Add(sorted[i]);
                return limited;
            }
            return sorted;
        }

        private static IReadOnlyList<WidgetDataRow> SortRows(
            IReadOnlyList<WidgetDataRow> rows,
            DashboardQuerySort sort)
        {
            var copy = new List<WidgetDataRow>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
                copy.Add(rows[i]);

            copy.Sort((a, b) => Compare(a, b, sort));
            return copy;
        }

        private static int Compare(WidgetDataRow a, WidgetDataRow b, DashboardQuerySort sort)
        {
            int primary;
            switch (sort)
            {
                case DashboardQuerySort.ValueAscending:
                    primary = a.Value.CompareTo(b.Value);
                    break;
                case DashboardQuerySort.LabelAscending:
                    primary = string.Compare(a.Label, b.Label, StringComparison.Ordinal);
                    break;
                case DashboardQuerySort.LabelDescending:
                    primary = string.Compare(b.Label, a.Label, StringComparison.Ordinal);
                    break;
                default:
                    primary = b.Value.CompareTo(a.Value);
                    break;
            }

            if (primary != 0)
                return primary;

            var byKey = string.Compare(a.Key, b.Key, StringComparison.Ordinal);
            if (byKey != 0)
                return byKey;
            return string.Compare(a.Label, b.Label, StringComparison.Ordinal);
        }
    }
}
