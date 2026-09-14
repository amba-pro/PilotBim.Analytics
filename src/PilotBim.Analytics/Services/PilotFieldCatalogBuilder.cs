using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Builds a semantic <see cref="DashboardFieldCatalog"/> from already-normalized inventory metadata.
    /// Does not call Pilot SDK. O(types + attributes). No object scanning.
    /// </summary>
    internal sealed class PilotFieldCatalogBuilder
    {
        /// <summary>
        /// Collision policy: retain the first descriptor for a given Id; skip later duplicates.
        /// </summary>
        public int SkippedDuplicateIds { get; private set; }

        /// <summary>
        /// Attributes skipped due to null/empty Name.
        /// </summary>
        public int SkippedEmptyAttributeNames { get; private set; }

        public DashboardFieldCatalog Build(ProjectInventoryReport report)
        {
            if (report == null)
                return Build(Enumerable.Empty<TypeInventoryRecord>());
            return Build(report.Types ?? Enumerable.Empty<TypeInventoryRecord>());
        }

        public DashboardFieldCatalog Build(IEnumerable<TypeInventoryRecord> types)
        {
            SkippedDuplicateIds = 0;
            SkippedEmptyAttributeNames = 0;

            var ordered = new List<DashboardFieldDescriptor>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var system in BuildSystemFields())
                TryAdd(ordered, seen, system);

            if (types != null)
            {
                foreach (var type in types
                    .Where(t => t != null)
                    .OrderBy(t => t.TypeId)
                    .ThenBy(t => t.Name ?? string.Empty, StringComparer.Ordinal))
                {
                    var attrs = type.Attributes ?? new List<AttributeInventoryRecord>();
                    foreach (var attr in attrs
                        .Where(a => a != null)
                        .OrderBy(a => a.Name ?? string.Empty, StringComparer.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(attr.Name))
                        {
                            SkippedEmptyAttributeNames++;
                            continue;
                        }

                        var descriptor = BuildAttributeField(type.TypeId, attr);
                        if (!TryAdd(ordered, seen, descriptor))
                            SkippedDuplicateIds++;
                    }
                }
            }

            return new DashboardFieldCatalog(ordered);
        }

        private bool TryAdd(
            List<DashboardFieldDescriptor> ordered,
            HashSet<string> seen,
            DashboardFieldDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrEmpty(descriptor.Id))
                return false;
            if (!seen.Add(descriptor.Id))
                return false;
            ordered.Add(descriptor);
            return true;
        }

        private static IEnumerable<DashboardFieldDescriptor> BuildSystemFields()
        {
            // Only fields backed by IDataObject / existing scan aggregates (not every SDK matrix row).
            yield return SystemField(
                DashboardFieldIds.SystemObjectId,
                "objectId",
                "Object Id",
                DashboardFieldType.Guid,
                filter: true, group: false, sort: true);

            yield return SystemField(
                DashboardFieldIds.SystemTypeId,
                "typeId",
                "Type",
                DashboardFieldType.Integer,
                filter: true, group: true, sort: true);

            yield return SystemField(
                DashboardFieldIds.SystemParentId,
                "parentId",
                "Parent Id",
                DashboardFieldType.Guid,
                filter: true, group: true, sort: true);

            yield return SystemField(
                DashboardFieldIds.SystemCreatorId,
                "creatorId",
                "Creator",
                DashboardFieldType.Integer,
                filter: true, group: true, sort: true);

            yield return SystemField(
                DashboardFieldIds.SystemCreated,
                "created",
                "Created",
                DashboardFieldType.DateTime,
                filter: true, group: true, sort: true);

            yield return SystemField(
                DashboardFieldIds.SystemObjectState,
                "objectState",
                "Object State",
                DashboardFieldType.Enum,
                filter: true, group: true, sort: true);
        }

        private static DashboardFieldDescriptor SystemField(
            string id,
            string sourceName,
            string displayName,
            DashboardFieldType fieldType,
            bool filter,
            bool group,
            bool sort)
        {
            return new DashboardFieldDescriptor(
                id,
                displayName,
                fieldType,
                DashboardFieldSourceKind.System,
                objectTypeId: null,
                sourceName: sourceName,
                capabilities: CapabilitiesFor(fieldType, filter, group, sort));
        }

        private static DashboardFieldDescriptor BuildAttributeField(int typeId, AttributeInventoryRecord attr)
        {
            var name = attr.Name.Trim();
            var id = DashboardFieldIds.Attribute(typeId, name);
            var display = ResolveDisplayName(attr.Title, name);
            var fieldType = MapValueType(attr.ValueType);
            return new DashboardFieldDescriptor(
                id,
                display,
                fieldType,
                DashboardFieldSourceKind.Attribute,
                objectTypeId: typeId,
                sourceName: name,
                capabilities: CapabilitiesFor(fieldType));
        }

        internal static string ResolveDisplayName(string title, string name)
        {
            if (!string.IsNullOrWhiteSpace(title))
                return title.Trim();
            if (!string.IsNullOrWhiteSpace(name))
                return name.Trim();
            return "(unnamed)";
        }

        /// <summary>
        /// Maps AttributeInventoryRecord.ValueType (AttributeType.ToString()) without referencing Pilot SDK.
        /// </summary>
        internal static DashboardFieldType MapValueType(string valueType)
        {
            if (string.IsNullOrWhiteSpace(valueType))
                return DashboardFieldType.Unknown;

            switch (valueType.Trim())
            {
                case "String":
                case "Numerator":
                    return DashboardFieldType.Text;
                case "Integer":
                    return DashboardFieldType.Integer;
                case "Double":
                case "Decimal":
                    return DashboardFieldType.Number;
                case "Boolean":
                    return DashboardFieldType.Boolean;
                case "DateTime":
                    return DashboardFieldType.DateTime;
                case "UserState":
                    return DashboardFieldType.Enum;
                case "OrgUnit":
                    return DashboardFieldType.User;
                case "ElementBook":
                    return DashboardFieldType.Reference;
                case "Array":
                case "Inherited":
                    return DashboardFieldType.Unknown;
                default:
                    return DashboardFieldType.Unknown;
            }
        }

        private static DashboardFieldCapabilities CapabilitiesFor(DashboardFieldType fieldType)
        {
            switch (fieldType)
            {
                case DashboardFieldType.Text:
                    return new DashboardFieldCapabilities(true, true, true);
                case DashboardFieldType.Integer:
                case DashboardFieldType.Number:
                    return new DashboardFieldCapabilities(true, true, true);
                case DashboardFieldType.Boolean:
                    return new DashboardFieldCapabilities(true, true, true);
                case DashboardFieldType.DateTime:
                    return new DashboardFieldCapabilities(true, true, true);
                case DashboardFieldType.Enum:
                    return new DashboardFieldCapabilities(true, true, true);
                case DashboardFieldType.User:
                case DashboardFieldType.Reference:
                    return new DashboardFieldCapabilities(true, true, false);
                case DashboardFieldType.Guid:
                    return new DashboardFieldCapabilities(true, false, true);
                default:
                    return new DashboardFieldCapabilities(false, false, false);
            }
        }

        private static DashboardFieldCapabilities CapabilitiesFor(
            DashboardFieldType fieldType,
            bool filter,
            bool group,
            bool sort)
        {
            // System fields: explicit overrides; still clamp Unknown to conservative.
            if (fieldType == DashboardFieldType.Unknown)
                return new DashboardFieldCapabilities(false, false, false);
            return new DashboardFieldCapabilities(filter, group, sort);
        }
    }
}
