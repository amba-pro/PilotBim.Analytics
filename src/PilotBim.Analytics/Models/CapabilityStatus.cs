namespace PilotBim.Analytics.Models
{
    public static class CapabilityStatus
    {
        public const string Available = "AVAILABLE";
        public const string Partial = "PARTIAL";
        public const string NotExposed = "NOT_EXPOSED";
        public const string NotVerified = "NOT_VERIFIED";
        public const string NotExposedBySdk = "NOT_EXPOSED_BY_CURRENT_SDK";
        public const string Blocked = "BLOCKED";
        public const string BlockedBySdk = "BLOCKED_BY_SDK";
        public const string Error = "ERROR";
        public const string Ready = "READY";
        public const string NeedsMapping = "NEEDS_MAPPING";
        public const string NeedsRuntime = "RUNTIME_VALIDATION_REQUIRED";
        public const string NotAvailable = "NOT_AVAILABLE";
        public const string Unknown = "UNKNOWN";
        public const string Pass = "PASS";
    }

    public static class AttributePopulationStatus
    {
        public const string Available = "AVAILABLE";
        public const string Empty = "EMPTY";
        public const string PartiallyPopulated = "PARTIALLY_POPULATED";
        public const string Unreadable = "UNREADABLE";
        public const string UnsupportedValueType = "UNSUPPORTED_VALUE_TYPE";
    }

    public static class ScanModeNames
    {
        public const string Fast = "FAST";
        public const string Standard = "STANDARD";
        public const string Full = "FULL";
    }

    public enum ScanMode
    {
        Fast = 50,
        Standard = 200,
        Full = 10000
    }
}
