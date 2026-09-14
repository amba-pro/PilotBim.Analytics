using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardGridMetricsTests
    {
        [Fact]
        public void Snap_UsesDeviceIndependentColumnAndRow()
        {
            var metrics = new DashboardGridMetrics(720, 10, 112);
            Assert.Equal(0, metrics.ColumnAt(0));
            Assert.Equal(0, metrics.ColumnAt(metrics.ColumnWidth - 1));
            Assert.Equal(1, metrics.ColumnAt(metrics.ColumnWidth + metrics.Gutter));
            Assert.Equal(11, metrics.ColumnAt(10000));
            Assert.Equal(0, metrics.RowAt(0));
            Assert.Equal(1, metrics.RowAt(metrics.RowHeight + metrics.Gutter));
        }

        [Fact]
        public void SnapRect_AndSnapResize_ClampToGrid()
        {
            var metrics = DashboardGridMetrics.FromAvailableWidth(900);
            var moved = metrics.SnapRect(metrics.PixelX(3) + 2, metrics.PixelY(1) + 2, 6, 2);
            Assert.Equal(3, moved.X);
            Assert.Equal(1, moved.Y);
            Assert.Equal(6, moved.Width);
            Assert.Equal(2, moved.Height);

            var resized = metrics.SnapResize(0, 0, metrics.PixelX(8) + 1, metrics.PixelY(4) + 1);
            Assert.Equal(0, resized.X);
            Assert.Equal(0, resized.Y);
            Assert.Equal(9, resized.Width);
            Assert.Equal(5, resized.Height);

            var min = metrics.SnapResize(0, 0, 1, 1);
            Assert.Equal(DashboardGridLayoutEngine.MinWidth, min.Width);
            Assert.Equal(DashboardGridLayoutEngine.MinHeight, min.Height);
        }

        [Fact]
        public void NarrowWidth_UsesMinDashboardWidth()
        {
            var metrics = DashboardGridMetrics.FromAvailableWidth(100);
            Assert.Equal(DashboardGridMetrics.MinDashboardWidth, metrics.AvailableWidth);
        }
    }
}
