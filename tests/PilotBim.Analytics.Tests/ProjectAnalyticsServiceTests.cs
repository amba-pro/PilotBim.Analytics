using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class ProjectAnalyticsServiceTests
    {
        private readonly ProjectAnalyticsService _sut = new ProjectAnalyticsService();

        private static readonly string[] ExpectedKpiOrder =
        {
            "Всего объектов",
            "Типов карточек",
            "BIM моделей",
            "Частей моделей",
            "BIM элементов (индекс)",
            "Пользователей",
            "Организаций",
            "Средний fill %",
            "Готовность аналитики"
        };

        [Fact]
        public void Build_NullReport_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _sut.Build(null));
        }

        [Fact]
        public void Build_EmptyReport_HasFixedKpiOrder_AndDefaults()
        {
            var report = new ProjectInventoryReport
            {
                GeneratedAt = new DateTime(2026, 4, 1, 12, 0, 0),
                ScanMode = "STANDARD",
                AnalyticsReadiness = CapabilityStatus.Partial,
                FinalStatus = "PARTIAL_RUNTIME_INVENTORY"
            };

            var snapshot = _sut.Build(report);

            Assert.Equal(ExpectedKpiOrder, snapshot.Summary.Select(k => k.Label).ToArray());
            Assert.Equal("0", snapshot.Summary.Single(k => k.Label == "Всего объектов").Value);
            Assert.Equal("0", snapshot.Summary.Single(k => k.Label == "Типов карточек").Value);
            Assert.Equal("0", snapshot.Summary.Single(k => k.Label == "BIM элементов (индекс)").Value);
            // fill uses current-culture ToString("0.0")
            var expectedFill = (0.0).ToString("0.0") + "%";
            Assert.Equal(expectedFill, snapshot.Summary.Single(k => k.Label == "Средний fill %").Value);
            Assert.Equal(CapabilityStatus.Partial, snapshot.Summary.Single(k => k.Label == "Готовность аналитики").Value);
            Assert.Equal("PARTIAL_RUNTIME_INVENTORY", snapshot.Summary.Single(k => k.Label == "Готовность аналитики").Detail);
            Assert.Equal("Inventory scan", snapshot.DataSource);
            Assert.Empty(snapshot.ObjectsByType);
            Assert.True(snapshot.Limitations.Count >= 3);
            Assert.Equal(string.Join(Environment.NewLine, snapshot.Limitations.Take(3)), snapshot.Notes);
        }

        [Fact]
        public void Build_SearchZonePass_SetsDataSource()
        {
            var report = new ProjectInventoryReport();
            report.ZoneResults.Add(new ZoneResult { Zone = "Search", Status = CapabilityStatus.Pass, Message = "ok" });
            Assert.Equal("ISearchService", _sut.Build(report).DataSource);
        }

        [Fact]
        public void Build_HierarchySearchZone_SetsDataSource()
        {
            var report = new ProjectInventoryReport();
            report.ZoneResults.Add(new ZoneResult
            {
                Zone = "Search",
                Status = CapabilityStatus.Partial,
                Message = "hierarchy walk visited=10"
            });
            Assert.Equal("Hierarchy walk", _sut.Build(report).DataSource);
        }

        [Fact]
        public void Build_TypeDistribution_ShareAndOrder_AndEstimateMarkerOnTotal()
        {
            var report = new ProjectInventoryReport
            {
                ObjectsFound = 100,
                TypesDiscovered = 2,
                Types =
                {
                    new TypeInventoryRecord { TypeId = 1, Title = "Small", ObjectCount = 25, FillPercent = 10, SampledCount = 5 },
                    new TypeInventoryRecord { TypeId = 2, Title = "Large", ObjectCount = 75, ObjectCountIsEstimate = true, FillPercent = 50, SampledCount = 5 }
                }
            };

            var snapshot = _sut.Build(report);
            Assert.Equal("100 (~)", snapshot.Summary.Single(k => k.Label == "Всего объектов").Value);
            Assert.Equal(2, snapshot.ObjectsByType.Count);
            Assert.Equal("Large", snapshot.ObjectsByType[0].TypeName);
            Assert.Equal(75, snapshot.ObjectsByType[0].SharePercent);
            Assert.Equal("Small", snapshot.ObjectsByType[1].TypeName);
            Assert.Equal(25, snapshot.ObjectsByType[1].SharePercent);
        }

        [Fact]
        public void Build_IndexedCount_TruncationTilde_CurrentBehavior()
        {
            var report = new ProjectInventoryReport
            {
                BimPartAnalytics = new List<BimPartAnalyticsRow>
                {
                    new BimPartAnalyticsRow { ElementCount = 1000, IsTruncated = true },
                    new BimPartAnalyticsRow { ElementCount = 500, IsTruncated = false }
                }
            };

            var snapshot = _sut.Build(report);
            Assert.Equal("1500 ~", snapshot.Summary.Single(k => k.Label == "BIM элементов (индекс)").Value);
        }

        [Fact]
        public void Build_AddsRemarksPer1000_WhenPositive()
        {
            var report = new ProjectInventoryReport
            {
                RemarkAnalytics = new RemarkAnalyticsSummary { RemarksPer1000Elements = 2.5 }
            };

            var snapshot = _sut.Build(report);
            var row = snapshot.Summary.Single(k => k.Label == "Замечаний / 1000 элементов");
            // current-culture ToString("0.0")
            Assert.Equal((2.5).ToString("0.0"), row.Value);
        }

        [Fact]
        public void Build_DoesNotAddRemarksPer1000_WhenZero()
        {
            var report = new ProjectInventoryReport
            {
                RemarkAnalytics = new RemarkAnalyticsSummary { RemarksPer1000Elements = 0 }
            };

            var snapshot = _sut.Build(report);
            Assert.DoesNotContain(snapshot.Summary, k => k.Label == "Замечаний / 1000 элементов");
        }

        [Fact]
        public void Build_AverageFill_UsesSampledTypesOnly_CurrentCultureFormat()
        {
            var report = new ProjectInventoryReport
            {
                Types =
                {
                    new TypeInventoryRecord { SampledCount = 0, FillPercent = 99 },
                    new TypeInventoryRecord { SampledCount = 3, FillPercent = 10 },
                    new TypeInventoryRecord { SampledCount = 3, FillPercent = 30 }
                }
            };

            var snapshot = _sut.Build(report);
            var expected = (20.0).ToString("0.0") + "%";
            Assert.Equal(expected, snapshot.Summary.Single(k => k.Label == "Средний fill %").Value);
        }
    }
}
