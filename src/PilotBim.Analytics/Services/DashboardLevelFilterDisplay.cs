using System.Globalization;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Properties;

namespace PilotBim.Analytics.Services
{
    internal static class DashboardLevelFilterDisplay
    {
        public static string ValueText(DashboardLevelFilterDefinition filter, DashboardFieldCatalog catalog)
        {
            if (filter == null)
                return string.Empty;

            DashboardFilterDefinition runtime;
            string error;
            if (!DashboardFilterCompatibility.TryToRuntimeFilter(filter, out runtime, out error) || runtime == null)
                return string.Empty;

            if (runtime.Operator == DashboardFilterOperator.IsEmpty)
                return Resources.QueryEditor_OpIsEmpty;
            if (runtime.Operator == DashboardFilterOperator.IsNotEmpty)
                return Resources.QueryEditor_OpIsNotEmpty;

            if (runtime.Value == null || runtime.Value.Value == null)
                return string.Empty;

            switch (runtime.Value.Kind)
            {
                case DashboardFieldType.Boolean:
                    return runtime.Value.Value is bool && (bool)runtime.Value.Value
                        ? Resources.QueryEditor_BoolYes
                        : Resources.QueryEditor_BoolNo;
                case DashboardFieldType.Integer:
                    return string.Format(CultureInfo.CurrentCulture, "{0:N0}", runtime.Value.Value);
                case DashboardFieldType.Number:
                    return string.Format(CultureInfo.CurrentCulture, "{0}", runtime.Value.Value);
                default:
                    return runtime.Value.Value.ToString();
            }
        }

        public static bool IsFieldUnavailable(DashboardLevelFilterDefinition filter, DashboardFieldCatalog catalog)
        {
            if (filter == null || catalog == null)
                return true;
            DashboardFieldDescriptor unused;
            return !catalog.TryGet(filter.FieldId, out unused) || unused == null;
        }
    }
}
