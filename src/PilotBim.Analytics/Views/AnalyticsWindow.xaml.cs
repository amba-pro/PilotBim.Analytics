using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Export;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using PilotBim.Analytics.ViewModels;
using UiResources = PilotBim.Analytics.Properties.Resources;

namespace PilotBim.Analytics.Views
{
    public partial class AnalyticsWindow : Window
    {
        private readonly InventoryService _inventory;
        private readonly IProjectAnalyticsService _projectAnalytics;
        private readonly IAnalyticsCsvExporter _csvExporter;
        private readonly AnalyticsWindowViewModel _vm;
        private readonly ScanSessionScope _scanSession = new ScanSessionScope();
        private DashboardWidgetVm _layoutWidget;
        private DashboardGridRect _layoutOrigin;
        private DashboardGridRect _layoutPreview;
        private Point _layoutStart;
        private double _grabOffsetX;
        private double _grabOffsetY;
        private bool _layoutResize;
        private bool _layoutArmed;
        private bool _layoutActive;
        private bool _layoutCommitted;
        private UIElement _layoutCapture;
        private UIElement _layoutContainer;

        public AnalyticsWindow(
            InventoryService inventory,
            IProjectAnalyticsService projectAnalyticsService,
            IAnalyticsCsvExporter analyticsCsvExporter)
        {
            if (projectAnalyticsService == null)
                throw new ArgumentNullException(nameof(projectAnalyticsService));
            if (analyticsCsvExporter == null)
                throw new ArgumentNullException(nameof(analyticsCsvExporter));

            InitializeComponent();
            _inventory = inventory;
            _projectAnalytics = projectAnalyticsService;
            _csvExporter = analyticsCsvExporter;
            _vm = CreateViewModel();
            DataContext = _vm;
            ShowPanel("Summary");
        }

        private AnalyticsWindowViewModel CreateViewModel()
        {
            if (_inventory == null)
                return new AnalyticsWindowViewModel();

            try
            {
                var projectKey = _inventory.GetDatabaseId();
                if (projectKey == Guid.Empty)
                    return new AnalyticsWindowViewModel();

                var options = new AnalyticsDashboardRuntimeOptions
                {
                    ProjectKey = projectKey,
                    DefinitionStore = new DashboardDefinitionStore(),
                    LayoutStore = new DashboardLayoutStore(),
                    TypeDatasetProvider = _inventory.CreateTypeDatasetProvider(),
                    PostToUi = action =>
                    {
                        if (action == null)
                            return;
                        if (Dispatcher.CheckAccess())
                            action();
                        else
                            Dispatcher.BeginInvoke(action);
                    }
                };
                var vm = new AnalyticsWindowViewModel(options);
                try
                {
                    vm.ApplyTypeMetadata(_inventory.DiscoverTypes());
                }
                catch (Exception ex)
                {
                    AnalyticsLogger.Warning("Dashboard", "type metadata: " + ex.Message);
                }
                return vm;
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("Dashboard", "V2 runtime not attached: " + ex.Message);
                return new AnalyticsWindowViewModel();
            }
        }

        private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowPanel(_vm.SelectedNavKey);
        }

        private void ShowPanel(string key)
        {
            PanelSummary.Visibility = key == "Summary" ? Visibility.Visible : Visibility.Collapsed;
            PanelCharts.Visibility = key == "Charts" ? Visibility.Visible : Visibility.Collapsed;
            PanelChartBuilder.Visibility = key == "ChartBuilder" ? Visibility.Visible : Visibility.Collapsed;
            PanelTypes.Visibility = key == "Types" ? Visibility.Visible : Visibility.Collapsed;
            PanelCreators.Visibility = key == "Creators" ? Visibility.Visible : Visibility.Collapsed;
            PanelCreated.Visibility = key == "Created" ? Visibility.Visible : Visibility.Collapsed;
            PanelStates.Visibility = key == "States" ? Visibility.Visible : Visibility.Collapsed;
            PanelStateSemantic.Visibility = key == "StateSemantic" ? Visibility.Visible : Visibility.Collapsed;
            PanelResponsible.Visibility = key == "Responsible" ? Visibility.Visible : Visibility.Collapsed;
            PanelDocuments.Visibility = key == "Documents" ? Visibility.Visible : Visibility.Collapsed;
            PanelBim.Visibility = key == "Bim" ? Visibility.Visible : Visibility.Collapsed;
            PanelBimModels.Visibility = key == "BimModels" ? Visibility.Visible : Visibility.Collapsed;
            PanelBimParts.Visibility = key == "BimParts" ? Visibility.Visible : Visibility.Collapsed;
            PanelBimTypes.Visibility = key == "BimTypes" ? Visibility.Visible : Visibility.Collapsed;
            PanelQuality.Visibility = key == "Quality" ? Visibility.Visible : Visibility.Collapsed;
            PanelRemarks.Visibility = key == "Remarks" ? Visibility.Visible : Visibility.Collapsed;
            PanelRemarkLinks.Visibility = key == "RemarkLinks" ? Visibility.Visible : Visibility.Collapsed;
            PanelScanDiff.Visibility = key == "ScanDiff" ? Visibility.Visible : Visibility.Collapsed;

            BimFilterBar.Visibility = key == "BimParts" || key == "BimTypes" || key == "Charts"
                || key == "ChartBuilder" || key == "Summary"
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Snapshot == null)
            {
                MessageBox.Show(UiResources.Common_ScanRequiredFirst, "PilotBim.Analytics");
                return;
            }

            try
            {
                var folder = _csvExporter.Export(_vm.Snapshot);
                _vm.ProgressText = "CSV экспорт: " + folder;
                MessageBox.Show("Экспорт сохранён в:\n" + folder, "PilotBim.Analytics");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PilotBim.Analytics", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenExports_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PilotBim.Analytics",
                    "Exports");
                System.IO.Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PilotBim.Analytics", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.IsBusy)
                return;

            if (_vm.ScanMode == ScanMode.Full)
            {
                var confirm = MessageBox.Show(
                    "Полный режим может занять много времени. Продолжить?",
                    "Полный scan",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            var session = _scanSession.Begin();
            _vm.IsBusy = true;
            _vm.ProgressText = "Сканирование...";
            var mode = _vm.ScanMode;
            var token = session.Token;

            try
            {
                var report = await Task.Run(() =>
                {
                    return _inventory.Run(mode, token, msg =>
                    {
                        Dispatcher.BeginInvoke(new Action(() => _vm.ProgressText = msg));
                    });
                }, token);

                var snapshot = _projectAnalytics.Build(report);
                _vm.Snapshot = snapshot;
                try
                {
                    _vm.ApplyTypeMetadata(report.Types);
                }
                catch (Exception ex)
                {
                    AnalyticsLogger.Warning("Dashboard", "scan metadata: " + ex.Message);
                }
                _vm.ProgressText = report.Cancelled
                    ? "Отменено. Показаны частичные данные."
                    : "Готово. Объектов: " + report.ObjectsFound + ", статус: " + report.FinalStatus;
            }
            catch (Exception ex)
            {
                _vm.ProgressText = "Ошибка: " + ex.Message;
                MessageBox.Show(ex.Message, "PilotBim.Analytics", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _scanSession.Complete(session);
                _vm.IsBusy = false;
            }
        }
        
        private void DashboardVisible_Click(object sender, RoutedEventArgs e)
        {
            var box = sender as CheckBox;
            if (box == null || box.Tag == null || !_vm.DashboardMutationsEnabled)
                return;
            _vm.SetDashboardBlockVisible(box.Tag.ToString(), box.IsChecked == true);
        }

        private void DashboardEditMode_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.DashboardMutationsEnabled)
                return;
            _vm.SetDashboardEditMode(!_vm.IsDashboardEditMode);
        }

        private void DashboardCardHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BeginLayoutGesture(sender as FrameworkElement, e, resize: false);
        }

        private void DashboardResizeGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BeginLayoutGesture(sender as FrameworkElement, e, resize: true);
        }

        private void BeginLayoutGesture(FrameworkElement source, MouseButtonEventArgs e, bool resize)
        {
            if (source == null || !_vm.IsDashboardEditMode || !_vm.DashboardMutationsEnabled)
                return;
            var vm = source.DataContext as DashboardWidgetVm;
            if (vm == null)
                return;
            var panel = FindDashboardGridPanel();
            if (panel == null)
                return;
            var metrics = panel.LastMetrics ?? DashboardGridMetrics.FromAvailableWidth(panel.ActualWidth);
            _layoutWidget = vm;
            _layoutOrigin = new DashboardGridRect(vm.GridX, vm.GridY, vm.GridWidth, vm.GridHeight);
            _layoutPreview = _layoutOrigin;
            _layoutStart = e.GetPosition(panel);
            _grabOffsetX = _layoutStart.X - metrics.PixelX(vm.GridX);
            _grabOffsetY = _layoutStart.Y - metrics.PixelY(vm.GridY);
            _layoutResize = resize;
            _layoutArmed = true;
            _layoutActive = false;
            _layoutCommitted = false;
            _layoutCapture = source;
            _layoutContainer = FindAncestor<ContentPresenter>(source);
            source.CaptureMouse();
            e.Handled = true;
        }

        private void DashboardCard_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_layoutArmed || _layoutWidget == null || e.LeftButton != MouseButtonState.Pressed)
                return;
            var panel = FindDashboardGridPanel();
            if (panel == null)
                return;
            var pos = e.GetPosition(panel);
            if (!_layoutActive)
            {
                if (Math.Abs(pos.X - _layoutStart.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(pos.Y - _layoutStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                    return;
                _layoutActive = true;
                if (_layoutContainer != null)
                    Panel.SetZIndex(_layoutContainer, 100);
            }

            var metrics = panel.LastMetrics ?? DashboardGridMetrics.FromAvailableWidth(panel.ActualWidth);
            DashboardGridRect snapped;
            if (_layoutResize)
                snapped = metrics.SnapResize(_layoutOrigin.X, _layoutOrigin.Y, pos.X, pos.Y);
            else
                snapped = metrics.SnapRect(pos.X - _grabOffsetX, pos.Y - _grabOffsetY, _layoutOrigin.Width, _layoutOrigin.Height);

            if (snapped.Equals(_layoutPreview))
                return;
            _layoutPreview = snapped;
            _vm.PreviewDashboardWidgetRect(_layoutWidget.Id, snapped, _layoutResize);
        }

        private void DashboardCard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CompleteLayoutGesture(commit: true);
        }

        private void DashboardCard_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_layoutArmed && !_layoutCommitted)
                CompleteLayoutGesture(commit: false);
        }

        private void CompleteLayoutGesture(bool commit)
        {
            if (!_layoutArmed)
                return;
            var widget = _layoutWidget;
            var preview = _layoutPreview;
            var origin = _layoutOrigin;
            var resize = _layoutResize;
            var active = _layoutActive;
            var capture = _layoutCapture;
            var container = _layoutContainer;
            _layoutArmed = false;
            _layoutActive = false;
            _layoutWidget = null;
            _layoutCapture = null;
            _layoutContainer = null;
            _layoutCommitted = true;
            if (container != null)
                Panel.SetZIndex(container, 0);
            if (capture != null && capture.IsMouseCaptured)
                capture.ReleaseMouseCapture();

            if (!commit || !active || widget == null || preview.Equals(origin))
            {
                _vm.CancelDashboardLayoutPreview();
                return;
            }

            string error;
            if (!_vm.TryCommitDashboardWidgetRect(widget.Id, preview, resize, out error))
                _vm.CancelDashboardLayoutPreview();
        }

        private DashboardGridPanel FindDashboardGridPanel()
        {
            return FindVisualChild<DashboardGridPanel>(DashboardWidgetsHost);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var match = child as T ?? FindVisualChild<T>(child);
                if (match != null)
                    return match;
            }
            return null;
        }

        private static T FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            var current = start;
            while (current != null)
            {
                var match = current as T;
                if (match != null)
                    return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void DashboardAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.DashboardMutationsEnabled)
                return;
            var dlg = new DashboardWidgetEditorWindow(
                _vm.ChartSourceOptions,
                _vm.ChartKindOptions,
                _vm.ChartTopNOptions,
                null,
                chartOnly: false)
            {
                Owner = this
            };
            if (dlg.ShowDialog() == true && dlg.Result != null)
                _vm.AddDashboardWidget(dlg.Result);
        }

        private void DashboardAddQuery_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.DashboardMutationsEnabled)
                return;
            OpenQueryEditor(null);
        }

        private void DashboardEdit_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || btn.Tag == null || !_vm.DashboardMutationsEnabled)
                return;
            var id = btn.Tag.ToString();
            if (_vm.IsQueryWidget(id))
            {
                OpenQueryEditor(_vm.GetWidgetDefinition(id));
                return;
            }
            var existing = _vm.GetWidgetState(id);
            if (existing == null)
                return;
            var dlg = new DashboardWidgetEditorWindow(
                _vm.ChartSourceOptions,
                _vm.ChartKindOptions,
                _vm.ChartTopNOptions,
                existing,
                chartOnly: true)
            {
                Owner = this
            };
            if (dlg.ShowDialog() == true && dlg.Result != null)
                _vm.UpdateDashboardWidget(id, dlg.Result);
        }

        private void OpenQueryEditor(DashboardWidgetDefinition existing)
        {
            var editorVm = _vm.CreateQueryEditor(existing);
            var dlg = new DashboardQueryWidgetEditorWindow(editorVm, existing != null)
            {
                Owner = this
            };
            if (dlg.ShowDialog() != true || dlg.Result == null)
                return;
            string error;
            if (!_vm.TrySaveQueryWidget(dlg.Result, out error))
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(error) ? UiResources.Dashboard_SaveFailed : error,
                    "PilotBim.Analytics",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void DashboardRemove_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || btn.Tag == null || !_vm.DashboardMutationsEnabled)
                return;
            var confirm = MessageBox.Show(
                "Удалить виджет с дашборда?",
                "PilotBim.Analytics",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;
            _vm.RemoveDashboardWidget(btn.Tag.ToString());
        }

        private void ChartBuilderToDashboard_Click(object sender, RoutedEventArgs e)
        {
            var title = _vm.AddChartBuilderToDashboard();
            _vm.ProgressText = "Виджет добавлен на Сводку: " + title;
            MessageBox.Show(
                "График добавлен на дашборд (Сводка):\n" + title,
                "PilotBim.Analytics");
        }

        private void ScanCompare_Click(object sender, RoutedEventArgs e)
        {
            _vm.CompareWithSelectedHistory();
        }

        private void ScanSaveNamed_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SimplePromptWindow(
                "Сохранить снимок",
                "Имя снимка скана:",
                "Снимок " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
            {
                Owner = this
            };
            if (dlg.ShowDialog() != true)
                return;
            var name = (dlg.ResultText ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(UiResources.Snapshot_EnterName, "PilotBim.Analytics");
                return;
            }
            _vm.SaveNamedScan(name);
        }

        private void ScanDeleteHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedScanHistory == null)
            {
                MessageBox.Show("Выберите снимок в списке.", "PilotBim.Analytics");
                return;
            }
            var confirm = MessageBox.Show(
                "Удалить снимок «" + _vm.SelectedScanHistory.Name + "» из истории?",
                "PilotBim.Analytics",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;
            _vm.DeleteSelectedHistory();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _scanSession.Cancel();
            _vm.ProgressText = "Отмена запрошена...";
        }

        protected override void OnClosed(EventArgs e)
        {
            _vm.DisposeDashboard();
            _scanSession.Dispose();
            base.OnClosed(e);
        }
    }
}
