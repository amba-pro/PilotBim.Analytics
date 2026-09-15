using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using PilotBim.Analytics.ViewModels;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardFilterEditorTests
    {
        [Fact]
        public void TypeList_OnlyFromQueryWidgets()
        {
            var vm = NewEditor(null, Query("q1", 10), Legacy(), Query("q2", 10));
            Assert.Single(vm.TypeOptions);
            Assert.Equal(10, vm.TypeOptions[0].TypeId);
        }

        [Fact]
        public void Fields_CanFilterObjectRowsOnly()
        {
            var vm = NewEditor(null, Query("q1", 10));
            Assert.Contains(vm.Fields, f => f.FieldId == DashboardFieldIds.Attribute(10, "Status"));
            Assert.Contains(vm.Fields, f => f.FieldId == DashboardFieldIds.Attribute(10, "Code"));
            Assert.DoesNotContain(vm.Fields, f => f.FieldId == DashboardFieldIds.Attribute(10, "Blob"));
            Assert.DoesNotContain(vm.Fields, f => f.FieldId == DashboardFieldIds.SystemCreated);
        }

        [Fact]
        public void Targets_CompatibleQueryWidgetsOnly()
        {
            var vm = NewEditor(null, Query("a", 10), Query("b", 20), Legacy());
            Assert.Equal(new[] { "a" }, vm.Targets.Select(t => t.Id).ToArray());
            Assert.True(vm.Targets[0].IsSelected);
        }

        [Fact]
        public void Equals_RequiresValue_IsEmptyDoesNot()
        {
            var vm = NewEditor(null, Query("q1", 10));
            vm.Title = "Status";
            vm.ValueText = string.Empty;
            string error;
            Assert.Null(vm.TryBuild(out error));
            Assert.False(string.IsNullOrEmpty(error));

            vm.SelectedOperator = vm.Operators.First(o => o.Id == "IsEmpty");
            var built = vm.TryBuild(out error);
            Assert.NotNull(built);
            Assert.Null(error);
            Assert.Equal("IsEmpty", built.Operator);
        }

        [Fact]
        public void TypedNumberAndGuid_Validation()
        {
            var vm = NewEditor(null, Query("q1", 10));
            vm.SelectedField = vm.Fields.First(f => f.FieldId == DashboardFieldIds.Attribute(10, "Code"));
            vm.ValueText = "x";
            string error;
            Assert.Null(vm.TryBuild(out error));

            vm.ValueText = "7";
            Assert.NotNull(vm.TryBuild(out error));

            vm.SelectedField = vm.Fields.First(f => f.FieldId == DashboardFieldIds.SystemObjectId);
            vm.ValueText = "not-a-guid";
            Assert.Null(vm.TryBuild(out error));
            vm.ValueText = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa").ToString("D");
            Assert.NotNull(vm.TryBuild(out error));
        }

        [Fact]
        public void Cancel_DoesNotMutateExisting_SaveReturnsCopy()
        {
            var field = DashboardFieldIds.Attribute(10, "Status");
            DashboardFilterDocument doc;
            string unused;
            Assert.True(DashboardQueryPersistence.TryToFilterDocument(
                new DashboardFilterDefinition(field, DashboardFilterOperator.Equals, DashboardFilterValue.Text("Open")),
                out doc,
                out unused));
            var existing = new DashboardLevelFilterDefinition
            {
                Id = "keep-id",
                Title = "Original",
                EntityTypeId = 10,
                FieldId = field,
                Operator = doc.Operator,
                ValueKind = doc.ValueKind,
                Value = doc.Value,
                TargetWidgetIds = new List<string> { "q1" }
            };
            var vm = NewEditor(existing, Query("q1", 10));
            vm.Title = "Changed";
            vm.ValueText = "Closed";
            Assert.Equal("Original", existing.Title);
            Assert.Equal("Open", existing.Value);

            string error;
            var built = vm.TryBuild(out error);
            Assert.NotNull(built);
            Assert.Equal("keep-id", built.Id);
            Assert.Equal("Changed", built.Title);
            Assert.NotSame(existing, built);
            Assert.Equal("Original", existing.Title);
        }

        private static DashboardFilterEditorViewModel NewEditor(
            DashboardLevelFilterDefinition existing,
            params DashboardWidgetDefinition[] widgets)
        {
            var catalog = new PilotFieldCatalogBuilder().Build(new[] { Type(10), Type(20) });
            var types = DashboardObjectTypeOption.FromInventory(new[] { Type(10), Type(20), Type(30) });
            return new DashboardFilterEditorViewModel(catalog, types, widgets, existing);
        }

        private static DashboardWidgetDefinition Query(string id, int typeId)
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = id,
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

        private static DashboardWidgetDefinition Legacy()
        {
            return new DashboardWidgetDefinition
            {
                Id = "legacy",
                Title = "KPI",
                ContentKind = DashboardPersistenceV2.ContentLegacy,
                Legacy = new DashboardLegacyWidgetContent { WidgetKind = DashboardWidgetKinds.Kpi }
            };
        }

        private static TypeInventoryRecord Type(int typeId)
        {
            return new TypeInventoryRecord
            {
                TypeId = typeId,
                Name = "type" + typeId,
                Title = "Type " + typeId,
                Attributes = new List<AttributeInventoryRecord>
                {
                    Attr("Status", "Статус", "String"),
                    Attr("Code", "Code", "Integer"),
                    Attr("Blob", "Blob", "Array")
                }
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
