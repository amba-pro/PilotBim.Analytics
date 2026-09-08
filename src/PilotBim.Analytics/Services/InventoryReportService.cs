using System;
using System.IO;
using System.Linq;
using System.Text;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    internal sealed class InventoryReportService
    {
        public string BuildTextReport(ProjectInventoryReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PILOTBIM ANALYTICS");
            sb.AppendLine("STAGE 0A + 0B DATA INVENTORY REPORT");
            sb.AppendLine();
            sb.AppendLine("## ENVIRONMENT");
            sb.AppendLine();
            sb.AppendLine("Pilot version: " + (report.PilotVersion ?? "-"));
            sb.AppendLine("Target: Pilot-BIM desktop client");
            sb.AppendLine("Architecture: " + (report.Architecture ?? "-"));
            sb.AppendLine("Framework: " + (report.Framework ?? "-"));
            sb.AppendLine("Scan mode: " + report.ScanMode + " (sample limit=" + report.SampleLimit + ")");
            sb.AppendLine("Generated: " + report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("## SDK ASSEMBLIES");
            sb.AppendLine();
            sb.AppendLine("Ascon.Pilot.SDK: " + (report.SdkVersion ?? "-"));
            sb.AppendLine("Ascon.Pilot.Bim.SDK: " + (report.BimSdkVersion ?? "-"));
            sb.AppendLine();
            sb.AppendLine("## SDK DISCOVERY");
            sb.AppendLine();
            sb.AppendLine("Verified APIs:");
            foreach (var a in report.VerifiedApis)
                sb.AppendLine("- " + a.Member + " [" + a.Status + "] " + a.Notes);
            sb.AppendLine();
            sb.AppendLine("Not verified / not exposed:");
            foreach (var a in report.NotVerifiedApis)
                sb.AppendLine("- " + a.Member + " [" + a.Status + "] " + a.Notes);
            sb.AppendLine();
            sb.AppendLine("## PROJECT SUMMARY");
            sb.AppendLine();
            sb.AppendLine("Types: " + report.TypesDiscovered);
            sb.AppendLine("Objects: " + report.ObjectsFound);
            sb.AppendLine("Attributes: " + report.AttributesDiscovered);
            sb.AppendLine("States: " + report.StatesDiscovered);
            sb.AppendLine("Persons: " + report.PersonsResolved);
            sb.AppendLine("Organisations: " + report.OrganisationsResolved);
            sb.AppendLine("BIM models: " + report.BimModelsCount);
            sb.AppendLine("BIM model parts: " + report.BimModelPartsCount);
            sb.AppendLine("Analytics readiness: " + report.AnalyticsReadiness);
            sb.AppendLine();
            sb.AppendLine("## TYPE INVENTORY");
            sb.AppendLine();
            foreach (var t in report.Types)
            {
                sb.AppendLine("TYPE: " + t.Title);
                sb.AppendLine("ID: " + t.TypeId + " (" + t.Name + ")");
                sb.AppendLine("OBJECTS: " + t.ObjectCount + (t.ObjectCountIsEstimate ? " (estimate/partial)" : ""));
                sb.AppendLine("SAMPLED: " + t.SampledCount);
                sb.AppendLine("ATTRIBUTES: " + t.AttributeCount);
                sb.AppendLine("FILL%: " + AnalyticsFormats.InvariantOneDecimal(t.FillPercent));
                sb.AppendLine("STATUS: " + t.Status);
                if (!string.IsNullOrEmpty(t.Warnings))
                    sb.AppendLine("WARNINGS: " + t.Warnings);
                foreach (var a in t.Attributes)
                {
                    sb.AppendLine("  ATTRIBUTE: " + a.Title);
                    sb.AppendLine("  ID: " + a.AttributeId);
                    sb.AppendLine("  TYPE: " + a.ValueType);
                    sb.AppendLine("  REQUIRED: " + a.IsObligatory);
                    sb.AppendLine("  POPULATED: " + a.PopulatedCount);
                    sb.AppendLine("  EMPTY: " + a.EmptyCount);
                    sb.AppendLine("  FILL: " + AnalyticsFormats.InvariantOneDecimal(a.FillRate * 100) + "%");
                    sb.AppendLine("  STATUS: " + a.PopulationStatus);
                    if (a.SampleValues.Count > 0)
                        sb.AppendLine("  EXAMPLES: " + string.Join(" | ", a.SampleValues));
                    if (!string.IsNullOrEmpty(a.ConfigurationSummary))
                        sb.AppendLine("  CONFIG: " + a.ConfigurationSummary);
                }
                sb.AppendLine();
            }

            sb.AppendLine("## SYSTEM FIELDS");
            sb.AppendLine();
            foreach (var f in report.SystemFields)
            {
                sb.AppendLine(f.Field + ":");
                sb.AppendLine("  Availability: " + f.Availability);
                sb.AppendLine("  SDK Source: " + f.SdkSource);
                if (!string.IsNullOrEmpty(f.Example))
                    sb.AppendLine("  Example: " + f.Example);
                if (!string.IsNullOrEmpty(f.Notes))
                    sb.AppendLine("  Notes: " + f.Notes);
            }
            sb.AppendLine();

            sb.AppendLine("## STATES");
            sb.AppendLine();
            foreach (var s in report.States)
            {
                sb.AppendLine("- " + s.Title + " [" + s.StateId + "] refs=" + s.ReferencedObjects
                    + " types=" + string.Join(", ", s.ReferencedTypes)
                    + " semantic=" + s.SemanticStatus);
            }
            sb.AppendLine();

            sb.AppendLine("## PERSONS");
            sb.AppendLine();
            sb.AppendLine("Count: " + report.PersonsResolved + " (DisplayName + Organisation only)");
            foreach (var p in report.Persons.Take(50))
            {
                sb.AppendLine("- id=" + p.PersonId + " name=" + p.DisplayName + " org=" + (p.OrganisationName ?? "-"));
            }
            if (report.Persons.Count > 50)
                sb.AppendLine("... +" + (report.Persons.Count - 50) + " more");
            sb.AppendLine();

            sb.AppendLine("## ORGANISATIONS");
            sb.AppendLine();
            sb.AppendLine("Count: " + report.OrganisationsResolved);
            foreach (var o in report.Organisations.Where(x => !x.IsPosition).Take(80))
            {
                sb.AppendLine("- id=" + o.OrganisationId + " name=" + o.Name + " parent=" + (o.ParentOrganisationId.HasValue ? o.ParentOrganisationId.Value.ToString() : "-"));
            }
            sb.AppendLine();

            sb.AppendLine("## DOCUMENT / VERSION CAPABILITIES");
            sb.AppendLine();
            foreach (var d in report.DocumentCapabilities)
                sb.AppendLine(d.Capability + ": " + d.Availability + " (" + d.SdkSource + ") " + d.Notes);
            sb.AppendLine("Samples: " + report.DocumentSamples.Count);
            foreach (var d in report.DocumentSamples.Take(10))
            {
                sb.AppendLine("- " + d.DisplayName + " type=" + d.TypeName + " files=" + d.FileCount
                    + " prevSnapshots=" + d.PreviousSnapshotCount + " latest=" + d.LatestFileName);
            }
            sb.AppendLine();

            sb.AppendLine("## HISTORY CAPABILITIES");
            sb.AppendLine();
            foreach (var h in report.HistoryCapabilities)
                sb.AppendLine(h.Capability + ": " + h.Availability + " (" + h.SdkSource + ") " + h.Notes);
            sb.AppendLine("History samples:");
            foreach (var e in report.HistorySamples)
                sb.AppendLine("- " + e.Created.ToString("o") + " object=" + e.ObjectId + " creator=" + e.CreatorId + " reason=" + e.Reason);
            sb.AppendLine();

            sb.AppendLine("## BIM CAPABILITIES");
            sb.AppendLine();
            foreach (var b in report.BimCapabilities)
                sb.AppendLine(b.Name + ": " + b.Availability + " source=" + b.SdkSource + " " + b.Notes);
            sb.AppendLine("Element samples: " + report.BimElementSamples.Count);
            foreach (var e in report.BimElementSamples.Take(20))
            {
                sb.AppendLine("- element=" + e.ElementId + " part=" + e.ModelPartId + " model=" + e.ModelId
                    + " type=" + e.Type + " globalId=" + e.GlobalId + " props=" + e.PropertyCount);
            }
            sb.AppendLine();

            sb.AppendLine("## ANALYTICS CAPABILITY MATRIX");
            sb.AppendLine();
            foreach (var m in report.AnalyticsCapabilities)
            {
                sb.AppendLine("Metric: " + m.Metric);
                sb.AppendLine("Required fields: " + m.RequiredData);
                sb.AppendLine("Availability: " + m.Availability);
                sb.AppendLine("Status: " + m.Status);
                if (!string.IsNullOrEmpty(m.Notes))
                    sb.AppendLine("Notes: " + m.Notes);
                sb.AppendLine();
            }

            sb.AppendLine("## DATA QUALITY CAPABILITIES");
            sb.AppendLine();
            foreach (var m in report.DataQualityCapabilities)
                sb.AppendLine("- " + m.Metric + " | " + m.Availability + " | " + m.Status + " | " + m.Notes);
            sb.AppendLine();

            sb.AppendLine("## ZONE RESULTS");
            sb.AppendLine();
            foreach (var z in report.ZoneResults)
                sb.AppendLine(z.Zone + ": " + z.Status + " — " + z.Message);
            sb.AppendLine();

            sb.AppendLine("## WARNINGS");
            sb.AppendLine();
            foreach (var w in report.Warnings)
                sb.AppendLine("- " + w);
            sb.AppendLine();

            sb.AppendLine("## ERRORS");
            sb.AppendLine();
            foreach (var e in report.Errors)
                sb.AppendLine("- " + e);
            sb.AppendLine();

            sb.AppendLine("## SDK MEMBERS USED");
            sb.AppendLine();
            foreach (var a in report.VerifiedApis)
                sb.AppendLine("- " + a.Member);
            sb.AppendLine();

            sb.AppendLine("## SDK MEMBERS REQUIRING RUNTIME VALIDATION");
            sb.AppendLine();
            foreach (var a in report.RuntimeValidationApis)
                sb.AppendLine("- " + a.Member + " — " + a.Notes);
            sb.AppendLine();

            sb.AppendLine("## PERFORMANCE NOTES");
            sb.AppendLine();
            foreach (var n in report.PerformanceNotes)
                sb.AppendLine("- " + n);
            sb.AppendLine();

            sb.AppendLine("## FILES CREATED");
            sb.AppendLine();
            sb.AppendLine("- PilotBim.Analytics plugin sources under Plagin2/PilotBim.Analytics");
            sb.AppendLine("- Logs: %LOCALAPPDATA%\\PilotBim.Analytics\\Logs\\");
            sb.AppendLine();

            sb.AppendLine("## BUILD OUTPUT");
            sb.AppendLine();
            sb.AppendLine("- See dist / bin after build");
            sb.AppendLine();

            sb.AppendLine("## FINAL STATUS");
            sb.AppendLine();
            sb.AppendLine(report.FinalStatus ?? "PARTIAL_RUNTIME_INVENTORY");
            return sb.ToString();
        }
    }
}

namespace PilotBim.Analytics.Export
{
    internal sealed class InventoryReportExporter
    {
        public string SaveReport(string text)
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PilotBim.Analytics",
                "Reports");
            Directory.CreateDirectory(dir);
            var name = "PilotBim.Analytics.Inventory." + AnalyticsFormats.FileTimestamp(DateTime.Now) + ".txt";
            var path = Path.Combine(dir, name);
            File.WriteAllText(path, text ?? string.Empty, new UTF8Encoding(true));
            return path;
        }
    }
}
