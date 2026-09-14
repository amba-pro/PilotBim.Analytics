using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PilotBim.Analytics.Services;
using PilotBim.Analytics.ViewModels;

namespace PilotBim.Analytics.Views
{
    /// <summary>
    /// Maps logical X/Y/W/H on <see cref="DashboardWidgetVm"/> to child rectangles.
    /// No query, persistence, or Pilot SDK logic.
    /// </summary>
    internal sealed class DashboardGridPanel : Panel
    {
        public static readonly DependencyProperty ShowGuidesProperty = DependencyProperty.Register(
            "ShowGuides",
            typeof(bool),
            typeof(DashboardGridPanel),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GridXProperty = DependencyProperty.RegisterAttached(
            "GridX",
            typeof(int),
            typeof(DashboardGridPanel),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));
        public static readonly DependencyProperty GridYProperty = DependencyProperty.RegisterAttached(
            "GridY",
            typeof(int),
            typeof(DashboardGridPanel),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));
        public static readonly DependencyProperty GridWidthProperty = DependencyProperty.RegisterAttached(
            "GridWidth",
            typeof(int),
            typeof(DashboardGridPanel),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));
        public static readonly DependencyProperty GridHeightProperty = DependencyProperty.RegisterAttached(
            "GridHeight",
            typeof(int),
            typeof(DashboardGridPanel),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

        public static int GetGridX(DependencyObject obj)
        {
            return obj == null ? 0 : (int)obj.GetValue(GridXProperty);
        }

        public static void SetGridX(DependencyObject obj, int value)
        {
            if (obj != null)
                obj.SetValue(GridXProperty, value);
        }

        public static int GetGridY(DependencyObject obj)
        {
            return obj == null ? 0 : (int)obj.GetValue(GridYProperty);
        }

        public static void SetGridY(DependencyObject obj, int value)
        {
            if (obj != null)
                obj.SetValue(GridYProperty, value);
        }

        public static int GetGridWidth(DependencyObject obj)
        {
            return obj == null ? 0 : (int)obj.GetValue(GridWidthProperty);
        }

        public static void SetGridWidth(DependencyObject obj, int value)
        {
            if (obj != null)
                obj.SetValue(GridWidthProperty, value);
        }

        public static int GetGridHeight(DependencyObject obj)
        {
            return obj == null ? 0 : (int)obj.GetValue(GridHeightProperty);
        }

        public static void SetGridHeight(DependencyObject obj, int value)
        {
            if (obj != null)
                obj.SetValue(GridHeightProperty, value);
        }

        public bool ShowGuides
        {
            get { return (bool)GetValue(ShowGuidesProperty); }
            set { SetValue(ShowGuidesProperty, value); }
        }

        internal DashboardGridMetrics LastMetrics { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var metrics = DashboardGridMetrics.FromAvailableWidth(availableSize.Width);
            LastMetrics = metrics;
            var bottom = 0.0;
            foreach (var child in InternalChildren)
            {
                var element = child as UIElement;
                if (element == null)
                    continue;
                var rect = PixelRect(metrics, element);
                element.Measure(new Size(Math.Max(0, rect.Width), Math.Max(0, rect.Height)));
                bottom = Math.Max(bottom, rect.Y + rect.Height);
            }

            var width = double.IsInfinity(availableSize.Width) ? metrics.AvailableWidth : Math.Max(availableSize.Width, metrics.AvailableWidth);
            return new Size(width, bottom);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var metrics = DashboardGridMetrics.FromAvailableWidth(finalSize.Width);
            LastMetrics = metrics;
            var bottom = 0.0;
            foreach (var child in InternalChildren)
            {
                var element = child as UIElement;
                if (element == null)
                    continue;
                var rect = PixelRect(metrics, element);
                element.Arrange(new Rect(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height)));
                bottom = Math.Max(bottom, rect.Y + rect.Height);
            }

            return new Size(Math.Max(finalSize.Width, metrics.AvailableWidth), Math.Max(finalSize.Height, bottom));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (!ShowGuides || LastMetrics == null)
                return;
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(40, 31, 42, 36)), 1);
            pen.Freeze();
            for (var c = 0; c <= DashboardGridLayoutEngine.Columns; c++)
            {
                var x = LastMetrics.PixelX(c);
                dc.DrawLine(pen, new Point(x, 0), new Point(x, RenderSize.Height));
            }
        }

        internal static DashboardWidgetVm WidgetFrom(DependencyObject child)
        {
            var element = child as FrameworkElement;
            if (element == null)
                return null;
            var vm = element.DataContext as DashboardWidgetVm;
            if (vm != null)
                return vm;
            var presenter = child as ContentPresenter;
            if (presenter != null)
                return presenter.Content as DashboardWidgetVm ?? presenter.DataContext as DashboardWidgetVm;
            return null;
        }

        private static Rect PixelRect(DashboardGridMetrics metrics, UIElement child)
        {
            var x = GetGridX(child);
            var y = GetGridY(child);
            var width = GetGridWidth(child);
            var height = GetGridHeight(child);
            var vm = WidgetFrom(child);
            if (width < 1 || height < 1)
            {
                if (vm == null)
                    return new Rect(0, 0, metrics.PixelWidth(DashboardGridLayoutEngine.DefaultWidth), metrics.PixelHeight(DashboardGridLayoutEngine.DefaultHeight));
                x = vm.GridX;
                y = vm.GridY;
                width = vm.GridWidth;
                height = vm.GridHeight;
            }
            return new Rect(
                metrics.PixelX(x),
                metrics.PixelY(y),
                metrics.PixelWidth(width),
                metrics.PixelHeight(height));
        }
    }
}
