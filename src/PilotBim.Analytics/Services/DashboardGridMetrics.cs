using System;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Device-independent mapping between pointer/layout pixels and the 12-column logical grid.
    /// </summary>
    internal sealed class DashboardGridMetrics
    {
        public const double DefaultGutter = 10;
        public const double DefaultRowHeight = 112;
        public const double MinDashboardWidth = 720;

        public DashboardGridMetrics(double availableWidth, double gutter, double rowHeight)
        {
            Gutter = gutter < 0 ? 0 : gutter;
            RowHeight = rowHeight <= 0 ? DefaultRowHeight : rowHeight;
            var width = availableWidth;
            if (double.IsNaN(width) || double.IsInfinity(width) || width < MinDashboardWidth)
                width = MinDashboardWidth;
            AvailableWidth = width;
            var gutters = DashboardGridLayoutEngine.Columns - 1;
            ColumnWidth = (width - gutters * Gutter) / DashboardGridLayoutEngine.Columns;
            if (ColumnWidth < 1)
                ColumnWidth = 1;
        }

        public static DashboardGridMetrics FromAvailableWidth(double availableWidth)
        {
            return new DashboardGridMetrics(availableWidth, DefaultGutter, DefaultRowHeight);
        }

        public double AvailableWidth { get; private set; }
        public double Gutter { get; private set; }
        public double RowHeight { get; private set; }
        public double ColumnWidth { get; private set; }

        public int ColumnAt(double x)
        {
            var stride = ColumnWidth + Gutter;
            if (stride <= 0)
                return 0;
            var column = (int)Math.Floor(x / stride);
            if (column < 0)
                return 0;
            if (column >= DashboardGridLayoutEngine.Columns)
                return DashboardGridLayoutEngine.Columns - 1;
            return column;
        }

        public int RowAt(double y)
        {
            var stride = RowHeight + Gutter;
            if (stride <= 0)
                return 0;
            var row = (int)Math.Floor(y / stride);
            if (row < 0)
                return 0;
            return row;
        }

        public DashboardGridRect SnapRect(double x, double y, int width, int height)
        {
            return DashboardGridLayoutEngine.Clamp(new DashboardGridRect(
                ColumnAt(x),
                RowAt(y),
                width,
                height));
        }

        public DashboardGridRect SnapResize(
            int originX,
            int originY,
            double pointerX,
            double pointerY)
        {
            return SnapResize(originX, originY, pointerX, pointerY, DashboardGridLayoutEngine.MinWidth, DashboardGridLayoutEngine.MinHeight);
        }

        public DashboardGridRect SnapResize(
            int originX,
            int originY,
            double pointerX,
            double pointerY,
            int minWidth,
            int minHeight)
        {
            var right = ColumnAt(pointerX) + 1;
            var bottom = RowAt(pointerY) + 1;
            var width = right - originX;
            var height = bottom - originY;
            return DashboardGridLayoutEngine.Clamp(new DashboardGridRect(originX, originY, width, height), minWidth, minHeight);
        }

        public double PixelX(int column)
        {
            return column * (ColumnWidth + Gutter);
        }

        public double PixelY(int row)
        {
            return row * (RowHeight + Gutter);
        }

        public double PixelWidth(int columns)
        {
            if (columns <= 0)
                return 0;
            return columns * ColumnWidth + (columns - 1) * Gutter;
        }

        public double PixelHeight(int rows)
        {
            if (rows <= 0)
                return 0;
            return rows * RowHeight + (rows - 1) * Gutter;
        }
    }
}
