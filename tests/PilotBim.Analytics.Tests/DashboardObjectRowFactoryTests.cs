using System;
using System.Collections.Generic;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardObjectRowFactoryTests
    {
        private readonly DashboardObjectRowFactory _factory = new DashboardObjectRowFactory();

        [Fact]
        public void ObjectId_TypeId_ParentId_Preserved()
        {
            var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var parent = Guid.Parse("11111111-2222-3333-4444-555555555555");
            var row = _factory.Create(Source(id, 12, parent));

            Assert.Equal(id, row.ObjectId);
            Assert.Equal(12, row.TypeId);
            Assert.Equal(parent, row.ParentId);
        }

        [Fact]
        public void EmptyParent_IsNull_AndOmitsParentField()
        {
            var row = _factory.Create(Source(Guid.NewGuid(), 3, Guid.Empty));
            Assert.Null(row.ParentId);
            Assert.False(row.Fields.ContainsKey(DashboardFieldIds.SystemParentId));
        }

        [Fact]
        public void SystemFields_UseDb1Ids()
        {
            var id = Guid.NewGuid();
            var created = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc);
            var row = _factory.Create(new DashboardObjectSource
            {
                Id = id,
                TypeId = 7,
                ParentId = Guid.NewGuid(),
                CreatorId = 42,
                Created = created,
                ObjectState = "Alive"
            });

            Assert.True(row.Fields.ContainsKey(DashboardFieldIds.SystemObjectId));
            Assert.True(row.Fields.ContainsKey(DashboardFieldIds.SystemTypeId));
            Assert.True(row.Fields.ContainsKey(DashboardFieldIds.SystemParentId));
            Assert.True(row.Fields.ContainsKey(DashboardFieldIds.SystemCreatorId));
            Assert.True(row.Fields.ContainsKey(DashboardFieldIds.SystemCreated));
            Assert.True(row.Fields.ContainsKey(DashboardFieldIds.SystemObjectState));
            Assert.False(row.Fields.ContainsKey(DashboardFieldIds.SystemCreatedMonth));
            Assert.Equal(id, row.Fields[DashboardFieldIds.SystemObjectId].Value);
            Assert.Equal(7L, row.Fields[DashboardFieldIds.SystemTypeId].Value);
            Assert.Equal(42L, row.Fields[DashboardFieldIds.SystemCreatorId].Value);
            Assert.Equal(created, row.Fields[DashboardFieldIds.SystemCreated].Value);
            Assert.Equal("Alive", row.Fields[DashboardFieldIds.SystemObjectState].Value);
        }

        [Fact]
        public void AttributeIds_MatchFieldCatalog()
        {
            var row = _factory.Create(Source(
                Guid.NewGuid(),
                10,
                Guid.Empty,
                Attr("resp", "OrgUnit", 9)));

            Assert.True(row.Fields.ContainsKey(DashboardFieldIds.Attribute(10, "resp")));
            Assert.True(row.Fields.ContainsKey("attribute:10:resp"));
        }

        [Fact]
        public void DisplayTitle_DoesNotAffectAttributeIdentity()
        {
            var id = DashboardFieldIds.Attribute(5, "code");
            var row = _factory.Create(Source(
                Guid.NewGuid(),
                5,
                Guid.Empty,
                Attr("code", "String", "A")));

            Assert.True(row.Fields.ContainsKey(id));
            Assert.Equal("A", row.Fields[id].Value);
        }

        [Fact]
        public void DisplayText_IsNotIdentity()
        {
            var a = new DashboardFieldValue(DashboardFieldType.Integer, 5L, "5", "five");
            var b = new DashboardFieldValue(DashboardFieldType.Integer, 5L, "5", "FIVE");
            Assert.Equal(a.StableKey, b.StableKey);
            Assert.Equal(a.Value, b.Value);
            Assert.Equal(a.Kind, b.Kind);
            Assert.NotEqual(a.DisplayText, b.DisplayText);
        }

        [Fact]
        public void Number_RemainsNumeric()
        {
            DashboardFieldValue mapped;
            Assert.True(DashboardObjectRowFactory.TryMapAttribute(Attr("n", "Double", 1.5), out mapped));
            Assert.Equal(DashboardFieldType.Number, mapped.Kind);
            Assert.IsType<double>(mapped.Value);
            Assert.Equal(1.5, (double)mapped.Value);
        }

        [Fact]
        public void Integer_RemainsInteger()
        {
            DashboardFieldValue mapped;
            Assert.True(DashboardObjectRowFactory.TryMapAttribute(Attr("n", "Integer", 8), out mapped));
            Assert.Equal(DashboardFieldType.Integer, mapped.Kind);
            Assert.IsType<long>(mapped.Value);
            Assert.Equal(8L, mapped.Value);
        }

        [Fact]
        public void DateTime_RemainsDateTime()
        {
            var dt = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            DashboardFieldValue mapped;
            Assert.True(DashboardObjectRowFactory.TryMapAttribute(Attr("d", "DateTime", dt), out mapped));
            Assert.Equal(DashboardFieldType.DateTime, mapped.Kind);
            Assert.IsType<DateTime>(mapped.Value);
            Assert.Equal(dt, mapped.Value);
        }

        [Fact]
        public void Bool_RemainsBool()
        {
            DashboardFieldValue mapped;
            Assert.True(DashboardObjectRowFactory.TryMapAttribute(Attr("b", "Boolean", true), out mapped));
            Assert.Equal(DashboardFieldType.Boolean, mapped.Kind);
            Assert.IsType<bool>(mapped.Value);
            Assert.True((bool)mapped.Value);
        }

        [Fact]
        public void Guid_RemainsGuid()
        {
            var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var row = _factory.Create(Source(id, 1, Guid.Empty));
            var value = row.Fields[DashboardFieldIds.SystemObjectId].Value;
            Assert.IsType<Guid>(value);
            Assert.Equal(id, value);
        }

        [Fact]
        public void NullAttribute_IsDeterministic()
        {
            DashboardFieldValue mapped;
            Assert.True(DashboardObjectRowFactory.TryMapAttribute(Attr("empty", "String", null), out mapped));
            Assert.Equal(DashboardFieldType.Text, mapped.Kind);
            Assert.Null(mapped.Value);
            Assert.Null(mapped.StableKey);
        }

        [Fact]
        public void UnsupportedValue_DoesNotCrash()
        {
            var row = _factory.Create(Source(
                Guid.NewGuid(),
                4,
                Guid.Empty,
                Attr("blob", "Array", new object()),
                Attr("ok", "String", "kept")));

            Assert.NotNull(row);
            Assert.Equal(1, _factory.SkippedUnsupportedValues);
            Assert.True(row.Fields.ContainsKey(DashboardFieldIds.Attribute(4, "ok")));
            Assert.False(row.Fields.ContainsKey(DashboardFieldIds.Attribute(4, "blob")));
        }

        [Fact]
        public void SameAttributeName_AcrossTypeIds_RemainsDistinct()
        {
            var a = _factory.Create(Source(Guid.NewGuid(), 1, Guid.Empty, Attr("status", "UserState", Guid.NewGuid())));
            var b = _factory.Create(Source(Guid.NewGuid(), 2, Guid.Empty, Attr("status", "UserState", Guid.NewGuid())));

            Assert.True(a.Fields.ContainsKey("attribute:1:status"));
            Assert.True(b.Fields.ContainsKey("attribute:2:status"));
            Assert.False(a.Fields.ContainsKey("attribute:2:status"));
            Assert.False(b.Fields.ContainsKey("attribute:1:status"));
        }

        [Fact]
        public void UserStateAndResponsible_CopiedFromAttributes_WhenPresent()
        {
            var stateId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
            var row = _factory.Create(Source(
                Guid.NewGuid(),
                8,
                Guid.Empty,
                Attr("cardState", "UserState", stateId),
                Attr("owner", "OrgUnit", 77)));

            Assert.Equal(stateId, row.Fields[DashboardFieldIds.SystemUserState].Value);
            Assert.Equal(77, row.Fields[DashboardFieldIds.SystemResponsible].Value);
            Assert.Equal("attribute:8:cardState", DashboardFieldIds.Attribute(8, "cardState"));
        }

        [Fact]
        public void EmptyGuidObject_ReturnsNull()
        {
            Assert.Null(_factory.Create(Source(Guid.Empty, 1, Guid.Empty)));
        }

        private static DashboardObjectSource Source(
            Guid id,
            int typeId,
            Guid parent,
            params DashboardAttributeSource[] attributes)
        {
            return new DashboardObjectSource
            {
                Id = id,
                TypeId = typeId,
                ParentId = parent,
                Attributes = new List<DashboardAttributeSource>(attributes)
            };
        }

        private static DashboardAttributeSource Attr(string name, string valueType, object value)
        {
            return new DashboardAttributeSource
            {
                Name = name,
                ValueType = valueType,
                Value = value,
                Present = true
            };
        }
    }
}
