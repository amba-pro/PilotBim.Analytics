using System.Collections.Generic;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardFilterCompatibilityTests
    {
        [Fact]
        public void SameTypeId_FilterableField_Compatible()
        {
            var catalog = Catalog(10, "Status", "String");
            var filter = Filter(10, DashboardFieldIds.Attribute(10, "Status"));
            var widget = Query(10);
            Assert.Equal(
                DashboardFilterCompatibilityStatus.Compatible,
                DashboardFilterCompatibility.Evaluate(filter, widget, catalog));
        }

        [Fact]
        public void DifferentTypeId_Incompatible()
        {
            var catalog = Catalog(10, "Status", "String");
            var filter = Filter(10, DashboardFieldIds.Attribute(10, "Status"));
            Assert.Equal(
                DashboardFilterCompatibilityStatus.Incompatible,
                DashboardFilterCompatibility.Evaluate(filter, Query(20), catalog));
        }

        [Fact]
        public void SameDisplayName_DifferentTypeId_Incompatible()
        {
            var catalog = new PilotFieldCatalogBuilder().Build(new[]
            {
                Type(10, Attr("Status", "Статус", "String")),
                Type(20, Attr("Status", "Статус", "String"))
            });
            var filter = Filter(10, DashboardFieldIds.Attribute(10, "Status"));
            Assert.Equal(
                DashboardFilterCompatibilityStatus.Incompatible,
                DashboardFilterCompatibility.Evaluate(filter, Query(20), catalog));
        }

        [Fact]
        public void UnknownField_Unavailable()
        {
            var catalog = Catalog(10, "Status", "String");
            var filter = Filter(10, DashboardFieldIds.Attribute(10, "OldStatus"));
            Assert.Equal(
                DashboardFilterCompatibilityStatus.Unavailable,
                DashboardFilterCompatibility.Evaluate(filter, Query(10), catalog));
        }

        [Fact]
        public void CanFilterFalse_Incompatible()
        {
            var catalog = Catalog(10, "Blob", "Array");
            var filter = Filter(10, DashboardFieldIds.Attribute(10, "Blob"));
            Assert.Equal(
                DashboardFilterCompatibilityStatus.Incompatible,
                DashboardFilterCompatibility.Evaluate(filter, Query(10), catalog));
        }

        [Fact]
        public void DateTime_Incompatible()
        {
            var catalog = new PilotFieldCatalogBuilder().Build(new[] { Type(10) });
            var filter = Filter(10, DashboardFieldIds.SystemCreated);
            Assert.Equal(
                DashboardFilterCompatibilityStatus.Incompatible,
                DashboardFilterCompatibility.Evaluate(filter, Query(10), catalog));
        }

        [Fact]
        public void LegacyTarget_Incompatible()
        {
            var catalog = Catalog(10, "Status", "String");
            var filter = Filter(10, DashboardFieldIds.Attribute(10, "Status"));
            var legacy = new DashboardWidgetDefinition
            {
                Id = "legacy",
                Title = "KPI",
                ContentKind = DashboardPersistenceV2.ContentLegacy,
                Legacy = new DashboardLegacyWidgetContent { WidgetKind = DashboardWidgetKinds.Kpi }
            };
            Assert.Equal(
                DashboardFilterCompatibilityStatus.Incompatible,
                DashboardFilterCompatibility.Evaluate(filter, legacy, catalog));
        }

        [Fact]
        public void QueryTarget_CompatibleWhenValid()
        {
            var catalog = Catalog(10, "Owner", "OrgUnit");
            var filter = Filter(10, DashboardFieldIds.Attribute(10, "Owner"), DashboardFilterValue.Identity(DashboardFieldType.User, "u1"));
            Assert.Equal(
                DashboardFilterCompatibilityStatus.Compatible,
                DashboardFilterCompatibility.Evaluate(filter, Query(10), catalog));
        }

        private static DashboardLevelFilterDefinition Filter(int typeId, string fieldId, DashboardFilterValue value = null)
        {
            DashboardFilterDocument doc;
            string error;
            var runtime = new DashboardFilterDefinition(
                fieldId,
                DashboardFilterOperator.Equals,
                value ?? DashboardFilterValue.Text("Open"));
            Assert.True(DashboardQueryPersistence.TryToFilterDocument(runtime, out doc, out error), error);
            return new DashboardLevelFilterDefinition
            {
                Id = "f1",
                Title = "Status",
                EntityTypeId = typeId,
                FieldId = fieldId,
                Operator = doc.Operator,
                ValueKind = doc.ValueKind,
                Value = doc.Value,
                TargetWidgetIds = new List<string> { "q1" }
            };
        }

        private static DashboardWidgetDefinition Query(int typeId)
        {
            return new DashboardWidgetDefinition
            {
                Id = "q1",
                Title = "Query",
                ContentKind = DashboardPersistenceV2.ContentQuery,
                Query = DashboardQueryPersistence.ToDocument(new DashboardWidgetQuery(
                    DashboardQueryScopeKind.CurrentProject,
                    null,
                    DashboardQueryMeasure.Count,
                    DashboardQuerySort.ValueDescending,
                    null,
                    typeId)),
                Visualization = new DashboardVisualizationDefinition { Type = "Kpi" }
            };
        }

        private static DashboardFieldCatalog Catalog(int typeId, string name, string valueType)
        {
            return new PilotFieldCatalogBuilder().Build(new[] { Type(typeId, Attr(name, name, valueType)) });
        }

        private static TypeInventoryRecord Type(int typeId, params AttributeInventoryRecord[] attrs)
        {
            return new TypeInventoryRecord
            {
                TypeId = typeId,
                Name = "type" + typeId,
                Title = "Type " + typeId,
                Attributes = new List<AttributeInventoryRecord>(attrs ?? new AttributeInventoryRecord[0])
            };
        }

        private static AttributeInventoryRecord Attr(string name, string title, string valueType)
        {
            return new AttributeInventoryRecord
            {
                Name = name,
                AttributeId = name,
                Title = title,
                ValueType = valueType
            };
        }
    }
}
