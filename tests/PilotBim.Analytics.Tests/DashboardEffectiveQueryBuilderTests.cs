using System.Collections.Generic;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardEffectiveQueryBuilderTests
    {
        [Fact]
        public void AppendsDashboardFilter_PreservesBase_AndIdentity()
        {
            var field = DashboardFieldIds.Attribute(10, "Status");
            var baseFilter = new DashboardFilterDefinition(field, DashboardFilterOperator.Equals, DashboardFilterValue.Text("A"));
            var baseQuery = Query(10, field, 8, baseFilter);
            var widget = Widget(baseQuery, "q1");
            var dash = Level(field, "B", "q1");
            var catalog = Catalog(10, "Status");

            var effective = DashboardEffectiveQueryBuilder.Build(
                baseQuery,
                new List<DashboardLevelFilterDefinition> { dash },
                widget,
                catalog);

            Assert.NotSame(baseQuery, effective);
            Assert.Single(baseQuery.Filters);
            Assert.Equal("A", (string)baseQuery.Filters[0].Value.Value);
            Assert.Equal(2, effective.Filters.Count);
            Assert.Equal("A", (string)effective.Filters[0].Value.Value);
            Assert.Equal("B", (string)effective.Filters[1].Value.Value);
            Assert.Equal(baseQuery.Sort, effective.Sort);
            Assert.Equal(baseQuery.DimensionFieldId, effective.DimensionFieldId);
            Assert.Equal(baseQuery.Limit, effective.Limit);
            Assert.Equal(baseQuery.Measure, effective.Measure);
            Assert.Equal(baseQuery.EntityTypeId, effective.EntityTypeId);
        }

        [Fact]
        public void TwoDashboardFilters_AndList_DisabledSkipped()
        {
            var field = DashboardFieldIds.Attribute(10, "Status");
            var baseQuery = Query(10, field, null);
            var widget = Widget(baseQuery, "q1");
            var a = Level(field, "A", "q1");
            var b = Level(field, "B", "q1");
            b.Disabled = true;
            var c = Level(field, "C", "q1");
            var catalog = Catalog(10, "Status");
            var effective = DashboardEffectiveQueryBuilder.Build(
                baseQuery,
                new List<DashboardLevelFilterDefinition> { a, b, c },
                widget,
                catalog);
            Assert.Equal(2, effective.Filters.Count);
            Assert.Equal("A", (string)effective.Filters[0].Value.Value);
            Assert.Equal("C", (string)effective.Filters[1].Value.Value);
        }

        [Fact]
        public void UnavailableFilter_NotAppended()
        {
            var field = DashboardFieldIds.Attribute(10, "Status");
            var baseQuery = Query(10, null, null);
            var widget = Widget(baseQuery, "q1");
            var dash = Level(DashboardFieldIds.Attribute(10, "Gone"), "X", "q1");
            var catalog = Catalog(10, "Status");
            var effective = DashboardEffectiveQueryBuilder.Build(
                baseQuery,
                new List<DashboardLevelFilterDefinition> { dash },
                widget,
                catalog);
            Assert.Same(baseQuery, effective);
        }

        private static DashboardWidgetQuery Query(
            int typeId,
            string dimension,
            int? limit,
            params DashboardFilterDefinition[] filters)
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                dimension,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                limit,
                typeId,
                filters);
        }

        private static DashboardWidgetDefinition Widget(DashboardWidgetQuery query, string id)
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = id,
                ContentKind = DashboardPersistenceV2.ContentQuery,
                Query = DashboardQueryPersistence.ToDocument(query),
                Visualization = new DashboardVisualizationDefinition { Type = "Bar" }
            };
        }

        private static DashboardLevelFilterDefinition Level(string fieldId, string text, string widgetId)
        {
            DashboardFilterDocument doc;
            string error;
            Assert.True(DashboardQueryPersistence.TryToFilterDocument(
                new DashboardFilterDefinition(fieldId, DashboardFilterOperator.Equals, DashboardFilterValue.Text(text)),
                out doc,
                out error), error);
            return new DashboardLevelFilterDefinition
            {
                Id = "f-" + text,
                Title = text,
                EntityTypeId = 10,
                FieldId = fieldId,
                Operator = doc.Operator,
                ValueKind = doc.ValueKind,
                Value = doc.Value,
                TargetWidgetIds = new List<string> { widgetId }
            };
        }

        private static DashboardFieldCatalog Catalog(int typeId, string name)
        {
            return new PilotFieldCatalogBuilder().Build(new[]
            {
                new TypeInventoryRecord
                {
                    TypeId = typeId,
                    Name = "t",
                    Title = "T",
                    Attributes = new List<AttributeInventoryRecord>
                    {
                        new AttributeInventoryRecord
                        {
                            Name = name,
                            AttributeId = name,
                            Title = name,
                            ValueType = "String"
                        }
                    }
                }
            });
        }
    }
}
