using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PilotBim.Analytics.Models;

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

        private void Redraw()
        {
            if (DrawSurface == null)
                return;

            DrawSurface.Children.Clear();
            var points = Points;
            if (points == null || points.Count == 0)
            {
                DrawEmpty();
                return;
            }

            var w = Math.Max(DrawSurface.ActualWidth, 40);
            var h = Math.Max(DrawSurface.ActualHeight, 40);

            switch (ChartKind)
            {
                case AnalyticsChartKind.Pie:
                    DrawPie(points, w, h);
                    break;
                case AnalyticsChartKind.Line:
                    DrawLine(points, w, h);
                    break;
                case AnalyticsChartKind.VerticalBar:
                    DrawColumns(points, w, h);
                    break;
                default:
                    DrawHorizontal(points, w, h);
                    break;
            }
        }

        private void DrawEmpty()
        {
            var tb = new TextBlock
            {
                Text = "Нет данных — выполните «Обновить» или выберите другой источник",
                Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                FontSize = 13,
                Margin = new Thickness(12)
            };
            DrawSurface.Children.Add(tb);
        }

        private void DrawHorizontal(IList<ChartSeriesPoint> points, double w, double h)
        {
            double labelW = 160;
            double valueW = 64;
            double rowH = Math.Max(22, Math.Min(36, (h - 16) / Math.Max(points.Count, 1)));
            double barMax = Math.Max(40, w - labelW - valueW - 36);
            double y = 8;

            foreach (var p in points)
            {
                var label = new TextBlock
                {
                    Text = p.Label ?? "?",
                    Width = labelW,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = p.Label,
                    Foreground = BrushesDark(),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Canvas.SetLeft(label, 8);
                Canvas.SetTop(label, y + 2);
                DrawSurface.Children.Add(label);

                var track = new Rectangle
                {
                    Width = barMax,
                    Height = rowH - 8,
                    Fill = new SolidColorBrush(Color.FromRgb(0xE8, 0xEC, 0xE9)),
                    RadiusX = 2,
                    RadiusY = 2
                };
                Canvas.SetLeft(track, 8 + labelW + 8);
                Canvas.SetTop(track, y + 4);
                DrawSurface.Children.Add(track);

                var fillW = Math.Max(2, p.BarWidth > 0 ? p.BarWidth / 420.0 * barMax : 2);
                var bar = new Rectangle
                {
                    Width = fillW,
                    Height = rowH - 8,
                    Fill = BrushFromHex(p.ColorHex),
                    RadiusX = 2,
                    RadiusY = 2
                };
                Canvas.SetLeft(bar, 8 + labelW + 8);
                Canvas.SetTop(bar, y + 4);
                DrawSurface.Children.Add(bar);

                var val = new TextBlock
                {
                    Text = p.ValueDisplay ?? p.Value.ToString("0", CultureInfo.InvariantCulture),
                    Foreground = BrushesDark(),
                    FontSize = 12,
                    Width = valueW,
                    TextAlignment = TextAlignment.Right
                };
                Canvas.SetLeft(val, w - valueW - 8);
                Canvas.SetTop(val, y + 2);
                DrawSurface.Children.Add(val);

                y += rowH;
            }
        }

        private void DrawColumns(IList<ChartSeriesPoint> points, double w, double h)
        {
            double padL = 28, padR = 12, padT = 16, padB = 48;
            double plotW = Math.Max(40, w - padL - padR);
            double plotH = Math.Max(40, h - padT - padB);
            double gap = 6;
            double colW = Math.Max(8, (plotW - gap * (points.Count + 1)) / points.Count);

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

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                double colH = Math.Max(2, p.ColumnHeight > 0 ? p.ColumnHeight / 220.0 * plotH : 2);
                double x = padL + gap + i * (colW + gap);
                double y = padT + plotH - colH;

                var rect = new Rectangle
                {
                    Width = colW,
                    Height = colH,
                    Fill = BrushFromHex(p.ColorHex),
                    RadiusX = 2,
                    RadiusY = 2,
                    ToolTip = (p.Label ?? "?") + ": " + (p.ValueDisplay ?? "")
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                DrawSurface.Children.Add(rect);

                var lbl = new TextBlock
                {
                    Text = Truncate(p.Label, 10),
                    FontSize = 10,
                    Foreground = BrushesDark(),
                    Width = colW + 8,
                    TextAlignment = TextAlignment.Center,
                    ToolTip = p.Label
                };
                Canvas.SetLeft(lbl, x - 4);
                Canvas.SetTop(lbl, padT + plotH + 4);
                DrawSurface.Children.Add(lbl);
            }
        }

        private void DrawLine(IList<ChartSeriesPoint> points, double w, double h)
        {
            double padL = 36, padR = 16, padT = 16, padB = 40;
            double plotW = Math.Max(40, w - padL - padR);
            double plotH = Math.Max(40, h - padT - padB);

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

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                double x = padL + (points.Count == 1 ? plotW / 2 : i * (plotW / Math.Max(points.Count - 1, 1)));
                double yNorm = p.LineY > 0 ? p.LineY / 220.0 : 0;
                double y = padT + plotH - Math.Max(0, Math.Min(1, yNorm)) * plotH;
                poly.Points.Add(new Point(x, y));

                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = BrushFromHex(p.ColorHex),
                    ToolTip = (p.Label ?? "?") + ": " + (p.ValueDisplay ?? "")
                };
                Canvas.SetLeft(dot, x - 4);
                Canvas.SetTop(dot, y - 4);
                DrawSurface.Children.Add(dot);

                var lbl = new TextBlock
                {
                    Text = Truncate(p.Label, 8),
                    FontSize = 10,
                    Foreground = BrushesDark(),
                    ToolTip = p.Label
                };
                Canvas.SetLeft(lbl, x - 20);
                Canvas.SetTop(lbl, padT + plotH + 6);
                DrawSurface.Children.Add(lbl);
            }

            DrawSurface.Children.Insert(1, poly);
        }

        private void DrawPie(IList<ChartSeriesPoint> points, double w, double h)
        {
            double size = Math.Min(w, h) - 24;
            if (size < 80)
                size = 80;
            double cx = w / 2;
            double cy = (h - 40) / 2 + 8;
            double r = size / 2;

            foreach (var p in points)
            {
                if (p.PieSweepDegrees <= 0.05)
                    continue;

                var path = new Path
                {
                    Fill = BrushFromHex(p.ColorHex),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    ToolTip = (p.Label ?? "?") + ": " + (p.ValueDisplay ?? "")
                        + " (" + p.SharePercent.ToString("0.0") + "%)",
                    Data = BuildPieSlice(cx, cy, r, p.PieStartDegrees, p.PieSweepDegrees)
                };
                DrawSurface.Children.Add(path);
            }

            // Legend under pie
            double lx = 12;
            double ly = Math.Min(h - 28, cy + r + 12);
            int shown = 0;
            foreach (var p in points)
            {
                if (shown >= 8)
                    break;
                var swatch = new Rectangle
                {
                    Width = 10,
                    Height = 10,
                    Fill = BrushFromHex(p.ColorHex)
                };
                Canvas.SetLeft(swatch, lx);
                Canvas.SetTop(swatch, ly);
                DrawSurface.Children.Add(swatch);

                var tb = new TextBlock
                {
                    Text = Truncate(p.Label, 18) + " " + (p.ValueDisplay ?? ""),
                    FontSize = 11,
                    Foreground = BrushesDark()
                };
                Canvas.SetLeft(tb, lx + 14);
                Canvas.SetTop(tb, ly - 2);
                DrawSurface.Children.Add(tb);
                lx += 160;
                if (lx > w - 140)
                {
                    lx = 12;
                    ly += 16;
                }
                shown++;
            }
        }

        private static Geometry BuildPieSlice(double cx, double cy, double r, double startDeg, double sweepDeg)
        {
            if (sweepDeg >= 359.9)
                return new EllipseGeometry(new Point(cx, cy), r, r);

            double startRad = startDeg * Math.PI / 180.0;
            double endRad = (startDeg + sweepDeg) * Math.PI / 180.0;
            var start = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
            var end = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));
            bool large = sweepDeg > 180;

            var fig = new PathFigure { StartPoint = new Point(cx, cy), IsClosed = true };
            fig.Segments.Add(new LineSegment(start, true));
            fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, large, SweepDirection.Clockwise, true));
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            return geo;
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

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
                return text ?? "";
            return text.Substring(0, max - 1) + "…";
        }
    }
}
