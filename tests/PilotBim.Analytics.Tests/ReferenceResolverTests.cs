using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Data;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    /// <summary>
    /// Pure helpers only — no Pilot-BIM runtime / IDataObject required.
    /// SDK-bound scanner/sampler coverage is deferred.
    /// </summary>
    public sealed class ReferenceResolverTests
    {
        [Fact]
        public void EnumerateIntIds_Null_Empty()
        {
            Assert.Empty(ReferenceResolver.EnumerateIntIds(null));
        }

        [Fact]
        public void EnumerateIntIds_IntAndLong()
        {
            Assert.Equal(new[] { 5 }, ReferenceResolver.EnumerateIntIds(5).ToArray());
            Assert.Equal(new[] { 7 }, ReferenceResolver.EnumerateIntIds(7L).ToArray());
        }

        [Fact]
        public void EnumerateIntIds_IntArray_AndNested()
        {
            Assert.Equal(new[] { 1, 2, 3 }, ReferenceResolver.EnumerateIntIds(new[] { 1, 2, 3 }).ToArray());
            Assert.Equal(new[] { 1, 2 }, ReferenceResolver.EnumerateIntIds(new object[] { 1, new[] { 2 } }).ToArray());
        }

        [Fact]
        public void EnumerateIntIds_ListOfInts()
        {
            Assert.Equal(new[] { 9, 8 }, ReferenceResolver.EnumerateIntIds(new List<int> { 9, 8 }).ToArray());
        }

        [Fact]
        public void EnumerateIntIds_String_YieldsNothing()
        {
            Assert.Empty(ReferenceResolver.EnumerateIntIds("123"));
        }

        [Fact]
        public void TryGetGuid_Variants()
        {
            Guid g;
            Assert.False(ReferenceResolver.TryGetGuid(null, out g));
            Assert.Equal(Guid.Empty, g);

            var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            Assert.True(ReferenceResolver.TryGetGuid(id, out g));
            Assert.Equal(id, g);

            Assert.True(ReferenceResolver.TryGetGuid(id.ToString(), out g));
            Assert.Equal(id, g);

            Assert.False(ReferenceResolver.TryGetGuid(Guid.Empty, out g));
            Assert.False(ReferenceResolver.TryGetGuid("not-a-guid", out g));
            Assert.False(ReferenceResolver.TryGetGuid("", out g));
        }

        [Fact]
        public void IsEmptyValue_Contracts()
        {
            Assert.True(ReferenceResolver.IsEmptyValue(null));
            Assert.True(ReferenceResolver.IsEmptyValue(""));
            Assert.True(ReferenceResolver.IsEmptyValue("   "));
            Assert.True(ReferenceResolver.IsEmptyValue(Guid.Empty));
            Assert.True(ReferenceResolver.IsEmptyValue(new int[0]));
            Assert.False(ReferenceResolver.IsEmptyValue("x"));
            Assert.False(ReferenceResolver.IsEmptyValue(Guid.NewGuid()));
            Assert.False(ReferenceResolver.IsEmptyValue(new[] { 1 }));
            Assert.False(ReferenceResolver.IsEmptyValue(0)); // non-null non-string non-guid non-empty-array
        }

        [Fact]
        public void SafeSampleString_NullEmptyTruncateAndNewlines()
        {
            Assert.Null(ReferenceResolver.SafeSampleString(null));
            Assert.Null(ReferenceResolver.SafeSampleString("   "));
            Assert.Equal("hello", ReferenceResolver.SafeSampleString("hello"));
            Assert.Equal("a  b", ReferenceResolver.SafeSampleString("a\r\nb"));

            var longText = new string('x', 100);
            var sample = ReferenceResolver.SafeSampleString(longText, 10);
            Assert.Equal(new string('x', 10) + "...", sample);

            Assert.Equal("[3 items]", ReferenceResolver.SafeSampleString(new[] { 1, 2, 3 }));
        }

        [Fact]
        public void SafeSampleString_GuidAndDateTime()
        {
            var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            Assert.Equal(id.ToString(), ReferenceResolver.SafeSampleString(id));

            var dt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            Assert.Equal(dt.ToString("o"), ReferenceResolver.SafeSampleString(dt));
        }
    }
}
