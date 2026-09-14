using System;
using System.Collections.Generic;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Maps runtime <see cref="DashboardWidgetQuery"/> to a JSON-safe document and back.
    /// Filter values use <see cref="DashboardFilterValueCodec"/>, not boxed objects.
    /// </summary>
    internal static class DashboardQueryPersistence
    {
        public static DashboardWidgetQueryDocument ToDocument(DashboardWidgetQuery query)
        {
            if (query == null)
                return null;

            var filters = new List<DashboardFilterDocument>();
            if (query.Filters != null)
            {
                for (var i = 0; i < query.Filters.Count; i++)
                {
                    DashboardFilterDocument doc;
                    string error;
                    if (!TryToFilterDocument(query.Filters[i], out doc, out error))
                        throw new InvalidOperationException(error ?? "filter cannot be persisted");
                    filters.Add(doc);
                }
            }

            return new DashboardWidgetQueryDocument
            {
                Scope = query.Scope.ToString(),
                EntityTypeId = query.EntityTypeId,
                DimensionFieldId = string.IsNullOrWhiteSpace(query.DimensionFieldId) ? null : query.DimensionFieldId,
                Measure = query.Measure.ToString(),
                Sort = query.Sort.ToString(),
                Limit = query.Limit,
                Filters = filters
            };
        }

        public static bool TryToQuery(DashboardWidgetQueryDocument document, out DashboardWidgetQuery query, out string error)
        {
            query = null;
            error = null;
            if (document == null)
            {
                error = "query is required";
                return false;
            }

            DashboardQueryScopeKind scope;
            if (!TryParseExact(document.Scope, out scope))
            {
                error = "query scope is invalid";
                return false;
            }

            DashboardQueryMeasure measure;
            if (!TryParseExact(document.Measure, out measure))
            {
                error = "query measure is invalid";
                return false;
            }

            DashboardQuerySort sort;
            if (!TryParseExact(document.Sort, out sort))
            {
                error = "query sort is invalid";
                return false;
            }

            var filters = new List<DashboardFilterDefinition>();
            if (document.Filters != null)
            {
                for (var i = 0; i < document.Filters.Count; i++)
                {
                    DashboardFilterDefinition filter;
                    if (!TryToFilter(document.Filters[i], out filter, out error))
                        return false;
                    filters.Add(filter);
                }
            }

            var dimension = string.IsNullOrWhiteSpace(document.DimensionFieldId) ? null : document.DimensionFieldId;
            query = new DashboardWidgetQuery(
                scope,
                dimension,
                measure,
                sort,
                document.Limit,
                document.EntityTypeId,
                filters);
            return true;
        }

        public static bool AreSemanticallyEqual(DashboardWidgetQuery left, DashboardWidgetQuery right)
        {
            if (left == null && right == null)
                return true;
            if (left == null || right == null)
                return false;
            if (left.Scope != right.Scope
                || left.EntityTypeId != right.EntityTypeId
                || left.Measure != right.Measure
                || left.Sort != right.Sort
                || left.Limit != right.Limit)
                return false;

            var leftDim = string.IsNullOrWhiteSpace(left.DimensionFieldId) ? null : left.DimensionFieldId;
            var rightDim = string.IsNullOrWhiteSpace(right.DimensionFieldId) ? null : right.DimensionFieldId;
            if (!string.Equals(leftDim, rightDim, StringComparison.Ordinal))
                return false;

            var leftCount = left.Filters == null ? 0 : left.Filters.Count;
            var rightCount = right.Filters == null ? 0 : right.Filters.Count;
            if (leftCount != rightCount)
                return false;
            for (var i = 0; i < leftCount; i++)
            {
                if (!AreFiltersEqual(left.Filters[i], right.Filters[i]))
                    return false;
            }

            return true;
        }

        internal static bool TryToFilterDocument(DashboardFilterDefinition filter, out DashboardFilterDocument document, out string error)
        {
            document = null;
            error = null;
            if (filter == null)
            {
                error = "filter is required";
                return false;
            }
            if (string.IsNullOrWhiteSpace(filter.FieldId))
            {
                error = "filter field id is required";
                return false;
            }

            DashboardFilterOperator op = filter.Operator;
            if (op != DashboardFilterOperator.Equals
                && op != DashboardFilterOperator.NotEquals
                && op != DashboardFilterOperator.IsEmpty
                && op != DashboardFilterOperator.IsNotEmpty)
            {
                error = "filter operator is invalid";
                return false;
            }

            document = new DashboardFilterDocument
            {
                FieldId = filter.FieldId,
                Operator = op.ToString()
            };

            if (op == DashboardFilterOperator.IsEmpty || op == DashboardFilterOperator.IsNotEmpty)
                return true;

            string kind;
            string encoded;
            if (!DashboardFilterValueCodec.TryEncode(filter.Value, out kind, out encoded, out error))
                return false;
            document.ValueKind = kind;
            document.Value = encoded;
            return true;
        }

        internal static bool TryToFilter(DashboardFilterDocument document, out DashboardFilterDefinition filter, out string error)
        {
            filter = null;
            error = null;
            if (document == null)
            {
                error = "filter is required";
                return false;
            }
            if (string.IsNullOrWhiteSpace(document.FieldId))
            {
                error = "filter field id is required";
                return false;
            }

            DashboardFilterOperator op;
            if (!TryParseExact(document.Operator, out op))
            {
                error = "filter operator is invalid";
                return false;
            }

            if (op == DashboardFilterOperator.IsEmpty || op == DashboardFilterOperator.IsNotEmpty)
            {
                filter = new DashboardFilterDefinition(document.FieldId, op, null);
                return true;
            }

            DashboardFilterValue value;
            if (!DashboardFilterValueCodec.TryDecode(document.ValueKind, document.Value, out value, out error))
                return false;
            filter = new DashboardFilterDefinition(document.FieldId, op, value);
            return true;
        }

        private static bool AreFiltersEqual(DashboardFilterDefinition left, DashboardFilterDefinition right)
        {
            if (left == null || right == null)
                return false;
            if (!string.Equals(left.FieldId, right.FieldId, StringComparison.Ordinal))
                return false;
            if (left.Operator != right.Operator)
                return false;
            if (left.Operator == DashboardFilterOperator.IsEmpty || left.Operator == DashboardFilterOperator.IsNotEmpty)
                return true;
            return DashboardFilterValueCodec.AreSemanticallyEqual(left.Value, right.Value);
        }

        private static bool TryParseExact<TEnum>(string name, out TEnum value) where TEnum : struct
        {
            value = default(TEnum);
            if (string.IsNullOrWhiteSpace(name))
                return false;
            TEnum parsed;
            if (!Enum.TryParse(name, false, out parsed))
                return false;
            if (!string.Equals(parsed.ToString(), name, StringComparison.Ordinal))
                return false;
            if (!Enum.IsDefined(typeof(TEnum), parsed))
                return false;
            value = parsed;
            return true;
        }
    }
}
