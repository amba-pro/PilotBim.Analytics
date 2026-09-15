using System;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    internal enum DashboardFilterCompatibilityStatus
    {
        Compatible = 0,
        Incompatible = 1,
        Unavailable = 2
    }

    /// <summary>
    /// Machine-identity compatibility for dashboard-level filters. No DisplayName matching. No SDK.
    /// </summary>
    internal static class DashboardFilterCompatibility
    {
        public static DashboardFilterCompatibilityStatus Evaluate(
            DashboardLevelFilterDefinition filter,
            DashboardWidgetDefinition widget,
            DashboardFieldCatalog catalog)
        {
            string unused;
            return Evaluate(filter, widget, catalog, out unused);
        }

        public static DashboardFilterCompatibilityStatus Evaluate(
            DashboardLevelFilterDefinition filter,
            DashboardWidgetDefinition widget,
            DashboardFieldCatalog catalog,
            out string reason)
        {
            reason = null;
            if (filter == null)
            {
                reason = "filter is required";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }
            if (widget == null || !string.Equals(widget.ContentKind, DashboardPersistenceV2.ContentQuery, StringComparison.Ordinal))
            {
                reason = "target is not a Query widget";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }
            if (widget.Query == null || !widget.Query.EntityTypeId.HasValue)
            {
                reason = "widget has no EntityTypeId";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }
            if (widget.Query.EntityTypeId.Value != filter.EntityTypeId)
            {
                reason = "EntityTypeId mismatch";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }
            if (string.IsNullOrWhiteSpace(filter.FieldId))
            {
                reason = "FieldId is required";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }

            if (catalog == null)
            {
                reason = "catalog is required";
                return DashboardFilterCompatibilityStatus.Unavailable;
            }

            DashboardFieldDescriptor descriptor;
            if (!catalog.TryGet(filter.FieldId, out descriptor) || descriptor == null)
            {
                reason = "unknown field: " + filter.FieldId;
                return DashboardFilterCompatibilityStatus.Unavailable;
            }

            if (descriptor.SourceKind == DashboardFieldSourceKind.Attribute
                && (!descriptor.ObjectTypeId.HasValue || descriptor.ObjectTypeId.Value != filter.EntityTypeId))
            {
                reason = "field is not applicable to TypeId " + filter.EntityTypeId;
                return DashboardFilterCompatibilityStatus.Incompatible;
            }

            if (descriptor.Capabilities == null || !descriptor.Capabilities.CanFilter)
            {
                reason = "field cannot be filtered";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }

            if (!IsObjectRowsFilterable(descriptor))
            {
                reason = "field is not executable from object rows";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }

            DashboardFilterDefinition runtime;
            string parseError;
            if (!TryToRuntimeFilter(filter, out runtime, out parseError))
            {
                reason = parseError ?? "filter value is invalid";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }

            if (runtime.Operator == DashboardFilterOperator.Equals
                || runtime.Operator == DashboardFilterOperator.NotEquals)
            {
                if (runtime.Value == null || runtime.Value.Value == null)
                {
                    reason = "filter value is required";
                    return DashboardFilterCompatibilityStatus.Incompatible;
                }
                if (!DashboardFieldPredicate.AreKindsCompatible(descriptor.FieldType, runtime.Value.Kind))
                {
                    reason = "filter value type does not match field";
                    return DashboardFilterCompatibilityStatus.Incompatible;
                }
            }

            return DashboardFilterCompatibilityStatus.Compatible;
        }

        /// <summary>
        /// Field/TypeId compatibility without requiring a completed Equals value.
        /// Used by the editor target list while the user is still composing the filter.
        /// </summary>
        public static DashboardFilterCompatibilityStatus EvaluateField(
            DashboardLevelFilterDefinition filter,
            DashboardWidgetDefinition widget,
            DashboardFieldCatalog catalog)
        {
            string unused;
            return EvaluateField(filter, widget, catalog, out unused);
        }

        public static DashboardFilterCompatibilityStatus EvaluateField(
            DashboardLevelFilterDefinition filter,
            DashboardWidgetDefinition widget,
            DashboardFieldCatalog catalog,
            out string reason)
        {
            reason = null;
            if (filter == null)
            {
                reason = "filter is required";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }
            if (!IsBindingStructurallyValid(filter, widget))
            {
                reason = "EntityTypeId mismatch or not a Query widget";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }
            if (string.IsNullOrWhiteSpace(filter.FieldId))
                return DashboardFilterCompatibilityStatus.Compatible;

            if (catalog == null)
            {
                reason = "catalog is required";
                return DashboardFilterCompatibilityStatus.Unavailable;
            }

            DashboardFieldDescriptor descriptor;
            if (!catalog.TryGet(filter.FieldId, out descriptor) || descriptor == null)
            {
                reason = "unknown field: " + filter.FieldId;
                return DashboardFilterCompatibilityStatus.Unavailable;
            }

            if (descriptor.SourceKind == DashboardFieldSourceKind.Attribute
                && (!descriptor.ObjectTypeId.HasValue || descriptor.ObjectTypeId.Value != filter.EntityTypeId))
            {
                reason = "field is not applicable to TypeId " + filter.EntityTypeId;
                return DashboardFilterCompatibilityStatus.Incompatible;
            }

            if (descriptor.Capabilities == null || !descriptor.Capabilities.CanFilter)
            {
                reason = "field cannot be filtered";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }

            if (!IsObjectRowsFilterable(descriptor))
            {
                reason = "field is not executable from object rows";
                return DashboardFilterCompatibilityStatus.Incompatible;
            }

            return DashboardFilterCompatibilityStatus.Compatible;
        }

        public static bool IsBindingStructurallyValid(
            DashboardLevelFilterDefinition filter,
            DashboardWidgetDefinition widget)
        {
            if (filter == null || widget == null)
                return false;
            if (!string.Equals(widget.ContentKind, DashboardPersistenceV2.ContentQuery, StringComparison.Ordinal))
                return false;
            if (widget.Query == null || !widget.Query.EntityTypeId.HasValue)
                return false;
            return widget.Query.EntityTypeId.Value == filter.EntityTypeId;
        }

        public static bool Targets(DashboardLevelFilterDefinition filter, string widgetId)
        {
            if (filter == null || filter.TargetWidgetIds == null || string.IsNullOrWhiteSpace(widgetId))
                return false;
            for (var i = 0; i < filter.TargetWidgetIds.Count; i++)
            {
                if (string.Equals(filter.TargetWidgetIds[i], widgetId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static bool IsObjectRowsFilterable(DashboardFieldDescriptor descriptor)
        {
            if (descriptor == null)
                return false;
            if (descriptor.FieldType == DashboardFieldType.DateTime || descriptor.FieldType == DashboardFieldType.Unknown)
                return false;
            if (descriptor.Id == DashboardFieldIds.SystemCreatedMonth)
                return false;
            return true;
        }

        public static bool TryToRuntimeFilter(
            DashboardLevelFilterDefinition filter,
            out DashboardFilterDefinition runtime,
            out string error)
        {
            runtime = null;
            error = null;
            if (filter == null)
            {
                error = "filter is required";
                return false;
            }

            var document = new DashboardFilterDocument
            {
                FieldId = filter.FieldId,
                Operator = filter.Operator,
                ValueKind = filter.ValueKind,
                Value = filter.Value
            };
            return DashboardQueryPersistence.TryToFilter(document, out runtime, out error);
        }
    }
}
