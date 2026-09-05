using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    internal sealed class ScanDiffService
    {
        private static readonly string[] KpiLabels =
        {
            "Всего объектов",
            "BIM моделей",
            "Частей моделей",
            "BIM элементов (индекс)",
            "Пользователей",
            "Организаций"
        };

        public StoredScanBaseline Capture(ProjectAnalyticsSnapshot snapshot)
        {
            if (snapshot == null)
                return null;

            var baseline = new StoredScanBaseline
            {
                GeneratedAt = snapshot.GeneratedAt,
                ScanMode = snapshot.ScanMode,
                BimModelCount = SumBimModels(snapshot),
                BimPartCount = snapshot.BimPartAnalytics != null ? snapshot.BimPartAnalytics.Count : 0,
                BimElementCount = snapshot.BimPartAnalytics != null
                    ? snapshot.BimPartAnalytics.Sum(p => p.ElementCount)
                    : 0L
            };

            if (snapshot.Summary != null)
            {
                foreach (var label in KpiLabels)
                {
                    var row = snapshot.Summary.FirstOrDefault(k => k != null && k.Label == label);
                    if (row == null)
                        continue;
                    baseline.Kpis.Add(new StoredKpi
                    {
                        Label = row.Label,
                        Value = row.Value,
                        NumericValue = TryParseNumber(row.Value)
                    });
                }
            }

            if (snapshot.ObjectsByResponsible != null)
            {
                foreach (var r in snapshot.ObjectsByResponsible)
                {
                    if (r == null)
                        continue;
                    baseline.Responsible.Add(new StoredResponsible
                    {
                        OrgUnitId = r.OrgUnitId,
                        PersonId = r.PersonId,
                        DisplayName = r.DisplayName,
                        SampledCount = r.SampledCount
                    });
                }
            }

            baseline.TopTypes = TakeNamed(
                (snapshot.ObjectsByType ?? Enumerable.Empty<TypeCountRow>())
                    .OrderByDescending(r => r.Count)
                    .Select(r => new StoredNamedCount { Name = r.TypeName ?? "?", Count = r.Count }),
                15);

            baseline.StateSemantic = TakeNamed(
                (snapshot.ObjectsByUserStateSemantic ?? Enumerable.Empty<StateSemanticCountRow>())
                    .OrderByDescending(r => r.Count)
                    .Select(r => new StoredNamedCount { Name = r.Semantic ?? "?", Count = r.Count }),
                10);

            baseline.TopIfcTypes = TakeNamed(
                (snapshot.BimElementTypeCounts ?? Enumerable.Empty<BimElementTypeCountRow>())
                    .GroupBy(r => r.IfcType ?? "?")
                    .Select(g => new StoredNamedCount { Name = g.Key, Count = g.Sum(x => x.Count) })
                    .OrderByDescending(x => x.Count),
                15);

            baseline.Remarks = TakeNamed(
                (snapshot.ModelRemarks ?? Enumerable.Empty<RemarkTypeRow>())
                    .OrderByDescending(r => r.ObjectCount)
                    .Select(r => new StoredNamedCount { Name = r.TypeName ?? "?", Count = r.ObjectCount }),
                15);

            return baseline;
        }

        private static List<StoredNamedCount> TakeNamed(IEnumerable<StoredNamedCount> items, int take)
        {
            return (items ?? Enumerable.Empty<StoredNamedCount>())
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Name))
                .Take(take)
                .ToList();
        }

        public List<ScanDiffRow> Diff(StoredScanBaseline previous, StoredScanBaseline current)
        {
            var rows = new List<ScanDiffRow>();
            if (current == null)
                return rows;

            if (previous == null)
            {
                rows.Add(new ScanDiffRow
                {
                    Area = "Скан",
                    Metric = "Базовый snapshot",
                    Previous = "—",
                    Current = current.GeneratedAt.ToString("g"),
                    Delta = "n/a",
                    Notes = "Первый сохранённый скан — сравнить можно после следующего Обновить"
                });
                return rows;
            }

            rows.Add(new ScanDiffRow
            {
                Area = "Скан",
                Metric = "Время скана",
                Previous = previous.GeneratedAt.ToString("g"),
                Current = current.GeneratedAt.ToString("g"),
                Delta = FormatTimeDelta(current.GeneratedAt - previous.GeneratedAt),
                Notes = (previous.ScanMode ?? "?") + " → " + (current.ScanMode ?? "?")
            });

            AddNumeric(rows, "BIM", "BIM элементов (индекс)", previous.BimElementCount, current.BimElementCount, null);
            AddNumeric(rows, "BIM", "BIM моделей", previous.BimModelCount, current.BimModelCount, null);
            AddNumeric(rows, "BIM", "Частей моделей", previous.BimPartCount, current.BimPartCount, null);

            var prevKpis = (previous.Kpis ?? new List<StoredKpi>()).ToDictionary(k => k.Label ?? "", k => k);
            var currKpis = (current.Kpis ?? new List<StoredKpi>()).ToDictionary(k => k.Label ?? "", k => k);
            foreach (var label in KpiLabels)
            {
                StoredKpi p;
                StoredKpi c;
                prevKpis.TryGetValue(label, out p);
                currKpis.TryGetValue(label, out c);
                if (p == null && c == null)
                    continue;
                if (label.StartsWith("BIM", StringComparison.Ordinal))
                    continue; // already covered by totals

                double? pn = p != null ? p.NumericValue : null;
                double? cn = c != null ? c.NumericValue : null;
                if (pn.HasValue && cn.HasValue)
                    AddNumeric(rows, "KPI", label, pn.Value, cn.Value, null);
                else
                {
                    rows.Add(new ScanDiffRow
                    {
                        Area = "KPI",
                        Metric = label,
                        Previous = p != null ? p.Value : "—",
                        Current = c != null ? c.Value : "—",
                        Delta = "n/a",
                        Notes = null
                    });
                }
            }

            var prevResp = (previous.Responsible ?? new List<StoredResponsible>())
                .GroupBy(ResponsibleKey)
                .ToDictionary(g => g.Key, g => g.First());
            var currResp = (current.Responsible ?? new List<StoredResponsible>())
                .GroupBy(ResponsibleKey)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var key in prevResp.Keys.Union(currResp.Keys).OrderBy(k => k))
            {
                StoredResponsible p;
                StoredResponsible c;
                prevResp.TryGetValue(key, out p);
                currResp.TryGetValue(key, out c);
                var name = c != null ? c.DisplayName : (p != null ? p.DisplayName : key);
                var prevCount = p != null ? p.SampledCount : 0;
                var currCount = c != null ? c.SampledCount : 0;
                if (prevCount == currCount && p != null && c != null)
                    continue;
                AddNumeric(rows, "Ответственные", name ?? key, prevCount, currCount,
                    p == null ? "новый" : (c == null ? "исчез из сэмпла" : null));
            }

            DiffNamedCounts(rows, "Типы", previous.TopTypes, current.TopTypes);
            DiffNamedCounts(rows, "OPEN/CLOSED", previous.StateSemantic, current.StateSemantic);
            DiffNamedCounts(rows, "IFC", previous.TopIfcTypes, current.TopIfcTypes);
            DiffNamedCounts(rows, "Замечания", previous.Remarks, current.Remarks);

            if (rows.Count == 1)
            {
                rows.Add(new ScanDiffRow
                {
                    Area = "Скан",
                    Metric = "Изменения",
                    Previous = "—",
                    Current = "—",
                    Delta = "0",
                    Notes = "KPI / BIM / распределения без изменений"
                });
            }

            return rows;
        }

        private static void DiffNamedCounts(
            List<ScanDiffRow> rows,
            string area,
            List<StoredNamedCount> previous,
            List<StoredNamedCount> current)
        {
            var prevMap = (previous ?? new List<StoredNamedCount>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Name)
                .ToDictionary(g => g.Key, g => g.First().Count);
            var currMap = (current ?? new List<StoredNamedCount>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Name)
                .ToDictionary(g => g.Key, g => g.First().Count);

            foreach (var key in prevMap.Keys.Union(currMap.Keys).OrderBy(k => k))
            {
                double p;
                double c;
                prevMap.TryGetValue(key, out p);
                currMap.TryGetValue(key, out c);
                if (Math.Abs(p - c) < 0.0001 && prevMap.ContainsKey(key) && currMap.ContainsKey(key))
                    continue;
                string notes = null;
                if (!prevMap.ContainsKey(key))
                    notes = "новый в top";
                else if (!currMap.ContainsKey(key))
                    notes = "выпал из top";
                AddNumeric(rows, area, key, p, c, notes);
            }
        }

        private static string ResponsibleKey(StoredResponsible r)
        {
            if (r == null)
                return "null";
            if (r.PersonId.HasValue && r.PersonId.Value != 0)
                return "p:" + r.PersonId.Value;
            return "o:" + r.OrgUnitId;
        }

        private static long SumBimModels(ProjectAnalyticsSnapshot snapshot)
        {
            if (snapshot.BimModelAnalytics != null && snapshot.BimModelAnalytics.Count > 0)
                return snapshot.BimModelAnalytics.Count;
            return 0;
        }

        private static void AddNumeric(List<ScanDiffRow> rows, string area, string metric, double previous, double current, string notes)
        {
            var delta = current - previous;
            rows.Add(new ScanDiffRow
            {
                Area = area,
                Metric = metric,
                Previous = FormatNum(previous),
                Current = FormatNum(current),
                Delta = (delta > 0 ? "+" : "") + FormatNum(delta),
                Notes = notes
            });
        }

        private static string FormatNum(double value)
        {
            if (Math.Abs(value % 1) < 0.0001)
                return ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatTimeDelta(TimeSpan span)
        {
            if (span.TotalDays >= 1)
                return span.TotalDays.ToString("0.0", CultureInfo.InvariantCulture) + " d";
            if (span.TotalHours >= 1)
                return span.TotalHours.ToString("0.0", CultureInfo.InvariantCulture) + " h";
            return span.TotalMinutes.ToString("0.0", CultureInfo.InvariantCulture) + " min";
        }

        private static double? TryParseNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var cleaned = Regex.Replace(value, @"[^\d\-,\.]", "");
            cleaned = cleaned.Replace(',', '.');
            double n;
            if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out n))
                return n;
            return null;
        }
    }
}