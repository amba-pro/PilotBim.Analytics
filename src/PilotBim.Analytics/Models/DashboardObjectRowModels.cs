using System;
using System.Collections.Generic;

namespace PilotBim.Analytics.Models
{
    internal enum DashboardTypeCoverage
    {
        Complete = 0,
        Partial = 1,
        Failed = 2
    }

    /// <summary>
    /// Typed field payload. DisplayText is never used as identity.
    /// </summary>
    internal sealed class DashboardFieldValue
    {
        public DashboardFieldValue(
            DashboardFieldType kind,
            object value,
            string stableKey,
            string displayText)
        {
            Kind = kind;
            Value = value;
            StableKey = stableKey;
            DisplayText = displayText;
        }

        public DashboardFieldType Kind { get; private set; }
        public object Value { get; private set; }
        public string StableKey { get; private set; }
        public string DisplayText { get; private set; }
    }

    /// <summary>
    /// One Pilot object after type-dataset materialization. Fields keyed by DB-1 ids.
    /// </summary>
    internal sealed class DashboardObjectRow
    {
        public DashboardObjectRow(
            Guid objectId,
            int typeId,
            Guid? parentId,
            IReadOnlyDictionary<string, DashboardFieldValue> fields)
        {
            ObjectId = objectId;
            TypeId = typeId;
            ParentId = parentId;
            Fields = fields ?? new Dictionary<string, DashboardFieldValue>();
        }

        public Guid ObjectId { get; private set; }
        public int TypeId { get; private set; }
        public Guid? ParentId { get; private set; }
        public IReadOnlyDictionary<string, DashboardFieldValue> Fields { get; private set; }
    }

    internal sealed class DashboardTypeDataset
    {
        public DashboardTypeDataset(
            int typeId,
            long expectedCount,
            int loadedUniqueCount,
            DashboardTypeCoverage coverage,
            string reason,
            IReadOnlyList<DashboardObjectRow> rows,
            int fieldValueCount,
            int skippedUnsupportedValues)
            : this(
                typeId,
                expectedCount,
                loadedUniqueCount,
                coverage,
                reason,
                rows,
                fieldValueCount,
                skippedUnsupportedValues,
                null)
        {
        }

        public DashboardTypeDataset(
            int typeId,
            long expectedCount,
            int loadedUniqueCount,
            DashboardTypeCoverage coverage,
            string reason,
            IReadOnlyList<DashboardObjectRow> rows,
            int fieldValueCount,
            int skippedUnsupportedValues,
            IReadOnlyList<string> skippedUnsupportedFieldIds)
        {
            TypeId = typeId;
            ExpectedCount = expectedCount;
            LoadedUniqueCount = loadedUniqueCount;
            Coverage = coverage;
            Reason = reason;
            Rows = rows ?? new DashboardObjectRow[0];
            FieldValueCount = fieldValueCount;
            SkippedUnsupportedValues = skippedUnsupportedValues;
            SkippedUnsupportedFieldIds = skippedUnsupportedFieldIds ?? new string[0];
        }

        public int TypeId { get; private set; }
        public long ExpectedCount { get; private set; }
        public int LoadedUniqueCount { get; private set; }
        public DashboardTypeCoverage Coverage { get; private set; }
        public string Reason { get; private set; }
        public IReadOnlyList<DashboardObjectRow> Rows { get; private set; }
        public int FieldValueCount { get; private set; }
        public int SkippedUnsupportedValues { get; private set; }
        public IReadOnlyList<string> SkippedUnsupportedFieldIds { get; private set; }
    }

    /// <summary>
    /// SDK-free snapshot of one loaded object for row factory / tests.
    /// </summary>
    internal sealed class DashboardObjectSource
    {
        public Guid Id { get; set; }
        public int TypeId { get; set; }
        public Guid ParentId { get; set; }
        public int? CreatorId { get; set; }
        public DateTime Created { get; set; }
        public string ObjectState { get; set; }
        public IList<DashboardAttributeSource> Attributes { get; set; }
    }

    internal sealed class DashboardAttributeSource
    {
        public string Name { get; set; }
        public string ValueType { get; set; }
        public object Value { get; set; }
        public bool Present { get; set; }
    }
}
