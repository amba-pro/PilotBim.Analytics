using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PilotBim.Analytics.Models
{
    [DataContract]
    public sealed class StoredScanBaseline
    {
        [DataMember(Order = 1)]
        public DateTime GeneratedAt { get; set; }

        [DataMember(Order = 2)]
        public string ScanMode { get; set; }

        [DataMember(Order = 3)]
        public List<StoredKpi> Kpis { get; set; } = new List<StoredKpi>();

        [DataMember(Order = 4)]
        public long BimElementCount { get; set; }

        [DataMember(Order = 5)]
        public long BimModelCount { get; set; }

        [DataMember(Order = 6)]
        public long BimPartCount { get; set; }

        [DataMember(Order = 7)]
        public List<StoredResponsible> Responsible { get; set; } = new List<StoredResponsible>();

        [DataMember(Order = 8)]
        public string Id { get; set; }

        [DataMember(Order = 9)]
        public string Name { get; set; }

        [DataMember(Order = 10)]
        public List<StoredNamedCount> TopTypes { get; set; } = new List<StoredNamedCount>();

        [DataMember(Order = 11)]
        public List<StoredNamedCount> StateSemantic { get; set; } = new List<StoredNamedCount>();

        [DataMember(Order = 12)]
        public List<StoredNamedCount> TopIfcTypes { get; set; } = new List<StoredNamedCount>();

        [DataMember(Order = 13)]
        public List<StoredNamedCount> Remarks { get; set; } = new List<StoredNamedCount>();
    }

    [DataContract]
    public sealed class StoredNamedCount
    {
        [DataMember(Order = 1)]
        public string Name { get; set; }

        [DataMember(Order = 2)]
        public double Count { get; set; }
    }

    [DataContract]
    public sealed class StoredKpi
    {
        [DataMember(Order = 1)]
        public string Label { get; set; }

        [DataMember(Order = 2)]
        public string Value { get; set; }

        [DataMember(Order = 3)]
        public double? NumericValue { get; set; }
    }

    [DataContract]
    public sealed class StoredResponsible
    {
        [DataMember(Order = 1)]
        public int OrgUnitId { get; set; }

        [DataMember(Order = 2)]
        public int? PersonId { get; set; }

        [DataMember(Order = 3)]
        public string DisplayName { get; set; }

        [DataMember(Order = 4)]
        public int SampledCount { get; set; }
    }

    [DataContract]
    public sealed class ScanHistoryIndex
    {
        [DataMember(Order = 1)]
        public List<ScanHistoryEntry> Entries { get; set; } = new List<ScanHistoryEntry>();
    }

    [DataContract]
    public sealed class ScanHistoryEntry
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }

        [DataMember(Order = 2)]
        public string Name { get; set; }

        [DataMember(Order = 3)]
        public DateTime GeneratedAt { get; set; }

        [DataMember(Order = 4)]
        public string ScanMode { get; set; }

        public string DisplayTitle
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(Name) ? "Скан" : Name;
                return name + " · " + GeneratedAt.ToString("g")
                    + (string.IsNullOrWhiteSpace(ScanMode) ? "" : " · " + ScanMode);
            }
        }
    }

    public sealed class ScanDiffRow
    {
        public string Area { get; set; }
        public string Metric { get; set; }
        public string Previous { get; set; }
        public string Current { get; set; }
        public string Delta { get; set; }
        public string Notes { get; set; }
    }
}
