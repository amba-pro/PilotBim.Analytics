using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Persists scan baselines under LocalAppData (never in Pilot):
    /// last-scan.json + named history (max N entries).
    /// </summary>
    internal sealed class ScanSnapshotStore
    {
        private const int MaxHistory = 40;

        private static string RootDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PilotBim.Analytics",
                    "Snapshots");
            }
        }

        private static string HistoryDir
        {
            get { return Path.Combine(RootDir, "history"); }
        }

        private static string LastPath
        {
            get { return Path.Combine(RootDir, "last-scan.json"); }
        }

        private static string IndexPath
        {
            get { return Path.Combine(RootDir, "history-index.json"); }
        }

        public StoredScanBaseline TryLoadLast()
        {
            return TryReadBaseline(LastPath);
        }

        public StoredScanBaseline TryLoadById(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "last")
                return TryLoadLast();

            var path = Path.Combine(HistoryDir, id + ".json");
            return TryReadBaseline(path);
        }

        public void SaveLast(StoredScanBaseline baseline)
        {
            if (baseline == null)
                return;

            if (string.IsNullOrWhiteSpace(baseline.Id))
                baseline.Id = "last";
            if (string.IsNullOrWhiteSpace(baseline.Name))
                baseline.Name = "Последний скан";

            WriteBaseline(LastPath, baseline);
            AnalyticsLogger.Info("scan-snapshot-saved", LastPath);
        }

        /// <summary>
        /// Archives a copy into history (ring buffer). Returns entry id.
        /// </summary>
        public string ArchiveToHistory(StoredScanBaseline baseline, string name)
        {
            if (baseline == null)
                return null;

            try
            {
                Directory.CreateDirectory(HistoryDir);
                var id = Guid.NewGuid().ToString("N");
                var copy = Clone(baseline);
                copy.Id = id;
                copy.Name = string.IsNullOrWhiteSpace(name)
                    ? ("Скан " + baseline.GeneratedAt.ToString("yyyy-MM-dd HH:mm"))
                    : name.Trim();

                var path = Path.Combine(HistoryDir, id + ".json");
                WriteBaseline(path, copy);

                var index = LoadIndex();
                index.Entries.Insert(0, new ScanHistoryEntry
                {
                    Id = id,
                    Name = copy.Name,
                    GeneratedAt = copy.GeneratedAt,
                    ScanMode = copy.ScanMode
                });

                while (index.Entries.Count > MaxHistory)
                {
                    var drop = index.Entries[index.Entries.Count - 1];
                    index.Entries.RemoveAt(index.Entries.Count - 1);
                    TryDeleteHistoryFile(drop.Id);
                }

                SaveIndex(index);
                AnalyticsLogger.Info("scan-history-archived", id + " · " + copy.Name);
                return id;
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("scan-history-archive", ex.Message);
                return null;
            }
        }

        public string SaveNamed(StoredScanBaseline baseline, string name)
        {
            return ArchiveToHistory(baseline, name);
        }

        public List<ScanHistoryEntry> ListHistory()
        {
            var index = LoadIndex();
            // Drop missing files
            var kept = new List<ScanHistoryEntry>();
            foreach (var e in index.Entries ?? new List<ScanHistoryEntry>())
            {
                if (e == null || string.IsNullOrWhiteSpace(e.Id))
                    continue;
                var path = Path.Combine(HistoryDir, e.Id + ".json");
                if (File.Exists(path))
                    kept.Add(e);
            }
            if (kept.Count != (index.Entries == null ? 0 : index.Entries.Count))
            {
                index.Entries = kept;
                SaveIndex(index);
            }
            return kept.OrderByDescending(e => e.GeneratedAt).ToList();
        }

        public bool DeleteHistory(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "last")
                return false;
            try
            {
                TryDeleteHistoryFile(id);
                var index = LoadIndex();
                index.Entries = (index.Entries ?? new List<ScanHistoryEntry>())
                    .Where(e => e != null && e.Id != id)
                    .ToList();
                SaveIndex(index);
                return true;
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("scan-history-delete", ex.Message);
                return false;
            }
        }

        private void TryDeleteHistoryFile(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;
            var path = Path.Combine(HistoryDir, id + ".json");
            if (File.Exists(path))
                File.Delete(path);
        }

        private ScanHistoryIndex LoadIndex()
        {
            try
            {
                if (!File.Exists(IndexPath))
                    return new ScanHistoryIndex();
                using (var stream = File.OpenRead(IndexPath))
                {
                    var ser = new DataContractJsonSerializer(typeof(ScanHistoryIndex));
                    var loaded = ser.ReadObject(stream) as ScanHistoryIndex;
                    return loaded ?? new ScanHistoryIndex();
                }
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("scan-history-index-load", ex.Message);
                return new ScanHistoryIndex();
            }
        }

        private void SaveIndex(ScanHistoryIndex index)
        {
            if (index == null)
                return;
            try
            {
                Directory.CreateDirectory(RootDir);
                var tmp = IndexPath + ".tmp";
                using (var stream = File.Create(tmp))
                {
                    var ser = new DataContractJsonSerializer(typeof(ScanHistoryIndex));
                    ser.WriteObject(stream, index);
                }
                if (File.Exists(IndexPath))
                    File.Delete(IndexPath);
                File.Move(tmp, IndexPath);
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("scan-history-index-save", ex.Message);
            }
        }

        private static StoredScanBaseline TryReadBaseline(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return null;
                using (var stream = File.OpenRead(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(StoredScanBaseline));
                    return ser.ReadObject(stream) as StoredScanBaseline;
                }
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("scan-snapshot-load", ex.Message);
                return null;
            }
        }

        private static void WriteBaseline(string path, StoredScanBaseline baseline)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tmp = path + ".tmp";
            using (var stream = File.Create(tmp))
            {
                var ser = new DataContractJsonSerializer(typeof(StoredScanBaseline));
                ser.WriteObject(stream, baseline);
            }
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
        }

        private static StoredScanBaseline Clone(StoredScanBaseline src)
        {
            // Round-trip via serializer for a deep-enough copy
            using (var ms = new MemoryStream())
            {
                var ser = new DataContractJsonSerializer(typeof(StoredScanBaseline));
                ser.WriteObject(ms, src);
                ms.Position = 0;
                return ser.ReadObject(ms) as StoredScanBaseline;
            }
        }
    }
}
