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

            if (string.IsNullOrWhiteSpace(query.DimensionFieldId))
                return ScalarCount(rows, query);

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

            return GroupBy(rows, descriptor.Id, query);
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
