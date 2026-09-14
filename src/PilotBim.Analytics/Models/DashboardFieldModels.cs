using System;
using System.Collections.Generic;
using System.Globalization;

namespace PilotBim.Analytics.Models
{
    /// <summary>
    /// Semantic field kinds for future dashboard queries.
    /// Mapped from Pilot AttributeType names / known system fields; unknown → Unknown.
    /// </summary>
    internal enum DashboardFieldType
    {
        Text = 0,
        Integer = 1,
        Number = 2,
        Boolean = 3,
        DateTime = 4,
        Enum = 5,
        User = 6,
        Reference = 7,
        Guid = 8,
        Unknown = 9
    }

    /// <summary>
    /// Where a dashboard field originates.
    /// </summary>
    internal enum DashboardFieldSourceKind
    {
        System = 0,
        Attribute = 1
    }

    /// <summary>
    /// Conservative V1 query capabilities (no Sum/Average claims).
    /// </summary>
    internal sealed class DashboardFieldCapabilities
    {
        public DashboardFieldCapabilities(bool canFilter, bool canGroup, bool canSort)
        {
            CanFilter = canFilter;
            CanGroup = canGroup;
            CanSort = canSort;
        }

        public bool CanFilter { get; private set; }
        public bool CanGroup { get; private set; }
        public bool CanSort { get; private set; }
    }

    /// <summary>
    /// Immutable semantic field descriptor. Id is machine identity; DisplayName is presentation only.
    /// </summary>
    internal sealed class DashboardFieldDescriptor
    {
        public DashboardFieldDescriptor(
            string id,
            string displayName,
            DashboardFieldType fieldType,
            DashboardFieldSourceKind sourceKind,
            int? objectTypeId,
            string sourceName,
            DashboardFieldCapabilities capabilities)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Field id is required.", "id");

            Id = id;
            DisplayName = displayName ?? string.Empty;
            FieldType = fieldType;
            SourceKind = sourceKind;
            ObjectTypeId = objectTypeId;
            SourceName = sourceName ?? string.Empty;
            Capabilities = capabilities ?? new DashboardFieldCapabilities(false, false, false);
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public DashboardFieldType FieldType { get; private set; }
        public DashboardFieldSourceKind SourceKind { get; private set; }

        /// <summary>Owning Pilot type id for attributes; null for system fields.</summary>
        public int? ObjectTypeId { get; private set; }

        /// <summary>System key (e.g. created) or attribute Name.</summary>
        public string SourceName { get; private set; }

        public DashboardFieldCapabilities Capabilities { get; private set; }
    }

    /// <summary>
    /// Centralized field-id formats. Display text must never be passed here.
    /// </summary>
    internal static class DashboardFieldIds
    {
        public const string SystemObjectId = "system:objectId";
        public const string SystemTypeId = "system:typeId";
        public const string SystemParentId = "system:parentId";
        public const string SystemCreatorId = "system:creatorId";
        public const string SystemCreated = "system:created";
        public const string SystemObjectState = "system:objectState";

        public static string System(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("System field key is required.", "key");
            return "system:" + key.Trim();
        }

        /// <summary>
        /// attribute:{typeId}:{attributeName} — Name is the best SDK key (dictionary key on IDataObject.Attributes).
        /// Rename-sensitive: no stronger attribute id exists on IAttribute.
        /// </summary>
        public static string Attribute(int typeId, string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
                throw new ArgumentException("Attribute name is required.", "attributeName");
            return "attribute:"
                + typeId.ToString(CultureInfo.InvariantCulture)
                + ":"
                + attributeName.Trim();
        }
    }

    /// <summary>
    /// Immutable catalog with Id lookup. Built by <see cref="Services.PilotFieldCatalogBuilder"/>.
    /// </summary>
    internal sealed class DashboardFieldCatalog
    {
        private readonly IReadOnlyList<DashboardFieldDescriptor> _fields;
        private readonly Dictionary<string, DashboardFieldDescriptor> _byId;

        public DashboardFieldCatalog(IReadOnlyList<DashboardFieldDescriptor> fields)
        {
            if (fields == null)
                fields = new DashboardFieldDescriptor[0];

            var copy = new List<DashboardFieldDescriptor>(fields.Count);
            _byId = new Dictionary<string, DashboardFieldDescriptor>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                if (field == null || string.IsNullOrEmpty(field.Id))
                    continue;
                if (_byId.ContainsKey(field.Id))
                    continue; // collision: retain first (builder should already enforce)
                _byId.Add(field.Id, field);
                copy.Add(field);
            }

            _fields = copy.AsReadOnly();
        }

        public IReadOnlyList<DashboardFieldDescriptor> Fields
        {
            get { return _fields; }
        }

        public int Count
        {
            get { return _fields.Count; }
        }

        public bool TryGet(string id, out DashboardFieldDescriptor descriptor)
        {
            if (string.IsNullOrEmpty(id))
            {
                descriptor = null;
                return false;
            }
            return _byId.TryGetValue(id, out descriptor);
        }

        /// <summary>
        /// System fields plus attributes owned by the given type id.
        /// </summary>
        public IReadOnlyList<DashboardFieldDescriptor> ForObjectType(int typeId)
        {
            var list = new List<DashboardFieldDescriptor>();
            foreach (var field in _fields)
            {
                if (field.SourceKind == DashboardFieldSourceKind.System)
                    list.Add(field);
                else if (field.ObjectTypeId.HasValue && field.ObjectTypeId.Value == typeId)
                    list.Add(field);
            }
            return list.AsReadOnly();
        }
    }
}
