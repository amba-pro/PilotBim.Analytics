using System;
using System.Globalization;
using System.Threading;
using PilotBim.Analytics.Diagnostics;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class AnalyticsFormatsTests
    {
        [Fact]
        public void MonthBucketKey_Invariant_UnderRuRuCulture()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
                var key = AnalyticsFormats.MonthBucketKey(new DateTime(2026, 3, 15, 10, 0, 0));
                Assert.Equal("2026-03", key);
                Assert.DoesNotContain(",", key);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void FileTimestamp_Invariant_UnderRuRuCulture()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
                var stamp = AnalyticsFormats.FileTimestamp(new DateTime(2026, 3, 15, 14, 22, 33));
                Assert.Equal("20260315-142233", stamp);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void InvariantOneDecimal_UsesDot_UnderRuRuCulture()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
                Assert.Equal("12.5", AnalyticsFormats.InvariantOneDecimal(12.5));
                Assert.NotEqual((12.5).ToString("0.0"), AnalyticsFormats.InvariantOneDecimal(12.5));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}
