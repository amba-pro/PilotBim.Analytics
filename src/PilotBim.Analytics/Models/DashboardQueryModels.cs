using System;
using System.Collections.Generic;

namespace PilotBim.Analytics.Models
{
    /// <summary>
    /// Query scope. DB-2/DB-4 execute CurrentProject only.
    /// </summary>
    internal enum DashboardQueryScopeKind
    {
        CurrentProject = 0
    }

    /// <summary>
    /// Aggregation. DB-2/DB-4 execute Count only.
    /// </summary>
    internal enum DashboardQueryMeasure
    {
        Count = 0
    }

    /// <summary>
    /// Deterministic sort of Category|Value rows.
    /// </summary>
    internal enum DashboardQuerySort
    {
        ValueDescending = 0,
        ValueAscending = 1,
        LabelAscending = 2,
        LabelDescending = 3
    }

    internal enum WidgetQueryStatus
    {
        Success = 0,
        Empty = 1,
        UnsupportedQuery = 2,
        InvalidQuery = 3,
        /// <summary>
        /// Query is valid and executable, but the ObjectRows dataset is not Complete.
        /// Never used as a stand-in for Empty. Contains no analytical numbers.
        /// Snapshot executor does not emit this status.
        /// </summary>
        IncompleteData = 4
    }

    /// <summary>
    /// Generic widget query. Independent of ChartSource. Snapshot and ObjectRows
    /// executors consume the same shape.
    /// </summary>
    internal sealed class DashboardWidgetQuery
    {
        public DashboardWidgetQuery(
            DashboardQueryScopeKind scope,
            string dimensionFieldId,
            DashboardQueryMeasure measure,
            DashboardQuerySort sort,
            int? limit)
            : this(scope, dimensionFieldId, measure, sort, limit, null)
        {
        }

        public DashboardWidgetQuery(
            DashboardQueryScopeKind scope,
            string dimensionFieldId,
            DashboardQueryMeasure measure,
            DashboardQuerySort sort,
            int? limit,
            int? entityTypeId)
        {
            Scope = scope;
            DimensionFieldId = dimensionFieldId;
            Measure = measure;
            Sort = sort;
            Limit = limit;
            EntityTypeId = entityTypeId;
        }

        public DashboardQueryScopeKind Scope { get; private set; }

        /// <summary>Stable field id, or null/empty for a scalar Count.</summary>
        public string DimensionFieldId { get; private set; }

        public DashboardQueryMeasure Measure { get; private set; }
        public DashboardQuerySort Sort { get; private set; }

        /// <summary>Null = unlimited. Non-positive is INVALID_QUERY at execution.</summary>
        public int? Limit { get; private set; }

        /// <summary>
        /// Pilot type id. Snapshot: null = project-wide; non-null = UnsupportedQuery.
        /// ObjectRows: required and must equal the dataset TypeId.
        /// </summary>
        public int? EntityTypeId { get; private set; }
    }

    /// <summary>
    /// Renderer-neutral category/value row. Key is machine identity; Label is display.
    /// </summary>
    internal sealed class WidgetDataRow
    {
        public WidgetDataRow(string key, string label, long value)
        {
            Key = key ?? string.Empty;
            Label = label ?? string.Empty;
            Value = value;
        }

        public string Key { get; private set; }
        public string Label { get; private set; }
        public long Value { get; private set; }
    }

    internal sealed class WidgetDataset
    {
        private static readonly IReadOnlyList<WidgetDataRow> NoRows = new WidgetDataRow[0];

        public WidgetDataset(IReadOnlyList<WidgetDataRow> rows)
        {
            Rows = rows ?? NoRows;
        }

        public IReadOnlyList<WidgetDataRow> Rows { get; private set; }
    }

    internal sealed class WidgetQueryResult
    {
        public WidgetQueryResult(WidgetQueryStatus status, WidgetDataset dataset, string reason)
        {
            Status = status;
            Dataset = dataset ?? new WidgetDataset(null);
            Reason = reason;
        }

        public WidgetQueryStatus Status { get; private set; }
        public WidgetDataset Dataset { get; private set; }
        public string Reason { get; private set; }

        public static WidgetQueryResult Success(IReadOnlyList<WidgetDataRow> rows)
        {
            return new WidgetQueryResult(WidgetQueryStatus.Success, new WidgetDataset(rows), null);
        }

        public static WidgetQueryResult Empty(string reason)
        {
            return new WidgetQueryResult(WidgetQueryStatus.Empty, new WidgetDataset(null), reason);
        }

        public static WidgetQueryResult Unsupported(string reason)
        {
            return new WidgetQueryResult(WidgetQueryStatus.UnsupportedQuery, new WidgetDataset(null), reason);
        }

        public static WidgetQueryResult Invalid(string reason)
        {
            return new WidgetQueryResult(WidgetQueryStatus.InvalidQuery, new WidgetDataset(null), reason);
        }

        /// <summary>
        /// Valid query, supported executor, but dataset Coverage is not Complete.
        /// Dataset rows are empty — callers must not treat this as Count=0.
        /// </summary>
        public static WidgetQueryResult IncompleteData(string reason)
        {
            return new WidgetQueryResult(WidgetQueryStatus.IncompleteData, new WidgetDataset(null), reason);
        }
    }
}
