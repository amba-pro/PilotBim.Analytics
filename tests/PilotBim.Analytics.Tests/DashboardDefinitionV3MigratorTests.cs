using System;
using System.Collections.Generic;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardDefinitionV3MigratorTests
    {
        private static readonly Guid Project = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        [Fact]
        public void V2_HalfAndFullSpan_MapToGridWidth()
        {
            var v2 = V2(
                Legacy("half", 0, 1, DashboardWidgetKinds.Kpi),
                Legacy("full", 1, 2, DashboardWidgetKinds.Chart));
            var v3 = DashboardDefinitionV3Migrator.FromV2(v2);
            Assert.Equal(3, v3.SchemaVersion);
            Assert.Equal(6, Find(v3, "half").Layout.Width);
            Assert.Equal(12, Find(v3, "full").Layout.Width);
            Assert.Equal(0, Find(v3, "half").Layout.X);
            Assert.Equal(0, Find(v3, "half").Layout.Y);
            Assert.Equal(0, Find(v3, "full").Layout.X);
            Assert.Equal(2, Find(v3, "full").Layout.Y);
        }

        [Fact]
        public void V2_OrderPreservedGeometrically_MultipleRows()
        {
            var v2 = V2(
                Legacy("a", 0, 1, DashboardWidgetKinds.Kpi),
                Legacy("b", 1, 1, DashboardWidgetKinds.Bim),
                Legacy("c", 2, 2, DashboardWidgetKinds.Chart));
            var v3 = DashboardDefinitionV3Migrator.FromV2(v2);
            Assert.Equal(0, Find(v3, "a").Layout.X);
            Assert.Equal(6, Find(v3, "b").Layout.X);
            Assert.Equal(0, Find(v3, "a").Layout.Y);
            Assert.Equal(0, Find(v3, "b").Layout.Y);
            Assert.Equal(0, Find(v3, "c").Layout.X);
            Assert.Equal(2, Find(v3, "c").Layout.Y);
        }

        [Fact]
        public void HiddenWidget_StackedBelowVisible_DoesNotOccupy()
        {
            var hidden = Legacy("hidden", 0, 2, DashboardWidgetKinds.Chart);
            hidden.Layout.IsVisible = false;
            var v2 = V2(Legacy("visible", 1, 2, DashboardWidgetKinds.Kpi), hidden);
            var v3 = DashboardDefinitionV3Migrator.FromV2(v2);
            Assert.False(Find(v3, "hidden").Layout.IsVisible);
            Assert.Equal(0, Find(v3, "visible").Layout.Y);
            Assert.True(Find(v3, "hidden").Layout.Y >= Find(v3, "visible").Layout.Y + Find(v3, "visible").Layout.Height);
            Assert.False(DashboardGridLayoutEngine.AnyVisibleOverlap(v3.Widgets));
        }

        [Fact]
        public void RepeatedV2ToV3_IsEquivalent()
        {
            var v2 = V2(
                Query("q", 0, 1),
                Legacy("kpi", 1, 2, DashboardWidgetKinds.Kpi));
            var first = DashboardDefinitionV3Migrator.FromV2(v2);
            var second = DashboardDefinitionV3Migrator.FromV2(v2);
            Assert.Equal(Find(first, "q").Layout.X, Find(second, "q").Layout.X);
            Assert.Equal(Find(first, "q").Layout.Y, Find(second, "q").Layout.Y);
            Assert.Equal(Find(first, "q").Layout.Width, Find(second, "q").Layout.Width);
            Assert.Equal(Find(first, "kpi").Layout.Y, Find(second, "kpi").Layout.Y);
        }

        [Fact]
        public void V1ToV3_IsStable_AndPreservesContent()
        {
            var v1 = DashboardLayoutStore.Default();
            var first = DashboardDefinitionV3Migrator.FromLegacy(v1, Project);
            var second = DashboardDefinitionV3Migrator.FromLegacy(v1, Project);
            Assert.Equal(first.Widgets.Count, second.Widgets.Count);
            for (var i = 0; i < first.Widgets.Count; i++)
            {
                Assert.Equal(first.Widgets[i].Id, second.Widgets[i].Id);
                Assert.Equal(first.Widgets[i].Layout.X, second.Widgets[i].Layout.X);
                Assert.Equal(first.Widgets[i].Layout.Y, second.Widgets[i].Layout.Y);
                Assert.Equal(first.Widgets[i].Layout.Width, second.Widgets[i].Layout.Width);
                Assert.Equal(first.Widgets[i].Layout.Height, second.Widgets[i].Layout.Height);
                Assert.Equal(first.Widgets[i].ContentKind, second.Widgets[i].ContentKind);
                Assert.Equal(first.Widgets[i].Legacy.WidgetKind, second.Widgets[i].Legacy.WidgetKind);
            }
        }

        [Fact]
        public void Migration_DoesNotChangeQueryOrLegacyPayloads()
        {
            var query = Query("q1", 0, 2);
            var legacy = Legacy("kpi", 1, 2, DashboardWidgetKinds.Kpi);
            var v3 = DashboardDefinitionV3Migrator.FromV2(V2(query, legacy));
            var q = Find(v3, "q1");
            Assert.Equal(DashboardPersistenceV2.ContentQuery, q.ContentKind);
            Assert.Equal(query.Query.EntityTypeId, q.Query.EntityTypeId);
            Assert.Equal(query.Query.Measure, q.Query.Measure);
            Assert.Equal(query.Visualization.Type, q.Visualization.Type);
            Assert.Null(q.Legacy);
            var k = Find(v3, "kpi");
            Assert.Equal(DashboardPersistenceV2.ContentLegacy, k.ContentKind);
            Assert.Equal(DashboardWidgetKinds.Kpi, k.Legacy.WidgetKind);
            Assert.Null(k.Query);
        }

        private static DashboardDefinition V2(params DashboardWidgetDefinition[] widgets)
        {
            return new DashboardDefinition
            {
                SchemaVersion = DashboardPersistenceV2.SchemaVersion,
                Id = "default",
                Title = "Dashboard",
                ProjectKey = Project.ToString("D"),
                Widgets = new List<DashboardWidgetDefinition>(widgets)
            };
        }

        private static DashboardWidgetDefinition Legacy(string id, int order, int columnSpan, string kind)
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = id,
                ContentKind = DashboardPersistenceV2.ContentLegacy,
                Layout = new DashboardWidgetLayoutDefinition { Order = order, ColumnSpan = columnSpan, IsVisible = true },
                Legacy = new DashboardLegacyWidgetContent
                {
                    WidgetKind = kind,
                    ChartSource = "Types",
                    ChartKind = "HorizontalBar",
                    TopN = 12
                }
            };
        }

        private static DashboardWidgetDefinition Query(string id, int order, int columnSpan)
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = id,
                ContentKind = DashboardPersistenceV2.ContentQuery,
                Layout = new DashboardWidgetLayoutDefinition { Order = order, ColumnSpan = columnSpan, IsVisible = true },
                Query = DashboardQueryPersistence.ToDocument(new DashboardWidgetQuery(
                    DashboardQueryScopeKind.CurrentProject,
                    null,
                    DashboardQueryMeasure.Count,
                    DashboardQuerySort.ValueDescending,
                    null,
                    100)),
                Visualization = new DashboardVisualizationDefinition { Type = "Kpi" }
            };
        }

        private static DashboardWidgetDefinition Find(DashboardDefinition definition, string id)
        {
            foreach (var widget in definition.Widgets)
            {
                if (widget.Id == id)
                    return widget;
            }
            return null;
        }
    }
}
