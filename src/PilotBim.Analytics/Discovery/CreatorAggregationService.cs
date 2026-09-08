using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Discovery
{
    /// <summary>
    /// Dedicated creator / created-date aggregation with a global object budget (search-based).
    /// </summary>
    internal sealed class CreatorAggregationService
    {
        public void Aggregate(
            ProjectInventoryReport report,
            PilotObjectScanner scanner,
            ScanMode mode,
            CancellationToken token,
            Action<string> progress)
        {
            report.CreatorFullCounts.Clear();
            report.CreatedMonthFullCounts.Clear();
            report.CreatorFullScanObjects = 0;
            report.CreatorCountsFromFullScan = false;

            if (!scanner.SearchAvailable)
                return;

            int budget = mode == ScanMode.Fast ? 500 : mode == ScanMode.Full ? 50000 : 5000;
            progress("Creator aggregation (budget=" + budget + ")...");

            var types = report.Types
                .Where(t => t.ObjectCount > 0)
                .OrderByDescending(t => t.ObjectCount)
                .ToList();

            var sampler = new PilotObjectSampler(scanner);
            int remaining = budget;

            foreach (var type in types)
            {
                if (token.IsCancellationRequested || remaining <= 0)
                    break;

                int take = (int)Math.Min(remaining, Math.Min(type.ObjectCount, mode == ScanMode.Full ? 10000L : 500L));
                if (take <= 0)
                    continue;

                var gate = new ManualResetEventSlim(false);
                int returned = 0;
                var abandoned = 0;
                sampler.SampleType(type.TypeId, take, (objects, total) =>
                {
                    try
                    {
                        if (!AsyncCallbackGuard.ShouldAccept(Interlocked.CompareExchange(ref abandoned, 0, 0)))
                            return;
                        var list = objects ?? new List<IDataObject>();
                        returned = list.Count;
                        foreach (var obj in list)
                        {
                            if (!AsyncCallbackGuard.ShouldAccept(Interlocked.CompareExchange(ref abandoned, 0, 0)))
                                return;
                            if (obj == null || obj.State != DataState.Loaded)
                                continue;
                            Record(report, obj);
                        }
                    }
                    finally
                    {
                        gate.Set();
                    }
                }, ex =>
                {
                    AnalyticsLogger.Warning("creator-agg", type.Name + " " + ex.Message);
                    gate.Set();
                });

                if (!gate.Wait(TimeSpan.FromSeconds(30)))
                    AsyncCallbackGuard.Abandon(ref abandoned);
                remaining = ReduceBudget(remaining, take, returned);
            }

            report.CreatorCountsFromFullScan = report.CreatorFullScanObjects > 0;
            report.ZoneResults.Add(new ZoneResult
            {
                Zone = "Creators",
                Status = report.CreatorCountsFromFullScan ? CapabilityStatus.Pass : CapabilityStatus.Partial,
                Message = "fullScanObjects=" + report.CreatorFullScanObjects + " budget=" + budget
            });
        }

        /// <summary>
        /// Budget accounting for one sample round-trip.
        /// Deducts objects actually returned, not the requested take size.
        /// </summary>
        internal static int ReduceBudget(int remaining, int requestedTake, int returnedCount)
        {
            _ = requestedTake; // retained for call-site clarity / future diagnostics
            if (returnedCount < 0)
                returnedCount = 0;
            if (returnedCount >= remaining)
                return 0;
            return remaining - returnedCount;
        }

        private static void Record(ProjectInventoryReport report, IDataObject obj)
        {
            report.CreatorFullScanObjects++;
            if (obj.Creator != null)
            {
                var id = obj.Creator.Id;
                if (report.CreatorFullCounts.ContainsKey(id))
                    report.CreatorFullCounts[id]++;
                else
                    report.CreatorFullCounts[id] = 1;
            }

            var monthKey = AnalyticsFormats.MonthBucketKey(obj.Created);
            if (report.CreatedMonthFullCounts.ContainsKey(monthKey))
                report.CreatedMonthFullCounts[monthKey]++;
            else
                report.CreatedMonthFullCounts[monthKey] = 1;
        }
    }
}
