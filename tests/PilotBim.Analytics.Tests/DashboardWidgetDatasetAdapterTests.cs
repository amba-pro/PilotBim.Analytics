using System.Collections.Generic;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Properties;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardWidgetDatasetAdapterTests
    {
        [Fact]
        public void Auto_Scalar_ResolvesKpi()
        {
            Assert.Equal("Kpi", DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", false, 1));
        }

        [Fact]
        public void Auto_GroupedSmall_ResolvesBar()
        {
            Assert.Equal("Bar", DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", true, 5));
        }

        [Fact]
        public void Auto_GroupedSixOrMore_ResolvesHorizontalBar()
        {
            Assert.Equal("HorizontalBar", DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", true, 6));
            Assert.Equal("HorizontalBar", DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", true, 15));
        }

        [Fact]
        public void Auto_GroupedSixteenOrMore_ResolvesTable()
        {
            Assert.Equal("Table", DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", true, 16));
            Assert.Equal("Table", DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", true, 20));
        }

        [Fact]
        public void Auto_DoesNotSelectPie()
        {
            Assert.NotEqual("Pie", DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", true, 3));
            Assert.NotEqual("Pie", DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", true, 12));
        }

        [Fact]
        public void Auto_DoesNotSelectLine()
        {
            Assert.NotEqual("Line", DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", false, 1));
            Assert.NotEqual("Line", DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", true, 8));
        }

        [Fact]
        public void Auto_IsDeterministic()
        {
            Assert.Equal(
                DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", true, 7),
                DashboardWidgetDatasetAdapter.ResolveVisualization("Auto", true, 7));
        }

        [Fact]
        public void Scalar_Kpi_OneRow()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(Dataset(Row("total", "Total", 42)), "Kpi", false);
            Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, model.Status);
            Assert.True(model.ShowKpi);
            Assert.False(model.ShowChart);
            Assert.Equal(DashboardVisualizationFormat.Count(42), model.KpiRows[0].Value);
        }

        [Fact]
        public void Scalar_Kpi_ZeroIsSuccess()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(Dataset(Row("total", "Total", 0)), "Kpi", false);
            Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, model.Status);
            Assert.Equal(DashboardVisualizationFormat.Count(0), model.KpiRows[0].Value);
        }

        [Fact]
        public void Scalar_Bar_Invalid()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(Dataset(Row("total", "Total", 4)), "Bar", false);
            Assert.Equal(DashboardQueryWidgetRuntimeStatus.Invalid, model.Status);
            Assert.False(model.ShowChart);
        }

        [Fact]
        public void Grouped_Kpi_Invalid()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(Dataset(Row("a", "A", 1)), "Kpi", true);
            Assert.Equal(DashboardQueryWidgetRuntimeStatus.Invalid, model.Status);
            Assert.False(model.ShowKpi);
        }

        [Fact]
        public void Scalar_Kpi_WrongShape_Invalid()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(
                Dataset(Row("a", "A", 1), Row("b", "B", 2)),
                "Kpi",
                false);
            Assert.Equal(DashboardQueryWidgetRuntimeStatus.Invalid, model.Status);
            Assert.False(model.ShowKpi);
            Assert.Null(model.KpiRows);
        }

        [Fact]
        public void Grouped_Bar_Vertical()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(
                Dataset(Row("open", "Open", 3), Row("closed", "Closed", 1)),
                "Bar",
                true);
            Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, model.Status);
            Assert.True(model.ShowChart);
            Assert.Equal(AnalyticsChartKind.VerticalBar, model.ChartKind);
            Assert.Equal(2, model.Points.Count);
            Assert.Equal("Open", model.Points[0].Label);
            Assert.Equal(3, model.Points[0].Value);
        }

        [Fact]
        public void Grouped_HorizontalBar()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(Dataset(Row("a", "A", 5)), "HorizontalBar", true);
            Assert.Equal(AnalyticsChartKind.HorizontalBar, model.ChartKind);
            Assert.True(model.ShowChart);
        }

        [Fact]
        public void Grouped_Pie()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(
                Dataset(Row("a", "A", 1), Row("b", "B", 1)),
                "Pie",
                true);
            Assert.Equal(AnalyticsChartKind.Pie, model.ChartKind);
            Assert.True(model.ShowChart);
        }

        [Fact]
        public void Table_CategoryAndValue()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(
                Dataset(Row("a", "Alpha", 9)),
                "Table",
                true);
            Assert.True(model.ShowTable);
            Assert.Equal("Alpha", model.TableRows[0].Category);
            Assert.Equal(DashboardVisualizationFormat.Count(9), model.TableRows[0].Value);
        }

        [Fact]
        public void Table_Scalar_UsesIndicatorColumn()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(Dataset(Row("total", "Total", 4)), "Table", false);
            Assert.True(model.ShowTable);
            Assert.True(model.TableIsScalar);
            Assert.Equal(Resources.QueryTable_Indicator, model.TableRows[0].Category);
            Assert.Equal(DashboardVisualizationFormat.Count(4), model.TableRows[0].Value);
        }

        [Fact]
        public void Table_MissingLabel_UsesDisplayPlaceholder_DatasetUnchanged()
        {
            var row = Row("k", "", 3);
            var model = DashboardWidgetDatasetAdapter.TryRender(Dataset(row), "Table", true);
            Assert.Equal(Resources.QueryViz_NotSet, model.TableRows[0].Category);
            Assert.Equal(string.Empty, row.Label);
        }

        [Fact]
        public void DuplicateDisplayLabels_RemainSeparate()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(
                Dataset(Row("a", "Same", 1), Row("b", "Same", 2)),
                "Bar",
                true);
            Assert.Equal(2, model.Points.Count);
            Assert.Equal("Same", model.Points[0].Label);
            Assert.Equal("Same", model.Points[1].Label);
            Assert.Equal(1, model.Points[0].Value);
            Assert.Equal(2, model.Points[1].Value);
        }

        [Fact]
        public void Pie_ManySlices_WarnsButStillRenders()
        {
            var rows = new WidgetDataRow[9];
            for (var i = 0; i < rows.Length; i++)
                rows[i] = Row("k" + i, "L" + i, i + 1);
            var model = DashboardWidgetDatasetAdapter.TryRender(new WidgetDataset(rows), "Pie", true);
            Assert.True(model.ShowChart);
            Assert.Equal(AnalyticsChartKind.Pie, model.ChartKind);
            Assert.Equal(Resources.QueryViz_PieTooMany, model.Warning);
            Assert.Equal(9, model.Points.Count);
        }

        [Fact]
        public void Auto_Grouped_RendersBarModel()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(
                Dataset(Row("a", "A", 1), Row("b", "B", 2)),
                "Auto",
                true);
            Assert.Equal("Bar", model.ResolvedVisualization);
            Assert.Equal(AnalyticsChartKind.VerticalBar, model.ChartKind);
        }

        [Fact]
        public void Auto_GroupedLarge_RendersHorizontalBar()
        {
            var rows = new WidgetDataRow[6];
            for (var i = 0; i < 6; i++)
                rows[i] = Row("k" + i, "L" + i, i + 1);
            var model = DashboardWidgetDatasetAdapter.TryRender(new WidgetDataset(rows), "Auto", true);
            Assert.Equal("HorizontalBar", model.ResolvedVisualization);
            Assert.Equal(AnalyticsChartKind.HorizontalBar, model.ChartKind);
        }

        [Fact]
        public void Line_Unsupported_NoChart()
        {
            var model = DashboardWidgetDatasetAdapter.TryRender(Dataset(Row("a", "A", 1)), "Line", true);
            Assert.Equal(DashboardQueryWidgetRuntimeStatus.Unsupported, model.Status);
            Assert.False(model.ShowChart);
            Assert.Null(model.Points);
        }

        private static WidgetDataset Dataset(params WidgetDataRow[] rows)
        {
            return new WidgetDataset(rows);
        }

        private static WidgetDataRow Row(string key, string label, long value)
        {
            return new WidgetDataRow(key, label, value);
        }
    }
}
