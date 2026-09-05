using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    internal sealed class ChartDataService
    {
        private const double MaxBarWidth = 420;
        private const double MaxColumnHeight = 220;

        private static readonly string[] Palette =
        {
            "#3A7D5C", "#2F6B9A", "#C47A2C", "#8B4D6B", "#5B6B3A",
            "#4A6FA5", "#A65D3F", "#3D7A7A", "#6B5B95", "#7A6A3D",
            "#2E7D6F", "#9A4E4E", "#4E6B8A", "#7D6B3A", "#5C6B7A"
        };

        public List<ChartBarRow> BuildTopTypes(ProjectAnalyticsSnapshot snapshot, int take)
        {
            return ToBars(ExtractRaw(snapshot, null, AnalyticsChartSource.Types, take));
        }

        public List<ChartBarRow> BuildTopCreators(ProjectAnalyticsSnapshot snapshot, int take)
        {
            return ToBars(ExtractRaw(snapshot, null, AnalyticsChartSource.Creators, take));
        }

        public List<ChartBarRow> BuildCreatedTimeline(ProjectAnalyticsSnapshot snapshot)
        {
            return ToBars(ExtractRaw(snapshot, null, AnalyticsChartSource.CreatedMonth, 0));
        }

        public List<ChartBarRow> BuildTopIfcTypes(IEnumerable<BimElementTypeCountRow> rows, int take)
        {
            if (rows == null)
                return new List<ChartBarRow>();

            var aggregated = rows
                .GroupBy(r => r.IfcType ?? "?")
                .Select(g => Tuple.Create(g.Key, (double)g.Sum(x => x.Count), g.Sum(x => x.Count).ToString()))
                .OrderByDescending(x => x.Item2)
                .Take(take > 0 ? take : int.MaxValue);

            return ToBars(aggregated);
        }

        public List<ChartBarRow> BuildStateSemantic(ProjectAnalyticsSnapshot snapshot)
        {
            return ToBars(ExtractRaw(snapshot, null, AnalyticsChartSource.StateSemantic, 0));
        }

        public List<ChartBarRow> BuildTopResponsible(ProjectAnalyticsSnapshot snapshot, int take)
        {
            return ToBars(ExtractRaw(snapshot, null, AnalyticsChartSource.Responsible, take));
        }

        public List<ChartSeriesPoint> BuildSeries(
            ProjectAnalyticsSnapshot snapshot,
            IEnumerable<BimElementTypeCountRow> ifcRows,
            AnalyticsChartSource source,
            AnalyticsChartKind kind,
            int take)
        {
            var raw = ExtractRaw(snapshot, ifcRows, source, take);
            // kind is selected by the caller for rendering; geometry fields are populated for all kinds.
            _ = kind;
            return ToSeries(raw);
        }

        private static IEnumerable<Tuple<string, double, string>> ExtractRaw(
            ProjectAnalyticsSnapshot snapshot,
            IEnumerable<BimElementTypeCountRow> ifcRows,
            AnalyticsChartSource source,
            int take)
        {
            if (snapshot == null)
                return Enumerable.Empty<Tuple<string, double, string>>();

            IEnumerable<Tuple<string, double, string>> query;
            switch (source)
            {
                case AnalyticsChartSource.Creators:
                    query = (snapshot.ObjectsByCreator ?? new List<CreatorCountRow>())
                        .Select(r => Tuple.Create(r.DisplayName ?? "?", (double)r.SampledCount, r.SampledCount.ToString()))
                        .OrderByDescending(t => t.Item2);
                    break;
                case AnalyticsChartSource.CreatedMonth:
                    query = (snapshot.ObjectsByCreatedMonth ?? new List<PeriodCountRow>())
                        .OrderBy(r => r.Period)
                        .Select(r => Tuple.Create(r.Period ?? "?", (double)r.Count, r.Count.ToString()));
                    break;
                case AnalyticsChartSource.UserStates:
                    query = (snapshot.ObjectsByUserState ?? new List<StateCountRow>())
                        .Select(r => Tuple.Create(r.StateTitle ?? "?", (double)r.Count, r.Count.ToString()))
                        .OrderByDescending(t => t.Item2);
                    break;
                case AnalyticsChartSource.StateSemantic:
                    query = (snapshot.ObjectsByUserStateSemantic ?? new List<StateSemanticCountRow>())
                        .Select(r => Tuple.Create(r.Semantic ?? "?", (double)r.Count, r.Count.ToString()))
                        .OrderByDescending(t => t.Item2);
                    break;
                case AnalyticsChartSource.Responsible:
                    query = (snapshot.ObjectsByResponsible ?? new List<ResponsibleCountRow>())
                        .Select(r => Tuple.Create(r.DisplayName ?? "?", (double)r.SampledCount, r.SampledCount.ToString()))
                        .OrderByDescending(t => t.Item2);
                    break;
                case AnalyticsChartSource.IfcTypes:
                    query = (ifcRows ?? Enumerable.Empty<BimElementTypeCountRow>())
                        .GroupBy(r => r.IfcType ?? "?")
                        .Select(g => Tuple.Create(g.Key, (double)g.Sum(x => x.Count), g.Sum(x => x.Count).ToString()))
                        .OrderByDescending(t => t.Item2);
                    break;
                case AnalyticsChartSource.BimModels:
                    query = (snapshot.BimModelAnalytics ?? new List<BimModelAnalyticsRow>())
                        .Select(r => Tuple.Create(r.ModelName ?? r.ModelId.ToString(), (double)r.ElementCount, r.ElementCountDisplay ?? r.ElementCount.ToString()))
                        .OrderByDescending(t => t.Item2);
                    break;
                case AnalyticsChartSource.Remarks:
                    query = (snapshot.ModelRemarks ?? new List<RemarkTypeRow>())
                        .Select(r => Tuple.Create(r.TypeName ?? "?", (double)r.ObjectCount, r.ObjectCount.ToString()))
                        .OrderByDescending(t => t.Item2);
                    break;
                default:
                    query = (snapshot.ObjectsByType ?? new List<TypeCountRow>())
                        .Select(r => Tuple.Create(r.TypeName ?? "?", (double)r.Count, r.CountDisplay ?? r.Count.ToString()))
                        .OrderByDescending(t => t.Item2);
                    break;
            }

            // Timeline and semantic: keep natural/full order; others apply top-N
            if (source == AnalyticsChartSource.CreatedMonth)
                return take > 0 ? query.Take(take) : query;

            if (take > 0)
                return query.Take(take);
            return query;
        }

        private static List<ChartBarRow> ToBars(IEnumerable<Tuple<string, double, string>> items)
        {
            var series = ToSeries(items);
            return series.Select(p => new ChartBarRow
            {
                Label = p.Label,
                Value = p.Value,
                ValueDisplay = p.ValueDisplay,
                SharePercent = p.SharePercent,
                BarWidth = p.BarWidth
            }).ToList();
        }

        private static List<ChartSeriesPoint> ToSeries(IEnumerable<Tuple<string, double, string>> items)
        {
            var list = items?.ToList() ?? new List<Tuple<string, double, string>>();
            if (list.Count == 0)
                return new List<ChartSeriesPoint>();

            var max = list.Max(i => i.Item2);
            if (max <= 0)
                max = 1;
            var total = list.Sum(i => i.Item2);
            if (total <= 0)
                total = 1;

            double pieCursor = -90; // start at top
            var result = new List<ChartSeriesPoint>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var share = item.Item2 / total * 100.0;
                var sweep = item.Item2 / total * 360.0;
                var point = new ChartSeriesPoint
                {
                    Label = item.Item1,
                    Value = item.Item2,
                    ValueDisplay = item.Item3,
                    SharePercent = share,
                    BarWidth = item.Item2 / max * MaxBarWidth,
                    ColumnHeight = item.Item2 / max * MaxColumnHeight,
                    LineY = item.Item2 / max * MaxColumnHeight,
                    LineX = i,
                    ColorHex = Palette[i % Palette.Length],
                    PieStartDegrees = pieCursor,
                    PieSweepDegrees = sweep
                };
                pieCursor += sweep;
                result.Add(point);
            }

            return result;
        }
    }
}
