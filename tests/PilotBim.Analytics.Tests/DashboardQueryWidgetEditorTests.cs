using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Properties;
using PilotBim.Analytics.Services;
using PilotBim.Analytics.ViewModels;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardQueryWidgetEditorTests
    {
        private const int Remarks = 12;
        private const int Documents = 34;

        [Fact]
        public void NewEditor_InitialQueryState_IsValid()
        {
            var vm = CreateNew();
            Assert.Equal(Resources.QueryEditor_ScopeCurrentProject, vm.ScopeDisplay);
            Assert.Equal(Resources.QueryEditor_MeasureCount, vm.MeasureDisplay);
            Assert.Empty(vm.FilterRows);
            Assert.True(vm.SelectedDimension == null || vm.SelectedDimension.IsNone);
            Assert.Equal("Auto", vm.SelectedVisualization.Id);
            Assert.True(vm.IsValid);
            var saved = vm.TrySave();
            Assert.NotNull(saved);
            Assert.Equal(DashboardPersistenceV2.ContentQuery, saved.ContentKind);
            Assert.Equal("Count", saved.Query.Measure);
            Assert.Equal("CurrentProject", saved.Query.Scope);
            Assert.Equal(Remarks, saved.Query.EntityTypeId);
            Assert.Null(saved.Query.DimensionFieldId);
            Assert.Equal("Auto", saved.Visualization.Type);
            AssertDefinitionValid(saved);
        }

        [Fact]
        public void TypeList_UsesTypeIdIdentity_NotDisplayTitle()
        {
            var types = new[]
            {
                new DashboardObjectTypeOption(34, "Documents", false),
                new DashboardObjectTypeOption(12, "Замечания к ЦИМ", false)
            };
            var vm = new DashboardQueryWidgetEditorViewModel(Catalog(), types, null);
            Assert.Equal(new[] { 34, 12 }, vm.TypeOptions.Select(t => t.TypeId).ToArray());
            Assert.Equal("Documents", vm.TypeOptions[0].DisplayName);
            vm.SelectedType = vm.TypeOptions.Single(t => t.TypeId == 12);
            Assert.Equal(12, vm.TrySave().Query.EntityTypeId);
            Assert.NotEqual("Замечания к ЦИМ", vm.TrySave().Query.EntityTypeId.ToString());
        }

        [Fact]
        public void ChangingType_RefreshesFields_AndResetsDimensionAndFilters()
        {
            var vm = CreateNew();
            var remarkField = DashboardFieldIds.Attribute(Remarks, "RemarkType");
            vm.SelectedDimension = vm.GroupByFields.Single(f => f.FieldId == remarkField);
            vm.AddFilter();
            vm.FilterRows[0].SelectedField = vm.FilterFields.Single(f => f.FieldId == remarkField);
            Assert.Contains(vm.GroupByFields, f => f.FieldId == remarkField);
            Assert.DoesNotContain(vm.GroupByFields, f => f.FieldId == DashboardFieldIds.Attribute(Documents, "Code"));

            vm.SelectedType = vm.TypeOptions.Single(t => t.TypeId == Documents);
            Assert.True(vm.SelectedDimension.IsNone);
            Assert.Empty(vm.FilterRows);
            Assert.DoesNotContain(vm.GroupByFields, f => f.FieldId == remarkField);
            Assert.Contains(vm.GroupByFields, f => f.FieldId == DashboardFieldIds.Attribute(Documents, "Code"));
            Assert.Contains(vm.GroupByFields, f => f.FieldId == DashboardFieldIds.SystemCreatorId);
            Assert.DoesNotContain(vm.GroupByFields, f => f.FieldId == DashboardFieldIds.SystemObjectId);
            Assert.Contains(vm.FilterFields, f => f.FieldId == DashboardFieldIds.SystemObjectId);
        }

        [Fact]
        public void GroupBy_None_AndGroupableOnly()
        {
            var vm = CreateNew();
            Assert.True(vm.SelectedDimension.IsNone);
            Assert.Null(vm.TrySave().Query.DimensionFieldId);

            var field = DashboardFieldIds.Attribute(Remarks, "RemarkType");
            vm.SelectedDimension = vm.GroupByFields.Single(f => f.FieldId == field);
            Assert.Equal(field, vm.TrySave().Query.DimensionFieldId);
            Assert.DoesNotContain(vm.GroupByFields, f => f.FieldId == DashboardFieldIds.SystemObjectId);
            Assert.DoesNotContain(vm.GroupByFields, f => f.FieldId == DashboardFieldIds.SystemCreated);
            Assert.DoesNotContain(vm.GroupByFields, f => f.FieldId == DashboardFieldIds.SystemCreatedMonth);
        }

        [Fact]
        public void Filters_AddRemove_Operators_AndTypedValues()
        {
            var vm = CreateNew();
            vm.AddFilter();
            vm.AddFilter();
            Assert.Equal(2, vm.FilterRows.Count);
            vm.RemoveFilter(vm.FilterRows[0]);
            Assert.Single(vm.FilterRows);

            var status = DashboardFieldIds.Attribute(Remarks, "RemarkType");
            var row = vm.FilterRows[0];
            row.SelectedField = vm.FilterFields.Single(f => f.FieldId == status);
            Assert.Equal(new[] { "Equals", "NotEquals", "IsEmpty", "IsNotEmpty" }, row.Operators.Select(o => o.Id).ToArray());
            Assert.DoesNotContain(vm.FilterFields, f => f.FieldId == DashboardFieldIds.SystemCreated);

            row.SelectedOperator = row.Operators.Single(o => o.Id == "IsEmpty");
            Assert.False(row.NeedsValue);
            Assert.True(vm.IsValid);

            row.SelectedOperator = row.Operators.Single(o => o.Id == "IsNotEmpty");
            Assert.False(row.NeedsValue);
            Assert.True(vm.IsValid);

            row.SelectedOperator = row.Operators.Single(o => o.Id == "Equals");
            Assert.True(row.NeedsValue);
            Assert.False(vm.IsValid);
            row.ValueText = "Coordination";
            Assert.True(vm.IsValid);
            var eq = vm.TrySave();
            Assert.Equal("Equals", eq.Query.Filters[0].Operator);
            Assert.Equal("Text", eq.Query.Filters[0].ValueKind);
            Assert.Equal("Coordination", eq.Query.Filters[0].Value);

            row.SelectedOperator = row.Operators.Single(o => o.Id == "NotEquals");
            row.ValueText = "Attributes";
            Assert.Equal("NotEquals", vm.TrySave().Query.Filters[0].Operator);
        }

        [Fact]
        public void Filter_InvalidNumberAndGuid_BlockSave()
        {
            var vm = CreateNew();
            vm.AddFilter();
            var code = DashboardFieldIds.Attribute(Documents, "Code");
            vm.SelectedType = vm.TypeOptions.Single(t => t.TypeId == Documents);
            vm.AddFilter();
            var row = vm.FilterRows[0];
            row.SelectedField = vm.FilterFields.Single(f => f.FieldId == code);
            row.SelectedOperator = row.Operators.Single(o => o.Id == "Equals");
            row.ValueText = "12x";
            Assert.False(vm.IsValid);
            Assert.Null(vm.TrySave());

            row.SelectedField = vm.FilterFields.Single(f => f.FieldId == DashboardFieldIds.SystemParentId);
            row.ValueText = "not-a-guid";
            Assert.False(vm.IsValid);
            row.ValueText = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            Assert.True(vm.IsValid);
        }

        [Fact]
        public void Filter_BooleanAndMultipleOrder()
        {
            var vm = CreateNew();
            vm.SelectedType = vm.TypeOptions.Single(t => t.TypeId == Documents);
            vm.AddFilter();
            vm.AddFilter();
            var flag = DashboardFieldIds.Attribute(Documents, "Flag");
            var code = DashboardFieldIds.Attribute(Documents, "Code");
            vm.FilterRows[0].SelectedField = vm.FilterFields.Single(f => f.FieldId == flag);
            vm.FilterRows[0].SelectedOperator = vm.FilterRows[0].Operators.Single(o => o.Id == "Equals");
            vm.FilterRows[0].SelectedBoolean = vm.FilterRows[0].BooleanChoices.Single(b => b.Id == "true");
            vm.FilterRows[1].SelectedField = vm.FilterFields.Single(f => f.FieldId == code);
            vm.FilterRows[1].SelectedOperator = vm.FilterRows[1].Operators.Single(o => o.Id == "Equals");
            vm.FilterRows[1].ValueText = "10";
            var saved = vm.TrySave();
            Assert.Equal(2, saved.Query.Filters.Count);
            Assert.Equal(flag, saved.Query.Filters[0].FieldId);
            Assert.Equal("Boolean", saved.Query.Filters[0].ValueKind);
            Assert.Equal("true", saved.Query.Filters[0].Value);
            Assert.Equal(code, saved.Query.Filters[1].FieldId);
            Assert.Equal("10", saved.Query.Filters[1].Value);
        }

        [Fact]
        public void SortAndLimit_RoundTrip()
        {
            var vm = CreateNew();
            vm.SelectedSort = vm.SortChoices.Single(s => s.Id == "LabelAscending");
            Assert.Equal("LabelAscending", vm.TrySave().Query.Sort);

            vm.LimitText = "";
            Assert.Null(vm.TrySave().Query.Limit);
            vm.LimitText = "10";
            Assert.Equal(10, vm.TrySave().Query.Limit);
            vm.LimitText = "0";
            Assert.False(vm.IsValid);
            vm.LimitText = "-3";
            Assert.False(vm.IsValid);
            vm.LimitText = "x";
            Assert.False(vm.IsValid);
            vm.LimitText = "8";
            Assert.True(vm.IsValid);
        }

        [Fact]
        public void Visualization_Compatibility()
        {
            var vm = CreateNew();
            Assert.Contains(vm.VisualizationChoices, v => v.Id == "Auto");
            Assert.Contains(vm.VisualizationChoices, v => v.Id == "Kpi");
            Assert.DoesNotContain(vm.VisualizationChoices, v => v.Id == "Bar");
            Assert.DoesNotContain(vm.VisualizationChoices, v => v.Id == "Line");
            vm.SelectedVisualization = vm.VisualizationChoices.Single(v => v.Id == "Kpi");
            Assert.Equal("Kpi", vm.TrySave().Visualization.Type);

            vm.SelectedDimension = vm.GroupByFields.Single(f => f.FieldId == DashboardFieldIds.Attribute(Remarks, "RemarkType"));
            Assert.Contains(vm.VisualizationChoices, v => v.Id == "Bar");
            Assert.DoesNotContain(vm.VisualizationChoices, v => v.Id == "Kpi");
            Assert.DoesNotContain(vm.VisualizationChoices, v => v.Id == "Line");
            vm.SelectedVisualization = vm.VisualizationChoices.Single(v => v.Id == "Bar");
            Assert.Equal("Bar", vm.TrySave().Visualization.Type);
        }

        [Fact]
        public void Edit_PreservesIdentityAndCancelLeavesOriginal()
        {
            var original = CreateQueryWidget();
            var originalTitle = original.Title;
            var originalLimit = original.Query.Limit;
            var originalFilter = original.Query.Filters[0].Value;
            var vm = new DashboardQueryWidgetEditorViewModel(Catalog(), Types(), original);

            Assert.Equal("query-remarks", vm.TrySave().Id);
            Assert.Equal(original.Layout.Order, vm.TrySave().Layout.Order);
            Assert.Equal(original.Layout.ColumnSpan, vm.TrySave().Layout.ColumnSpan);
            Assert.Equal("Bar", vm.TrySave().Visualization.Type);
            Assert.Equal(2, vm.FilterRows.Count);
            Assert.Equal("Open", vm.FilterRows[0].ValueText);

            vm.Title = "Changed";
            vm.LimitText = "3";
            Assert.Equal(originalTitle, original.Title);
            Assert.Equal(originalLimit, original.Query.Limit);
            Assert.Equal(originalFilter, original.Query.Filters[0].Value);

            var saved = vm.TrySave();
            Assert.Equal("query-remarks", saved.Id);
            Assert.Equal("Changed", saved.Title);
            Assert.Equal(3, saved.Query.Limit);
            Assert.NotSame(original, saved);
        }

        [Fact]
        public void MissingType_AndMissingField_AreNotSilentlyReplaced()
        {
            var widget = CreateQueryWidget();
            widget.Query.EntityTypeId = 999;
            widget.Query.DimensionFieldId = "attribute:12:OldField";
            var vm = new DashboardQueryWidgetEditorViewModel(Catalog(), Types(), widget);
            Assert.Equal(999, vm.SelectedType.TypeId);
            Assert.True(vm.SelectedType.IsUnavailable);
            Assert.Equal("attribute:12:OldField", vm.SelectedDimension.FieldId);
            Assert.True(vm.SelectedDimension.IsUnavailable);
            Assert.False(vm.IsValid);
            Assert.Null(vm.TrySave());
            Assert.Equal(999, widget.Query.EntityTypeId);
            Assert.Equal("attribute:12:OldField", widget.Query.DimensionFieldId);
        }

        [Fact]
        public void FilterKindMismatch_BlocksSave()
        {
            var widget = CreateQueryWidget();
            widget.Query.Filters = new List<DashboardFilterDocument>
            {
                new DashboardFilterDocument
                {
                    FieldId = DashboardFieldIds.Attribute(Remarks, "RemarkType"),
                    Operator = "Equals",
                    ValueKind = "Integer",
                    Value = "5"
                }
            };
            var vm = new DashboardQueryWidgetEditorViewModel(Catalog(), Types(), widget);
            Assert.False(vm.IsValid);
            Assert.Contains(Resources.QueryEditor_KindMismatch, vm.ValidationMessage);
        }

        private static void AssertDefinitionValid(DashboardWidgetDefinition widget)
        {
            var dash = new DashboardDefinition
            {
                SchemaVersion = 2,
                Id = "default",
                Title = "Dashboard",
                ProjectKey = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa").ToString("D"),
                Widgets = new List<DashboardWidgetDefinition> { widget }
            };
            string error;
            Assert.True(DashboardDefinitionValidator.TryValidate(dash, out error), error);
        }

        private static DashboardQueryWidgetEditorViewModel CreateNew()
        {
            var vm = new DashboardQueryWidgetEditorViewModel(Catalog(), Types(), null);
            vm.SelectedType = vm.TypeOptions.Single(t => t.TypeId == Remarks);
            return vm;
        }

        private static IReadOnlyList<DashboardObjectTypeOption> Types()
        {
            return DashboardObjectTypeOption.FromInventory(new[]
            {
                TypeRec(Remarks, "remarks", "Замечания к ЦИМ", Attr("RemarkType", "Тип замечания", "String")),
                TypeRec(Documents, "docs", "Documents", Attr("Code", "Code", "Integer"), Attr("Flag", "Flag", "Boolean"))
            });
        }

        private static DashboardFieldCatalog Catalog()
        {
            return new PilotFieldCatalogBuilder().Build(new[]
            {
                TypeRec(Remarks, "remarks", "Замечания к ЦИМ", Attr("RemarkType", "Тип замечания", "String")),
                TypeRec(Documents, "docs", "Documents", Attr("Code", "Code", "Integer"), Attr("Flag", "Flag", "Boolean"))
            });
        }

        private static DashboardWidgetDefinition CreateQueryWidget()
        {
            var query = new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                DashboardFieldIds.Attribute(Remarks, "RemarkType"),
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                10,
                Remarks,
                new[]
                {
                    new DashboardFilterDefinition(
                        DashboardFieldIds.Attribute(Remarks, "RemarkType"),
                        DashboardFilterOperator.Equals,
                        DashboardFilterValue.Text("Open")),
                    new DashboardFilterDefinition(
                        DashboardFieldIds.SystemCreatorId,
                        DashboardFilterOperator.IsNotEmpty,
                        null)
                });
            return new DashboardWidgetDefinition
            {
                Id = "query-remarks",
                Title = "Remarks",
                ContentKind = DashboardPersistenceV2.ContentQuery,
                Layout = new DashboardWidgetLayoutDefinition { Order = 4, ColumnSpan = 1, IsVisible = true },
                Query = DashboardQueryPersistence.ToDocument(query),
                Visualization = new DashboardVisualizationDefinition { Type = "Bar" }
            };
        }

        private static TypeInventoryRecord TypeRec(int typeId, string name, string title, params AttributeInventoryRecord[] attrs)
        {
            return new TypeInventoryRecord
            {
                TypeId = typeId,
                Name = name,
                Title = title,
                Attributes = (attrs ?? new AttributeInventoryRecord[0]).ToList()
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
