using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;

namespace PilotBim.Analytics.Diagnostics
{
    /// <summary>
    /// Opt-in DB-3.1 runtime canary. Dormant unless
    /// PILOTBIM_ANALYTICS_DASHBOARD_CANARY_TYPE_ID is set. No UI.
    /// </summary>
    internal static class DashboardTypeMaterializerCanary
    {
        internal const string Area = "DashboardCanary";
        private static readonly DashboardCanaryGate ProcessGate = new DashboardCanaryGate();

        public static void TryStart(IObjectsRepository repository, ISearchService search)
        {
            try
            {
                var settings = DashboardCanarySettings.FromEnvironment();
                if (!settings.IsEnabled)
                    return;
                if (!ProcessGate.TryBegin())
                    return;

                if (settings.HasInvalidTypeId)
                {
                    Info("INVALID_TYPE_ID");
                    return;
                }

                if (repository == null)
                {
                    Info("FAILED reason=repository unavailable");
                    return;
                }

                Task.Run(() => Run(repository, search, settings));
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error(Area, Prefix("FAILED"), ex);
            }
        }

        private static void Run(
            IObjectsRepository repository,
            ISearchService search,
            DashboardCanarySettings settings)
        {
            CancellationTokenSource cts = null;
            try
            {
                Info("START TypeId=" + settings.TypeId
                    + (settings.CancelAfterMs.HasValue
                        ? " CancelAfterMs=" + settings.CancelAfterMs.Value
                        : string.Empty));

                var before = CaptureMemory();
                var sw = Stopwatch.StartNew();

                if (settings.CancelAfterMs.HasValue)
                {
                    cts = new CancellationTokenSource();
                    cts.CancelAfter(settings.CancelAfterMs.Value);
                }

                var token = cts != null ? cts.Token : CancellationToken.None;
                var materializer = new DashboardTypeDatasetMaterializer(repository, search);
                var dataset = materializer.Materialize(settings.TypeId, token);
                sw.Stop();
                var after = CaptureMemory();

                LogResult(dataset, sw.ElapsedMilliseconds, before, after);
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error(Area, Prefix("FAILED"), ex);
                Info("FAILED");
            }
            finally
            {
                if (cts != null)
                {
                    try
                    {
                        cts.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
        }

        private static void LogResult(
            DashboardTypeDataset dataset,
            long elapsedMs,
            MemorySnapshot before,
            MemorySnapshot after)
        {
            if (dataset == null)
            {
                Info("FAILED");
                return;
            }

            var rows = dataset.Rows == null ? 0 : dataset.Rows.Count;
            Info(
                "RESULT TypeId=" + dataset.TypeId
                + " ExpectedCount=" + dataset.ExpectedCount
                + " LoadedUniqueCount=" + dataset.LoadedUniqueCount
                + " Coverage=" + dataset.Coverage
                + " Rows=" + rows
                + " FieldValueCount=" + dataset.FieldValueCount
                + " SkippedUnsupportedValues=" + dataset.SkippedUnsupportedValues
                + " ElapsedMs=" + elapsedMs);

            Info(
                "MEMORY approximate"
                + " beforeManaged=" + before.ManagedBytes
                + " afterManaged=" + after.ManagedBytes
                + " deltaManaged=" + (after.ManagedBytes - before.ManagedBytes)
                + " beforeWorkingSet=" + before.WorkingSetBytes
                + " afterWorkingSet=" + after.WorkingSetBytes
                + " deltaWorkingSet=" + (after.WorkingSetBytes - before.WorkingSetBytes));

            if (dataset.SkippedUnsupportedValues > 0)
            {
                AnalyticsLogger.Warning(
                    Area,
                    Prefix("DATA_QUALITY_WARNING SkippedUnsupportedValues=" + dataset.SkippedUnsupportedValues));
            }

            Info(DashboardCanaryClassifier.Classify(dataset));
        }

        private static MemorySnapshot CaptureMemory()
        {
            var snapshot = new MemorySnapshot();
            try
            {
                snapshot.ManagedBytes = GC.GetTotalMemory(false);
            }
            catch
            {
                snapshot.ManagedBytes = -1;
            }
            try
            {
                snapshot.WorkingSetBytes = Process.GetCurrentProcess().WorkingSet64;
            }
            catch
            {
                snapshot.WorkingSetBytes = -1;
            }
            return snapshot;
        }

        private static void Info(string message)
        {
            AnalyticsLogger.Info(Area, Prefix(message));
        }

        private static string Prefix(string message)
        {
            return "[DashboardCanary] " + message;
        }

        private struct MemorySnapshot
        {
            public long ManagedBytes;
            public long WorkingSetBytes;
        }
    }
}
