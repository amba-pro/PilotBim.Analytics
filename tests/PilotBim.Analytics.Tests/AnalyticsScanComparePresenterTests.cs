using System;
using System.Collections.Generic;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using PilotBim.Analytics.ViewModels;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class AnalyticsScanComparePresenterTests
    {
        [Fact]
        public void IsMeaningfulChange_ZeroDelta_IsFalse()
        {
            Assert.False(AnalyticsScanComparePresenter.IsMeaningfulChange(new ScanDiffRow
            {
                Area = "KPI",
                Metric = "Всего",
                Delta = "0"
            }));
            Assert.False(AnalyticsScanComparePresenter.IsMeaningfulChange(new ScanDiffRow
            {
                Area = "KPI",
                Metric = "Всего",
                Delta = "+0"
            }));
            Assert.False(AnalyticsScanComparePresenter.IsMeaningfulChange(new ScanDiffRow
            {
                Area = "KPI",
                Metric = "Всего",
                Delta = "n/a"
            }));
        }

        [Fact]
        public void IsMeaningfulChange_ScanTimeAndError_AreTrue()
        {
            Assert.True(AnalyticsScanComparePresenter.IsMeaningfulChange(new ScanDiffRow
            {
                Area = "Скан",
                Metric = "Время скана",
                Delta = "0"
            }));
            Assert.True(AnalyticsScanComparePresenter.IsMeaningfulChange(new ScanDiffRow
            {
                Area = "Ошибка",
                Metric = "Сравнение сканов",
                Delta = "n/a"
            }));
        }

        [Fact]
        public void CompareWithSelectedHistory_WithoutBaseline_SetsHint()
        {
            var notified = new List<string>();
            var presenter = new AnalyticsScanComparePresenter(
                new ScanSnapshotStore(),
                new ScanDiffService(),
                name => notified.Add(name));

            presenter.CompareWithSelectedHistory(null);

            Assert.Equal("Нет текущего скана — нажмите «Обновить».", presenter.ScanDiffHint);
            Assert.Contains(nameof(AnalyticsScanComparePresenter.ScanDiffHint), notified);
        }

        [Fact]
        public void CompareWithSelectedHistory_WithoutSelection_SetsHint()
        {
            var presenter = new AnalyticsScanComparePresenter(
                new ScanSnapshotStore(),
                new ScanDiffService(),
                _ => { });

            var snapshot = new ProjectAnalyticsSnapshot
            {
                GeneratedAt = DateTime.Now,
                ScanMode = "STANDARD",
                Summary = new List<AnalyticsKpiRow>()
            };

            // Captures baseline from snapshot, then stops on null selection — no disk write.
            presenter.CompareWithSelectedHistory(snapshot);

            Assert.Equal("Выберите снимок в списке истории.", presenter.ScanDiffHint);
        }

        [Fact]
        public void SaveNamedScan_WithoutBaseline_ReturnsFalse()
        {
            var presenter = new AnalyticsScanComparePresenter(
                new ScanSnapshotStore(),
                new ScanDiffService(),
                _ => { });

            Assert.False(presenter.SaveNamedScan("test", null));
            Assert.Equal("Нет текущего скана — нажмите «Обновить».", presenter.ScanDiffHint);
        }
    }
}
