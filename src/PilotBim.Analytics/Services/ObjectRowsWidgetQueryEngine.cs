using System;
using System.Collections.Generic;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Executes <see cref="DashboardWidgetQuery"/> against a complete TypeId object dataset.
    /// Stateless. Zero Pilot SDK calls. Does not materialize or mutate rows.
    /// </summary>
    internal sealed class ObjectRowsWidgetQueryEngine
    {
        public WidgetQueryResult Execute(
            DashboardTypeDataset dataset,
            DashboardFieldCatalog catalog,
            DashboardWidgetQuery query)
        {
            if (query == null)
                return WidgetQueryResult.Invalid("query is required");
            if (catalog == null)
                return WidgetQueryResult.Invalid("field catalog is required");
            if (dataset == null)
                return WidgetQueryResult.Invalid("dataset is required");

            if (query.Limit.HasValue && query.Limit.Value <= 0)
                return WidgetQueryResult.Invalid("limit must be null or a positive integer");

            if (query.Scope != DashboardQueryScopeKind.CurrentProject)
                return WidgetQueryResult.Unsupported("scope is not executable from object rows");

            if (query.Measure != DashboardQueryMeasure.Count)
                return WidgetQueryResult.Unsupported("measure is not executable from object rows");

            if (!query.EntityTypeId.HasValue)
                return WidgetQueryResult.Invalid("entity type id is required");

            if (query.EntityTypeId.Value != dataset.TypeId)
                return WidgetQueryResult.Invalid("entity type id does not match dataset type id");

            if (dataset.Coverage != DashboardTypeCoverage.Complete)
            {
                return WidgetQueryResult.IncompleteData(
                    "dataset coverage is " + dataset.Coverage
                    + " Expected=" + dataset.ExpectedCount
                    + " Loaded=" + dataset.LoadedUniqueCount);
            }

            var rows = dataset.Rows ?? new DashboardObjectRow[0];
            if (dataset.LoadedUniqueCount != rows.Count || dataset.ExpectedCount != dataset.LoadedUniqueCount)
                return WidgetQueryResult.Invalid("complete coverage metadata is inconsistent with rows");

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.TypeId != dataset.TypeId)
                    return WidgetQueryResult.Invalid("dataset contains a row that does not match type id");
            }

            var filterError = ValidateFilters(catalog, dataset, query);
            if (filterError != null)
                return filterError;

            var quality = CheckSkippedFields(dataset, query);
            if (quality != null)
                return quality;

            var matched = ApplyFilters(rows, query);

            if (string.IsNullOrWhiteSpace(query.DimensionFieldId))
                return ScalarCount(matched, query);

            DashboardFieldDescriptor descriptor;
            if (!catalog.TryGet(query.DimensionFieldId, out descriptor) || descriptor == null)
                return WidgetQueryResult.Unsupported("unknown dimension: " + query.DimensionFieldId);

            if (!descriptor.Capabilities.CanGroup)
                return WidgetQueryResult.Unsupported("dimension cannot be grouped: " + descriptor.Id);

            if (descriptor.SourceKind == DashboardFieldSourceKind.Attribute
                && (!descriptor.ObjectTypeId.HasValue || descriptor.ObjectTypeId.Value != dataset.TypeId))
            {
                return WidgetQueryResult.Unsupported("dimension is not valid for entity type " + dataset.TypeId);
            }

            if (!IsExecutableFromObjectRows(descriptor))
                return WidgetQueryResult.Unsupported("dimension is not executable from object rows: " + descriptor.Id);

            return GroupBy(matched, descriptor.Id, query);
        }

        private static bool IsExecutableFromObjectRows(DashboardFieldDescriptor descriptor)
        {
            if (descriptor.FieldType == DashboardFieldType.DateTime)
                return false;
            if (descriptor.FieldType == DashboardFieldType.Unknown)
                return false;
            if (descriptor.Id == DashboardFieldIds.SystemCreatedMonth)
                return false;
            return true;
        }

        private static WidgetQueryResult ValidateFilters(
            DashboardFieldCatalog catalog,
            DashboardTypeDataset dataset,
            DashboardWidgetQuery query)
        {
            if (!query.HasFilters)
                return null;

            for (var i = 0; i < query.Filters.Count; i++)
            {
                var filter = query.Filters[i];
                if (filter == null || string.IsNullOrWhiteSpace(filter.FieldId))
                    return WidgetQueryResult.Invalid("filter field id is required");

                DashboardFieldDescriptor descriptor;
                if (!catalog.TryGet(filter.FieldId, out descriptor) || descriptor == null)
                    return WidgetQueryResult.Unsupported("unknown filter field: " + filter.FieldId);

                if (!descriptor.Capabilities.CanFilter)
                    return WidgetQueryResult.Unsupported("field cannot be filtered: " + descriptor.Id);

                if (descriptor.SourceKind == DashboardFieldSourceKind.Attribute
                    && (!descriptor.ObjectTypeId.HasValue || descriptor.ObjectTypeId.Value != dataset.TypeId))
                {
                    return WidgetQueryResult.Unsupported("filter field is not valid for entity type " + dataset.TypeId);
                }

                if (!IsExecutableFromObjectRows(descriptor))
                    return WidgetQueryResult.Unsupported("filter field is not executable from object rows: " + descriptor.Id);

                if (filter.Operator != DashboardFilterOperator.Equals
                    && filter.Operator != DashboardFilterOperator.NotEquals
                    && filter.Operator != DashboardFilterOperator.IsEmpty
                    && filter.Operator != DashboardFilterOperator.IsNotEmpty)
                {
                    return WidgetQueryResult.Unsupported("unsupported filter operator");
                }

                if (filter.Operator == DashboardFilterOperator.Equals
                    || filter.Operator == DashboardFilterOperator.NotEquals)
                {
                    if (filter.Value == null || filter.Value.Value == null)
                        return WidgetQueryResult.Invalid("filter value is required");
                    if (!DashboardFieldPredicate.AreKindsCompatible(descriptor.FieldType, filter.Value.Kind))
                        return WidgetQueryResult.Invalid("filter value type does not match field");
                }
            }

            return null;
        }

        private static WidgetQueryResult CheckSkippedFields(DashboardTypeDataset dataset, DashboardWidgetQuery query)
        {
            var skipped = dataset.SkippedUnsupportedFieldIds;
            if (skipped == null || skipped.Count == 0)
                return null;

            var touched = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            if (query.HasFilters)
            {
                for (var i = 0; i < query.Filters.Count; i++)
                {
                    if (query.Filters[i] != null && !string.IsNullOrEmpty(query.Filters[i].FieldId))
                        touched.Add(query.Filters[i].FieldId);
                }
            }
            if (!string.IsNullOrWhiteSpace(query.DimensionFieldId))
                touched.Add(query.DimensionFieldId);

            if (touched.Count == 0)
                return null;

            for (var i = 0; i < skipped.Count; i++)
            {
                if (skipped[i] != null && touched.Contains(skipped[i]))
                {
                    return WidgetQueryResult.IncompleteData(
                        "field has skipped unsupported values: " + skipped[i]);
                }
            }

            return null;
        }

        private static IReadOnlyList<DashboardObjectRow> ApplyFilters(
            IReadOnlyList<DashboardObjectRow> rows,
            DashboardWidgetQuery query)
        {
            if (!query.HasFilters)
                return rows;

            var matched = new List<DashboardObjectRow>();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var pass = true;
                for (var f = 0; f < query.Filters.Count; f++)
                {
                    if (!DashboardFieldPredicate.Matches(row, query.Filters[f]))
                    {
                        pass = false;
                        break;
                    }
                }
                if (pass)
                    matched.Add(row);
            }

            return matched;
        }

        private static WidgetQueryResult ScalarCount(
            IReadOnlyList<DashboardObjectRow> rows,
            DashboardWidgetQuery query)
        {
            var mapped = new[]
            {
                new WidgetDataRow(string.Empty, string.Empty, rows.Count)
            };
            var shaped = WidgetQueryPresentation.SortAndLimit(mapped, query.Sort, query.Limit);
            return WidgetQueryResult.Success(shaped);
        }

        private static WidgetQueryResult GroupBy(
            IReadOnlyList<DashboardObjectRow> rows,
            string fieldId,
            DashboardWidgetQuery query)
        {
            var groups = new Dictionary<string, GroupAcc>(System.StringComparer.Ordinal);
            for (var i = 0; i < rows.Count; i++)
            {
                var group = Resolve(rows[i], fieldId);
                GroupAcc acc;
                if (!groups.TryGetValue(group.Key, out acc))
                {
                    acc = new GroupAcc { Label = group.Label ?? string.Empty, Count = 0 };
                    groups.Add(group.Key, acc);
                }
                acc.Count++;
                if (string.IsNullOrEmpty(acc.Label) && !string.IsNullOrEmpty(group.Label))
                    acc.Label = group.Label;
            }

            if (groups.Count == 0)
                return WidgetQueryResult.Empty("no rows for query");

            var mapped = new List<WidgetDataRow>(groups.Count);
            foreach (var pair in groups)
                mapped.Add(new WidgetDataRow(pair.Key, pair.Value.Label, pair.Value.Count));

            var shaped = WidgetQueryPresentation.SortAndLimit(mapped, query.Sort, query.Limit);
            return WidgetQueryResult.Success(shaped);
        }

        private static DashboardGroupValue Resolve(DashboardObjectRow row, string fieldId)
        {
            if (row == null || row.Fields == null)
                return DashboardGroupValue.Missing;

            DashboardFieldValue field;
            if (!row.Fields.TryGetValue(fieldId, out field) || field == null)
                return DashboardGroupValue.Missing;

            return DashboardGroupValue.FromField(field);
        }

        private sealed class GroupAcc
        {
            public string Label;
            public long Count;
        }
    }
}
