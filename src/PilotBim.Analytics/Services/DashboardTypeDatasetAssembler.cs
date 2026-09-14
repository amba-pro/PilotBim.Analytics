using System;
using System.Collections.Generic;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    internal enum DashboardTypeLoadOutcome
    {
        Succeeded = 0,
        TimedOut = 1,
        Cancelled = 2,
        Failed = 3,
        TotalExceedsInt32 = 4
    }

    /// <summary>
    /// Pure coverage + row assembly. Duplicate ObjectId: first wins.
    /// Complete only when outcome succeeded AND LoadedUniqueCount == ExpectedCount.
    /// </summary>
    internal static class DashboardTypeDatasetAssembler
    {
        public static DashboardTypeDataset Assemble(
            int typeId,
            long expectedCount,
            IEnumerable<DashboardObjectSource> objects,
            DashboardTypeLoadOutcome outcome,
            string reason)
        {
            var factory = new DashboardObjectRowFactory();
            var rows = new List<DashboardObjectRow>();
            var seen = new HashSet<Guid>();
            var skipped = 0;
            var fieldValues = 0;

            if (objects != null)
            {
                foreach (var source in objects)
                {
                    if (source == null || source.Id == Guid.Empty)
                        continue;
                    if (!seen.Add(source.Id))
                        continue;
                    var row = factory.Create(source);
                    if (row == null)
                        continue;
                    skipped += factory.SkippedUnsupportedValues;
                    fieldValues += row.Fields.Count;
                    rows.Add(row);
                }
            }

            var unique = rows.Count;
            DashboardTypeCoverage coverage;
            string coverageReason = reason;

            if (outcome == DashboardTypeLoadOutcome.TotalExceedsInt32)
            {
                coverage = DashboardTypeCoverage.Failed;
                coverageReason = coverageReason ?? "expected count exceeds Int32 MaxResults";
            }
            else if (outcome == DashboardTypeLoadOutcome.Cancelled)
            {
                coverage = DashboardTypeCoverage.Failed;
                coverageReason = coverageReason ?? "cancelled";
            }
            else if (outcome == DashboardTypeLoadOutcome.Failed)
            {
                coverage = DashboardTypeCoverage.Failed;
                coverageReason = coverageReason ?? "failed";
            }
            else if (outcome == DashboardTypeLoadOutcome.TimedOut)
            {
                coverage = unique > 0 ? DashboardTypeCoverage.Partial : DashboardTypeCoverage.Failed;
                coverageReason = coverageReason ?? "timeout";
            }
            else if (expectedCount == unique)
            {
                coverage = DashboardTypeCoverage.Complete;
                coverageReason = coverageReason ?? "complete";
            }
            else
            {
                coverage = DashboardTypeCoverage.Partial;
                coverageReason = coverageReason
                    ?? ("loaded unique " + unique + " != expected " + expectedCount);
            }

            return new DashboardTypeDataset(
                typeId,
                expectedCount,
                unique,
                coverage,
                coverageReason,
                rows,
                fieldValues,
                skipped);
        }

        /// <summary>
        /// MaxResults is Int32. Total is Int64. Never unchecked (int)Total.
        /// </summary>
        public static bool TryToMaxResults(long total, out int maxResults)
        {
            maxResults = 0;
            if (total < 0 || total > int.MaxValue)
                return false;
            maxResults = (int)total;
            return true;
        }
    }
}
