using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class ObjectRowsWidgetFilterTests
    {
        private readonly ObjectRowsWidgetQueryEngine _engine = new ObjectRowsWidgetQueryEngine();
        private const int TypeId = 12;

        [Fact]
        public void IsEmpty_MatchesMissingNullEmptyWhitespace()
        {
            var field = Note();
            var dataset = Complete(
                Row(),
                Row(Kv(field, new DashboardFieldValue(DashboardFieldType.Text, null, null, null))),
                Row(Text(field, "")),
                Row(Text(field, "  ")),
                Row(Text(field, "A")));
            var empty = Execute(dataset, Scalar(IsEmpty(field)), CatalogNote());
            Assert.Equal(4, empty.Dataset.Rows[0].Value);
            var notEmpty = Execute(dataset, Scalar(IsNotEmpty(field)), CatalogNote());
            Assert.Equal(1, notEmpty.Dataset.Rows[0].Value);
            Assert.Equal(5, empty.Dataset.Rows[0].Value + notEmpty.Dataset.Rows[0].Value);
        }

        [Fact]
        public void IsNotEmpty_MatchesNormalValue()
        {
            var field = Note();
            var result = Execute(
                Complete(Row(Text(field, "A")), Row()),
                Scalar(IsNotEmpty(field)),
                CatalogNote());
            Assert.Equal(1, result.Dataset.Rows[0].Value);
        }

        [Fact]
        public void Text_Equals_OrdinalCaseSensitive()
        {
            var field = Note();
            var dataset = Complete(Row(Text(field, "Open")), Row(Text(field, "open")), Row(Text(field, "Open")));
            var result = Execute(dataset, Scalar(Eq(field, DashboardFilterValue.Text("Open"))), CatalogNote());
            Assert.Equal(2, result.Dataset.Rows[0].Value);
        }

        [Fact]
        public void IntegerAndNumber_Equals()
        {
            var code = DashboardFieldIds.Attribute(TypeId, "Code");
            var ratio = DashboardFieldIds.Attribute(TypeId, "Ratio");
            var catalog = Catalog(Attr("Code", "Code", "Integer"), Attr("Ratio", "Ratio", "Double"));
            var ints = Execute(
                Complete(Row(Integer(code, 10)), Row(Integer(code, 2))),
                Scalar(Eq(code, DashboardFilterValue.Integer(10))),
                catalog);
            Assert.Equal(1, ints.Dataset.Rows[0].Value);

            var nums = Execute(
                Complete(Row(Number(ratio, 1.5)), Row(Number(ratio, 2.0))),
                Scalar(Eq(ratio, DashboardFilterValue.Number(1.5))),
                catalog);
            Assert.Equal(1, nums.Dataset.Rows[0].Value);

            var unified = Execute(
                Complete(Row(Number(ratio, 2.0))),
                Scalar(Eq(ratio, DashboardFilterValue.Integer(2))),
                catalog);
            Assert.Equal(1, unified.Dataset.Rows[0].Value);
        }

        [Fact]
        public void BooleanGuid_Equals()
        {
            var flag = DashboardFieldIds.Attribute(TypeId, "Flag");
            var catalog = Catalog(Attr("Flag", "Flag", "Boolean"));
            var result = Execute(
                Complete(Row(Bool(flag, true)), Row(Bool(flag, false))),
                Scalar(Eq(flag, DashboardFilterValue.Boolean(true))),
                catalog);
            Assert.Equal(1, result.Dataset.Rows[0].Value);

            var a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var guidResult = Execute(
                Complete(RowWithParent(a), RowWithParent(b)),
                Scalar(Eq(DashboardFieldIds.SystemParentId, DashboardFilterValue.Guid(a))),
                Catalog());
            Assert.Equal(1, guidResult.Dataset.Rows[0].Value);
        }

        [Fact]
        public void UserReferenceEnum_StableKey_NotDisplay()
        {
            var owner = DashboardFieldIds.Attribute(TypeId, "owner");
            var link = DashboardFieldIds.Attribute(TypeId, "link");
            var state = DashboardFieldIds.Attribute(TypeId, "state");
            var catalog = Catalog(
                Attr("owner", "Owner", "OrgUnit"),
                Attr("link", "Link", "ElementBook"),
                Attr("state", "State", "UserState"));
            var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

            var sameId = Execute(
                Complete(
                    Row(User(owner, 5, "Ivan")),
                    Row(User(owner, 5, "IVAN"))),
                Scalar(Eq(owner, DashboardFilterValue.Identity(DashboardFieldType.User, "5"))),
                catalog);
            Assert.Equal(2, sameId.Dataset.Rows[0].Value);

            var sameDisplay = Execute(
                Complete(
                    Row(User(owner, 5, "Ivan")),
                    Row(User(owner, 9, "Ivan"))),
                Scalar(Eq(owner, DashboardFilterValue.Identity(DashboardFieldType.User, "5"))),
                catalog);
            Assert.Equal(1, sameDisplay.Dataset.Rows[0].Value);

            var reference = Execute(
                Complete(Row(Ref(link, 77, "Door"))),
                Scalar(Eq(link, DashboardFilterValue.Identity(DashboardFieldType.Reference, "77"))),
                catalog);
            Assert.Equal(1, reference.Dataset.Rows[0].Value);

            var enumEq = Execute(
                Complete(
                    Row(EnumVal(state, id, "Open")),
                    Row(EnumVal(state, id, "Открыто"))),
                Scalar(Eq(state, DashboardFilterValue.Identity(DashboardFieldType.Enum, id.ToString("D")))),
                catalog);
            Assert.Equal(2, enumEq.Dataset.Rows[0].Value);
        }

        [Fact]
        public void NotEquals_IncludesMissing()
        {
            var field = Note();
            var dataset = Complete(Row(Text(field, "A")), Row(Text(field, "B")), Row());
            var ne = Execute(dataset, Scalar(Neq(field, DashboardFilterValue.Text("A"))), CatalogNote());
            Assert.Equal(2, ne.Dataset.Rows[0].Value);
            var eq = Execute(dataset, Scalar(Eq(field, DashboardFilterValue.Text("A"))), CatalogNote());
            Assert.Equal(1, eq.Dataset.Rows[0].Value);
        }

        [Fact]
        public void MultipleFilters_And()
        {
            var status = DashboardFieldIds.Attribute(TypeId, "status");
            var owner = DashboardFieldIds.Attribute(TypeId, "owner");
            var catalog = Catalog(Attr("status", "Status", "String"), Attr("owner", "Owner", "OrgUnit"));
            var rows = Complete(
                Row(Text(status, "Open"), User(owner, 42, "A")),
                Row(Text(status, "Open"), User(owner, 7, "B")),
                Row(Text(status, "Closed"), User(owner, 42, "A")));
            var both = new[]
            {
                Eq(status, DashboardFilterValue.Text("Open")),
                Eq(owner, DashboardFilterValue.Identity(DashboardFieldType.User, "42"))
            };
            var reversed = new[] { both[1], both[0] };
            Assert.Equal(1, Execute(rows, Scalar(both), catalog).Dataset.Rows[0].Value);
            Assert.Equal(1, Execute(rows, Scalar(reversed), catalog).Dataset.Rows[0].Value);
            Assert.Equal(0, Execute(rows, Scalar(Eq(status, DashboardFilterValue.Text("Missing")), both[1]), catalog).Dataset.Rows[0].Value);

            var three = Execute(
                rows,
                Scalar(
                    Eq(status, DashboardFilterValue.Text("Open")),
                    Eq(owner, DashboardFilterValue.Identity(DashboardFieldType.User, "42")),
                    IsNotEmpty(status)),
                catalog);
            Assert.Equal(1, three.Dataset.Rows[0].Value);
        }

        [Fact]
        public void Scalar_ZeroMatches_SuccessZero_DoesNotMutate()
        {
            var field = Note();
            var fields = new Dictionary<string, DashboardFieldValue>
            {
                { field, new DashboardFieldValue(DashboardFieldType.Text, "A", null, null) }
            };
            var row = new DashboardObjectRow(Guid.NewGuid(), TypeId, null, fields);
            var dataset = Complete(row);
            var result = Execute(dataset, Scalar(Eq(field, DashboardFilterValue.Text("Nope"))), CatalogNote());
            Assert.Equal(WidgetQueryStatus.Success, result.Status);
            Assert.Equal(0, result.Dataset.Rows[0].Value);
            Assert.Same(row, dataset.Rows[0]);
            Assert.Same(fields, dataset.Rows[0].Fields);
            Assert.Equal("A", dataset.Rows[0].Fields[field].Value);

            var all = Execute(dataset, Scalar(Eq(field, DashboardFilterValue.Text("A"))), CatalogNote());
            Assert.Equal(1, all.Dataset.Rows[0].Value);
        }

        [Fact]
        public void GroupBy_AfterFilter_SumsToPassedRows()
        {
            var status = DashboardFieldIds.Attribute(TypeId, "status");
            var kind = DashboardFieldIds.Attribute(TypeId, "RemarkType");
            var catalog = Catalog(Attr("status", "S", "String"), Attr("RemarkType", "T", "String"));
            var dataset = Complete(
                Row(Text(status, "Open"), Text(kind, "Coordination")),
                Row(Text(status, "Open"), Text(kind, "Coordination")),
                Row(Text(status, "Open")),
                Row(Text(status, "Closed"), Text(kind, "Detailing")));
            var result = Execute(
                dataset,
                CountBy(kind, Eq(status, DashboardFilterValue.Text("Open"))),
                catalog);
            Assert.Equal(WidgetQueryStatus.Success, result.Status);
            Assert.Equal(3, result.Dataset.Rows.Sum(r => r.Value));
            Assert.Contains(result.Dataset.Rows, r => r.Key == "Coordination" && r.Value == 2);
            Assert.Contains(result.Dataset.Rows, r => r.Key == DashboardGroupValue.MissingKey && r.Value == 1);
        }

        [Fact]
        public void GroupBy_SortAndLimit_AfterFilter()
        {
            var status = DashboardFieldIds.Attribute(TypeId, "status");
            var kind = DashboardFieldIds.Attribute(TypeId, "RemarkType");
            var catalog = Catalog(Attr("status", "S", "String"), Attr("RemarkType", "T", "String"));
            var dataset = Complete(
                Row(Text(status, "Open"), Text(kind, "A")),
                Row(Text(status, "Open"), Text(kind, "B")),
                Row(Text(status, "Open"), Text(kind, "B")),
                Row(Text(status, "Open"), Text(kind, "C")),
                Row(Text(status, "Open"), Text(kind, "C")),
                Row(Text(status, "Open"), Text(kind, "C")));
            var result = Execute(
                dataset,
                new DashboardWidgetQuery(
                    DashboardQueryScopeKind.CurrentProject,
                    kind,
                    DashboardQueryMeasure.Count,
                    DashboardQuerySort.ValueDescending,
                    2,
                    TypeId,
                    new[] { Eq(status, DashboardFilterValue.Text("Open")) }),
                catalog);
            Assert.Equal(2, result.Dataset.Rows.Count);
            Assert.Equal("C", result.Dataset.Rows[0].Label);
            Assert.Equal("B", result.Dataset.Rows[1].Label);
        }

        [Fact]
        public void UnknownFilterField_Unsupported()
        {
            var result = Execute(Complete(Row()), Scalar(IsEmpty("no-such-field")), Catalog());
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
        }

        [Fact]
        public void WrongTypeAttribute_Unsupported()
        {
            var other = DashboardFieldIds.Attribute(99, "note");
            var catalog = new PilotFieldCatalogBuilder().Build(new[]
            {
                TypeRec(TypeId, Attr("note", "Note", "String")),
                TypeRec(99, Attr("note", "Note", "String"))
            });
            var result = Execute(Complete(Row()), Scalar(IsEmpty(other)), catalog);
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
        }

        [Fact]
        public void CanFilterFalse_Unsupported()
        {
            var blob = DashboardFieldIds.Attribute(TypeId, "blob");
            var result = Execute(
                Complete(Row()),
                Scalar(IsEmpty(blob)),
                Catalog(Attr("blob", "Blob", "Array")));
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
        }

        [Fact]
        public void UnsupportedOperator_Unsupported()
        {
            var field = Note();
            var filter = new DashboardFilterDefinition(field, (DashboardFilterOperator)99, null);
            var result = Execute(Complete(Row()), Scalar(filter), CatalogNote());
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
        }

        [Fact]
        public void InvalidFilterValueType_InvalidQuery()
        {
            var code = DashboardFieldIds.Attribute(TypeId, "Code");
            var result = Execute(
                Complete(Row(Integer(code, 1))),
                Scalar(Eq(code, DashboardFilterValue.Text("1"))),
                Catalog(Attr("Code", "Code", "Integer")));
            Assert.Equal(WidgetQueryStatus.InvalidQuery, result.Status);
        }

        [Fact]
        public void PartialDataset_IncompleteData_BeforeFilters()
        {
            var field = Note();
            var dataset = new DashboardTypeDataset(
                TypeId, 4, 3, DashboardTypeCoverage.Partial, "partial", new[] { Row(), Row(), Row() }, 0, 0);
            var result = Execute(dataset, Scalar(IsEmpty(field)), CatalogNote());
            Assert.Equal(WidgetQueryStatus.IncompleteData, result.Status);
            Assert.Empty(result.Dataset.Rows);
        }

        [Fact]
        public void SkippedUnsupportedField_IncompleteData()
        {
            var field = Note();
            var dataset = new DashboardTypeDataset(
                TypeId,
                1,
                1,
                DashboardTypeCoverage.Complete,
                "complete",
                new[] { Row() },
                0,
                1,
                new[] { field });
            var result = Execute(dataset, Scalar(IsEmpty(field)), CatalogNote());
            Assert.Equal(WidgetQueryStatus.IncompleteData, result.Status);
            Assert.Empty(result.Dataset.Rows);
        }

        [Fact]
        public void Equals_NullValue_InvalidQuery()
        {
            var field = Note();
            var filter = new DashboardFilterDefinition(field, DashboardFilterOperator.Equals, null);
            var result = Execute(Complete(Row()), Scalar(filter), CatalogNote());
            Assert.Equal(WidgetQueryStatus.InvalidQuery, result.Status);
        }

        private WidgetQueryResult Execute(
            DashboardTypeDataset dataset,
            DashboardWidgetQuery query,
            DashboardFieldCatalog catalog)
        {
            return _engine.Execute(dataset, catalog, query);
        }

        private static DashboardWidgetQuery Scalar(params DashboardFilterDefinition[] filters)
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                null,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                TypeId,
                filters);
        }

        private static DashboardWidgetQuery CountBy(string fieldId, params DashboardFilterDefinition[] filters)
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                fieldId,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                TypeId,
                filters);
        }

        private static DashboardFilterDefinition Eq(string field, DashboardFilterValue value)
        {
            return new DashboardFilterDefinition(field, DashboardFilterOperator.Equals, value);
        }

        private static DashboardFilterDefinition Neq(string field, DashboardFilterValue value)
        {
            return new DashboardFilterDefinition(field, DashboardFilterOperator.NotEquals, value);
        }

        private static DashboardFilterDefinition IsEmpty(string field)
        {
            return new DashboardFilterDefinition(field, DashboardFilterOperator.IsEmpty, null);
        }

        private static DashboardFilterDefinition IsNotEmpty(string field)
        {
            return new DashboardFilterDefinition(field, DashboardFilterOperator.IsNotEmpty, null);
        }

        private static string Note()
        {
            return DashboardFieldIds.Attribute(TypeId, "note");
        }

        private static DashboardFieldCatalog CatalogNote()
        {
            return Catalog(Attr("note", "Note", "String"));
        }

        private static DashboardTypeDataset Complete(params DashboardObjectRow[] rows)
        {
            var list = rows ?? new DashboardObjectRow[0];
            return new DashboardTypeDataset(
                TypeId, list.Length, list.Length, DashboardTypeCoverage.Complete, "complete", list, 0, 0);
        }

        private static DashboardFieldCatalog Catalog(params AttributeInventoryRecord[] attrs)
        {
            return new PilotFieldCatalogBuilder().Build(new[] { TypeRec(TypeId, attrs) });
        }

        private static TypeInventoryRecord TypeRec(int typeId, params AttributeInventoryRecord[] attrs)
        {
            return new TypeInventoryRecord
            {
                TypeId = typeId,
                Name = "type" + typeId,
                Title = "Type " + typeId,
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

        private static DashboardObjectRow Row(params KeyValuePair<string, DashboardFieldValue>[] fields)
        {
            var map = new Dictionary<string, DashboardFieldValue>(StringComparer.Ordinal);
            foreach (var pair in fields)
                map[pair.Key] = pair.Value;
            return new DashboardObjectRow(Guid.NewGuid(), TypeId, null, map);
        }

        private static DashboardObjectRow RowWithParent(Guid parentId)
        {
            return new DashboardObjectRow(
                Guid.NewGuid(),
                TypeId,
                parentId,
                new Dictionary<string, DashboardFieldValue>
                {
                    {
                        DashboardFieldIds.SystemParentId,
                        new DashboardFieldValue(DashboardFieldType.Guid, parentId, parentId.ToString("D"), null)
                    }
                });
        }

        private static KeyValuePair<string, DashboardFieldValue> Kv(string id, DashboardFieldValue value)
        {
            return new KeyValuePair<string, DashboardFieldValue>(id, value);
        }

        private static KeyValuePair<string, DashboardFieldValue> Text(string id, string value)
        {
            return Kv(id, new DashboardFieldValue(DashboardFieldType.Text, value, null, null));
        }

        private static KeyValuePair<string, DashboardFieldValue> Integer(string id, long value)
        {
            return Kv(id, new DashboardFieldValue(DashboardFieldType.Integer, value, value.ToString(CultureInfo.InvariantCulture), null));
        }

        private static KeyValuePair<string, DashboardFieldValue> Number(string id, double value)
        {
            return Kv(id, new DashboardFieldValue(DashboardFieldType.Number, value, value.ToString("R", CultureInfo.InvariantCulture), null));
        }

        private static KeyValuePair<string, DashboardFieldValue> Bool(string id, bool value)
        {
            return Kv(id, new DashboardFieldValue(DashboardFieldType.Boolean, value, value ? "1" : "0", null));
        }

        private static KeyValuePair<string, DashboardFieldValue> User(string id, int orgId, string display)
        {
            var key = orgId.ToString(CultureInfo.InvariantCulture);
            return Kv(id, new DashboardFieldValue(DashboardFieldType.User, orgId, key, display));
        }

        private static KeyValuePair<string, DashboardFieldValue> Ref(string id, int refId, string display)
        {
            var key = refId.ToString(CultureInfo.InvariantCulture);
            return Kv(id, new DashboardFieldValue(DashboardFieldType.Reference, refId, key, display));
        }

        private static KeyValuePair<string, DashboardFieldValue> EnumVal(string id, Guid stateId, string display)
        {
            var key = stateId.ToString("D");
            return Kv(id, new DashboardFieldValue(DashboardFieldType.Enum, stateId, key, display));
        }
    }
}
