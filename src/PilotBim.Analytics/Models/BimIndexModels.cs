using System;
using System.Collections.Generic;

namespace PilotBim.Analytics.Models
{
    public sealed class BimPartRef
    {
        public Guid PartId { get; set; }
        public string PartName { get; set; }
        public Guid ModelId { get; set; }
        public string ModelName { get; set; }
    }

    public sealed class BimModelAnalyticsRow
    {
        public Guid ModelId { get; set; }
        public string ModelName { get; set; }
        public int PartCount { get; set; }
        public long ElementCount { get; set; }
        public long GlobalIdCount { get; set; }
        public double GlobalIdFillPercent { get; set; }
        public bool IsEstimate { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public string ElementCountDisplay
        {
            get { return IsEstimate ? ElementCount + " ~" : ElementCount.ToString(); }
        }
    }

    public sealed class BimPartAnalyticsRow
    {
        public Guid PartId { get; set; }
        public string PartName { get; set; }
        public Guid ModelId { get; set; }
        public string ModelName { get; set; }
        public long ElementCount { get; set; }
        public long GlobalIdCount { get; set; }
        public long WithoutGlobalIdCount { get; set; }
        public double GlobalIdFillPercent { get; set; }
        public bool IsTruncated { get; set; }
        public string Status { get; set; }
        public string DataSource { get; set; }
        public string Notes { get; set; }
        public List<BimElementTypeCountRow> TypeBreakdown { get; set; } = new List<BimElementTypeCountRow>();
        public string ElementCountDisplay
        {
            get { return IsTruncated ? ElementCount + " ~" : ElementCount.ToString(); }
        }
    }

    public sealed class BimElementTypeCountRow
    {
        public Guid ModelId { get; set; }
        public string ModelName { get; set; }
        public Guid PartId { get; set; }
        public string PartName { get; set; }
        public string IfcType { get; set; }
        public long Count { get; set; }
        public bool IsEstimate { get; set; }
        public double SharePercent { get; set; }
    }
}
