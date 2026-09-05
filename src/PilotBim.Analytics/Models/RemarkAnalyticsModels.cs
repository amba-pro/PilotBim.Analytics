using System;

namespace PilotBim.Analytics.Models
{
    public sealed class RemarkLinkRow
    {
        public Guid ObjectId { get; set; }
        public string DisplayName { get; set; }
        public string TypeName { get; set; }
        public string BimObjectId { get; set; }
        public bool ResolvedInIndex { get; set; }
        public string MappingStatus { get; set; }
        public string Notes { get; set; }
    }

    public sealed class RemarkAnalyticsSummary
    {
        public long TotalRemarkObjects { get; set; }
        public int SampledRemarks { get; set; }
        public int WithBimObjectId { get; set; }
        public int ResolvedInIndex { get; set; }
        public double RemarksPer1000Elements { get; set; }
        public long IndexedElements { get; set; }
        public string Scope { get; set; }
    }

    public sealed class ResponsibleCountRow
    {
        public int OrgUnitId { get; set; }
        public int? PersonId { get; set; }
        public string DisplayName { get; set; }
        public string OrganisationName { get; set; }
        public int SampledCount { get; set; }
        public double SharePercent { get; set; }
        public string Scope { get; set; }
    }
}
