using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class ChartDataServiceTests
    {
        private readonly ChartDataService _sut = new ChartDataService();

        private static ProjectAnalyticsSnapshot SnapshotWithTypes(params (string name, long count)[] items)
        {
            return new ProjectAnalyticsSnapshot
            {
                ObjectsByType = items.Select((t, i) => new TypeCountRow
                {
                    TypeId = i + 1,
                    TypeName = t.name,
                    Count = t.count
                }).ToList()
            };
        }

        [Fact]
        public void BuildTopTypes_EmptySnapshot_ReturnsEmpty()
        {
            var bars = _sut.BuildTopTypes(new ProjectAnalyticsSnapshot(), 12);
            Assert.Empty(bars);
        }

        [Fact]
        public void BuildTopTypes_NullSnapshot_ReturnsEmpty()
        {
            var bars = _sut.BuildTopTypes(null, 12);
            Assert.Empty(bars);
        }

        [Fact]
        public void BuildTopTypes_OrdersByCountDescending_AndAppliesTopN()
        {
            var snapshot = SnapshotWithTypes(("C", 5), ("A", 30), ("B", 10));
            var bars = _sut.BuildTopTypes(snapshot, 2);

            Assert.Equal(2, bars.Count);
            Assert.Equal("A", bars[0].Label);
            Assert.Equal(30, bars[0].Value);
            Assert.Equal("B", bars[1].Label);
            Assert.Equal(10, bars[1].Value);
        }

        [Fact]
        public void BuildTopTypes_TakeZero_ReturnsAll()
        {
            var snapshot = SnapshotWithTypes(("A", 1), ("B", 2), ("C", 3));
            var bars = _sut.BuildTopTypes(snapshot, 0);
            Assert.Equal(3, bars.Count);
        }

        [Fact]
        public void BuildTopTypes_NormalizesBarWidthToMax420()
        {
            var snapshot = SnapshotWithTypes(("Big", 100), ("Half", 50));
            var bars = _sut.BuildTopTypes(snapshot, 10);

            Assert.Equal(420, bars[0].BarWidth);
            Assert.Equal(210, bars[1].BarWidth);
        }

        [Fact]
        public void BuildCreatedTimeline_KeepsPeriodOrder_NotCountOrder()
        {
            var snapshot = new ProjectAnalyticsSnapshot
            {
                ObjectsByCreatedMonth = new List<PeriodCountRow>
                {
                    new PeriodCountRow { Period = "2024-01", Count = 1 },
                    new PeriodCountRow { Period = "2024-03", Count = 99 },
                    new PeriodCountRow { Period = "2024-02", Count = 5 }
                }
            };

            var bars = _sut.BuildCreatedTimeline(snapshot);
            Assert.Equal(new[] { "2024-01", "2024-02", "2024-03" }, bars.Select(b => b.Label).ToArray());
        }

        [Fact]
        public void BuildTopIfcTypes_Null_ReturnsEmpty()
        {
            Assert.Empty(_sut.BuildTopIfcTypes(null, 5));
        }

        [Fact]
        public void BuildTopIfcTypes_AggregatesSameTypeAcrossParts()
        {
            var rows = new List<BimElementTypeCountRow>
            {
                new BimElementTypeCountRow { IfcType = "IfcWall", Count = 10 },
                new BimElementTypeCountRow { IfcType = "IfcWall", Count = 5 },
                new BimElementTypeCountRow { IfcType = "IfcDoor", Count = 3 }
            };

            var bars = _sut.BuildTopIfcTypes(rows, 10);
            Assert.Equal(2, bars.Count);
            Assert.Equal("IfcWall", bars[0].Label);
            Assert.Equal(15, bars[0].Value);
            Assert.Equal("IfcDoor", bars[1].Label);
        }

        [Fact]
        public void BuildSeries_PieSweepsSumTo360()
        {
            var snapshot = SnapshotWithTypes(("A", 25), ("B", 75));
            var series = _sut.BuildSeries(snapshot, null, AnalyticsChartSource.Types, AnalyticsChartKind.Pie, 10);

            Assert.Equal(2, series.Count);
            // ExtractRaw orders Types by count descending → B(75) then A(25)
            Assert.Equal("B", series[0].Label);
            Assert.Equal(-90, series[0].PieStartDegrees);
            Assert.Equal(270, series[0].PieSweepDegrees); // 75% of 360
            Assert.Equal(90, series[1].PieSweepDegrees);  // 25% of 360
            Assert.Equal(360, series.Sum(p => p.PieSweepDegrees), 3);
        }

        [Fact]
        public void BuildSeries_SharePercentUsesTotal()
        {
            var snapshot = SnapshotWithTypes(("A", 1), ("B", 3));
            var series = _sut.BuildSeries(snapshot, null, AnalyticsChartSource.Types, AnalyticsChartKind.HorizontalBar, 10);

            Assert.Equal(75, series[0].SharePercent); // B first (count desc)
            Assert.Equal(25, series[1].SharePercent);
        }

        [Fact]
        public void BuildSeries_ZeroValues_AvoidDivideByZero_UsesMaxOne()
        {
            var snapshot = SnapshotWithTypes(("A", 0), ("B", 0));
            var series = _sut.BuildSeries(snapshot, null, AnalyticsChartSource.Types, AnalyticsChartKind.VerticalBar, 10);

            Assert.Equal(2, series.Count);
            Assert.Equal(0, series[0].BarWidth);
            Assert.Equal(0, series[1].BarWidth);
            // With total forced to 1, each zero value contributes 0 share
            Assert.Equal(0, series[0].SharePercent);
        }

        [Fact]
        public void BuildSeries_KindDoesNotChangeGeometry_CurrentBehavior()
        {
            var snapshot = SnapshotWithTypes(("A", 10), ("B", 20));
            var pie = _sut.BuildSeries(snapshot, null, AnalyticsChartSource.Types, AnalyticsChartKind.Pie, 10);
            var line = _sut.BuildSeries(snapshot, null, AnalyticsChartSource.Types, AnalyticsChartKind.Line, 10);

            Assert.Equal(pie.Select(p => p.BarWidth), line.Select(p => p.BarWidth));
            Assert.Equal(pie.Select(p => p.ColumnHeight), line.Select(p => p.ColumnHeight));
            Assert.Equal(pie.Select(p => p.PieSweepDegrees), line.Select(p => p.PieSweepDegrees));
        }

        [Fact]
        public void BuildTopCreators_AndResponsible_TopN()
        {
            var snapshot = new ProjectAnalyticsSnapshot
            {
                ObjectsByCreator = new List<CreatorCountRow>
                {
                    new CreatorCountRow { DisplayName = "Low", SampledCount = 1 },
                    new CreatorCountRow { DisplayName = "High", SampledCount = 9 }
                },
                ObjectsByResponsible = new List<ResponsibleCountRow>
                {
                    new ResponsibleCountRow { DisplayName = "R1", SampledCount = 2 },
                    new ResponsibleCountRow { DisplayName = "R2", SampledCount = 8 }
                }
            };

            var creators = _sut.BuildTopCreators(snapshot, 1);
            Assert.Single(creators);
            Assert.Equal("High", creators[0].Label);

            var responsible = _sut.BuildTopResponsible(snapshot, 1);
            Assert.Single(responsible);
            Assert.Equal("R2", responsible[0].Label);
        }

        [Fact]
        public void BuildStateSemantic_ReturnsAllSemantics_OrderedByCount()
        {
            var snapshot = new ProjectAnalyticsSnapshot
            {
                ObjectsByUserStateSemantic = new List<StateSemanticCountRow>
                {
                    new StateSemanticCountRow { Semantic = "OPEN", Count = 2 },
                    new StateSemanticCountRow { Semantic = "CLOSED", Count = 8 }
                }
            };

            var bars = _sut.BuildStateSemantic(snapshot);
            Assert.Equal(new[] { "CLOSED", "OPEN" }, bars.Select(b => b.Label).ToArray());
        }
    }
}
