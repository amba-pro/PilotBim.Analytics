using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PilotBim.Analytics.Models
{
    public static class DashboardWidgetKinds
    {
        public const string Kpi = "Kpi";
        public const string Bim = "Bim";
        public const string Responsible = "Responsible";
        public const string Chart = "Chart";
    }

    [DataContract]
    public sealed class DashboardLayoutState
    {
        /// <summary>Legacy blocks (pre-0.7). Migrated into Widgets by Normalize.</summary>
        [DataMember(Order = 1)]
        public List<DashboardBlockState> Blocks { get; set; } = new List<DashboardBlockState>();

        [DataMember(Order = 2)]
        public List<DashboardWidgetState> Widgets { get; set; } = new List<DashboardWidgetState>();
    }

    [DataContract]
    public sealed class DashboardBlockState
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }

        [DataMember(Order = 2)]
        public bool IsVisible { get; set; }

        [DataMember(Order = 3)]
        public int Order { get; set; }
    }

    [DataContract]
    public sealed class DashboardWidgetState
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }

        [DataMember(Order = 2)]
        public string Title { get; set; }

        [DataMember(Order = 3)]
        public string WidgetKind { get; set; }

        [DataMember(Order = 4)]
        public bool IsVisible { get; set; }

        [DataMember(Order = 5)]
        public int Order { get; set; }

        [DataMember(Order = 6)]
        public int ColumnSpan { get; set; }

        [DataMember(Order = 7)]
        public string ChartSource { get; set; }

        [DataMember(Order = 8)]
        public string ChartKind { get; set; }

        [DataMember(Order = 9)]
        public int TopN { get; set; }
    }

    public static class DashboardBlockIds
    {
        public const string Kpi = "kpi";
        public const string Bim = "bim";
        public const string Responsible = "responsible";
    }
}
