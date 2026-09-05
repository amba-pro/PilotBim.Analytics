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
                sampler.SampleType(type.TypeId, take, (objects, total) =>
                {
                    try
                    {
                        foreach (var obj in objects ?? new List<IDataObject>())
                        {
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

                gate.Wait(TimeSpan.FromSeconds(30));
                remaining -= take;
            }

            report.CreatorCountsFromFullScan = report.CreatorFullScanObjects > 0;
            report.ZoneResults.Add(new ZoneResult
            {
                Zone = "Creators",
                Status = report.CreatorCountsFromFullScan ? CapabilityStatus.Pass : CapabilityStatus.Partial,
                Message = "fullScanObjects=" + report.CreatorFullScanObjects + " budget=" + budget
            });
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

            var monthKey = obj.Created.ToString("yyyy-MM");
            if (report.CreatedMonthFullCounts.ContainsKey(monthKey))
                report.CreatedMonthFullCounts[monthKey]++;
            else
                report.CreatedMonthFullCounts[monthKey] = 1;
        }
    }
}
