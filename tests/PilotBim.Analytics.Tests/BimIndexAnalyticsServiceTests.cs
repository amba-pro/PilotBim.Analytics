using PilotBim.Analytics.Discovery;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class BimIndexAnalyticsServiceTests
    {
        [Fact]
        public void ComputeFillPercent_Normal()
        {
            Assert.Equal(50.0, BimIndexAnalyticsService.ComputeFillPercent(50, 100));
        }

        [Fact]
        public void ComputeFillPercent_ZeroDenominator()
        {
            Assert.Equal(0, BimIndexAnalyticsService.ComputeFillPercent(10, 0));
        }

        [Fact]
        public void ComputeFillPercent_InconsistentCappedCounts_ClampsTo100()
        {
            // Independent capped queries can yield GlobalIdCount > ElementCount
            Assert.Equal(100.0, BimIndexAnalyticsService.ComputeFillPercent(120, 100));
        }

        [Fact]
        public void ComputeWithoutGlobalId_ClampsWhenGlobalExceedsElements()
        {
            Assert.Equal(0, BimIndexAnalyticsService.ComputeWithoutGlobalId(100, 120));
            Assert.Equal(40, BimIndexAnalyticsService.ComputeWithoutGlobalId(100, 60));
        }

        [Fact]
        public void ComputeSharePercent_DoesNotExceed100()
        {
            Assert.Equal(100.0, BimIndexAnalyticsService.ComputeSharePercent(150, 100));
        }

        [Fact]
        public void IsCountTruncated_EqualityMeansPossiblyMore_ConservativeContract()
        {
            Assert.False(BimIndexAnalyticsService.IsCountTruncated(99, 100));
            Assert.True(BimIndexAnalyticsService.IsCountTruncated(100, 100));
            Assert.True(BimIndexAnalyticsService.IsCountTruncated(101, 100));
            Assert.False(BimIndexAnalyticsService.IsCountTruncated(0, 100));
            Assert.False(BimIndexAnalyticsService.IsCountTruncated(5, 0));
        }
    }
}
