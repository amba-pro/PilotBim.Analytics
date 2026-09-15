using System.Collections.Generic;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardVisualizationConstraintsTests
    {
        [Fact]
        public void QueryMins()
        {
            Assert.Equal(3, DashboardVisualizationConstraints.ForQuery("Kpi", true).MinWidth);
            Assert.Equal(2, DashboardVisualizationConstraints.ForQuery("Kpi", true).MinHeight);
            Assert.Equal(4, DashboardVisualizationConstraints.ForQuery("Bar", false).MinWidth);
            Assert.Equal(3, DashboardVisualizationConstraints.ForQuery("Bar", false).MinHeight);
            Assert.Equal(4, DashboardVisualizationConstraints.ForQuery("HorizontalBar", false).MinWidth);
            Assert.Equal(4, DashboardVisualizationConstraints.ForQuery("Pie", false).MinWidth);
            Assert.Equal(4, DashboardVisualizationConstraints.ForQuery("Table", false).MinWidth);
            Assert.Equal(3, DashboardVisualizationConstraints.ForQuery("Auto", true).MinWidth);
            Assert.Equal(2, DashboardVisualizationConstraints.ForQuery("Auto", true).MinHeight);
            Assert.Equal(4, DashboardVisualizationConstraints.ForQuery("Auto", false).MinWidth);
            Assert.Equal(3, DashboardVisualizationConstraints.ForQuery("Auto", false).MinHeight);
        }

        [Fact]
        public void ResizeBelowMin_Clamps()
        {
            var def = Def(Query("q1", "Bar", false, 3, 2));
            DashboardGridLayoutEngine.Resize(def, "q1", new DashboardGridRect(0, 0, 2, 1), 4, 3);
            Assert.Equal(4, def.Widgets[0].Layout.Width);
            Assert.Equal(3, def.Widgets[0].Layout.Height);
        }

        [Fact]
        public void VisualizationChange_ExpandsAndResolvesCollision()
        {
            var kpi = Query("kpi", "Kpi", true, 0, 0, 3, 2);
            var other = Query("other", "Bar", false, 0, 2, 6, 3);
            var def = Def(kpi, other);
            kpi.Visualization.Type = "Table";
            DashboardVisualizationConstraints.ExpandToMin(def, kpi);
            Assert.True(kpi.Layout.Width >= 4);
            Assert.True(kpi.Layout.Height >= 3);
            Assert.False(DashboardGridLayoutEngine.AnyVisibleOverlap(def.Widgets));
            var again = Def(Query("kpi", "Kpi", true, 0, 0, 3, 2), Query("other", "Bar", false, 0, 2, 6, 3));
            again.Widgets[0].Visualization.Type = "Table";
            DashboardVisualizationConstraints.ExpandToMin(again, again.Widgets[0]);
            Assert.Equal(kpi.Layout.Y, again.Widgets[0].Layout.Y);
            Assert.Equal(def.Widgets[1].Layout.Y, again.Widgets[1].Layout.Y);
        }

        private static DashboardDefinition Def(params DashboardWidgetDefinition[] widgets)
        {
            return new DashboardDefinition
            {
                SchemaVersion = 3,
                Id = "default",
                Title = "Dashboard",
                ProjectKey = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                Widgets = new List<DashboardWidgetDefinition>(widgets)
            };
        }

        private static DashboardWidgetDefinition Query(
            string id,
            string viz,
            bool scalar,
            int x,
            int y,
            int width = 6,
            int height = 3)
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = id,
                ContentKind = DashboardPersistenceV2.ContentQuery,
                Layout = new DashboardWidgetLayoutDefinition
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    IsVisible = true
                },
                Query = DashboardQueryPersistence.ToDocument(new DashboardWidgetQuery(
                    DashboardQueryScopeKind.CurrentProject,
                    scalar ? null : "attribute:1:Name",
                    DashboardQueryMeasure.Count,
                    DashboardQuerySort.ValueDescending,
                    null,
                    1)),
                Visualization = new DashboardVisualizationDefinition { Type = viz }
            };
        }
    }
}
