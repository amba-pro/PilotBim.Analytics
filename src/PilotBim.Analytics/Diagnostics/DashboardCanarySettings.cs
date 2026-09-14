using System;
using System.Globalization;

namespace PilotBim.Analytics.Diagnostics
{
    internal sealed class DashboardCanarySettings
    {
        public const string TypeIdVariable = "PILOTBIM_ANALYTICS_DASHBOARD_CANARY_TYPE_ID";
        public const string CancelAfterMsVariable = "PILOTBIM_ANALYTICS_DASHBOARD_CANARY_CANCEL_AFTER_MS";

        private DashboardCanarySettings(bool enabled, bool invalidTypeId, int typeId, int? cancelAfterMs)
        {
            IsEnabled = enabled;
            HasInvalidTypeId = invalidTypeId;
            TypeId = typeId;
            CancelAfterMs = cancelAfterMs;
        }

        public bool IsEnabled { get; private set; }
        public bool HasInvalidTypeId { get; private set; }
        public int TypeId { get; private set; }
        public int? CancelAfterMs { get; private set; }

        public static DashboardCanarySettings FromEnvironment()
        {
            return Parse(
                Environment.GetEnvironmentVariable(TypeIdVariable),
                Environment.GetEnvironmentVariable(CancelAfterMsVariable));
        }

        public static DashboardCanarySettings Parse(string typeIdRaw, string cancelAfterMsRaw)
        {
            if (string.IsNullOrWhiteSpace(typeIdRaw))
                return new DashboardCanarySettings(false, false, 0, ParseCancel(cancelAfterMsRaw));

            int typeId;
            if (!int.TryParse(typeIdRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out typeId)
                || typeId <= 0)
            {
                return new DashboardCanarySettings(true, true, 0, ParseCancel(cancelAfterMsRaw));
            }

            return new DashboardCanarySettings(true, false, typeId, ParseCancel(cancelAfterMsRaw));
        }

        private static int? ParseCancel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            int ms;
            if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ms) || ms <= 0)
                return null;
            return ms;
        }
    }

    internal sealed class DashboardCanaryGate
    {
        private int _started;

        public bool TryBegin()
        {
            return System.Threading.Interlocked.Exchange(ref _started, 1) == 0;
        }
    }

    internal static class DashboardCanaryClassifier
    {
        public const string Pass = "PASS";
        public const string Partial = "PARTIAL";
        public const string Failed = "FAILED";
        public const string Cancelled = "CANCELLED";
        public const string Timeout = "TIMEOUT";

        public static string Classify(PilotBim.Analytics.Models.DashboardTypeDataset dataset)
        {
            if (dataset == null)
                return Failed;

            if (dataset.Coverage == PilotBim.Analytics.Models.DashboardTypeCoverage.Complete)
            {
                var rows = dataset.Rows == null ? 0 : dataset.Rows.Count;
                if (dataset.LoadedUniqueCount == dataset.ExpectedCount
                    && rows == dataset.LoadedUniqueCount)
                    return Pass;
                return Failed;
            }

            var reason = dataset.Reason ?? string.Empty;
            if (ContainsToken(reason, "cancelled"))
                return Cancelled;
            if (ContainsToken(reason, "timeout"))
                return Timeout;
            if (dataset.Coverage == PilotBim.Analytics.Models.DashboardTypeCoverage.Partial)
                return Partial;
            return Failed;
        }

        private static bool ContainsToken(string reason, string token)
        {
            return reason.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
