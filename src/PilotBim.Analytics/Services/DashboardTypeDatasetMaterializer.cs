using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Loads every object of one TypeId via search Total + SubscribeObjects.
    /// Separate from inventory sampling. Not a widget operation.
    /// </summary>
    internal sealed class DashboardTypeDatasetMaterializer
    {
        internal static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(30);
        internal static readonly TimeSpan SubscribeTimeout = TimeSpan.FromSeconds(30);

        private readonly PilotObjectScanner _scanner;

        public DashboardTypeDatasetMaterializer(IObjectsRepository repository, ISearchService search)
        {
            _scanner = new PilotObjectScanner(repository, search);
        }

        /// <summary>Test seam: search/load without Pilot.</summary>
        internal DashboardTypeDatasetMaterializer()
        {
            _scanner = null;
        }

        public DashboardTypeDataset Materialize(int typeId, CancellationToken token)
        {
            var sw = Stopwatch.StartNew();
            if (_scanner == null || !_scanner.SearchAvailable)
            {
                var failed = DashboardTypeDatasetAssembler.Assemble(
                    typeId, -1, null, DashboardTypeLoadOutcome.Failed, "ISearchService unavailable");
                Log(failed, sw.Elapsed);
                return failed;
            }

            if (token.IsCancellationRequested)
            {
                var cancelled = DashboardTypeDatasetAssembler.Assemble(
                    typeId, -1, null, DashboardTypeLoadOutcome.Cancelled, "cancelled");
                Log(cancelled, sw.Elapsed);
                return cancelled;
            }

            long expected;
            IReadOnlyList<Guid> probeIds;
            var probe = Search(typeId, 1, token, out probeIds, out expected);
            if (probe != DashboardTypeLoadOutcome.Succeeded)
            {
                var bad = DashboardTypeDatasetAssembler.Assemble(typeId, expected, null, probe, Describe(probe));
                Log(bad, sw.Elapsed);
                return bad;
            }

            if (expected < 0)
            {
                var bad = DashboardTypeDatasetAssembler.Assemble(
                    typeId, expected, null, DashboardTypeLoadOutcome.Failed, "search Total unavailable");
                Log(bad, sw.Elapsed);
                return bad;
            }

            if (expected == 0)
            {
                var empty = DashboardTypeDatasetAssembler.Assemble(
                    typeId, 0, new DashboardObjectSource[0], DashboardTypeLoadOutcome.Succeeded, "complete");
                Log(empty, sw.Elapsed);
                return empty;
            }

            int maxResults;
            if (!DashboardTypeDatasetAssembler.TryToMaxResults(expected, out maxResults))
            {
                var overflow = DashboardTypeDatasetAssembler.Assemble(
                    typeId, expected, null, DashboardTypeLoadOutcome.TotalExceedsInt32, null);
                Log(overflow, sw.Elapsed);
                return overflow;
            }

            IReadOnlyList<Guid> ids;
            if (expected == 1 && probeIds != null && UniqueCount(probeIds) == 1)
            {
                ids = probeIds;
            }
            else
            {
                var full = Search(typeId, maxResults, token, out ids, out expected);
                if (full != DashboardTypeLoadOutcome.Succeeded)
                {
                    var bad = DashboardTypeDatasetAssembler.Assemble(typeId, expected, null, full, Describe(full));
                    Log(bad, sw.Elapsed);
                    return bad;
                }
            }

            var uniqueIds = UniqueGuids(ids);
            List<DashboardObjectSource> loaded;
            var loadOutcome = Subscribe(typeId, uniqueIds, token, out loaded);
            var dataset = DashboardTypeDatasetAssembler.Assemble(
                typeId,
                expected,
                loaded,
                loadOutcome,
                loadOutcome == DashboardTypeLoadOutcome.Succeeded && loaded.Count != expected
                    ? "loaded unique " + loaded.Count + " != expected " + expected
                    : Describe(loadOutcome));
            Log(dataset, sw.Elapsed);
            return dataset;
        }

        /// <summary>Pure coverage tests without SDK.</summary>
        internal static DashboardTypeDataset FromLoadedSources(
            int typeId,
            long expectedCount,
            IEnumerable<DashboardObjectSource> objects,
            DashboardTypeLoadOutcome outcome,
            string reason)
        {
            return DashboardTypeDatasetAssembler.Assemble(typeId, expectedCount, objects, outcome, reason);
        }

        private DashboardTypeLoadOutcome Search(
            int typeId,
            int maxResults,
            CancellationToken token,
            out IReadOnlyList<Guid> ids,
            out long total)
        {
            ids = new List<Guid>();
            total = -1;
            IReadOnlyList<Guid> capturedIds = new List<Guid>();
            long capturedTotal = -1;

            using (var session = new CallbackWaitSession())
            {
                _scanner.SearchByType(typeId, maxResults, (resultIds, resultTotal) =>
                {
                    if (!session.ShouldAccept())
                        return;
                    capturedIds = resultIds ?? new List<Guid>();
                    capturedTotal = resultTotal;
                    session.SignalCompleted();
                }, ex =>
                {
                    if (!session.ShouldAccept())
                        return;
                    session.SignalFailed(ex);
                });

                var wait = session.Wait(SearchTimeout, token);
                ids = capturedIds;
                total = capturedTotal;
                return MapWait(wait);
            }
        }

        private DashboardTypeLoadOutcome Subscribe(
            int typeId,
            IReadOnlyList<Guid> ids,
            CancellationToken token,
            out List<DashboardObjectSource> loaded)
        {
            loaded = new List<DashboardObjectSource>();
            if (ids == null || ids.Count == 0)
                return DashboardTypeLoadOutcome.Succeeded;

            var requested = new HashSet<Guid>(ids);
            var remaining = new HashSet<Guid>(ids);
            var collected = new Dictionary<Guid, DashboardObjectSource>();

            using (var session = new CallbackWaitSession())
            {
                _scanner.SubscribeObjects(ids, obj =>
                {
                    if (obj == null || obj.State != DataState.Loaded)
                        return;
                    var source = DashboardObjectSourceAdapter.FromDataObject(obj, typeId);
                    if (!DashboardTypeCallbackCollector.TryAdd(session, requested, collected, source))
                        return;
                    remaining.Remove(obj.Id);
                    if (remaining.Count == 0)
                        session.SignalCompleted();
                }, () =>
                {
                    if (!session.ShouldAccept())
                        return;
                    session.SignalCompleted();
                }, ex =>
                {
                    if (!session.ShouldAccept())
                        return;
                    session.SignalFailed(ex);
                });

                var wait = session.Wait(SubscribeTimeout, token);
                loaded = new List<DashboardObjectSource>(collected.Values);
                return MapWait(wait);
            }
        }

        private static DashboardTypeLoadOutcome MapWait(CallbackWaitResult wait)
        {
            if (wait == null)
                return DashboardTypeLoadOutcome.Failed;
            switch (wait.Status)
            {
                case CallbackWaitStatus.Completed:
                    return DashboardTypeLoadOutcome.Succeeded;
                case CallbackWaitStatus.TimedOut:
                    return DashboardTypeLoadOutcome.TimedOut;
                case CallbackWaitStatus.Cancelled:
                    return DashboardTypeLoadOutcome.Cancelled;
                default:
                    return DashboardTypeLoadOutcome.Failed;
            }
        }

        private static string Describe(DashboardTypeLoadOutcome outcome)
        {
            switch (outcome)
            {
                case DashboardTypeLoadOutcome.Succeeded:
                    return null;
                case DashboardTypeLoadOutcome.TimedOut:
                    return "timeout";
                case DashboardTypeLoadOutcome.Cancelled:
                    return "cancelled";
                case DashboardTypeLoadOutcome.TotalExceedsInt32:
                    return "expected count exceeds Int32 MaxResults";
                default:
                    return "failed";
            }
        }

        private static int UniqueCount(IReadOnlyList<Guid> ids)
        {
            return UniqueGuids(ids).Count;
        }

        private static List<Guid> UniqueGuids(IReadOnlyList<Guid> ids)
        {
            var list = new List<Guid>();
            if (ids == null)
                return list;
            var seen = new HashSet<Guid>();
            foreach (var id in ids)
            {
                if (id == Guid.Empty)
                    continue;
                if (seen.Add(id))
                    list.Add(id);
            }
            return list;
        }

        private static void Log(DashboardTypeDataset dataset, TimeSpan elapsed)
        {
            if (dataset == null)
                return;
            AnalyticsLogger.Info(
                "dashboard-type-dataset",
                "TypeId=" + dataset.TypeId
                + " Expected=" + dataset.ExpectedCount
                + " Loaded=" + dataset.LoadedUniqueCount
                + " Coverage=" + dataset.Coverage
                + " FieldValues=" + dataset.FieldValueCount
                + " DurationMs=" + (int)elapsed.TotalMilliseconds);
        }
    }
}
