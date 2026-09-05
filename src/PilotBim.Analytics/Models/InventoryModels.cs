using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PilotBim.Analytics.Models
{
    public sealed class DiagnosticEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Area { get; set; }
        public string Message { get; set; }
    }

    public sealed class SystemFieldCapability
    {
        public string Field { get; set; }
        public string SdkSource { get; set; }
        public string Availability { get; set; }
        public string Example { get; set; }
        public string Notes { get; set; }
    }

    public sealed class AttributeInventoryRecord
    {
        public int TypeId { get; set; }
        public string TypeName { get; set; }
        public string AttributeId { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public string ValueType { get; set; }
        public bool IsObligatory { get; set; }
        public bool IsService { get; set; }
        public string ConfigurationSummary { get; set; }
        public int PopulatedCount { get; set; }
        public int EmptyCount { get; set; }
        public int SampledCount { get; set; }
        public double FillRate { get; set; }
        public string PopulationStatus { get; set; }
        public List<string> SampleValues { get; set; } = new List<string>();
        public string ExamplesText
        {
            get { return SampleValues == null ? "" : string.Join(" | ", SampleValues); }
        }
        public string ReferenceKind { get; set; }
        public int DistinctReferenceCount { get; set; }
    }

    public sealed class TypeInventoryRecord
    {
        public int TypeId { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public string Kind { get; set; }
        public bool IsService { get; set; }
        public bool IsDeleted { get; set; }
        public bool HasFiles { get; set; }
        public long ObjectCount { get; set; }
        public bool ObjectCountIsEstimate { get; set; }
        public int AttributeCount { get; set; }
        public int SampledCount { get; set; }
        public double FillPercent { get; set; }
        public string Status { get; set; }
        public string Warnings { get; set; }
        public List<AttributeInventoryRecord> Attributes { get; set; } = new List<AttributeInventoryRecord>();
        public Dictionary<Guid, int> ObservedStateCounts { get; set; } = new Dictionary<Guid, int>();
        public Dictionary<string, int> PersonAttributeHits { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> OrgAttributeHits { get; set; } = new Dictionary<string, int>();
    }

    public sealed class StateInventoryRecord
    {
        public Guid StateId { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public string Color { get; set; }
        public bool IsDeleted { get; set; }
        public int ReferencedObjects { get; set; }
        public List<string> ReferencedTypes { get; set; } = new List<string>();
        public string ReferencedTypesText
        {
            get { return ReferencedTypes == null ? "" : string.Join(", ", ReferencedTypes); }
        }
        public string SemanticStatus { get; set; } = CapabilityStatus.Unknown;
    }

    public sealed class PersonInventoryRecord
    {
        public int PersonId { get; set; }
        public string DisplayName { get; set; }
        public int? OrganisationId { get; set; }
        public string OrganisationName { get; set; }
        public bool IsDeleted { get; set; }
        public int ObjectReferences { get; set; }
        public List<string> ReferencedAttributes { get; set; } = new List<string>();
    }

    public sealed class OrganisationInventoryRecord
    {
        public int OrganisationId { get; set; }
        public string Name { get; set; }
        public int? ParentOrganisationId { get; set; }
        public bool IsPosition { get; set; }
        public bool IsDeleted { get; set; }
        public int ObjectReferences { get; set; }
        public int ChildrenCount { get; set; }
        /// <summary>Person id from OrganisationUnitExtensions.Person when IsPosition.</summary>
        public int? PersonId { get; set; }
    }

    public sealed class DocumentCapabilityRecord
    {
        public string Capability { get; set; }
        public string Availability { get; set; }
        public string SdkSource { get; set; }
        public string Notes { get; set; }
        public string Sample { get; set; }
    }

    public sealed class HistoryCapabilityRecord
    {
        public string Capability { get; set; }
        public string Availability { get; set; }
        public string SdkSource { get; set; }
        public string Notes { get; set; }
    }

    public sealed class HistorySampleEvent
    {
        public Guid HistoryItemId { get; set; }
        public Guid ObjectId { get; set; }
        public DateTime Created { get; set; }
        public int CreatorId { get; set; }
        public string Reason { get; set; }
    }

    public sealed class BimCapability
    {
        public string Name { get; set; }
        public string Availability { get; set; }
        public string SdkSource { get; set; }
        public string Notes { get; set; }
    }

    public sealed class BimElementSample
    {
        public Guid ElementId { get; set; }
        public Guid ModelPartId { get; set; }
        public Guid ModelId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string GlobalId { get; set; }
        public string BimObjectId { get; set; }
        public int PropertyCount { get; set; }
        public List<string> PropertyPreview { get; set; } = new List<string>();
    }

    public sealed class AnalyticsCapability
    {
        public string Metric { get; set; }
        public string RequiredData { get; set; }
        public string Availability { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
    }

    public sealed class ZoneResult
    {
        public string Zone { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }

    public sealed class SdkMemberRecord
    {
        public string Member { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
    }

    public sealed class ProjectInventoryReport
    {
        public DateTime GeneratedAt { get; set; }
        public string ScanMode { get; set; }
        public int SampleLimit { get; set; }
        public string PilotVersion { get; set; }
        public string SdkVersion { get; set; }
        public string BimSdkVersion { get; set; }
        public string Framework { get; set; }
        public string Architecture { get; set; }
        public string FinalStatus { get; set; }

        public List<TypeInventoryRecord> Types { get; set; } = new List<TypeInventoryRecord>();
        public List<AttributeInventoryRecord> AllAttributes { get; set; } = new List<AttributeInventoryRecord>();
        public List<SystemFieldCapability> SystemFields { get; set; } = new List<SystemFieldCapability>();
        public List<StateInventoryRecord> States { get; set; } = new List<StateInventoryRecord>();
        public List<PersonInventoryRecord> Persons { get; set; } = new List<PersonInventoryRecord>();
        public List<OrganisationInventoryRecord> Organisations { get; set; } = new List<OrganisationInventoryRecord>();
        public List<DocumentCapabilityRecord> DocumentCapabilities { get; set; } = new List<DocumentCapabilityRecord>();
        public List<HistoryCapabilityRecord> HistoryCapabilities { get; set; } = new List<HistoryCapabilityRecord>();
        public List<HistorySampleEvent> HistorySamples { get; set; } = new List<HistorySampleEvent>();
        public List<BimCapability> BimCapabilities { get; set; } = new List<BimCapability>();
        public List<BimElementSample> BimElementSamples { get; set; } = new List<BimElementSample>();
        public List<BimModelAnalyticsRow> BimModelAnalytics { get; set; } = new List<BimModelAnalyticsRow>();
        public List<BimPartAnalyticsRow> BimPartAnalytics { get; set; } = new List<BimPartAnalyticsRow>();
        public List<BimElementTypeCountRow> BimElementTypeCounts { get; set; } = new List<BimElementTypeCountRow>();
        public List<RemarkLinkRow> RemarkLinks { get; set; } = new List<RemarkLinkRow>();
        public RemarkAnalyticsSummary RemarkAnalytics { get; set; } = new RemarkAnalyticsSummary();
        public Dictionary<int, int> ResponsibleSampleCounts { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> CreatorFullCounts { get; set; } = new Dictionary<int, int>();
        public Dictionary<string, int> CreatedMonthFullCounts { get; set; } = new Dictionary<string, int>();
        public int CreatorFullScanObjects { get; set; }
        public bool CreatorCountsFromFullScan { get; set; }
        public List<AnalyticsCapability> AnalyticsCapabilities { get; set; } = new List<AnalyticsCapability>();
        public List<AnalyticsCapability> DataQualityCapabilities { get; set; } = new List<AnalyticsCapability>();
        public List<SdkMemberRecord> VerifiedApis { get; set; } = new List<SdkMemberRecord>();
        public List<SdkMemberRecord> NotVerifiedApis { get; set; } = new List<SdkMemberRecord>();
        public List<SdkMemberRecord> RuntimeValidationApis { get; set; } = new List<SdkMemberRecord>();
        public List<ZoneResult> ZoneResults { get; set; } = new List<ZoneResult>();
        public List<DiagnosticEntry> Diagnostics { get; set; } = new List<DiagnosticEntry>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> PerformanceNotes { get; set; } = new List<string>();
        public List<DocumentSampleRow> DocumentSamples { get; set; } = new List<DocumentSampleRow>();

        public int TypesDiscovered { get; set; }
        public long ObjectsFound { get; set; }
        public int AttributesDiscovered { get; set; }
        public int StatesDiscovered { get; set; }
        public int PersonsResolved { get; set; }
        public int OrganisationsResolved { get; set; }
        public long BimModelsCount { get; set; }
        public long BimModelPartsCount { get; set; }
        public string AnalyticsReadiness { get; set; }
        public bool Cancelled { get; set; }

        public Dictionary<int, int> CreatorSampleCounts { get; set; } = new Dictionary<int, int>();
        public Dictionary<string, int> CreatedMonthSampleCounts { get; set; } = new Dictionary<string, int>();
        public int CreatorSampleTotal { get; set; }
    }

    public sealed class DocumentSampleRow
    {
        public Guid ObjectId { get; set; }
        public string DisplayName { get; set; }
        public string TypeName { get; set; }
        public int FileCount { get; set; }
        public int PreviousSnapshotCount { get; set; }
        public string LatestFileName { get; set; }
        public long? LatestFileSize { get; set; }
        public DateTime? SnapshotCreated { get; set; }
        public int? SnapshotCreatorId { get; set; }
    }

    public sealed class StructureNodeModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public int TypeId { get; set; }
        public string StateText { get; set; }
        public Guid ParentId { get; set; }
        public int ChildrenCount { get; set; }
        public bool ChildrenLoaded { get; set; }
        public ObservableCollection<StructureNodeModel> Children { get; set; } = new ObservableCollection<StructureNodeModel>();
    }
}
