using System;
using System.Collections.Generic;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Pure chart geometry. Device-independent. No WPF. Count stays long; coordinates are double.
    /// </summary>
    internal static class DashboardChartGeometry
    {
        public struct Rect
        {
            public Rect(double x, double y, double width, double height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public double X { get; private set; }
            public double Y { get; private set; }
            public double Width { get; private set; }
            public double Height { get; private set; }

            public bool IsFiniteNonNegative
            {
                get
                {
                    return IsFinite(X) && IsFinite(Y) && IsFinite(Width) && IsFinite(Height)
                        && Width >= 0 && Height >= 0;
                }
            }
        }

        public static double Ratio(long value, long max)
        {
            if (value <= 0 || max <= 0)
                return 0;
            return (double)value / (double)max;
        }

        public static long Max(IList<long> values)
        {
            long max = 0;
            if (values == null)
                return 0;
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] > max)
                    max = values[i];
            }
            return max;
        }

        public static long Sum(IList<long> values)
        {
            long total = 0;
            if (values == null)
                return 0;
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] > 0)
                    total += values[i];
            }
            return total;
        }

        public static Rect VerticalBar(
            int index,
            int count,
            long value,
            long max,
            double plotX,
            double plotY,
            double plotW,
            double plotH)
        {
            if (count < 1)
                count = 1;
            var gap = 6.0;
            var colW = (plotW - gap * (count + 1)) / count;
            if (colW < 1)
                colW = 1;
            var height = Ratio(value, max) * plotH;
            if (height > 0 && height < 1)
                height = 1;
            var x = plotX + gap + index * (colW + gap);
            var y = plotY + plotH - height;
            return new Rect(x, y, colW, height);
        }

        public static Rect HorizontalBar(
            int index,
            long value,
            long max,
            double barX,
            double barMaxWidth,
            double rowTop,
            double barHeight)
        {
            var width = Ratio(value, max) * barMaxWidth;
            if (width > 0 && width < 1)
                width = 1;
            if (barHeight < 0)
                barHeight = 0;
            return new Rect(barX, rowTop, width, barHeight);
        }

        public static bool TryPieSweep(long value, long total, out double sweepDegrees)
        {
            sweepDegrees = 0;
            if (total <= 0 || value <= 0)
                return false;
            sweepDegrees = (double)value / (double)total * 360.0;
            if (double.IsNaN(sweepDegrees) || double.IsInfinity(sweepDegrees) || sweepDegrees < 0)
            {
                sweepDegrees = 0;
                return false;
            }
            return true;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
