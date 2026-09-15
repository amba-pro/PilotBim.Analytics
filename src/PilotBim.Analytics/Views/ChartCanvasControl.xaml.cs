using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using UiResources = PilotBim.Analytics.Properties.Resources;

namespace PilotBim.Analytics.Views
{
    public partial class ChartCanvasControl : UserControl
    {
        public static readonly DependencyProperty ChartKindProperty =
            DependencyProperty.Register(
                "ChartKind",
                typeof(AnalyticsChartKind),
                typeof(ChartCanvasControl),
                new PropertyMetadata(AnalyticsChartKind.HorizontalBar, OnChartChanged));

        public static readonly DependencyProperty PointsProperty =
            DependencyProperty.Register(
                "Points",
                typeof(IList<ChartSeriesPoint>),
                typeof(ChartCanvasControl),
                new PropertyMetadata(null, OnChartChanged));

        public ChartCanvasControl()
        {
            InitializeComponent();
            SizeChanged += (s, e) => Redraw();
            Loaded += (s, e) => Redraw();
        }

        public AnalyticsChartKind ChartKind
        {
            get { return (AnalyticsChartKind)GetValue(ChartKindProperty); }
            set { SetValue(ChartKindProperty, value); }
        }

        public IList<ChartSeriesPoint> Points
        {
            get { return (IList<ChartSeriesPoint>)GetValue(PointsProperty); }
            set { SetValue(PointsProperty, value); }
        }

        private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = d as ChartCanvasControl;
            if (ctrl != null)
                ctrl.Redraw();
        }

        private void ChartScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        private void Redraw()
        {
            if (DrawSurface == null || ChartScroll == null)
                return;

            DrawSurface.Children.Clear();
            var points = Points;
            var viewportW = ViewportWidth();
            var viewportH = ViewportHeight();
            DrawSurface.Width = viewportW;

            if (points == null || points.Count == 0)
            {
                DrawSurface.Height = viewportH;
                DrawMessage(UiResources.QueryViz_NoChartData);
                return;
            }

            switch (ChartKind)
            {
                case AnalyticsChartKind.Pie:
                    DrawSurface.Height = viewportH;
                    DrawPie(points, viewportW, viewportH);
                    break;
                case AnalyticsChartKind.Line:
                    DrawSurface.Height = viewportH;
                    DrawLine(points, viewportW, viewportH);
                    break;
                case AnalyticsChartKind.VerticalBar:
                    DrawSurface.Height = viewportH;
                    DrawColumns(points, viewportW, viewportH);
                    break;
                default:
                    DrawHorizontal(points, viewportW, viewportH);
                    break;
            }
        }

        private double ViewportWidth()
        {
            var w = ChartScroll.ViewportWidth;
            if (w <= 1)
                w = ActualWidth;
            if (double.IsNaN(w) || w < 40)
                w = 40;
            return w;
        }

        private double ViewportHeight()
        {
            var h = ChartScroll.ViewportHeight;
            if (h <= 1)
                h = ActualHeight;
            if (double.IsNaN(h) || h < 40)
                h = 40;
            return h;
        }

        private void DrawMessage(string text)
        {
            var tb = new TextBlock
            {
                Text = text ?? string.Empty,
                Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Width = Math.Max(40, DrawSurface.Width - 24),
                Margin = new Thickness(12)
            };
            DrawSurface.Children.Add(tb);
        }

        private void DrawHorizontal(IList<ChartSeriesPoint> points, double w, double h)
        {
            var labelW = Math.Max(80, Math.Min(220, w * 0.32));
            var valueW = 72;
            var rowH = 28.0;
            var contentH = Math.Max(h, 16 + rowH * points.Count);
            DrawSurface.Height = contentH;
            var barMax = Math.Max(24, w - labelW - valueW - 36);
            var y = 8.0;
            var max = MaxCount(points);

            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var display = DashboardVisualizationFormat.DisplayLabel(p.Label);
                var tooltip = display + ": " + (p.ValueDisplay ?? string.Empty);

                var label = new TextBlock
                {
                    Text = display,
                    Width = labelW,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = tooltip,
                    Foreground = BrushesDark(),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Canvas.SetLeft(label, 8);
                Canvas.SetTop(label, y + 4);
                DrawSurface.Children.Add(label);

                var track = new Rectangle
                {
                    Width = barMax,
                    Height = rowH - 10,
                    Fill = new SolidColorBrush(Color.FromRgb(0xE8, 0xEC, 0xE9)),
                    RadiusX = 2,
                    RadiusY = 2
                };
                Canvas.SetLeft(track, 8 + labelW + 8);
                Canvas.SetTop(track, y + 5);
                DrawSurface.Children.Add(track);

                var geo = DashboardChartGeometry.HorizontalBar(
                    i,
                    CountOf(p),
                    max,
                    8 + labelW + 8,
                    barMax,
                    y + 5,
                    rowH - 10);
                var fillW = geo.IsFiniteNonNegative ? geo.Width : 0;
                if (fillW > 0)
                {
                    var bar = new Rectangle
                    {
                        Width = fillW,
                        Height = rowH - 10,
                        Fill = BrushFromHex(p.ColorHex),
                        RadiusX = 2,
                        RadiusY = 2,
                        ToolTip = tooltip
                    };
                    Canvas.SetLeft(bar, 8 + labelW + 8);
                    Canvas.SetTop(bar, y + 5);
                    DrawSurface.Children.Add(bar);
                }

                var val = new TextBlock
                {
                    Text = p.ValueDisplay ?? string.Empty,
                    Foreground = BrushesDark(),
                    FontSize = 12,
                    Width = valueW,
                    TextAlignment = TextAlignment.Right,
                    ToolTip = tooltip
                };
                Canvas.SetLeft(val, Math.Max(8, w - valueW - 8));
                Canvas.SetTop(val, y + 4);
                DrawSurface.Children.Add(val);

                y += rowH;
            }
        }

        private void DrawColumns(IList<ChartSeriesPoint> points, double w, double h)
        {
            double padL = 20, padR = 12, padT = 12, padB = 44;
            var plotW = Math.Max(24, w - padL - padR);
            var plotH = Math.Max(24, h - padT - padB);

            var axis = new Line
            {
                X1 = padL,
                Y1 = padT + plotH,
                X2 = padL + plotW,
                Y2 = padT + plotH,
                Stroke = new SolidColorBrush(Color.FromRgb(0xC8, 0xCE, 0xCB)),
                StrokeThickness = 1
            };
            DrawSurface.Children.Add(axis);

            var max = MaxCount(points);
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var geo = DashboardChartGeometry.VerticalBar(
                    i,
                    points.Count,
                    CountOf(p),
                    max,
                    padL,
                    padT,
                    plotW,
                    plotH);
                var display = DashboardVisualizationFormat.DisplayLabel(p.Label);
                var tooltip = display + ": " + (p.ValueDisplay ?? string.Empty);

                if (geo.Height > 0 && geo.IsFiniteNonNegative)
                {
                    var rect = new Rectangle
                    {
                        Width = geo.Width,
                        Height = geo.Height,
                        Fill = BrushFromHex(p.ColorHex),
                        RadiusX = 2,
                        RadiusY = 2,
                        ToolTip = tooltip
                    };
                    Canvas.SetLeft(rect, geo.X);
                    Canvas.SetTop(rect, geo.Y);
                    DrawSurface.Children.Add(rect);
                }

                var lbl = new TextBlock
                {
                    Text = display,
                    FontSize = 10,
                    Foreground = BrushesDark(),
                    Width = geo.Width + 8,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = tooltip
                };
                Canvas.SetLeft(lbl, geo.X - 4);
                Canvas.SetTop(lbl, padT + plotH + 4);
                DrawSurface.Children.Add(lbl);
            }
        }

        private void DrawLine(IList<ChartSeriesPoint> points, double w, double h)
        {
            double padL = 36, padR = 16, padT = 16, padB = 40;
            var plotW = Math.Max(40, w - padL - padR);
            var plotH = Math.Max(40, h - padT - padB);

            var axis = new Line
            {
                X1 = padL,
                Y1 = padT + plotH,
                X2 = padL + plotW,
                Y2 = padT + plotH,
                Stroke = new SolidColorBrush(Color.FromRgb(0xC8, 0xCE, 0xCB)),
                StrokeThickness = 1
            };
            DrawSurface.Children.Add(axis);

            var poly = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x3A, 0x7D, 0x5C)),
                StrokeThickness = 2.5
            };

            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var x = padL + (points.Count == 1 ? plotW / 2 : i * (plotW / Math.Max(points.Count - 1, 1)));
                var ratio = RatioOf(p);
                var y = padT + plotH - Math.Max(0, Math.Min(1, ratio)) * plotH;
                poly.Points.Add(new Point(x, y));
                var display = DashboardVisualizationFormat.DisplayLabel(p.Label);
                var tooltip = display + ": " + (p.ValueDisplay ?? string.Empty);

                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = BrushFromHex(p.ColorHex),
                    ToolTip = tooltip
                };
                Canvas.SetLeft(dot, x - 4);
                Canvas.SetTop(dot, y - 4);
                DrawSurface.Children.Add(dot);

                var lbl = new TextBlock
                {
                    Text = display,
                    FontSize = 10,
                    Foreground = BrushesDark(),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Width = 72,
                    TextAlignment = TextAlignment.Center,
                    ToolTip = tooltip
                };
                Canvas.SetLeft(lbl, x - 36);
                Canvas.SetTop(lbl, padT + plotH + 6);
                DrawSurface.Children.Add(lbl);
            }

            DrawSurface.Children.Insert(1, poly);
        }

        private void DrawPie(IList<ChartSeriesPoint> points, double w, double h)
        {
            var total = 0.0;
            for (var i = 0; i < points.Count; i++)
            {
                if (points[i].Value > 0)
                    total += points[i].Value;
            }
            if (total <= 0)
            {
                DrawMessage(UiResources.QueryViz_AllZero);
                return;
            }

            var legendW = w >= 360 ? Math.Min(200, w * 0.38) : 0;
            var box = Math.Min(w - legendW - 24, h - 24);
            if (box < 32)
                box = Math.Max(24, Math.Min(w, h) - 16);
            var cx = legendW > 0 ? 12 + box / 2 : w / 2;
            var cy = h / 2;
            var r = box / 2;

            foreach (var p in points)
            {
                if (p.PieSweepDegrees <= 0.05)
                    continue;
                var display = DashboardVisualizationFormat.DisplayLabel(p.Label);
                var tooltip = display + ": " + (p.ValueDisplay ?? string.Empty)
                    + " (" + p.SharePercent.ToString("0.0") + "%)";
                var path = new Path
                {
                    Fill = BrushFromHex(p.ColorHex),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    ToolTip = tooltip,
                    Data = BuildPieSlice(cx, cy, r, p.PieStartDegrees, p.PieSweepDegrees)
                };
                DrawSurface.Children.Add(path);
            }

            if (legendW > 0)
                DrawLegendColumn(points, w - legendW + 4, 8, legendW - 12, h - 16);
            else
                DrawLegendWrap(points, 8, Math.Min(h - 8, cy + r + 8), w - 16);
        }

        private void DrawLegendColumn(IList<ChartSeriesPoint> points, double x, double y, double width, double height)
        {
            var rowH = 18.0;
            var needed = 8 + points.Count * rowH;
            if (needed > DrawSurface.Height)
                DrawSurface.Height = needed;
            foreach (var p in points)
            {
                DrawLegendItem(p, x, y, width);
                y += rowH;
            }
        }

        private void DrawLegendWrap(IList<ChartSeriesPoint> points, double x0, double y, double maxW)
        {
            var x = x0;
            var rowH = 18.0;
            foreach (var p in points)
            {
                if (x > x0 && x + 150 > x0 + maxW)
                {
                    x = x0;
                    y += rowH;
                }
                DrawLegendItem(p, x, y, 150);
                x += 160;
            }
            if (y + rowH > DrawSurface.Height)
                DrawSurface.Height = y + rowH + 8;
        }

        private void DrawLegendItem(ChartSeriesPoint p, double x, double y, double width)
        {
            var display = DashboardVisualizationFormat.DisplayLabel(p.Label);
            var tooltip = display + ": " + (p.ValueDisplay ?? string.Empty);
            var swatch = new Rectangle
            {
                Width = 10,
                Height = 10,
                Fill = BrushFromHex(p.ColorHex),
                ToolTip = tooltip
            };
            Canvas.SetLeft(swatch, x);
            Canvas.SetTop(swatch, y);
            DrawSurface.Children.Add(swatch);

            var tb = new TextBlock
            {
                Text = display + " " + (p.ValueDisplay ?? string.Empty),
                FontSize = 11,
                Foreground = BrushesDark(),
                Width = Math.Max(40, width - 16),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = tooltip
            };
            Canvas.SetLeft(tb, x + 14);
            Canvas.SetTop(tb, y - 3);
            DrawSurface.Children.Add(tb);
        }

        private static Geometry BuildPieSlice(double cx, double cy, double r, double startDeg, double sweepDeg)
        {
            if (r <= 0)
                return Geometry.Empty;
            if (sweepDeg >= 359.9)
                return new EllipseGeometry(new Point(cx, cy), r, r);

            var startRad = startDeg * Math.PI / 180.0;
            var endRad = (startDeg + sweepDeg) * Math.PI / 180.0;
            var start = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
            var end = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));
            var large = sweepDeg > 180;

            var fig = new PathFigure { StartPoint = new Point(cx, cy), IsClosed = true };
            fig.Segments.Add(new LineSegment(start, true));
            fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, large, SweepDirection.Clockwise, true));
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            return geo;
        }

        private static long MaxCount(IList<ChartSeriesPoint> points)
        {
            long max = 0;
            if (points == null)
                return 0;
            for (var i = 0; i < points.Count; i++)
            {
                var value = CountOf(points[i]);
                if (value > max)
                    max = value;
            }
            return max;
        }

        private static long CountOf(ChartSeriesPoint point)
        {
            if (point == null)
                return 0;
            var value = point.Value;
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                return 0;
            if (value >= long.MaxValue)
                return long.MaxValue;
            return (long)value;
        }

        private static double RatioOf(ChartSeriesPoint point)
        {
            var ratio = point.ValueRatio;
            // Legacy ChartDataService points may only have pixel BarWidth/ColumnHeight.
            // Dashboard Query charts set ValueRatio. Charts-tab list bars still use 420px Width.
            if (ratio <= 0 && point.BarWidth > 0)
                ratio = point.BarWidth / 420.0;
            else if (ratio <= 0 && point.ColumnHeight > 0)
                ratio = point.ColumnHeight / 220.0;
            else if (ratio <= 0 && point.LineY > 0)
                ratio = point.LineY / 220.0;
            if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio < 0)
                return 0;
            if (ratio > 1)
                return 1;
            return ratio;
        }

        private static Brush BrushesDark()
        {
            return new SolidColorBrush(Color.FromRgb(0x1F, 0x2A, 0x24));
        }

        private static Brush BrushFromHex(string hex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex))
                    return new SolidColorBrush(Color.FromRgb(0x3A, 0x7D, 0x5C));
                return (Brush)new BrushConverter().ConvertFromString(hex);
            }
            catch
            {
                return new SolidColorBrush(Color.FromRgb(0x3A, 0x7D, 0x5C));
            }
        }
    }
}
