using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardLayoutStoreTests
    {
        [Fact]
        public void Normalize_Null_ReturnsDefaultBuiltins()
        {
            var state = DashboardLayoutStore.Normalize(null);

            Assert.Empty(state.Blocks);
            Assert.Equal(3, state.Widgets.Count);
            Assert.Equal(new[] { "kpi", "bim", "responsible" }, state.Widgets.Select(w => w.Id).ToArray());
            Assert.Equal(new[] { 0, 1, 2 }, state.Widgets.Select(w => w.Order).ToArray());
            Assert.All(state.Widgets, w => Assert.True(w.IsVisible));
            Assert.All(state.Widgets, w => Assert.Equal(2, w.ColumnSpan));
        }

        [Fact]
        public void Normalize_EmptyWidgetsAndBlocks_EnsuresBuiltins()
        {
            var state = DashboardLayoutStore.Normalize(new DashboardLayoutState
            {
                Widgets = new List<DashboardWidgetState>(),
                Blocks = new List<DashboardBlockState>()
            });

            Assert.Equal(3, state.Widgets.Count);
            Assert.Empty(state.Blocks);
        }

        [Fact]
        public void Normalize_LegacyBlocks_MigrateToWidgets_AndClearBlocks()
        {
            var state = DashboardLayoutStore.Normalize(new DashboardLayoutState
            {
                Widgets = new List<DashboardWidgetState>(),
                Blocks = new List<DashboardBlockState>
                {
                    new DashboardBlockState { Id = "responsible", IsVisible = false, Order = 0 },
                    new DashboardBlockState { Id = "kpi", IsVisible = true, Order = 1 },
                    new DashboardBlockState { Id = "bim", IsVisible = true, Order = 2 },
                    new DashboardBlockState { Id = "unknown-legacy", Order = 3 }
                }
            });

            Assert.Empty(state.Blocks);
            // EnsureBuiltin adds missing; legacy mapped first in OrderBy block.Order
            // responsible(0), kpi(1), bim(2) — unknown skipped; all three present
            Assert.Equal(3, state.Widgets.Count);
            Assert.Equal("responsible", state.Widgets[0].Id);
            Assert.Equal("kpi", state.Widgets[1].Id);
            Assert.Equal("bim", state.Widgets[2].Id);
            // CreateBuiltin always sets IsVisible=true (legacy IsVisible is not preserved — current behavior)
            Assert.All(state.Widgets, w => Assert.True(w.IsVisible));
        }

        [Fact]
        public void Normalize_DuplicateIds_KeepsFirst_ReindexesOrder()
        {
            var state = DashboardLayoutStore.Normalize(new DashboardLayoutState
            {
                Widgets = new List<DashboardWidgetState>
                {
                    new DashboardWidgetState
                    {
                        Id = "chart-a",
                        Title = "First",
                        WidgetKind = DashboardWidgetKinds.Chart,
                        Order = 5,
                        ColumnSpan = 1,
                        TopN = 8
                    },
                    new DashboardWidgetState
                    {
                        Id = "chart-a",
                        Title = "SecondDuplicate",
                        WidgetKind = DashboardWidgetKinds.Chart,
                        Order = 1,
                        ColumnSpan = 1,
                        TopN = 20
                    },
                    new DashboardWidgetState
                    {
                        Id = "kpi",
                        Title = "KPI / сводка",
                        WidgetKind = DashboardWidgetKinds.Kpi,
                        Order = 0,
                        IsVisible = true
                    },
                    new DashboardWidgetState
                    {
                        Id = "bim",
                        Title = "BIM",
                        WidgetKind = DashboardWidgetKinds.Bim,
                        Order = 2,
                        IsVisible = true
                    },
                    new DashboardWidgetState
                    {
                        Id = "responsible",
                        Title = "Ответственные",
                        WidgetKind = DashboardWidgetKinds.Responsible,
                        Order = 3,
                        IsVisible = true
                    }
                }
            });

            var charts = state.Widgets.Where(w => w.Id == "chart-a").ToList();
            Assert.Single(charts);
            Assert.Equal("First", charts[0].Title);
            Assert.Equal(8, charts[0].TopN);

            // Orders reindexed 0..n-1 after OrderBy original Order
            Assert.Equal(Enumerable.Range(0, state.Widgets.Count), state.Widgets.Select(w => w.Order));
        }

        [Fact]
        public void Normalize_SanitizesUnknownKind_EmptyTitle_NegativeTopN_ColumnSpan()
        {
            var state = DashboardLayoutStore.Normalize(new DashboardLayoutState
            {
                Widgets = new List<DashboardWidgetState>
                {
                    new DashboardWidgetState
                    {
                        Id = "chart-x",
                        Title = "  ",
                        WidgetKind = "Weird",
                        Order = 0,
                        ColumnSpan = 0,
                        TopN = -5,
                        ChartSource = "",
                        ChartKind = null
                    },
                    new DashboardWidgetState
                    {
                        Id = "kpi",
                        WidgetKind = DashboardWidgetKinds.Kpi,
                        Order = 1
                    },
                    new DashboardWidgetState
                    {
                        Id = "bim",
                        WidgetKind = DashboardWidgetKinds.Bim,
                        Order = 2
                    },
                    new DashboardWidgetState
                    {
                        Id = "responsible",
                        WidgetKind = DashboardWidgetKinds.Responsible,
                        Order = 3
                    }
                }
            });

            var chart = state.Widgets.Single(w => w.Id == "chart-x");
            Assert.Equal(DashboardWidgetKinds.Chart, chart.WidgetKind);
            Assert.Equal("График", chart.Title);
            Assert.Equal(12, chart.TopN); // negative → 12
            Assert.Equal(1, chart.ColumnSpan); // <=1 → 1 for charts
            Assert.Equal("Types", chart.ChartSource);
            Assert.Equal("HorizontalBar", chart.ChartKind);

            var kpi = state.Widgets.Single(w => w.Id == "kpi");
            Assert.Equal(2, kpi.ColumnSpan); // non-chart forced to 2
        }

        [Fact]
        public void Normalize_SkipsNullOrBlankIds()
        {
            var state = DashboardLayoutStore.Normalize(new DashboardLayoutState
            {
                Widgets = new List<DashboardWidgetState>
                {
                    null,
                    new DashboardWidgetState { Id = "  ", WidgetKind = DashboardWidgetKinds.Chart },
                    new DashboardWidgetState { Id = "kpi", WidgetKind = DashboardWidgetKinds.Kpi, Title = "KPI / сводка" },
                    new DashboardWidgetState { Id = "bim", WidgetKind = DashboardWidgetKinds.Bim, Title = "BIM" },
                    new DashboardWidgetState { Id = "responsible", WidgetKind = DashboardWidgetKinds.Responsible, Title = "Ответственные" }
                }
            });

            Assert.Equal(3, state.Widgets.Count);
        }

        [Fact]
        public void Default_MatchesNormalizeNullBuiltins()
        {
            var d = DashboardLayoutStore.Default();
            var n = DashboardLayoutStore.Normalize(null);
            Assert.Equal(d.Widgets.Select(w => w.Id), n.Widgets.Select(w => w.Id));
            Assert.Equal(d.Widgets.Select(w => w.WidgetKind), n.Widgets.Select(w => w.WidgetKind));
        }
    }
}
