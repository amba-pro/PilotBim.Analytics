using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class ObjectRowsWidgetQueryEngineTests
    {
        private readonly ObjectRowsWidgetQueryEngine _engine = new ObjectRowsWidgetQueryEngine();
        private const int TypeId = 12;

        [Fact]
        public void Complete_ScalarCount_Succeeds()
        {
            var result = Execute(Complete(Row(), Row(), Row()), Scalar());
            Assert.Equal(WidgetQueryStatus.Success, result.Status);
            Assert.Single(result.Dataset.Rows);
            Assert.Equal(string.Empty, result.Dataset.Rows[0].Key);
            Assert.Equal(string.Empty, result.Dataset.Rows[0].Label);
            Assert.Equal(3, result.Dataset.Rows[0].Value);
        }

        [Fact]
        public void Complete_CustomTextDimension_Groups()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "RemarkType");
            var result = Execute(
                Complete(
                    Row(Text(field, "Coordination")),
                    Row(Text(field, "Coordination")),
                    Row(Text(field, "Attributes")),
                    Row(Text(field, "Detailing"))),
                CountBy(field),
                Catalog(Attr("RemarkType", "Remark type", "String")));

            Assert.Equal(WidgetQueryStatus.Success, result.Status);
            Assert.Equal(3, result.Dataset.Rows.Count);
            Assert.Equal("Coordination", result.Dataset.Rows[0].Key);
            Assert.Equal(2, result.Dataset.Rows[0].Value);
            Assert.Equal("Attributes", result.Dataset.Rows[1].Key);
            Assert.Equal(1, result.Dataset.Rows[1].Value);
        }

        [Fact]
        public void CustomIntegerDimension_GroupsInvariantly()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "Code");
            var result = Execute(
                Complete(Row(Integer(field, 10)), Row(Integer(field, 10)), Row(Integer(field, 2))),
                CountBy(field),
                Catalog(Attr("Code", "Code", "Integer")));

            Assert.Equal("10", result.Dataset.Rows[0].Key);
            Assert.Equal(2, result.Dataset.Rows[0].Value);
            Assert.Equal("2", result.Dataset.Rows[1].Key);
        }

        [Fact]
        public void BooleanDimension_GroupsTrueFalse()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "Flag");
            var result = Execute(
                Complete(Row(Bool(field, true)), Row(Bool(field, true)), Row(Bool(field, false))),
                CountBy(field, DashboardQuerySort.ValueDescending),
                Catalog(Attr("Flag", "Flag", "Boolean")));

            Assert.Equal("1", result.Dataset.Rows[0].Key);
            Assert.Equal(2, result.Dataset.Rows[0].Value);
            Assert.Equal("0", result.Dataset.Rows[1].Key);
            Assert.Equal(1, result.Dataset.Rows[1].Value);
        }

        [Fact]
        public void GuidDimension_GroupsByDFormat()
        {
            var a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var result = Execute(
                Complete(RowWithParent(a), RowWithParent(a), RowWithParent(b)),
                CountBy(DashboardFieldIds.SystemParentId),
                Catalog());

            Assert.Equal(a.ToString("D"), result.Dataset.Rows[0].Key);
            Assert.Equal(2, result.Dataset.Rows[0].Value);
            Assert.Equal(b.ToString("D"), result.Dataset.Rows[1].Key);
        }

        [Fact]
        public void CustomAttribute_UsesDb1Id()
        {
            var id = DashboardFieldIds.Attribute(TypeId, "RemarkType");
            var result = Execute(
                Complete(Row(Text(id, "Coordination"))),
                CountBy(id),
                Catalog(Attr("RemarkType", "Тип замечания", "String")));

            Assert.Equal(WidgetQueryStatus.Success, result.Status);
            Assert.Equal("Coordination", result.Dataset.Rows[0].Key);
        }

        [Fact]
        public void SystemField_CreatorId_Groups()
        {
            var result = Execute(
                Complete(RowWithCreator(7), RowWithCreator(7), RowWithCreator(3)),
                CountBy(DashboardFieldIds.SystemCreatorId),
                Catalog());

            Assert.Equal("7", result.Dataset.Rows[0].Key);
            Assert.Equal(2, result.Dataset.Rows[0].Value);
            Assert.Equal("3", result.Dataset.Rows[1].Key);
        }

        [Fact]
        public void AttributeName_OnWrongTypeId_Unsupported()
        {
            var other = DashboardFieldIds.Attribute(99, "RemarkType");
            var catalog = new PilotFieldCatalogBuilder().Build(new[]
            {
                TypeRec(TypeId, Attr("RemarkType", "Type", "String")),
                TypeRec(99, Attr("RemarkType", "Type", "String"))
            });
            var result = Execute(
                Complete(Row(Text(DashboardFieldIds.Attribute(TypeId, "RemarkType"), "A"))),
                CountBy(other),
                catalog);

            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
            Assert.Empty(result.Dataset.Rows);
        }

        [Fact]
        public void QueryTypeId_Mismatch_Invalid()
        {
            var result = _engine.Execute(
                Complete(Row()),
                Catalog(),
                new DashboardWidgetQuery(
                    DashboardQueryScopeKind.CurrentProject,
                    null,
                    DashboardQueryMeasure.Count,
                    DashboardQuerySort.ValueDescending,
                    null,
                    99));
            Assert.Equal(WidgetQueryStatus.InvalidQuery, result.Status);
            Assert.Empty(result.Dataset.Rows);
        }

        [Fact]
        public void UnknownField_Unsupported()
        {
            var result = Execute(Complete(Row()), CountBy("no-such-field"), Catalog());
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
        }

        [Fact]
        public void CanGroupFalse_Unsupported()
        {
            var result = Execute(Complete(Row()), CountBy(DashboardFieldIds.SystemObjectId), Catalog());
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
        }

        [Fact]
        public void DateTimeField_Unsupported_NoHiddenBuckets()
        {
            var result = Execute(Complete(Row()), CountBy(DashboardFieldIds.SystemCreated), Catalog());
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
            Assert.Empty(result.Dataset.Rows);
        }

        [Fact]
        public void CreatedMonth_Unsupported_OnObjectRows()
        {
            var result = Execute(Complete(Row()), CountBy(DashboardFieldIds.SystemCreatedMonth), Catalog());
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
        }

        [Fact]
        public void PartialDataset_IncompleteData_NoNumbers()
        {
            var dataset = new DashboardTypeDataset(
                TypeId, 4, 3, DashboardTypeCoverage.Partial, "partial", new[] { Row(), Row(), Row() }, 0, 0);
            var result = Execute(dataset, Scalar());
            Assert.Equal(WidgetQueryStatus.IncompleteData, result.Status);
            Assert.Empty(result.Dataset.Rows);
            Assert.NotEqual(WidgetQueryStatus.Success, result.Status);
            Assert.NotEqual(WidgetQueryStatus.Empty, result.Status);
        }

        [Fact]
        public void FailedDataset_IncompleteData_NoNumbers()
        {
            var dataset = new DashboardTypeDataset(
                TypeId, 4, 0, DashboardTypeCoverage.Failed, "failed", new DashboardObjectRow[0], 0, 0);
            var result = Execute(dataset, CountBy(DashboardFieldIds.SystemTypeId), Catalog());
            Assert.Equal(WidgetQueryStatus.IncompleteData, result.Status);
            Assert.Empty(result.Dataset.Rows);
        }

        [Fact]
        public void EmptyComplete_ScalarZero_DimensionEmpty()
        {
            var empty = Complete();
            var scalar = Execute(empty, Scalar());
            Assert.Equal(WidgetQueryStatus.Success, scalar.Status);
            Assert.Single(scalar.Dataset.Rows);
            Assert.Equal(0, scalar.Dataset.Rows[0].Value);

            var grouped = Execute(empty, CountBy(DashboardFieldIds.SystemTypeId), Catalog());
            Assert.Equal(WidgetQueryStatus.Empty, grouped.Status);
            Assert.Empty(grouped.Dataset.Rows);
        }

        [Fact]
        public void SameUserStableId_SameDisplay_OneBucket()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "owner");
            var result = Execute(
                Complete(
                    Row(User(field, 5, "Ivan")),
                    Row(User(field, 5, "Ivan"))),
                CountBy(field),
                Catalog(Attr("owner", "Owner", "OrgUnit")));
            Assert.Single(result.Dataset.Rows);
            Assert.Equal("5", result.Dataset.Rows[0].Key);
            Assert.Equal("Ivan", result.Dataset.Rows[0].Label);
            Assert.Equal(2, result.Dataset.Rows[0].Value);
        }

        [Fact]
        public void SameStableId_DifferentDisplay_OneBucket_FirstLabelWins()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "owner");
            var result = Execute(
                Complete(
                    Row(User(field, 5, "Ivan")),
                    Row(User(field, 5, "IVAN I."))),
                CountBy(field),
                Catalog(Attr("owner", "Owner", "OrgUnit")));
            Assert.Single(result.Dataset.Rows);
            Assert.Equal("5", result.Dataset.Rows[0].Key);
            Assert.Equal("Ivan", result.Dataset.Rows[0].Label);
            Assert.Equal(2, result.Dataset.Rows[0].Value);
        }

        [Fact]
        public void DifferentIds_SameDisplay_TwoBuckets()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "owner");
            var result = Execute(
                Complete(
                    Row(User(field, 5, "Ivan")),
                    Row(User(field, 9, "Ivan"))),
                CountBy(field, DashboardQuerySort.LabelAscending),
                Catalog(Attr("owner", "Owner", "OrgUnit")));
            Assert.Equal(2, result.Dataset.Rows.Count);
            Assert.Equal("5", result.Dataset.Rows[0].Key);
            Assert.Equal("9", result.Dataset.Rows[1].Key);
            Assert.Equal("Ivan", result.Dataset.Rows[0].Label);
            Assert.Equal("Ivan", result.Dataset.Rows[1].Label);
        }

        [Fact]
        public void DisplayNameChange_DoesNotAffectIdentity()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "state");
            var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var result = Execute(
                Complete(
                    Row(EnumVal(field, id, "Open")),
                    Row(EnumVal(field, id, "Открыто"))),
                CountBy(field),
                Catalog(Attr("state", "State", "UserState")));
            Assert.Single(result.Dataset.Rows);
            Assert.Equal(id.ToString("D"), result.Dataset.Rows[0].Key);
            Assert.Equal("Open", result.Dataset.Rows[0].Label);
        }

        [Fact]
        public void Enum_GroupsOnStableIdentity()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "state");
            var open = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var closed = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var result = Execute(
                Complete(
                    Row(EnumVal(field, open, "Open")),
                    Row(EnumVal(field, closed, "Closed")),
                    Row(EnumVal(field, open, "OPEN"))),
                CountBy(field),
                Catalog(Attr("state", "State", "UserState")));
            Assert.Equal(2, result.Dataset.Rows.Count);
            Assert.Equal(open.ToString("D"), result.Dataset.Rows[0].Key);
            Assert.Equal(2, result.Dataset.Rows[0].Value);
        }

        [Fact]
        public void Text_GroupsOrdinalExact()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "code");
            var result = Execute(
                Complete(Row(Text(field, "a")), Row(Text(field, "A")), Row(Text(field, "a"))),
                CountBy(field),
                Catalog(Attr("code", "Code", "String")));
            Assert.Equal(2, result.Dataset.Rows.Count);
            Assert.Contains(result.Dataset.Rows, r => r.Key == "a" && r.Value == 2);
            Assert.Contains(result.Dataset.Rows, r => r.Key == "A" && r.Value == 1);
        }

        [Fact]
        public void Number_GroupsCultureIndependent()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "ratio");
            var result = Execute(
                Complete(Row(Number(field, 1.5)), Row(Number(field, 1.5)), Row(Number(field, 2.0))),
                CountBy(field),
                Catalog(Attr("ratio", "Ratio", "Double")));
            Assert.Equal("1.5", result.Dataset.Rows[0].Key);
            Assert.DoesNotContain(",", result.Dataset.Rows[0].Key);
            Assert.Equal(2, result.Dataset.Rows[0].Value);
            Assert.Equal("2", result.Dataset.Rows[1].Key);
        }

        [Fact]
        public void MissingNullEmptyWhitespace_ShareMissingBucket()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "note");
            var nullVal = new DashboardFieldValue(DashboardFieldType.Text, null, null, null);
            var emptyVal = new DashboardFieldValue(DashboardFieldType.Text, "", null, null);
            var spaceVal = new DashboardFieldValue(DashboardFieldType.Text, "  ", null, null);
            var result = Execute(
                Complete(
                    Row(Text(field, "A")),
                    Row(new Dictionary<string, DashboardFieldValue> { { field, nullVal } }),
                    Row(new Dictionary<string, DashboardFieldValue> { { field, emptyVal } }),
                    Row(new Dictionary<string, DashboardFieldValue> { { field, spaceVal } }),
                    Row()),
                CountBy(field),
                Catalog(Attr("note", "Note", "String")));

            Assert.DoesNotContain(result.Dataset.Rows, r => (r.Label ?? string.Empty).IndexOf("не задано", StringComparison.Ordinal) >= 0);
            var missing = result.Dataset.Rows.Single(r => r.Key == DashboardGroupValue.MissingKey);
            Assert.Equal(string.Empty, missing.Label);
            Assert.Equal(4, missing.Value);
            Assert.Equal(1, result.Dataset.Rows.Single(r => r.Key == "A").Value);
        }

        [Fact]
        public void GroupCounts_IncludingMissing_SumToRowCount()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "note");
            var dataset = Complete(
                Row(Text(field, "A")),
                Row(Text(field, "B")),
                Row(),
                Row(Text(field, "A")));
            var result = Execute(dataset, CountBy(field), Catalog(Attr("note", "Note", "String")));
            Assert.Equal(dataset.Rows.Count, result.Dataset.Rows.Sum(r => r.Value));
        }

        [Fact]
        public void Sort_ValueDescending()
        {
            var rows = SortLabels(DashboardQuerySort.ValueDescending);
            Assert.Equal(new[] { "B", "C", "A" }, rows);
        }

        [Fact]
        public void Sort_ValueAscending()
        {
            Assert.Equal(new[] { "A", "C", "B" }, SortLabels(DashboardQuerySort.ValueAscending));
        }

        [Fact]
        public void Sort_LabelAscending()
        {
            Assert.Equal(new[] { "A", "B", "C" }, SortLabels(DashboardQuerySort.LabelAscending, names: true));
        }

        [Fact]
        public void Sort_LabelDescending()
        {
            Assert.Equal(new[] { "C", "B", "A" }, SortLabels(DashboardQuerySort.LabelDescending, names: true));
        }

        [Fact]
        public void Sort_TieBreaksByKeyOrdinal()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "code");
            var result = Execute(
                Complete(Row(Text(field, "11")), Row(Text(field, "10"))),
                CountBy(field),
                Catalog(Attr("code", "Code", "String")));
            Assert.Equal("10", result.Dataset.Rows[0].Key);
            Assert.Equal("11", result.Dataset.Rows[1].Key);
        }

        [Fact]
        public void Limit_TakesFirstAfterSort()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "name");
            var result = Execute(
                Flatten(
                    Two(field, "A", 1),
                    Two(field, "B", 9),
                    Two(field, "C", 3)),
                CountBy(field, DashboardQuerySort.ValueDescending, 2),
                Catalog(Attr("name", "Name", "String")));
            Assert.Equal(2, result.Dataset.Rows.Count);
            Assert.Equal("B", result.Dataset.Rows[0].Label);
            Assert.Equal("C", result.Dataset.Rows[1].Label);
        }

        [Fact]
        public void Limit_Zero_Invalid()
        {
            var result = Execute(Complete(Row()), CountBy(DashboardFieldIds.SystemTypeId, DashboardQuerySort.ValueDescending, 0), Catalog());
            Assert.Equal(WidgetQueryStatus.InvalidQuery, result.Status);
        }

        [Fact]
        public void Limit_Oversize_ReturnsAll()
        {
            var result = Execute(
                Complete(Row(), Row()),
                CountBy(DashboardFieldIds.SystemTypeId, DashboardQuerySort.ValueDescending, 50),
                Catalog());
            Assert.Single(result.Dataset.Rows);
        }

        [Fact]
        public void Execute_DoesNotMutateDatasetOrFields()
        {
            var fields = new Dictionary<string, DashboardFieldValue>
            {
                { DashboardFieldIds.SystemTypeId, new DashboardFieldValue(DashboardFieldType.Integer, 12L, "12", null) }
            };
            var row = new DashboardObjectRow(Guid.NewGuid(), TypeId, null, fields);
            var dataset = Complete(row);
            var before = dataset.Rows[0].Fields.Count;
            Execute(dataset, CountBy(DashboardFieldIds.SystemTypeId), Catalog());
            Assert.Same(row, dataset.Rows[0]);
            Assert.Same(fields, dataset.Rows[0].Fields);
            Assert.Equal(before, dataset.Rows[0].Fields.Count);
            Assert.Equal(DashboardTypeCoverage.Complete, dataset.Coverage);
        }

        [Fact]
        public void RepeatedQuery_EquivalentResult()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "name");
            var dataset = Complete(Row(Text(field, "A")), Row(Text(field, "B")), Row(Text(field, "A")));
            var query = CountBy(field, DashboardQuerySort.ValueDescending, 2);
            var catalog = Catalog(Attr("name", "Name", "String"));
            var a = _engine.Execute(dataset, catalog, query);
            var b = _engine.Execute(dataset, catalog, query);
            Assert.Equal(a.Status, b.Status);
            Assert.Equal(
                a.Dataset.Rows.Select(r => r.Key + "|" + r.Label + "|" + r.Value).ToList(),
                b.Dataset.Rows.Select(r => r.Key + "|" + r.Label + "|" + r.Value).ToList());
        }

        [Fact]
        public void SkippedUnsupportedValues_FoldIntoMissing_DoNotBlockMappedGroups()
        {
            var field = DashboardFieldIds.Attribute(TypeId, "note");
            var dataset = new DashboardTypeDataset(
                TypeId,
                2,
                2,
                DashboardTypeCoverage.Complete,
                "complete",
                new[] { Row(Text(field, "A")), Row() },
                0,
                skippedUnsupportedValues: 1);
            var result = Execute(dataset, CountBy(field), Catalog(Attr("note", "Note", "String")));
            Assert.Equal(WidgetQueryStatus.Success, result.Status);
            Assert.Equal(2, result.Dataset.Rows.Sum(r => r.Value));
        }

        [Fact]
        public void MismatchedRowType_InvalidQuery()
        {
            var bad = new DashboardObjectRow(Guid.NewGuid(), 99, null, new Dictionary<string, DashboardFieldValue>());
            var dataset = new DashboardTypeDataset(
                TypeId, 1, 1, DashboardTypeCoverage.Complete, "complete", new[] { bad }, 0, 0);
            var result = Execute(dataset, Scalar());
            Assert.Equal(WidgetQueryStatus.InvalidQuery, result.Status);
            Assert.Empty(result.Dataset.Rows);
        }

        [Fact]
        public void EntityTypeId_Required()
        {
            var result = _engine.Execute(
                Complete(Row()),
                Catalog(),
                new DashboardWidgetQuery(
                    DashboardQueryScopeKind.CurrentProject,
                    null,
                    DashboardQueryMeasure.Count,
                    DashboardQuerySort.ValueDescending,
                    null));
            Assert.Equal(WidgetQueryStatus.InvalidQuery, result.Status);
        }

        private string[] SortLabels(DashboardQuerySort sort, bool names = false)
        {
            var field = DashboardFieldIds.Attribute(TypeId, "name");
            DashboardTypeDataset dataset;
            if (names)
            {
                dataset = Complete(
                    Row(Text(field, "C")),
                    Row(Text(field, "A")),
                    Row(Text(field, "B")));
            }
            else
            {
                dataset = Flatten(
                    Two(field, "A", 1),
                    Two(field, "B", 9),
                    Two(field, "C", 3));
            }
            var result = Execute(dataset, CountBy(field, sort), Catalog(Attr("name", "Name", "String")));
            Assert.Equal(WidgetQueryStatus.Success, result.Status);
            return result.Dataset.Rows.Select(r => r.Label).ToArray();
        }

        private WidgetQueryResult Execute(
            DashboardTypeDataset dataset,
            DashboardWidgetQuery query,
            DashboardFieldCatalog catalog = null)
        {
            return _engine.Execute(dataset, catalog ?? Catalog(), query);
        }

        private static DashboardWidgetQuery Scalar()
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                null,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                TypeId);
        }

        private static DashboardWidgetQuery CountBy(
            string fieldId,
            DashboardQuerySort sort = DashboardQuerySort.ValueDescending,
            int? limit = null)
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                fieldId,
                DashboardQueryMeasure.Count,
                sort,
                limit,
                TypeId);
        }

        private static DashboardTypeDataset Complete(params DashboardObjectRow[] rows)
        {
            var list = rows ?? new DashboardObjectRow[0];
            return new DashboardTypeDataset(
                TypeId,
                list.Length,
                list.Length,
                DashboardTypeCoverage.Complete,
                "complete",
                list,
                0,
                0);
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

        private static DashboardObjectRow Row(IReadOnlyDictionary<string, DashboardFieldValue> fields)
        {
            return new DashboardObjectRow(Guid.NewGuid(), TypeId, null, fields);
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

        private static DashboardObjectRow RowWithCreator(int creatorId)
        {
            long n = creatorId;
            return new DashboardObjectRow(
                Guid.NewGuid(),
                TypeId,
                null,
                new Dictionary<string, DashboardFieldValue>
                {
                    {
                        DashboardFieldIds.SystemCreatorId,
                        new DashboardFieldValue(DashboardFieldType.Integer, n, n.ToString(CultureInfo.InvariantCulture), null)
                    }
                });
        }

        private static KeyValuePair<string, DashboardFieldValue> Text(string id, string value)
        {
            return new KeyValuePair<string, DashboardFieldValue>(
                id,
                new DashboardFieldValue(DashboardFieldType.Text, value, null, null));
        }

        private static KeyValuePair<string, DashboardFieldValue> Integer(string id, long value)
        {
            return new KeyValuePair<string, DashboardFieldValue>(
                id,
                new DashboardFieldValue(DashboardFieldType.Integer, value, value.ToString(CultureInfo.InvariantCulture), null));
        }

        private static KeyValuePair<string, DashboardFieldValue> Number(string id, double value)
        {
            return new KeyValuePair<string, DashboardFieldValue>(
                id,
                new DashboardFieldValue(DashboardFieldType.Number, value, value.ToString("R", CultureInfo.InvariantCulture), null));
        }

        private static KeyValuePair<string, DashboardFieldValue> Bool(string id, bool value)
        {
            return new KeyValuePair<string, DashboardFieldValue>(
                id,
                new DashboardFieldValue(DashboardFieldType.Boolean, value, value ? "1" : "0", null));
        }

        private static KeyValuePair<string, DashboardFieldValue> User(string id, int orgId, string display)
        {
            var key = orgId.ToString(CultureInfo.InvariantCulture);
            return new KeyValuePair<string, DashboardFieldValue>(
                id,
                new DashboardFieldValue(DashboardFieldType.User, orgId, key, display));
        }

        private static KeyValuePair<string, DashboardFieldValue> EnumVal(string id, Guid stateId, string display)
        {
            var key = stateId.ToString("D");
            return new KeyValuePair<string, DashboardFieldValue>(
                id,
                new DashboardFieldValue(DashboardFieldType.Enum, stateId, key, display));
        }

        private static Tuple<DashboardTypeDataset, string> Two(string field, string label, int copies)
        {
            var rows = new List<DashboardObjectRow>();
            for (var i = 0; i < copies; i++)
                rows.Add(Row(Text(field, label)));
            return Tuple.Create(Complete(rows.ToArray()), label);
        }

        private static DashboardTypeDataset Flatten(params Tuple<DashboardTypeDataset, string>[] parts)
        {
            var rows = new List<DashboardObjectRow>();
            foreach (var part in parts)
                rows.AddRange(part.Item1.Rows);
            return Complete(rows.ToArray());
        }
    }
}
