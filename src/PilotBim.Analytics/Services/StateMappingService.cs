using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Optional override: %LOCALAPPDATA%\PilotBim.Analytics\state-mapping.json
    /// Format: { "guid": "OPEN|CLOSED|REJECTED|UNKNOWN", ... }
    /// </summary>
    internal sealed class StateMappingService
    {
        public void ApplyOverrides(List<StateInventoryRecord> states)
        {
            if (states == null || states.Count == 0)
                return;

            var map = LoadMapping();
            if (map.Count == 0)
                return;

            foreach (var state in states)
            {
                string semantic;
                if (map.TryGetValue(state.StateId.ToString(), out semantic)
                    || map.TryGetValue(state.StateId.ToString("D"), out semantic)
                    || (!string.IsNullOrEmpty(state.Name) && map.TryGetValue(state.Name, out semantic)))
                {
                    state.SemanticStatus = Normalize(semantic);
                }
            }
        }

        private static Dictionary<string, string> LoadMapping()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PilotBim.Analytics",
                    "state-mapping.json");
                if (!File.Exists(path))
                    return result;

                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = (line ?? string.Empty).Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                        continue;
                    if (trimmed.StartsWith("{") || trimmed.StartsWith("}"))
                        continue;

                    var sep = trimmed.IndexOf(':');
                    if (sep <= 0)
                        continue;

                    var key = trimmed.Substring(0, sep).Trim().Trim('"');
                    var value = trimmed.Substring(sep + 1).Trim().Trim('"', ',');
                    if (key.Length > 0 && value.Length > 0)
                        result[key] = value;
                }
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("state-mapping", ex.Message);
            }

            return result;
        }

        private static string Normalize(string semantic)
        {
            if (string.IsNullOrWhiteSpace(semantic))
                return CapabilityStatus.Unknown;

            var upper = semantic.Trim().ToUpperInvariant();
            if (upper == "OPEN" || upper == "CLOSED" || upper == "REJECTED")
                return upper;
            return semantic.Trim();
        }
    }
}
