using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardVisualizationCompatibilityTests
    {
        [Fact]
        public void Scalar_AllowsAutoKpiTable()
        {
            Assert.True(DashboardVisualizationCompatibility.IsAllowed("Auto", false));
            Assert.True(DashboardVisualizationCompatibility.IsAllowed("Kpi", false));
            Assert.True(DashboardVisualizationCompatibility.IsAllowed("Table", false));
            Assert.False(DashboardVisualizationCompatibility.IsAllowed("Bar", false));
            Assert.False(DashboardVisualizationCompatibility.IsAllowed("HorizontalBar", false));
            Assert.False(DashboardVisualizationCompatibility.IsAllowed("Pie", false));
        }

        [Fact]
        public void Grouped_AllowsChartsAndTable_NotKpi()
        {
            Assert.True(DashboardVisualizationCompatibility.IsAllowed("Auto", true));
            Assert.True(DashboardVisualizationCompatibility.IsAllowed("Bar", true));
            Assert.True(DashboardVisualizationCompatibility.IsAllowed("HorizontalBar", true));
            Assert.True(DashboardVisualizationCompatibility.IsAllowed("Pie", true));
            Assert.True(DashboardVisualizationCompatibility.IsAllowed("Table", true));
            Assert.False(DashboardVisualizationCompatibility.IsAllowed("Kpi", true));
        }

        [Fact]
        public void TypeIdLine_Invalid()
        {
            Assert.False(DashboardVisualizationCompatibility.IsAllowed("Line", false));
            Assert.False(DashboardVisualizationCompatibility.IsAllowed("Line", true));
        }
    }
}
