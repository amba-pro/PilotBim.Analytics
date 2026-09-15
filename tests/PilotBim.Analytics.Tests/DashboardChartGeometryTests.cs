using System.Collections.Generic;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardChartGeometryTests
    {
        [Fact]
        public void AllZero_NoDivideByZero()
        {
            var values = new List<long> { 0, 0 };
            Assert.Equal(0, DashboardChartGeometry.Max(values));
            Assert.Equal(0, DashboardChartGeometry.Ratio(0, 0));
            double sweep;
            Assert.False(DashboardChartGeometry.TryPieSweep(0, 0, out sweep));
            Assert.Equal(0, sweep);
        }

        [Fact]
        public void OneCategory_FullBarAndPie()
        {
            var bar = DashboardChartGeometry.VerticalBar(0, 1, 10, 10, 0, 0, 100, 80);
            Assert.True(bar.IsFiniteNonNegative);
            Assert.Equal(80, bar.Height, 5);
            double sweep;
            Assert.True(DashboardChartGeometry.TryPieSweep(10, 10, out sweep));
            Assert.Equal(360, sweep, 5);
        }

        [Fact]
        public void LargeLong_RemainsFinite()
        {
            var max = 1L << 50;
            var bar = DashboardChartGeometry.VerticalBar(0, 2, max, max, 0, 0, 200, 100);
            Assert.True(bar.IsFiniteNonNegative);
            Assert.True(bar.Height > 0);
            var small = DashboardChartGeometry.VerticalBar(0, 1, 1, 1, 0, 0, 10, 10);
            Assert.True(small.IsFiniteNonNegative);
            Assert.True(small.Width >= 1);
        }

        [Fact]
        public void PieSlices_SumToCircle()
        {
            var values = new List<long> { 1, 2, 3 };
            var total = DashboardChartGeometry.Sum(values);
            var sum = 0.0;
            for (var i = 0; i < values.Count; i++)
            {
                double sweep;
                Assert.True(DashboardChartGeometry.TryPieSweep(values[i], total, out sweep));
                Assert.True(sweep >= 0);
                Assert.False(double.IsNaN(sweep));
                sum += sweep;
            }
            Assert.Equal(360, sum, 6);
        }

        [Fact]
        public void HorizontalBar_ZeroWidthWhenZeroValue()
        {
            var rect = DashboardChartGeometry.HorizontalBar(0, 0, 10, 20, 100, 0, 12);
            Assert.Equal(0, rect.Width);
            Assert.True(rect.IsFiniteNonNegative);
        }
    }
}
