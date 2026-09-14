using System;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardTypeMaterializerCanaryTests
    {
        [Fact]
        public void MissingTypeId_IsDisabled()
        {
            var settings = DashboardCanarySettings.Parse(null, null);
            Assert.False(settings.IsEnabled);
            Assert.False(settings.HasInvalidTypeId);
            Assert.Null(settings.CancelAfterMs);

            settings = DashboardCanarySettings.Parse("  ", "500");
            Assert.False(settings.IsEnabled);
        }

        [Fact]
        public void InvalidTypeId_DoesNotRun()
        {
            var settings = DashboardCanarySettings.Parse("abc", null);
            Assert.True(settings.IsEnabled);
            Assert.True(settings.HasInvalidTypeId);
            Assert.Equal(0, settings.TypeId);

            settings = DashboardCanarySettings.Parse("0", null);
            Assert.True(settings.HasInvalidTypeId);

            settings = DashboardCanarySettings.Parse("-3", null);
            Assert.True(settings.HasInvalidTypeId);
        }

        [Fact]
        public void ValidTypeId_IsParsed()
        {
            var settings = DashboardCanarySettings.Parse(" 12 ", null);
            Assert.True(settings.IsEnabled);
            Assert.False(settings.HasInvalidTypeId);
            Assert.Equal(12, settings.TypeId);
        }

        [Fact]
        public void CancelMs_ParsedSafely()
        {
            Assert.Equal(500, DashboardCanarySettings.Parse("12", "500").CancelAfterMs);
            Assert.Equal(500, DashboardCanarySettings.Parse("12", " 500 ").CancelAfterMs);
            Assert.Null(DashboardCanarySettings.Parse("12", "0").CancelAfterMs);
            Assert.Null(DashboardCanarySettings.Parse("12", "-1").CancelAfterMs);
            Assert.Null(DashboardCanarySettings.Parse("12", "nope").CancelAfterMs);
            Assert.Null(DashboardCanarySettings.Parse("12", "").CancelAfterMs);
        }

        [Fact]
        public void Gate_IsOneShot()
        {
            var gate = new DashboardCanaryGate();
            Assert.True(gate.TryBegin());
            Assert.False(gate.TryBegin());
            Assert.False(gate.TryBegin());
        }

        [Fact]
        public void Classify_Complete_IsPass()
        {
            var dataset = Dataset(DashboardTypeCoverage.Complete, expected: 2, loaded: 2, rows: 2, "complete");
            Assert.Equal(DashboardCanaryClassifier.Pass, DashboardCanaryClassifier.Classify(dataset));
        }

        [Fact]
        public void Classify_Partial_IsPartial()
        {
            var dataset = Dataset(DashboardTypeCoverage.Partial, 4, 3, 3, "loaded unique 3 != expected 4");
            Assert.Equal(DashboardCanaryClassifier.Partial, DashboardCanaryClassifier.Classify(dataset));
        }

        [Fact]
        public void Classify_Failed_IsFailed()
        {
            var dataset = Dataset(DashboardTypeCoverage.Failed, 4, 0, 0, "failed");
            Assert.Equal(DashboardCanaryClassifier.Failed, DashboardCanaryClassifier.Classify(dataset));
            Assert.Equal(DashboardCanaryClassifier.Failed, DashboardCanaryClassifier.Classify(null));
        }

        [Fact]
        public void Classify_CancelledAndTimeout()
        {
            Assert.Equal(
                DashboardCanaryClassifier.Cancelled,
                DashboardCanaryClassifier.Classify(Dataset(DashboardTypeCoverage.Failed, -1, 0, 0, "cancelled")));
            Assert.Equal(
                DashboardCanaryClassifier.Timeout,
                DashboardCanaryClassifier.Classify(Dataset(DashboardTypeCoverage.Partial, 10, 2, 2, "timeout")));
        }

        [Fact]
        public void Classify_CompleteWithMismatchedCounts_IsFailed()
        {
            var dataset = Dataset(DashboardTypeCoverage.Complete, 2, 2, 1, "complete");
            Assert.Equal(DashboardCanaryClassifier.Failed, DashboardCanaryClassifier.Classify(dataset));
        }

        private static DashboardTypeDataset Dataset(
            DashboardTypeCoverage coverage,
            long expected,
            int loaded,
            int rows,
            string reason)
        {
            var list = new DashboardObjectRow[rows];
            for (var i = 0; i < rows; i++)
                list[i] = new DashboardObjectRow(Guid.NewGuid(), 1, null, null);
            return new DashboardTypeDataset(1, expected, loaded, coverage, reason, list, 0, 0);
        }
    }
}
