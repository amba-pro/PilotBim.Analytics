using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    /// <summary>
    /// Characterization against LocalAppData store with isolated entries and cleanup.
    /// Does not fill the ring buffer to MaxHistory (would risk dropping user history).
    /// </summary>
    public sealed class ScanSnapshotStoreTests : IDisposable
    {
        private readonly ScanSnapshotStore _sut = new ScanSnapshotStore();
        private readonly string _marker = "__STAGE2_TEST__" + Guid.NewGuid().ToString("N");
        private string _createdHistoryId;
        private StoredScanBaseline _previousLast;
        private bool _hadPreviousLast;

        public ScanSnapshotStoreTests()
        {
            _previousLast = _sut.TryLoadLast();
            _hadPreviousLast = _previousLast != null;
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(_createdHistoryId))
                _sut.DeleteHistory(_createdHistoryId);

            // Restore previous last-scan.json when possible
            if (_hadPreviousLast)
                _sut.SaveLast(_previousLast);
            else
            {
                var lastPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PilotBim.Analytics", "Snapshots", "last-scan.json");
                if (File.Exists(lastPath))
                    File.Delete(lastPath);
            }
        }

        private static StoredScanBaseline MakeBaseline(DateTime at, string mode = "STANDARD")
        {
            return new StoredScanBaseline
            {
                GeneratedAt = at,
                ScanMode = mode,
                BimElementCount = 11,
                BimModelCount = 1,
                BimPartCount = 2
            };
        }

        [Fact]
        public void MaxHistory_Is40_CurrentRingBufferLimit()
        {
            var field = typeof(ScanSnapshotStore).GetField("MaxHistory",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
            Assert.Equal(40, (int)field.GetValue(null));
        }

        [Fact]
        public void TryLoadLast_WhenMissingOrAfterSave_RoundTrips()
        {
            var baseline = MakeBaseline(new DateTime(2026, 6, 1, 8, 15, 0), "FAST");
            baseline.Name = _marker + "-last";

            _sut.SaveLast(baseline);
            var loaded = _sut.TryLoadLast();

            Assert.NotNull(loaded);
            Assert.Equal("last", loaded.Id); // SaveLast forces Id=last when blank
            Assert.Equal(_marker + "-last", loaded.Name);
            Assert.Equal("FAST", loaded.ScanMode);
            Assert.Equal(11, loaded.BimElementCount);
            Assert.Equal(new DateTime(2026, 6, 1, 8, 15, 0), loaded.GeneratedAt);
        }

        [Fact]
        public void ArchiveToHistory_InsertsAtFront_LoadById_Delete()
        {
            var baseline = MakeBaseline(new DateTime(2026, 6, 2, 9, 0, 0));
            var id = _sut.ArchiveToHistory(baseline, _marker + "-hist");
            _createdHistoryId = id;

            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.NotEqual("last", id);

            var loaded = _sut.TryLoadById(id);
            Assert.NotNull(loaded);
            Assert.Equal(id, loaded.Id);
            Assert.Equal(_marker + "-hist", loaded.Name);
            Assert.Equal(11, loaded.BimElementCount);

            var history = _sut.ListHistory();
            Assert.Contains(history, e => e.Id == id);

            // Newest first by GeneratedAt
            if (history.Count >= 2)
                Assert.True(history[0].GeneratedAt >= history[1].GeneratedAt);

            Assert.True(_sut.DeleteHistory(id));
            Assert.Null(_sut.TryLoadById(id));
            _createdHistoryId = null;
        }

        [Fact]
        public void TryLoadById_LastAlias_LoadsLastScan()
        {
            var baseline = MakeBaseline(new DateTime(2026, 6, 3, 10, 0, 0));
            baseline.Name = _marker + "-alias";
            _sut.SaveLast(baseline);

            var viaAlias = _sut.TryLoadById("last");
            Assert.NotNull(viaAlias);
            Assert.Equal(_marker + "-alias", viaAlias.Name);
        }

        [Fact]
        public void DeleteHistory_RejectsLastAndBlank()
        {
            Assert.False(_sut.DeleteHistory("last"));
            Assert.False(_sut.DeleteHistory(""));
            Assert.False(_sut.DeleteHistory(null));
        }

        [Fact]
        public void ArchiveToHistory_Null_ReturnsNull()
        {
            Assert.Null(_sut.ArchiveToHistory(null, "x"));
        }

        [Fact]
        public void ArchiveToHistory_BlankName_UsesGeneratedAtFallback()
        {
            var at = new DateTime(2026, 7, 4, 15, 30, 0);
            var id = _sut.ArchiveToHistory(MakeBaseline(at), "   ");
            _createdHistoryId = id;

            var loaded = _sut.TryLoadById(id);
            Assert.Equal("Скан " + at.ToString("yyyy-MM-dd HH:mm"), loaded.Name);
        }
    }
}
