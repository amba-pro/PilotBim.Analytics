using System;
using System.Collections.Generic;
using PilotBim.Analytics.Discovery;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class HistoryDiscoveryServiceTests
    {
        [Fact]
        public void SnapshotHistoryRequestIds_IsIndependentCopy_NotSharedWithSource()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var source = new List<Guid> { a, b, Guid.NewGuid() };

            var snapshot = HistoryDiscoveryService.SnapshotHistoryRequestIds(source, maxEvents: 2);
            Assert.Equal(2, snapshot.Count);
            Assert.Equal(a, snapshot[0]);
            Assert.Equal(b, snapshot[1]);

            source.Clear();
            Assert.Equal(2, snapshot.Count);
            Assert.Equal(a, snapshot[0]);
        }

        [Fact]
        public void SnapshotHistoryRequestIds_NullOrEmptyMax_ReturnsEmpty()
        {
            Assert.Empty(HistoryDiscoveryService.SnapshotHistoryRequestIds(null, 10));
            Assert.Empty(HistoryDiscoveryService.SnapshotHistoryRequestIds(new[] { Guid.NewGuid() }, 0));
        }

        [Fact]
        public void ShouldAcceptHistoryCallback_RejectsAbandonedLateCallbacks()
        {
            Assert.True(HistoryDiscoveryService.ShouldAcceptHistoryCallback(abandoned: false));
            Assert.False(HistoryDiscoveryService.ShouldAcceptHistoryCallback(abandoned: true));
        }
    }
}
