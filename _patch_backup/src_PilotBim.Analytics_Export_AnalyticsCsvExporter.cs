using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Export
{
    internal sealed class AnalyticsCsvExporter
    {
        public string Export(ProjectAnalyticsSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PilotBim.Analytics",
                "Exports");
            Directory.CreateDirectory(dir);

            var stamp = snapshot.GeneratedAt.ToString("yyyyMMdd-HHmmss");
            var folder = Path.Combine(dir, "Analytics-" + stamp);
            Directory.CreateDirectory(folder);

            WriteKpi(Path.Combine(folder, "summary.csv"), snapshot.Summary);
            WriteRows(Path.Combine(folder, "objects_by_type.csv"),
                new[] { "TypeName", "TypeId", "Count", "IsEstimate", "SharePercent", "FillPercent" },
                snapshot.ObjectsByType,
                r => new[] { r.TypeName, r.TypeId.ToString(), r.Count.ToString(), r.IsEstimate.ToString(), F(r.SharePercent), F(r.FillPercent) });

            WriteRows(Path.Combine(folder, "objects_by_creator.csv"),
                new[] { "DisplayName", "CreatorId", "SampledCount", "SharePercent", "Scope" },
                snapshot.ObjectsByCreator,
                r => new[] { r.DisplayName, r.CreatorId.ToString(), r.SampledCount.ToString(), F(r.SharePercent), r.Scope });

            WriteRows(Path.Combine(folder, "objects_by_created_month.csv"),
                new[] { "Period", "Count", "SharePercent" },
                snapshot.ObjectsByCreatedMonth,
                r => new[] { r.Period, r.Count.ToString(), F(r.SharePercent) });

            WriteRows(Path.Combine(folder, "objects_by_user_state.csv"),
                new[] { "StateTitle", "StateId", "Count", "SharePercent", "Scope" },
                snapshot.ObjectsByUserState,
                r => new[] { r.StateTitle, r.StateId.ToString(), r.Count.ToString(), F(r.SharePercent), r.Scope });

            WriteRows(Path.Combine(folder, "bim_models.csv"),
                new[] { "ModelName", "ModelId", "PartCount", "ElementCount", "GlobalIdCount", "GlobalIdFillPercent", "IsEstimate", "Status", "Notes" },
                snapshot.BimModelAnalytics,
                r => new[] { r.ModelName, r.ModelId.ToString(), r.PartCount.ToString(), r.ElementCount.ToString(), r.GlobalIdCount.ToString(), F(r.GlobalIdFillPercent), r.IsEstimate.ToString(), r.Status, r.Notes });

            WriteRows(Path.Combine(folder, "bim_parts.csv"),
                new[] { "PartName", "ModelName", "PartId", "ElementCount", "GlobalIdCount", "WithoutGlobalIdCount", "GlobalIdFillPercent", "IsTruncated", "DataSource", "Status", "Notes" },
                snapshot.BimPartAnalytics,
                r => new[] { r.PartName, r.ModelName, r.PartId.ToString(), r.ElementCount.ToString(), r.GlobalIdCount.ToString(), r.WithoutGlobalIdCount.ToString(), F(r.GlobalIdFillPercent), r.IsTruncated.ToString(), r.DataSource, r.Status, r.Notes });

            WriteRows(Path.Combine(folder, "bim_ifc_types.csv"),
                new[] { "IfcType", "ModelName", "PartName", "Count", "SharePercent", "IsEstimate" },
                snapshot.BimElementTypeCounts,
                r => new[] { r.IfcType, r.ModelName, r.PartName, r.Count.ToString(), F(r.SharePercent), r.IsEstimate.ToString() });

            WriteRows(Path.Combine(folder, "model_remarks.csv"),
                new[] { "TypeName", "ObjectCount", "BimObjectIdFillRate", "MappingStatus", "Notes" },
                snapshot.ModelRemarks,
                r => new[] { r.TypeName, r.ObjectCount.ToString(), F(r.BimObjectIdFillRate * 100), r.MappingStatus, r.Notes });

            WriteRows(Path.Combine(folder, "remark_links.csv"),
                new[] { "DisplayName", "TypeName", "ObjectId", "BimObjectId", "ResolvedInIndex", "MappingStatus", "Notes" },
                snapshot.RemarkLinks,
                r => new[] { r.DisplayName, r.TypeName, r.ObjectId.ToString(), r.BimObjectId, r.ResolvedInIndex.ToString(), r.MappingStatus, r.Notes });

            WriteRows(Path.Combine(folder, "responsible.csv"),
                new[] { "DisplayName", "OrgUnitId", "SampledCount", "SharePercent", "Scope" },
                snapshot.ObjectsByResponsible,
                r => new[] { r.DisplayName, r.OrgUnitId.ToString(), r.SampledCount.ToString(), F(r.SharePercent), r.Scope });

            WriteRows(Path.Combine(folder, "state_semantics.csv"),
                new[] { "StateTitle", "StateId", "Count", "SharePercent", "Semantic", "Scope" },
                snapshot.ObjectsByUserStateSemantic,
                r => new[] { r.StateTitle, r.StateId.ToString(), r.Count.ToString(), F(r.SharePercent), r.Semantic, r.Scope });

            File.WriteAllText(Path.Combine(folder, "limitations.txt"),
                snapshot.Limitations != null ? string.Join(Environment.NewLine, snapshot.Limitations) : string.Empty,
                new UTF8Encoding(true));

            return folder;
        }

        private static void WriteKpi(string path, IList<AnalyticsKpiRow> rows)
        {
            WriteRows(path, new[] { "Label", "Value", "Detail" }, rows,
                r => new[] { r.Label, r.Value, r.Detail });
        }

        private static void WriteRows<T>(string path, string[] headers, IList<T> rows, Func<T, string[]> map)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", headers.Select(Csv)));
            if (rows != null)
            {
                foreach (var row in rows)
                    sb.AppendLine(string.Join(";", map(row).Select(Csv)));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (value.IndexOf(';') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private static string F(double value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
