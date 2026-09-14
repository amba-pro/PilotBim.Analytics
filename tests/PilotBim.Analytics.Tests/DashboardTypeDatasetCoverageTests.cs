using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardTypeDatasetCoverageTests
    {
        [Fact]
        public void EmptyType_IsComplete()
        {
            var dataset = DashboardTypeDatasetMaterializer.FromLoadedSources(
                12, 0, new DashboardObjectSource[0], DashboardTypeLoadOutcome.Succeeded, null);

            Assert.Equal(DashboardTypeCoverage.Complete, dataset.Coverage);
            Assert.Equal(0, dataset.ExpectedCount);
            Assert.Equal(0, dataset.LoadedUniqueCount);
            Assert.Empty(dataset.Rows);
        }

        [Fact]
        public void TotalN_UniqueN_IsComplete()
        {
            var objects = new[] { Obj(), Obj() };
            var dataset = DashboardTypeDatasetMaterializer.FromLoadedSources(
                3, 2, objects, DashboardTypeLoadOutcome.Succeeded, null);

            Assert.Equal(DashboardTypeCoverage.Complete, dataset.Coverage);
            Assert.Equal(2, dataset.LoadedUniqueCount);
            Assert.Equal(2, dataset.Rows.Count);
        }

        [Fact]
        public void TotalN_UniqueNMinus1_IsPartial()
        {
            var dataset = DashboardTypeDatasetMaterializer.FromLoadedSources(
                3, 2, new[] { Obj() }, DashboardTypeLoadOutcome.Succeeded, null);

            Assert.Equal(DashboardTypeCoverage.Partial, dataset.Coverage);
            Assert.Equal(1, dataset.LoadedUniqueCount);
            Assert.NotEqual(DashboardTypeCoverage.Complete, dataset.Coverage);
        }

        [Fact]
        public void DuplicateCallbacks_DoNotInflateUniqueCount()
        {
            var id = Guid.NewGuid();
            var first = Obj(id, 1);
            var second = Obj(id, 99);
            var dataset = DashboardTypeDatasetMaterializer.FromLoadedSources(
                1, 1, new[] { first, second, first }, DashboardTypeLoadOutcome.Succeeded, null);

            Assert.Equal(DashboardTypeCoverage.Complete, dataset.Coverage);
            Assert.Equal(1, dataset.LoadedUniqueCount);
            Assert.Single(dataset.Rows);
            Assert.Equal(1, dataset.Rows[0].TypeId);
        }

        [Fact]
        public void Failure_IsNotComplete()
        {
            var dataset = DashboardTypeDatasetMaterializer.FromLoadedSources(
                9, 1, new[] { Obj() }, DashboardTypeLoadOutcome.Failed, "failed");

            Assert.Equal(DashboardTypeCoverage.Failed, dataset.Coverage);
            Assert.NotEqual(DashboardTypeCoverage.Complete, dataset.Coverage);
        }

        [Fact]
        public void Timeout_IsNotComplete()
        {
            var withRows = DashboardTypeDatasetMaterializer.FromLoadedSources(
                9, 2, new[] { Obj() }, DashboardTypeLoadOutcome.TimedOut, null);
            var empty = DashboardTypeDatasetMaterializer.FromLoadedSources(
                9, 2, new DashboardObjectSource[0], DashboardTypeLoadOutcome.TimedOut, null);

            Assert.Equal(DashboardTypeCoverage.Partial, withRows.Coverage);
            Assert.Equal(DashboardTypeCoverage.Failed, empty.Coverage);
            Assert.NotEqual(DashboardTypeCoverage.Complete, withRows.Coverage);
            Assert.NotEqual(DashboardTypeCoverage.Complete, empty.Coverage);
        }

        [Fact]
        public void Cancellation_IsNotComplete()
        {
            var dataset = DashboardTypeDatasetMaterializer.FromLoadedSources(
                9, 1, new[] { Obj() }, DashboardTypeLoadOutcome.Cancelled, null);

            Assert.Equal(DashboardTypeCoverage.Failed, dataset.Coverage);
            Assert.NotEqual(DashboardTypeCoverage.Complete, dataset.Coverage);
        }

        [Fact]
        public void TotalBeyondInt32_NeverOverflows_AndIsNotComplete()
        {
            int maxResults;
            Assert.False(DashboardTypeDatasetAssembler.TryToMaxResults((long)int.MaxValue + 1, out maxResults));
            Assert.Equal(0, maxResults);
            Assert.False(DashboardTypeDatasetAssembler.TryToMaxResults(long.MaxValue, out maxResults));
            Assert.True(DashboardTypeDatasetAssembler.TryToMaxResults(int.MaxValue, out maxResults));
            Assert.Equal(int.MaxValue, maxResults);

            var dataset = DashboardTypeDatasetMaterializer.FromLoadedSources(
                4,
                (long)int.MaxValue + 1,
                null,
                DashboardTypeLoadOutcome.TotalExceedsInt32,
                null);

            Assert.Equal(DashboardTypeCoverage.Failed, dataset.Coverage);
            Assert.Equal((long)int.MaxValue + 1, dataset.ExpectedCount);
        }

        [Fact]
        public void SecondMaterialization_DoesNotContainFirstRows()
        {
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            var first = DashboardTypeDatasetMaterializer.FromLoadedSources(
                1, 1, new[] { Obj(firstId) }, DashboardTypeLoadOutcome.Succeeded, null);
            var second = DashboardTypeDatasetMaterializer.FromLoadedSources(
                1, 1, new[] { Obj(secondId) }, DashboardTypeLoadOutcome.Succeeded, null);

            Assert.Single(first.Rows);
            Assert.Equal(firstId, first.Rows[0].ObjectId);
            Assert.Single(second.Rows);
            Assert.Equal(secondId, second.Rows[0].ObjectId);
            Assert.DoesNotContain(firstId, UniqueIds(second));
        }

        [Fact]
        public void SampleBuffers_AreNeverUsed()
        {
            Assert.Equal(50, (int)ScanMode.Fast);
            Assert.Equal(200, (int)ScanMode.Standard);
            Assert.Equal(10000, (int)ScanMode.Full);
            Assert.Null(typeof(DashboardTypeDatasetMaterializer).GetField(
                "SampleLimit", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.Null(typeof(DashboardTypeDatasetMaterializer).GetProperty("SampleLimit"));
        }

        [Fact]
        public void MissingSearch_FailsWithoutClaimingComplete()
        {
            var dataset = new DashboardTypeDatasetMaterializer().Materialize(12, CancellationToken.None);
            Assert.Equal(DashboardTypeCoverage.Failed, dataset.Coverage);
            Assert.Empty(dataset.Rows);
        }

        [Fact]
        public void CallbackBeforeCompletion_IsAccepted()
        {
            using (var session = new CallbackWaitSession())
            {
                var collected = new Dictionary<Guid, DashboardObjectSource>();
                var source = Obj();
                var requested = new HashSet<Guid> { source.Id };
                Assert.True(DashboardTypeCallbackCollector.TryAdd(session, requested, collected, source));
                session.SignalCompleted();
                Assert.Equal(CallbackWaitStatus.Completed, session.Wait(TimeSpan.FromSeconds(1)).Status);
                Assert.True(collected.ContainsKey(source.Id));
            }
        }

        [Fact]
        public void LateCallbackAfterTimeout_IsIgnored()
        {
            using (var session = new CallbackWaitSession())
            {
                Assert.Equal(CallbackWaitStatus.TimedOut, session.Wait(TimeSpan.FromMilliseconds(1)).Status);
                var collected = new Dictionary<Guid, DashboardObjectSource>();
                var source = Obj();
                Assert.False(DashboardTypeCallbackCollector.TryAdd(
                    session, new HashSet<Guid> { source.Id }, collected, source));
                Assert.Empty(collected);
            }
        }

        [Fact]
        public void LateCallbackAfterCancellation_IsIgnored()
        {
            using (var session = new CallbackWaitSession())
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.Equal(CallbackWaitStatus.Cancelled, session.Wait(TimeSpan.FromSeconds(1), cts.Token).Status);
                var collected = new Dictionary<Guid, DashboardObjectSource>();
                var source = Obj();
                Assert.False(DashboardTypeCallbackCollector.TryAdd(
                    session, new HashSet<Guid> { source.Id }, collected, source));
                Assert.Empty(collected);
            }
        }

        [Fact]
        public void DoubleCallback_DoesNotDuplicateRows_FirstWins()
        {
            using (var session = new CallbackWaitSession())
            {
                var id = Guid.NewGuid();
                var first = Obj(id, 1);
                var second = Obj(id, 2);
                var collected = new Dictionary<Guid, DashboardObjectSource>();
                var requested = new HashSet<Guid> { id };
                Assert.True(DashboardTypeCallbackCollector.TryAdd(session, requested, collected, first));
                Assert.False(DashboardTypeCallbackCollector.TryAdd(session, requested, collected, second));
                Assert.Single(collected);
                Assert.Equal(1, collected[id].TypeId);
            }
        }

        private static IEnumerable<Guid> UniqueIds(DashboardTypeDataset dataset)
        {
            var list = new List<Guid>();
            foreach (var row in dataset.Rows)
                list.Add(row.ObjectId);
            return list;
        }

        private static DashboardObjectSource Obj(Guid? id = null, int typeId = 1)
        {
            return new DashboardObjectSource
            {
                Id = id ?? Guid.NewGuid(),
                TypeId = typeId
            };
        }
    }
}
