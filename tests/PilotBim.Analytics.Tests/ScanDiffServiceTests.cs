using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class ScanDiffServiceTests
    {
        private readonly ScanDiffService _sut = new ScanDiffService();

        private static StoredScanBaseline Baseline(
            DateTime at,
            string mode = "STANDARD",
            long elements = 100,
            long models = 2,
            long parts = 4)
        {
            return new StoredScanBaseline
            {
                GeneratedAt = at,
                ScanMode = mode,
                BimElementCount = elements,
                BimModelCount = models,
                BimPartCount = parts,
                Kpis = new List<StoredKpi>(),
                Responsible = new List<StoredResponsible>(),
                TopTypes = new List<StoredNamedCount>(),
                StateSemantic = new List<StoredNamedCount>(),
                TopIfcTypes = new List<StoredNamedCount>(),
                Remarks = new List<StoredNamedCount>()
            };
        }

        [Fact]
        public void Capture_NullSnapshot_ReturnsNull()
        {
            Assert.Null(_sut.Capture(null));
        }

        [Fact]
        public void Capture_TakesTopTypesOrderedAndLimitedTo15()
        {
            var snapshot = new ProjectAnalyticsSnapshot
            {
                GeneratedAt = new DateTime(2026, 1, 1, 12, 0, 0),
                ScanMode = "FAST",
                ObjectsByType = Enumerable.Range(1, 20)
                    .Select(i => new TypeCountRow { TypeName = "T" + i, Count = i })
                    .ToList(),
                Summary = new List<AnalyticsKpiRow>
                {
                    new AnalyticsKpiRow { Label = "Всего объектов", Value = "42" },
                    new AnalyticsKpiRow { Label = "Пользователей", Value = "7" }
                }
            };

            var captured = _sut.Capture(snapshot);
            Assert.Equal(15, captured.TopTypes.Count);
            Assert.Equal("T20", captured.TopTypes[0].Name);
            Assert.Equal(20, captured.TopTypes[0].Count);
            Assert.Equal("T6", captured.TopTypes[14].Name);

            Assert.Equal(2, captured.Kpis.Count);
            Assert.Equal("Всего объектов", captured.Kpis[0].Label);
            Assert.Equal(42, captured.Kpis[0].NumericValue);
        }

        [Fact]
        public void Diff_NullCurrent_ReturnsEmpty()
        {
            Assert.Empty(_sut.Diff(Baseline(DateTime.Now), null));
        }

        [Fact]
        public void Diff_NullPrevious_ReturnsFirstScanRow()
        {
            var current = Baseline(new DateTime(2026, 5, 1, 10, 30, 0));
            var rows = _sut.Diff(null, current);

            Assert.Single(rows);
            Assert.Equal("Скан", rows[0].Area);
            Assert.Equal("Базовый snapshot", rows[0].Metric);
            Assert.Equal("n/a", rows[0].Delta);
            Assert.Equal(current.GeneratedAt.ToString("g"), rows[0].Current);
        }

        [Fact]
        public void Diff_IdenticalBaselines_EmitsTimeAndBimRowsWithZeroDelta()
        {
            var t0 = new DateTime(2026, 5, 1, 10, 0, 0);
            var t1 = new DateTime(2026, 5, 1, 11, 0, 0);
            var prev = Baseline(t0, elements: 50, models: 1, parts: 2);
            var curr = Baseline(t1, elements: 50, models: 1, parts: 2);

            var rows = _sut.Diff(prev, curr);

            Assert.Equal(4, rows.Count);
            Assert.Equal("Время скана", rows[0].Metric);
            Assert.Equal("1.0 h", rows[0].Delta);
            Assert.Equal("STANDARD → STANDARD", rows[0].Notes);

            Assert.All(rows.Skip(1), r => Assert.Equal("0", r.Delta));
            Assert.Equal(new[] { "BIM элементов (индекс)", "BIM моделей", "Частей моделей" },
                rows.Skip(1).Select(r => r.Metric).ToArray());
        }

        [Fact]
        public void Diff_ChangedElements_ShowsPositiveDelta()
        {
            var prev = Baseline(new DateTime(2026, 5, 1), elements: 100);
            var curr = Baseline(new DateTime(2026, 5, 2), elements: 150);
            var rows = _sut.Diff(prev, curr);

            var el = rows.Single(r => r.Metric == "BIM элементов (индекс)");
            Assert.Equal("100", el.Previous);
            Assert.Equal("150", el.Current);
            Assert.Equal("+50", el.Delta);
        }

        [Fact]
        public void Diff_NamedCounts_MarksAddedRemovedAndChanged()
        {
            var prev = Baseline(new DateTime(2026, 5, 1));
            prev.TopTypes = new List<StoredNamedCount>
            {
                new StoredNamedCount { Name = "Keep", Count = 10 },
                new StoredNamedCount { Name = "Gone", Count = 5 }
            };

            var curr = Baseline(new DateTime(2026, 5, 2));
            curr.TopTypes = new List<StoredNamedCount>
            {
                new StoredNamedCount { Name = "Keep", Count = 12 },
                new StoredNamedCount { Name = "New", Count = 3 }
            };

            var rows = _sut.Diff(prev, curr);
            var typeRows = rows.Where(r => r.Area == "Типы").OrderBy(r => r.Metric).ToList();

            Assert.Equal(3, typeRows.Count);

            var gone = typeRows.Single(r => r.Metric == "Gone");
            Assert.Equal("выпал из top", gone.Notes);
            Assert.Equal("-5", gone.Delta);

            var keep = typeRows.Single(r => r.Metric == "Keep");
            Assert.Null(keep.Notes);
            Assert.Equal("+2", keep.Delta);

            var added = typeRows.Single(r => r.Metric == "New");
            Assert.Equal("новый в top", added.Notes);
            Assert.Equal("+3", added.Delta);
        }

        [Fact]
        public void Diff_Responsible_NewAndRemoved()
        {
            var prev = Baseline(new DateTime(2026, 5, 1));
            prev.Responsible.Add(new StoredResponsible
            {
                PersonId = 1,
                DisplayName = "Alice",
                SampledCount = 4
            });

            var curr = Baseline(new DateTime(2026, 5, 2));
            curr.Responsible.Add(new StoredResponsible
            {
                OrgUnitId = 9,
                DisplayName = "OrgOnly",
                SampledCount = 2
            });

            var rows = _sut.Diff(prev, curr);
            var resp = rows.Where(r => r.Area == "Ответственные").ToList();
            Assert.Equal(2, resp.Count);
            Assert.Contains(resp, r => r.Metric == "Alice" && r.Notes == "исчез из сэмпла");
            Assert.Contains(resp, r => r.Metric == "OrgOnly" && r.Notes == "новый");
        }

        [Fact]
        public void Diff_UnchangedNamedCount_Omitted()
        {
            var prev = Baseline(new DateTime(2026, 5, 1));
            prev.StateSemantic.Add(new StoredNamedCount { Name = "OPEN", Count = 3 });
            var curr = Baseline(new DateTime(2026, 5, 2));
            curr.StateSemantic.Add(new StoredNamedCount { Name = "OPEN", Count = 3 });

            var rows = _sut.Diff(prev, curr);
            Assert.DoesNotContain(rows, r => r.Area == "OPEN/CLOSED");
        }
    }
}
