using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    internal sealed class DashboardLayoutStore
    {
        private static string PathFile
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PilotBim.Analytics",
                    "dashboard-layout.json");
            }
        }

        public DashboardLayoutState LoadOrDefault()
        {
            try
            {
                var path = PathFile;
                if (File.Exists(path))
                {
                    using (var stream = File.OpenRead(path))
                    {
                        var ser = new DataContractJsonSerializer(typeof(DashboardLayoutState));
                        var loaded = ser.ReadObject(stream) as DashboardLayoutState;
                        if (loaded != null)
                            return Normalize(loaded);
                    }
                }
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("dashboard-layout-load", ex.Message);
            }

            var defaults = Default();
            Save(defaults);
            return defaults;
        }

        public void Save(DashboardLayoutState state)
        {
            if (state == null)
                return;
            try
            {
                var path = PathFile;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                var tmp = path + ".tmp";
                var normalized = Normalize(state);
                using (var stream = File.Create(tmp))
                {
                    var ser = new DataContractJsonSerializer(typeof(DashboardLayoutState));
                    ser.WriteObject(stream, normalized);
                }
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
                AnalyticsLogger.Info("dashboard-layout-saved", path);
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("dashboard-layout-save", ex.Message);
            }
        }

        public static DashboardLayoutState Default()
        {
            return new DashboardLayoutState
            {
                Blocks = new List<DashboardBlockState>(),
                Widgets = new List<DashboardWidgetState>
                {
                    CreateBuiltin(DashboardBlockIds.Kpi, "KPI / сводка", DashboardWidgetKinds.Kpi, 0),
                    CreateBuiltin(DashboardBlockIds.Bim, "BIM", DashboardWidgetKinds.Bim, 1),
                    CreateBuiltin(DashboardBlockIds.Responsible, "Ответственные", DashboardWidgetKinds.Responsible, 2)
                }
            };
        }

        public static DashboardWidgetState CreateBuiltin(string id, string title, string kind, int order)
        {
            return new DashboardWidgetState
            {
                Id = id,
                Title = title,
                WidgetKind = kind,
                IsVisible = true,
                Order = order,
                ColumnSpan = 2,
                ChartSource = "Types",
                ChartKind = "HorizontalBar",
                TopN = 12
            };
        }

        public static DashboardWidgetState CreateChartWidget(
            string title,
            string chartSource,
            string chartKind,
            int topN,
            int columnSpan,
            int order)
        {
            return new DashboardWidgetState
            {
                Id = "chart-" + Guid.NewGuid().ToString("N"),
                Title = string.IsNullOrWhiteSpace(title) ? "График" : title.Trim(),
                WidgetKind = DashboardWidgetKinds.Chart,
                IsVisible = true,
                Order = order,
                ColumnSpan = columnSpan <= 1 ? 1 : 2,
                ChartSource = string.IsNullOrWhiteSpace(chartSource) ? "Types" : chartSource,
                ChartKind = string.IsNullOrWhiteSpace(chartKind) ? "HorizontalBar" : chartKind,
                TopN = topN
            };
        }

        public static DashboardLayoutState Normalize(DashboardLayoutState state)
        {
            if (state == null)
                return Default();

            var widgets = new List<DashboardWidgetState>();

            if (state.Widgets != null && state.Widgets.Count > 0)
            {
                foreach (var w in state.Widgets)
                {
                    if (w == null || string.IsNullOrWhiteSpace(w.Id))
                        continue;
                    widgets.Add(SanitizeWidget(w));
                }
            }
            else if (state.Blocks != null && state.Blocks.Count > 0)
            {
                foreach (var b in state.Blocks.OrderBy(x => x.Order))
                {
                    if (b == null || string.IsNullOrWhiteSpace(b.Id))
                        continue;
                    var mapped = MapLegacyBlock(b);
                    if (mapped != null)
                        widgets.Add(mapped);
                }
            }

            EnsureBuiltin(widgets, DashboardBlockIds.Kpi, "KPI / сводка", DashboardWidgetKinds.Kpi);
            EnsureBuiltin(widgets, DashboardBlockIds.Bim, "BIM", DashboardWidgetKinds.Bim);
            EnsureBuiltin(widgets, DashboardBlockIds.Responsible, "Ответственные", DashboardWidgetKinds.Responsible);

            // Deduplicate by Id
            widgets = widgets
                .GroupBy(w => w.Id)
                .Select(g => g.First())
                .OrderBy(w => w.Order)
                .Select((w, i) =>
                {
                    w.Order = i;
                    return w;
                })
                .ToList();

            // Keep Blocks empty going forward (legacy field retained for schema)
            return new DashboardLayoutState
            {
                Blocks = new List<DashboardBlockState>(),
                Widgets = widgets
            };
        }

        private static void EnsureBuiltin(List<DashboardWidgetState> widgets, string id, string title, string kind)
        {
            if (widgets.Any(w => w.Id == id))
                return;
            widgets.Add(CreateBuiltin(id, title, kind, widgets.Count));
        }

        private static DashboardWidgetState MapLegacyBlock(DashboardBlockState block)
        {
            if (block.Id == DashboardBlockIds.Kpi)
                return CreateBuiltin(DashboardBlockIds.Kpi, "KPI / сводка", DashboardWidgetKinds.Kpi, block.Order);
            if (block.Id == DashboardBlockIds.Bim)
                return CreateBuiltin(DashboardBlockIds.Bim, "BIM", DashboardWidgetKinds.Bim, block.Order);
            if (block.Id == DashboardBlockIds.Responsible)
                return CreateBuiltin(DashboardBlockIds.Responsible, "Ответственные", DashboardWidgetKinds.Responsible, block.Order);

            // Unknown legacy id — skip
            return null;
        }

        private static DashboardWidgetState SanitizeWidget(DashboardWidgetState w)
        {
            var kind = w.WidgetKind;
            if (string.IsNullOrWhiteSpace(kind))
            {
                if (w.Id == DashboardBlockIds.Kpi)
                    kind = DashboardWidgetKinds.Kpi;
                else if (w.Id == DashboardBlockIds.Bim)
                    kind = DashboardWidgetKinds.Bim;
                else if (w.Id == DashboardBlockIds.Responsible)
                    kind = DashboardWidgetKinds.Responsible;
                else
                    kind = DashboardWidgetKinds.Chart;
            }

            if (kind != DashboardWidgetKinds.Kpi
                && kind != DashboardWidgetKinds.Bim
                && kind != DashboardWidgetKinds.Responsible
                && kind != DashboardWidgetKinds.Chart)
            {
                kind = DashboardWidgetKinds.Chart;
            }

            var title = w.Title;
            if (string.IsNullOrWhiteSpace(title))
            {
                if (kind == DashboardWidgetKinds.Kpi)
                    title = "KPI / сводка";
                else if (kind == DashboardWidgetKinds.Bim)
                    title = "BIM";
                else if (kind == DashboardWidgetKinds.Responsible)
                    title = "Ответственные";
                else
                    title = "График";
            }

            var span = w.ColumnSpan <= 1 ? 1 : 2;
            if (kind != DashboardWidgetKinds.Chart)
                span = 2;

            return new DashboardWidgetState
            {
                Id = w.Id,
                Title = title.Trim(),
                WidgetKind = kind,
                IsVisible = w.IsVisible,
                Order = w.Order,
                ColumnSpan = span,
                ChartSource = string.IsNullOrWhiteSpace(w.ChartSource) ? "Types" : w.ChartSource,
                ChartKind = string.IsNullOrWhiteSpace(w.ChartKind) ? "HorizontalBar" : w.ChartKind,
                TopN = w.TopN < 0 ? 12 : w.TopN
            };
        }
    }
}
