using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PilotBim.Analytics.Export;
using PilotBim.Analytics.Models;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class AnalyticsCsvExporterTests : IDisposable
    {
        private string _exportFolder;

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(_exportFolder) && Directory.Exists(_exportFolder))
                Directory.Delete(_exportFolder, true);
        }

        [Fact]
        public void Export_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AnalyticsCsvExporter().Export(null));
        }

        [Fact]
        public void Export_WritesExpectedHeaders_Quoting_AndInvariantNumbers()
        {
            var stamp = new DateTime(2026, 3, 15, 14, 22, 33);
            var snapshot = new ProjectAnalyticsSnapshot
            {
                GeneratedAt = stamp,
                ObjectsByType = new List<TypeCountRow>
                {
                    new TypeCountRow
                    {
                        TypeName = "Name;with;semi",
                        TypeId = 7,
                        Count = 3,
                        IsEstimate = true,
                        SharePercent = 12.5,
                        FillPercent = 33.333
                    },
                    new TypeCountRow
                    {
                        TypeName = "Quoted \"here\"",
                        TypeId = 8,
                        Count = 1,
                        IsEstimate = false,
                        SharePercent = 0,
                        FillPercent = 100
                    }
                },
                ObjectsByCreator = new List<CreatorCountRow>(),
                ObjectsByCreatedMonth = new List<PeriodCountRow>(),
                ObjectsByUserState = new List<StateCountRow>(),
                BimModelAnalytics = new List<BimModelAnalyticsRow>(),
                BimPartAnalytics = new List<BimPartAnalyticsRow>(),
                BimElementTypeCounts = new List<BimElementTypeCountRow>(),
                ModelRemarks = new List<RemarkTypeRow>(),
                RemarkLinks = new List<RemarkLinkRow>(),
                ObjectsByResponsible = new List<ResponsibleCountRow>(),
                ObjectsByUserStateSemantic = new List<StateSemanticCountRow>(),
                ScanDiffRows = new List<ScanDiffRow>
                {
                    new ScanDiffRow
                    {
                        Area = "A",
                        Metric = "M",
                        Previous = "1",
                        Current = "2",
                        Delta = "+1",
                        Notes = "note;with;semi"
                    }
                },
                Summary = new List<AnalyticsKpiRow>
                {
                    new AnalyticsKpiRow { Label = "Всего объектов", Value = "4", Detail = null }
                },
                Limitations = new List<string> { "lim-a", "lim-b" }
            };

            _exportFolder = new AnalyticsCsvExporter().Export(snapshot);

            Assert.Equal(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PilotBim.Analytics", "Exports", "Analytics-20260315-142233"),
                _exportFolder);

            var typeCsv = File.ReadAllText(Path.Combine(_exportFolder, "objects_by_type.csv"), Encoding.UTF8);
            // UTF-8 BOM may be present (UTF8Encoding(true))
            var lines = typeCsv.TrimStart('\uFEFF').Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("TypeName;TypeId;Count;IsEstimate;SharePercent;FillPercent", lines[0]);
            Assert.Equal("\"Name;with;semi\";7;3;True;12.5;33.33", lines[1]);
            Assert.Equal("\"Quoted \"\"here\"\"\";8;1;False;0;100", lines[2]);

            var compare = File.ReadAllText(Path.Combine(_exportFolder, "scan_compare.csv"), Encoding.UTF8)
                .TrimStart('\uFEFF')
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("Area;Metric;Previous;Current;Delta;Notes", compare[0]);
            Assert.Equal("A;M;1;2;+1;\"note;with;semi\"", compare[1]);

            var summary = File.ReadAllText(Path.Combine(_exportFolder, "summary.csv"), Encoding.UTF8)
                .TrimStart('\uFEFF');
            Assert.Contains("Label;Value;Detail", summary);
            Assert.Contains("Всего объектов;4;", summary);
            Assert.Contains("Scan compare rows;1;", summary);
            Assert.Contains("Scan compare changed metrics;1;", summary);

            var limitations = File.ReadAllText(Path.Combine(_exportFolder, "limitations.txt"), Encoding.UTF8)
                .TrimStart('\uFEFF');
            Assert.Equal("lim-a" + Environment.NewLine + "lim-b", limitations.TrimEnd('\r', '\n'));

            // Number format uses InvariantCulture (dot), not current culture
            Assert.Contains("12.5", lines[1]);
            Assert.DoesNotContain("12,5", lines[1]);
            Assert.Equal("12.5", (12.5).ToString("0.##", CultureInfo.InvariantCulture));
        }

        [Fact]
        public void Export_EmptyCollections_StillWriteHeaderOnlyFiles()
        {
            var stamp = new DateTime(2026, 1, 2, 3, 4, 5);
            var snapshot = new ProjectAnalyticsSnapshot { GeneratedAt = stamp };
            _exportFolder = new AnalyticsCsvExporter().Export(snapshot);

            var creator = File.ReadAllText(Path.Combine(_exportFolder, "objects_by_creator.csv"), Encoding.UTF8)
                .TrimStart('\uFEFF')
                .TrimEnd();
            Assert.Equal("DisplayName;CreatorId;SampledCount;SharePercent;Scope", creator);
        }
    }
}
