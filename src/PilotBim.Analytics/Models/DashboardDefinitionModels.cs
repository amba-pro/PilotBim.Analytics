using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PilotBim.Analytics.Models
{
    internal static class DashboardPersistenceV2
    {
        public const int SchemaVersion = 2;
        public const string DefaultDashboardId = "default";
        public const string DefaultTitle = "Dashboard";
        public const string FileName = "dashboard.json";
        public const string ContentLegacy = "Legacy";
        public const string ContentQuery = "Query";
    }

    internal enum DashboardDefinitionLoadStatus
    {
        Success = 0,
        Missing = 1,
        Corrupt = 2,
        UnsupportedVersion = 3,
        ProjectMismatch = 4,
        Invalid = 5,
        IoFailure = 6
    }

    internal enum DashboardDefinitionSaveStatus
    {
        Success = 0,
        Invalid = 1,
        IoFailure = 2
    }

    internal enum DashboardPersistenceKind
    {
        LegacyV1 = 0,
        V2 = 1,
        UnsupportedVersion = 2,
        Invalid = 3
    }

    [DataContract]
    internal sealed class DashboardDefinition
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string Id { get; set; }

        [DataMember(Order = 3)]
        public string Title { get; set; }

        /// <summary>Canonical Pilot database Guid ("D"). Machine identity, not a display name.</summary>
        [DataMember(Order = 4)]
        public string ProjectKey { get; set; }

        [DataMember(Order = 5)]
        public List<DashboardWidgetDefinition> Widgets { get; set; }
    }

    [DataContract]
    internal sealed class DashboardWidgetDefinition
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }

        [DataMember(Order = 2)]
        public string Title { get; set; }

        [DataMember(Order = 3)]
        public DashboardWidgetLayoutDefinition Layout { get; set; }

        /// <summary><see cref="DashboardPersistenceV2.ContentLegacy"/> or <see cref="DashboardPersistenceV2.ContentQuery"/>.</summary>
        [DataMember(Order = 4)]
        public string ContentKind { get; set; }

        [DataMember(Order = 5)]
        public DashboardLegacyWidgetContent Legacy { get; set; }

        [DataMember(Order = 6)]
        public DashboardWidgetQueryDocument Query { get; set; }

        [DataMember(Order = 7)]
        public DashboardVisualizationDefinition Visualization { get; set; }
    }

    [DataContract]
    internal sealed class DashboardWidgetLayoutDefinition
    {
        [DataMember(Order = 1)]
        public int Order { get; set; }

        [DataMember(Order = 2)]
        public int ColumnSpan { get; set; }

        [DataMember(Order = 3)]
        public bool IsVisible { get; set; }
    }

    [DataContract]
    internal sealed class DashboardLegacyWidgetContent
    {
        [DataMember(Order = 1)]
        public string WidgetKind { get; set; }

        [DataMember(Order = 2)]
        public string ChartSource { get; set; }

        [DataMember(Order = 3)]
        public string ChartKind { get; set; }

        [DataMember(Order = 4)]
        public int TopN { get; set; }
    }

    [DataContract]
    internal sealed class DashboardWidgetQueryDocument
    {
        [DataMember(Order = 1)]
        public string Scope { get; set; }

        [DataMember(Order = 2)]
        public int? EntityTypeId { get; set; }

        [DataMember(Order = 3)]
        public string DimensionFieldId { get; set; }

        [DataMember(Order = 4)]
        public string Measure { get; set; }

        [DataMember(Order = 5)]
        public string Sort { get; set; }

        [DataMember(Order = 6)]
        public int? Limit { get; set; }

        [DataMember(Order = 7)]
        public List<DashboardFilterDocument> Filters { get; set; }
    }

    [DataContract]
    internal sealed class DashboardFilterDocument
    {
        [DataMember(Order = 1)]
        public string FieldId { get; set; }

        [DataMember(Order = 2)]
        public string Operator { get; set; }

        [DataMember(Order = 3)]
        public string ValueKind { get; set; }

        /// <summary>Kind-specific invariant encoding. Null for IsEmpty / IsNotEmpty.</summary>
        [DataMember(Order = 4)]
        public string Value { get; set; }
    }

    [DataContract]
    internal sealed class DashboardVisualizationDefinition
    {
        [DataMember(Order = 1)]
        public string Type { get; set; }
    }

    internal sealed class DashboardDefinitionLoadResult
    {
        public DashboardDefinitionLoadStatus Status { get; set; }
        public DashboardDefinition Definition { get; set; }
        public string Reason { get; set; }
        public string Path { get; set; }
    }

    internal sealed class DashboardDefinitionSaveResult
    {
        public DashboardDefinitionSaveStatus Status { get; set; }
        public string Reason { get; set; }
        public string Path { get; set; }
    }

    [DataContract]
    internal sealed class DashboardSchemaVersionProbe
    {
        [DataMember]
        public int SchemaVersion { get; set; }
    }
}
