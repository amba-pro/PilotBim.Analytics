using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardVisualizationRecommendationTests
    {
        [Fact]
        public void Scalar_Kpi()
        {
            Assert.Equal("Kpi", DashboardVisualizationRecommendationService.Recommend(false, 1));
            Assert.Equal("Kpi", DashboardVisualizationRecommendationService.Recommend(false, 0));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public void GroupedSmall_Bar(int rows)
        {
            Assert.Equal("Bar", DashboardVisualizationRecommendationService.Recommend(true, rows));
        }

        [Theory]
        [InlineData(6)]
        [InlineData(15)]
        public void GroupedMedium_HorizontalBar(int rows)
        {
            Assert.Equal("HorizontalBar", DashboardVisualizationRecommendationService.Recommend(true, rows));
        }

        [Fact]
        public void GroupedLarge_Table()
        {
            Assert.Equal("Table", DashboardVisualizationRecommendationService.Recommend(true, 16));
        }

        [Fact]
        public void NeverPieOrLine()
        {
            for (var i = 0; i <= 20; i++)
            {
                var type = DashboardVisualizationRecommendationService.Recommend(true, i);
                Assert.NotEqual("Pie", type);
                Assert.NotEqual("Line", type);
            }
        }

        [Fact]
        public void Deterministic()
        {
            Assert.Equal(
                DashboardVisualizationRecommendationService.Recommend(true, 9),
                DashboardVisualizationRecommendationService.Recommend(true, 9));
        }
    }
}
