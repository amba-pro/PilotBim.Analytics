using System;
using System.Collections.Generic;

namespace PilotBim.Analytics.Models
{
    public enum AnalyticsChartKind
    {
        HorizontalBar = 0,
        VerticalBar = 1,
        Pie = 2,
        Line = 3
    }

    public enum AnalyticsChartSource
    {
        Types = 0,
        Creators = 1,
        CreatedMonth = 2,
        UserStates = 3,
        StateSemantic = 4,
        Responsible = 5,
        IfcTypes = 6,
        BimModels = 7,
        Remarks = 8
    }

    public sealed class ChartOptionItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public override string ToString() { return Title; }
    }

    public sealed class ChartSeriesPoint
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public string ValueDisplay { get; set; }
        public double SharePercent { get; set; }
        public double BarWidth { get; set; }
        public double ColumnHeight { get; set; }
        public string ColorHex { get; set; }
        public double PieStartDegrees { get; set; }
        public double PieSweepDegrees { get; set; }
        public double LineX { get; set; }
        public double LineY { get; set; }
    }

    public sealed class ChartBarRow
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public string ValueDisplay { get; set; }
        public double SharePercent { get; set; }
        public double BarWidth { get; set; }
    }

    public sealed class BimModelFilterItem
    {
        public Guid ModelId { get; set; }
        public string DisplayName { get; set; }
        public bool IsAll { get; set; }
    }
}
