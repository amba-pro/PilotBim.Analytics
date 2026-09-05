using System;
using System.Collections.Generic;

namespace PilotBim.Analytics.Models
{
    public sealed class ProjectAnalyticsSnapshot
    {
        public DateTime GeneratedAt { get; set; }
        public string ScanMode { get; set; }
        public string DataSource { get; set; }
        public string Notes { get; set; }

        public List<AnalyticsKpiRow> Summary { get; set; } = new List<AnalyticsKpiRow>();
        public List<TypeCountRow> ObjectsByType { get; set; } = new List<TypeCountRow>();
        public List<CreatorCountRow> ObjectsByCreator { get; set; } = new List<CreatorCountRow>();
        public List<PeriodCountRow> ObjectsByCreatedMonth { get; set; } = new List<PeriodCountRow>();
        public List<StateCountRow> ObjectsByUserState { get; set; } = new List<StateCountRow>();
        public List<DocumentVersionRow> DocumentVersions { get; set; } = new List<DocumentVersionRow>();
        public List<AnalyticsKpiRow> BimSummary { get; set; } = new List<AnalyticsKpiRow>();
        public List<BimModelAnalyticsRow> BimModelAnalytics { get; set; } = new List<BimModelAnalyticsRow>();
        public List<BimPartAnalyticsRow> BimPartAnalytics { get; set; } = new List<BimPartAnalyticsRow>();
        public List<BimElementTypeCountRow> BimElementTypeCounts { get; set; } = new List<BimElementTypeCountRow>();
        public List<AttributeQualityRow> DataQuality { get; set; } = new List<AttributeQualityRow>();
        public List<RemarkTypeRow> ModelRemarks { get; set; } = new List<RemarkTypeRow>();
        public List<RemarkLinkRow> RemarkLinks { get; set; } = new List<RemarkLinkRow>();
        public RemarkAnalyticsSummary RemarkAnalytics { get; set; }
        public List<ResponsibleCountRow> ObjectsByResponsible { get; set; } = new List<ResponsibleCountRow>();
        public List<StateSemanticCountRow> ObjectsByUserStateSemantic { get; set; } = new List<StateSemanticCountRow>();
        public List<ScanDiffRow> ScanDiffRows { get; set; } = new List<ScanDiffRow>();
        public List<string> Limitations { get; set; } = new List<string>();
    }

    public sealed class AnalyticsKpiRow
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Detail { get; set; }
    }

    public sealed class TypeCountRow
    {
        public int TypeId { get; set; }
        public string TypeName { get; set; }
        public long Count { get; set; }
        public bool IsEstimate { get; set; }
        public double SharePercent { get; set; }
        public double FillPercent { get; set; }
        public string CountDisplay
        {
            get { return IsEstimate ? Count + " ~" : Count.ToString(); }
        }
    }

    public sealed class CreatorCountRow
    {
        public int CreatorId { get; set; }
        public string DisplayName { get; set; }
        public int SampledCount { get; set; }
        public double SharePercent { get; set; }
        public string Scope { get; set; }
    }

    public sealed class PeriodCountRow
    {
        public string Period { get; set; }
        public int Count { get; set; }
        public double SharePercent { get; set; }
    }

    public sealed class StateCountRow
    {
        public Guid StateId { get; set; }
        public string StateTitle { get; set; }
        public int Count { get; set; }
        public double SharePercent { get; set; }
        public string Scope { get; set; }
    }

    public sealed class StateSemanticCountRow
    {
        public Guid StateId { get; set; }
        public string StateTitle { get; set; }
        public string Semantic { get; set; }
        public int Count { get; set; }
        public double SharePercent { get; set; }
        public string Scope { get; set; }
    }

    public sealed class DocumentVersionRow
    {
        public Guid ObjectId { get; set; }
        public string DisplayName { get; set; }
        public string TypeName { get; set; }
        public int FileCount { get; set; }
        public int PreviousSnapshotCount { get; set; }
        public int TotalIterations { get; set; }
    }

    public sealed class AttributeQualityRow
    {
        public string TypeName { get; set; }
        public string AttributeTitle { get; set; }
        public bool IsObligatory { get; set; }
        public double FillRate { get; set; }
        public int SampledCount { get; set; }
        public string Status { get; set; }
    }

    public sealed class RemarkTypeRow
    {
        public int TypeId { get; set; }
        public string TypeName { get; set; }
        public long ObjectCount { get; set; }
        public double BimObjectIdFillRate { get; set; }
        public string MappingStatus { get; set; }
        public string Notes { get; set; }
    }
}
