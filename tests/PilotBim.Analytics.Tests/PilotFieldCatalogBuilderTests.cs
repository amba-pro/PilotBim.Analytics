using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class PilotFieldCatalogBuilderTests
    {
        [Fact]
        public void Build_EmptyMetadata_ContainsOnlySystemFields()
        {
            var builder = new PilotFieldCatalogBuilder();
            var catalog = builder.Build(Enumerable.Empty<TypeInventoryRecord>());

            Assert.Equal(6, catalog.Count);
            Assert.True(catalog.Fields.All(f => f.SourceKind == DashboardFieldSourceKind.System));
            Assert.Equal(0, builder.SkippedEmptyAttributeNames);
            Assert.Equal(0, builder.SkippedDuplicateIds);
        }

        [Fact]
        public void Build_NullReport_ContainsOnlySystemFields()
        {
            var catalog = new PilotFieldCatalogBuilder().Build((ProjectInventoryReport)null);
            Assert.Equal(6, catalog.Count);
        }

        [Fact]
        public void SystemField_Created_HasStableId()
        {
            var catalog = new PilotFieldCatalogBuilder().Build(Enumerable.Empty<TypeInventoryRecord>());
            DashboardFieldDescriptor field;
            Assert.True(catalog.TryGet(DashboardFieldIds.SystemCreated, out field));
            Assert.Equal("system:created", field.Id);
            Assert.Equal(DashboardFieldType.DateTime, field.FieldType);
            Assert.True(field.Capabilities.CanGroup);
        }

        [Fact]
        public void AttributeId_IsDeterministic_AndUsesTypeIdPlusName()
        {
            var types = new[]
            {
                Type(10, Attr("resp", "Ответственный", "OrgUnit"))
            };
            var catalog = new PilotFieldCatalogBuilder().Build(types);

            DashboardFieldDescriptor field;
            Assert.True(catalog.TryGet("attribute:10:resp", out field));
            Assert.Equal(DashboardFieldIds.Attribute(10, "resp"), field.Id);
            Assert.Equal("Ответственный", field.DisplayName);
            Assert.Equal("resp", field.SourceName);
            Assert.Equal(10, field.ObjectTypeId);
            Assert.Equal(DashboardFieldType.User, field.FieldType);
        }

        [Fact]
        public void SameAttributeName_UnderDifferentTypeIds_DoesNotCollide()
        {
            var types = new[]
            {
                Type(1, Attr("status", "Статус A", "UserState")),
                Type(2, Attr("status", "Статус B", "UserState"))
            };
            var catalog = new PilotFieldCatalogBuilder().Build(types);

            DashboardFieldDescriptor a, b;
            Assert.True(catalog.TryGet("attribute:1:status", out a));
            Assert.True(catalog.TryGet("attribute:2:status", out b));
            Assert.NotEqual(a.Id, b.Id);
            Assert.Equal("Статус A", a.DisplayName);
            Assert.Equal("Статус B", b.DisplayName);
        }

        [Fact]
        public void DisplayTitle_DoesNotAffectIdentity()
        {
            var idA = CatalogAttrId(Type(5, Attr("code", "Код", "String")));
            var idB = CatalogAttrId(Type(5, Attr("code", "Other Title", "String")));
            Assert.Equal(idA, idB);
            Assert.Equal("attribute:5:code", idA);
        }

        [Fact]
        public void NullOrEmptyTitle_UsesNameFallback()
        {
            var catalog = new PilotFieldCatalogBuilder().Build(new[]
            {
                Type(3, Attr("plain", null, "String")),
                Type(4, Attr("blank", "   ", "Integer"))
            });

            DashboardFieldDescriptor plain, blank;
            Assert.True(catalog.TryGet("attribute:3:plain", out plain));
            Assert.True(catalog.TryGet("attribute:4:blank", out blank));
            Assert.Equal("plain", plain.DisplayName);
            Assert.Equal("blank", blank.DisplayName);
        }

        [Fact]
        public void UnsupportedValueType_MapsToUnknown_WithConservativeCapabilities()
        {
            Assert.Equal(DashboardFieldType.Unknown, PilotFieldCatalogBuilder.MapValueType("Inherited"));
            Assert.Equal(DashboardFieldType.Unknown, PilotFieldCatalogBuilder.MapValueType("Array"));
            Assert.Equal(DashboardFieldType.Unknown, PilotFieldCatalogBuilder.MapValueType("TotallyNewType"));
            Assert.Equal(DashboardFieldType.Unknown, PilotFieldCatalogBuilder.MapValueType(null));

            var catalog = new PilotFieldCatalogBuilder().Build(new[]
            {
                Type(7, Attr("arr", "Array Field", "Array"))
            });
            DashboardFieldDescriptor field;
            Assert.True(catalog.TryGet("attribute:7:arr", out field));
            Assert.Equal(DashboardFieldType.Unknown, field.FieldType);
            Assert.False(field.Capabilities.CanFilter);
            Assert.False(field.Capabilities.CanGroup);
            Assert.False(field.Capabilities.CanSort);
        }

        [Fact]
        public void Ordering_IsDeterministic_SystemThenTypeIdThenName()
        {
            var types = new[]
            {
                Type(20, Attr("z", "Z", "String"), Attr("a", "A", "String")),
                Type(10, Attr("m", "M", "Integer"))
            };
            var catalog = new PilotFieldCatalogBuilder().Build(types);
            var ids = catalog.Fields.Select(f => f.Id).ToList();

            Assert.Equal(DashboardFieldIds.SystemObjectId, ids[0]);
            Assert.Equal(DashboardFieldIds.SystemTypeId, ids[1]);
            Assert.Equal(DashboardFieldIds.SystemParentId, ids[2]);
            Assert.Equal(DashboardFieldIds.SystemCreatorId, ids[3]);
            Assert.Equal(DashboardFieldIds.SystemCreated, ids[4]);
            Assert.Equal(DashboardFieldIds.SystemObjectState, ids[5]);
            Assert.Equal("attribute:10:m", ids[6]);
            Assert.Equal("attribute:20:a", ids[7]);
            Assert.Equal("attribute:20:z", ids[8]);
        }

        [Fact]
        public void DuplicateAttributeIdentity_RetainsFirst_SkipsLater()
        {
            var type = new TypeInventoryRecord
            {
                TypeId = 9,
                Name = "t",
                Attributes = new List<AttributeInventoryRecord>
                {
                    Attr("dup", "First", "String"),
                    Attr("dup", "Second", "String")
                }
            };
            var builder = new PilotFieldCatalogBuilder();
            var catalog = builder.Build(new[] { type });

            Assert.Equal(1, builder.SkippedDuplicateIds);
            DashboardFieldDescriptor field;
            Assert.True(catalog.TryGet("attribute:9:dup", out field));
            Assert.Equal("First", field.DisplayName);
            Assert.Equal(1, catalog.Fields.Count(f => f.Id == "attribute:9:dup"));
        }

        [Fact]
        public void EmptyAttributeName_IsSkipped()
        {
            var builder = new PilotFieldCatalogBuilder();
            var catalog = builder.Build(new[]
            {
                Type(1, Attr("", "Empty", "String"), Attr("  ", "Blank", "String"), Attr("ok", "OK", "String"))
            });

            Assert.Equal(2, builder.SkippedEmptyAttributeNames);
            Assert.False(catalog.TryGet("attribute:1:", out _));
            Assert.True(catalog.TryGet("attribute:1:ok", out _));
        }

        [Fact]
        public void RussianDisplayText_NeverBecomesMachineIdentity()
        {
            var catalog = new PilotFieldCatalogBuilder().Build(new[]
            {
                Type(11, Attr("responsible", "Ответственный", "OrgUnit"))
            });
            Assert.DoesNotContain(catalog.Fields, f => f.Id.IndexOf("Ответственный") >= 0);
            Assert.DoesNotContain(catalog.Fields, f => f.Id == "Ответственный");
            Assert.Contains(catalog.Fields, f => f.Id == "attribute:11:responsible");
        }

        [Fact]
        public void RebuildSameMetadata_ProducesEquivalentCatalog()
        {
            var types = new[]
            {
                Type(1, Attr("a", "А", "String")),
                Type(2, Attr("b", "Б", "Integer"))
            };
            var a = new PilotFieldCatalogBuilder().Build(types);
            var b = new PilotFieldCatalogBuilder().Build(types);

            Assert.Equal(a.Count, b.Count);
            Assert.Equal(
                a.Fields.Select(f => f.Id + "|" + f.DisplayName + "|" + f.FieldType).ToList(),
                b.Fields.Select(f => f.Id + "|" + f.DisplayName + "|" + f.FieldType).ToList());
        }

        [Fact]
        public void ValueTypeMapping_KnownAttributeTypes()
        {
            Assert.Equal(DashboardFieldType.Text, PilotFieldCatalogBuilder.MapValueType("String"));
            Assert.Equal(DashboardFieldType.Integer, PilotFieldCatalogBuilder.MapValueType("Integer"));
            Assert.Equal(DashboardFieldType.Number, PilotFieldCatalogBuilder.MapValueType("Double"));
            Assert.Equal(DashboardFieldType.Number, PilotFieldCatalogBuilder.MapValueType("Decimal"));
            Assert.Equal(DashboardFieldType.Boolean, PilotFieldCatalogBuilder.MapValueType("Boolean"));
            Assert.Equal(DashboardFieldType.DateTime, PilotFieldCatalogBuilder.MapValueType("DateTime"));
            Assert.Equal(DashboardFieldType.Enum, PilotFieldCatalogBuilder.MapValueType("UserState"));
            Assert.Equal(DashboardFieldType.User, PilotFieldCatalogBuilder.MapValueType("OrgUnit"));
            Assert.Equal(DashboardFieldType.Reference, PilotFieldCatalogBuilder.MapValueType("ElementBook"));
            Assert.Equal(DashboardFieldType.Text, PilotFieldCatalogBuilder.MapValueType("Numerator"));
        }

        [Fact]
        public void ForObjectType_ReturnsSystemPlusTypeAttributes()
        {
            var catalog = new PilotFieldCatalogBuilder().Build(new[]
            {
                Type(1, Attr("a", "A", "String")),
                Type(2, Attr("b", "B", "String"))
            });
            var for1 = catalog.ForObjectType(1);
            Assert.Equal(6, for1.Count(f => f.SourceKind == DashboardFieldSourceKind.System));
            Assert.Contains(for1, f => f.Id == "attribute:1:a");
            Assert.DoesNotContain(for1, f => f.Id == "attribute:2:b");
        }

        private static string CatalogAttrId(TypeInventoryRecord type)
        {
            var catalog = new PilotFieldCatalogBuilder().Build(new[] { type });
            return catalog.Fields.Single(f => f.SourceKind == DashboardFieldSourceKind.Attribute).Id;
        }

        private static TypeInventoryRecord Type(int typeId, params AttributeInventoryRecord[] attrs)
        {
            return new TypeInventoryRecord
            {
                TypeId = typeId,
                Name = "type" + typeId,
                Title = "Type " + typeId,
                Attributes = attrs.ToList()
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
