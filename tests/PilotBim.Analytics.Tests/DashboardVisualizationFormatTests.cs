using PilotBim.Analytics.Properties;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardVisualizationFormatTests
    {
        [Fact]
        public void Count_ZeroOneAndGrouped()
        {
            Assert.Equal("0", DashboardVisualizationFormat.Count(0));
            Assert.Equal("1", DashboardVisualizationFormat.Count(1));
            Assert.Contains("234", DashboardVisualizationFormat.Count(1234));
            Assert.Contains("567", DashboardVisualizationFormat.Count(1234567));
        }

        [Fact]
        public void MissingLabel_IsLocalizedPlaceholder()
        {
            Assert.Equal(Resources.QueryViz_NotSet, DashboardVisualizationFormat.DisplayLabel(""));
            Assert.Equal(Resources.QueryViz_NotSet, DashboardVisualizationFormat.DisplayLabel(null));
            Assert.Equal("Open", DashboardVisualizationFormat.DisplayLabel("Open"));
        }
    }
}
