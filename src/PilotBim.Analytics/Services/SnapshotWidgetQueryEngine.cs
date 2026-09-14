using System;
using System.Collections.Generic;
using System.Globalization;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Executes <see cref="DashboardWidgetQuery"/> against existing snapshot aggregates.
    /// Stateless. Does not mutate the snapshot. Zero Pilot SDK calls.
    /// </summary>
    internal sealed class SnapshotWidgetQueryEngine
    {
        public WidgetQueryResult Execute(ProjectAnalyticsSnapshot snapshot, DashboardWidgetQuery query)
        {
            if (query == null)
                return WidgetQueryResult.Invalid("query is required");

            if (query.Limit.HasValue && query.Limit.Value <= 0)
                return WidgetQueryResult.Invalid("limit must be null or a positive integer");

            if (query.Scope != DashboardQueryScopeKind.CurrentProject)
                return WidgetQueryResult.Unsupported("scope is not executable from snapshot");

            if (query.Measure != DashboardQueryMeasure.Count)
                return WidgetQueryResult.Unsupported("measure is not executable from snapshot");

            if (query.EntityTypeId.HasValue)
                return WidgetQueryResult.Unsupported("entity type id is not executable from snapshot");

            IReadOnlyList<WidgetDataRow> mapped;
            var dimension = query.DimensionFieldId;
            if (string.IsNullOrWhiteSpace(dimension))
            {
                mapped = MapScalarCount(snapshot);
            }
            else if (!SnapshotDimensionRegistry.TryMap(dimension, snapshot, out mapped))
            {
                return WidgetQueryResult.Unsupported("dimension is not executable from snapshot: " + dimension);
            }

            if (mapped == null || mapped.Count == 0)
                return WidgetQueryResult.Empty("no rows for query");

            var sorted = WidgetQueryPresentation.SortAndLimit(mapped, query.Sort, query.Limit);
            return WidgetQueryResult.Success(sorted);
        }

        private static IReadOnlyList<WidgetDataRow> MapScalarCount(ProjectAnalyticsSnapshot snapshot)
        {
            if (snapshot == null || snapshot.ObjectsByType == null || snapshot.ObjectsByType.Count == 0)
                return new WidgetDataRow[0];

            long total = 0;
            foreach (var row in snapshot.ObjectsByType)
            {
                if (row != null)
                    total += row.Count;
            }

            return new[] { new WidgetDataRow(string.Empty, string.Empty, total) };
        }
    }

    /// <summary>
    /// Single map: semantic field id → snapshot aggregate. Unsupported ids return false.
    /// </summary>
    internal static class SnapshotDimensionRegistry
    {
        public static bool IsSupported(string fieldId)
        {
            IReadOnlyList<WidgetDataRow> unused;
            return TryMap(fieldId, new ProjectAnalyticsSnapshot(), out unused);
        }

        public static bool TryMap(
            string fieldId,
            ProjectAnalyticsSnapshot snapshot,
            out IReadOnlyList<WidgetDataRow> rows)
        {
            rows = null;
            if (string.IsNullOrEmpty(fieldId))
                return false;

            if (fieldId == DashboardFieldIds.SystemTypeId)
            {
                rows = MapTypes(snapshot);
                return true;
            }
            if (fieldId == DashboardFieldIds.SystemCreatorId)
            {
                rows = MapCreators(snapshot);
                return true;
            }
            if (fieldId == DashboardFieldIds.SystemCreatedMonth)
            {
                rows = MapCreatedMonth(snapshot);
                return true;
            }
            if (fieldId == DashboardFieldIds.SystemUserState)
            {
                rows = MapUserStates(snapshot);
                return true;
            }
            if (fieldId == DashboardFieldIds.SystemResponsible)
            {
                rows = MapResponsible(snapshot);
                return true;
            }

            return false;
        }

        private static IReadOnlyList<WidgetDataRow> MapTypes(ProjectAnalyticsSnapshot snapshot)
        {
            var source = snapshot == null ? null : snapshot.ObjectsByType;
            if (source == null || source.Count == 0)
                return new WidgetDataRow[0];

            var list = new List<WidgetDataRow>(source.Count);
            foreach (var row in source)
            {
                if (row == null)
                    continue;
                list.Add(new WidgetDataRow(
                    row.TypeId.ToString(CultureInfo.InvariantCulture),
                    NormalizeLabel(row.TypeName),
                    row.Count));
            }
            return list;
        }

        private static IReadOnlyList<WidgetDataRow> MapCreators(ProjectAnalyticsSnapshot snapshot)
        {
            var source = snapshot == null ? null : snapshot.ObjectsByCreator;
            if (source == null || source.Count == 0)
                return new WidgetDataRow[0];

            var list = new List<WidgetDataRow>(source.Count);
            foreach (var row in source)
            {
                if (row == null)
                    continue;
                list.Add(new WidgetDataRow(
                    row.CreatorId.ToString(CultureInfo.InvariantCulture),
                    NormalizeLabel(row.DisplayName),
                    row.SampledCount));
            }
            return list;
        }

        private static IReadOnlyList<WidgetDataRow> MapCreatedMonth(ProjectAnalyticsSnapshot snapshot)
        {
            var source = snapshot == null ? null : snapshot.ObjectsByCreatedMonth;
            if (source == null || source.Count == 0)
                return new WidgetDataRow[0];

            var list = new List<WidgetDataRow>(source.Count);
            foreach (var row in source)
            {
                if (row == null)
                    continue;
                var key = NormalizeLabel(row.Period);
                list.Add(new WidgetDataRow(key, key, row.Count));
            }
            return list;
        }

        private static IReadOnlyList<WidgetDataRow> MapUserStates(ProjectAnalyticsSnapshot snapshot)
        {
            var source = snapshot == null ? null : snapshot.ObjectsByUserState;
            if (source == null || source.Count == 0)
                return new WidgetDataRow[0];

            var list = new List<WidgetDataRow>(source.Count);
            foreach (var row in source)
            {
                if (row == null)
                    continue;
                list.Add(new WidgetDataRow(
                    row.StateId.ToString("D"),
                    NormalizeLabel(row.StateTitle),
                    row.Count));
            }
            return list;
        }

        private static IReadOnlyList<WidgetDataRow> MapResponsible(ProjectAnalyticsSnapshot snapshot)
        {
            var source = snapshot == null ? null : snapshot.ObjectsByResponsible;
            if (source == null || source.Count == 0)
                return new WidgetDataRow[0];

            var list = new List<WidgetDataRow>(source.Count);
            foreach (var row in source)
            {
                if (row == null)
                    continue;
                list.Add(new WidgetDataRow(
                    row.OrgUnitId.ToString(CultureInfo.InvariantCulture),
                    NormalizeLabel(row.DisplayName),
                    row.SampledCount));
            }
            return list;
        }

        private static string NormalizeLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
    }
}
