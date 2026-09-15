using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Per-project V2 dashboard files. Not wired to UI in DB-7.
    /// Does not read or overwrite <c>dashboard-layout.json</c>.
    /// </summary>
    internal sealed class DashboardDefinitionStore
    {
        private readonly string _root;

        public DashboardDefinitionStore()
            : this(DefaultRoot)
        {
        }

        public DashboardDefinitionStore(string dashboardsRoot)
        {
            if (string.IsNullOrWhiteSpace(dashboardsRoot))
                throw new ArgumentException("dashboards root is required.", "dashboardsRoot");
            _root = dashboardsRoot;
        }

        public static string DefaultRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PilotBim.Analytics",
                    "Dashboards");
            }
        }

        public string GetPath(Guid projectKey)
        {
            return Path.Combine(_root, DashboardProjectKey.ToFolderName(projectKey), DashboardPersistenceV2.FileName);
        }

        public DashboardDefinition CreateDefault(Guid projectKey)
        {
            return new DashboardDefinition
            {
                SchemaVersion = DashboardPersistenceV2.CurrentSchemaVersion,
                Id = DashboardPersistenceV2.DefaultDashboardId,
                Title = DashboardPersistenceV2.DefaultTitle,
                ProjectKey = DashboardProjectKey.ToFolderName(projectKey),
                Widgets = new System.Collections.Generic.List<DashboardWidgetDefinition>(),
                DashboardFilters = new System.Collections.Generic.List<DashboardLevelFilterDefinition>()
            };
        }

        public static DashboardPersistenceKind Detect(Stream stream)
        {
            if (stream == null)
                return DashboardPersistenceKind.Invalid;
            try
            {
                SkipUtf8Bom(stream);
                var ser = new DataContractJsonSerializer(typeof(DashboardSchemaVersionProbe));
                var probe = ser.ReadObject(stream) as DashboardSchemaVersionProbe;
                if (probe == null)
                    return DashboardPersistenceKind.Invalid;
                if (probe.SchemaVersion == DashboardPersistenceV2.CurrentSchemaVersion)
                    return DashboardPersistenceKind.V4;
                if (probe.SchemaVersion == DashboardPersistenceV2.SchemaVersionV3)
                    return DashboardPersistenceKind.V3;
                if (probe.SchemaVersion == DashboardPersistenceV2.SchemaVersion)
                    return DashboardPersistenceKind.V2;
                if (probe.SchemaVersion > DashboardPersistenceV2.CurrentSchemaVersion)
                    return DashboardPersistenceKind.UnsupportedVersion;
                return DashboardPersistenceKind.LegacyV1;
            }
            catch
            {
                return DashboardPersistenceKind.Invalid;
            }
        }

        public DashboardDefinitionLoadResult Load(Guid projectKey)
        {
            var path = GetPath(projectKey);
            var result = new DashboardDefinitionLoadResult { Path = path };
            try
            {
                if (!File.Exists(path))
                {
                    result.Status = DashboardDefinitionLoadStatus.Missing;
                    result.Reason = "V2 dashboard file is missing";
                    return result;
                }

                DashboardDefinition loaded;
                using (var stream = OpenReadJson(path))
                {
                    var kind = Detect(stream);
                    if (kind == DashboardPersistenceKind.UnsupportedVersion)
                    {
                        result.Status = DashboardDefinitionLoadStatus.UnsupportedVersion;
                        result.Reason = "dashboard schema version is newer than " + DashboardPersistenceV2.CurrentSchemaVersion;
                        AnalyticsLogger.Warning("DashboardPersistence", result.Reason + " path=" + path);
                        return result;
                    }
                    if (kind == DashboardPersistenceKind.Invalid)
                    {
                        result.Status = DashboardDefinitionLoadStatus.Corrupt;
                        result.Reason = "dashboard JSON is corrupt or unreadable";
                        AnalyticsLogger.Warning("DashboardPersistence", result.Reason + " path=" + path);
                        return result;
                    }
                    if (kind == DashboardPersistenceKind.LegacyV1)
                    {
                        result.Status = DashboardDefinitionLoadStatus.Invalid;
                        result.Reason = "V2 path contains a legacy unversioned document";
                        AnalyticsLogger.Warning("DashboardPersistence", result.Reason + " path=" + path);
                        return result;
                    }

                    if (kind != DashboardPersistenceKind.V2
                        && kind != DashboardPersistenceKind.V3
                        && kind != DashboardPersistenceKind.V4)
                    {
                        result.Status = DashboardDefinitionLoadStatus.Corrupt;
                        result.Reason = "dashboard JSON is corrupt or unreadable";
                        AnalyticsLogger.Warning("DashboardPersistence", result.Reason + " path=" + path);
                        return result;
                    }

                    if (stream.CanSeek)
                        stream.Position = 0;
                    SkipUtf8Bom(stream);
                    var ser = CreateSerializer();
                    loaded = ser.ReadObject(stream) as DashboardDefinition;
                }

                if (loaded == null)
                {
                    result.Status = DashboardDefinitionLoadStatus.Corrupt;
                    result.Reason = "dashboard JSON deserialized to null";
                    AnalyticsLogger.Warning("DashboardPersistence", result.Reason + " path=" + path);
                    return result;
                }

                if (loaded.SchemaVersion > DashboardPersistenceV2.CurrentSchemaVersion)
                {
                    result.Status = DashboardDefinitionLoadStatus.UnsupportedVersion;
                    result.Reason = "dashboard schema version " + loaded.SchemaVersion + " is unsupported";
                    AnalyticsLogger.Warning("DashboardPersistence", result.Reason + " path=" + path);
                    return result;
                }

                if (!DashboardProjectKey.AreEqual(loaded.ProjectKey, projectKey))
                {
                    result.Status = DashboardDefinitionLoadStatus.ProjectMismatch;
                    result.Reason = "dashboard project key does not match the current database";
                    AnalyticsLogger.Warning("DashboardPersistence", result.Reason + " path=" + path);
                    return result;
                }

                string error;
                if (!DashboardDefinitionValidator.TryValidate(loaded, out error))
                {
                    result.Status = DashboardDefinitionLoadStatus.Invalid;
                    result.Reason = error;
                    AnalyticsLogger.Warning("DashboardPersistence", "invalid V2 dashboard: " + error + " path=" + path);
                    return result;
                }

                Canonicalize(loaded, projectKey);
                result.Status = DashboardDefinitionLoadStatus.Success;
                result.Definition = loaded;
                return result;
            }
            catch (SerializationException ex)
            {
                result.Status = DashboardDefinitionLoadStatus.Corrupt;
                result.Reason = "dashboard JSON is corrupt";
                AnalyticsLogger.Warning("DashboardPersistence", result.Reason + " path=" + path + " " + ex.Message);
                return result;
            }
            catch (Exception ex)
            {
                result.Status = DashboardDefinitionLoadStatus.Corrupt;
                result.Reason = "dashboard JSON is unreadable";
                AnalyticsLogger.Warning("DashboardPersistence", result.Reason + " path=" + path + " " + ex.Message);
                return result;
            }
        }

        public DashboardDefinitionSaveResult Save(DashboardDefinition definition)
        {
            var result = new DashboardDefinitionSaveResult();
            string error;
            if (!DashboardDefinitionValidator.TryValidate(definition, out error))
            {
                result.Status = DashboardDefinitionSaveStatus.Invalid;
                result.Reason = error;
                return result;
            }

            Guid projectKey;
            DashboardProjectKey.TryParse(definition.ProjectKey, out projectKey);
            Canonicalize(definition, projectKey);
            var path = GetPath(projectKey);
            result.Path = path;

            var tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    CreateSerializer().WriteObject(stream, definition);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);

                AnalyticsLogger.Info("DashboardPersistence", "saved path=" + path);
                result.Status = DashboardDefinitionSaveStatus.Success;
                return result;
            }
            catch (Exception ex)
            {
                TryDelete(tmp);
                result.Status = DashboardDefinitionSaveStatus.IoFailure;
                result.Reason = ex.Message;
                AnalyticsLogger.Warning("DashboardPersistence", "save failed path=" + path + " " + ex.Message);
                return result;
            }
        }

        private static FileStream OpenReadJson(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private static void SkipUtf8Bom(Stream stream)
        {
            if (stream == null || !stream.CanSeek)
                return;
            var start = stream.Position;
            if (stream.Length - start < 3)
                return;
            var b0 = stream.ReadByte();
            var b1 = stream.ReadByte();
            var b2 = stream.ReadByte();
            if (b0 == 0xEF && b1 == 0xBB && b2 == 0xBF)
                return;
            stream.Position = start;
        }

        private static void Canonicalize(DashboardDefinition definition, Guid projectKey)
        {
            definition.ProjectKey = DashboardProjectKey.ToFolderName(projectKey);
            if (definition.Widgets == null)
                definition.Widgets = new System.Collections.Generic.List<DashboardWidgetDefinition>();
            definition.Widgets.Sort((a, b) =>
            {
                var ay = a != null && a.Layout != null ? a.Layout.Y : 0;
                var by = b != null && b.Layout != null ? b.Layout.Y : 0;
                var cmp = ay.CompareTo(by);
                if (cmp != 0)
                    return cmp;
                var ax = a != null && a.Layout != null ? a.Layout.X : 0;
                var bx = b != null && b.Layout != null ? b.Layout.X : 0;
                cmp = ax.CompareTo(bx);
                if (cmp != 0)
                    return cmp;
                var ao = a != null && a.Layout != null ? a.Layout.Order : 0;
                var bo = b != null && b.Layout != null ? b.Layout.Order : 0;
                return ao.CompareTo(bo);
            });
            if (definition.DashboardFilters == null)
                definition.DashboardFilters = new System.Collections.Generic.List<DashboardLevelFilterDefinition>();
        }

        private static DataContractJsonSerializer CreateSerializer()
        {
            return new DataContractJsonSerializer(
                typeof(DashboardDefinition),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true,
                    EmitTypeInformation = EmitTypeInformation.Never
                });
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
