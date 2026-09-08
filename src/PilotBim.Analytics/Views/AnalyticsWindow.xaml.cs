using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PilotBim.Analytics.Export;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using PilotBim.Analytics.ViewModels;

namespace PilotBim.Analytics.Views
{
    public partial class AnalyticsWindow : Window
    {
        private readonly InventoryService _inventory;
        private readonly IProjectAnalyticsService _projectAnalytics;
        private readonly IAnalyticsCsvExporter _csvExporter;
        private readonly AnalyticsWindowViewModel _vm;
        private CancellationTokenSource _cts;

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
            _vm = new AnalyticsWindowViewModel();
            DataContext = _vm;
            ShowPanel("Summary");
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
                MessageBox.Show("Сначала выполните сканирование.", "PilotBim.Analytics");
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

            _cts = new CancellationTokenSource();
            _vm.IsBusy = true;
            _vm.ProgressText = "Сканирование...";
            var mode = _vm.ScanMode;
            var token = _cts.Token;

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
                _vm.IsBusy = false;
            }
        }

        
        private void DashboardVisible_Click(object sender, RoutedEventArgs e)
        {
            var box = sender as CheckBox;
            if (box == null || box.Tag == null)
                return;
            _vm.SetDashboardBlockVisible(box.Tag.ToString(), box.IsChecked == true);
        }

        private void DashboardMoveUp_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || btn.Tag == null)
                return;
            _vm.MoveDashboardBlock(btn.Tag.ToString(), -1);
        }

        private void DashboardMoveDown_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || btn.Tag == null)
                return;
            _vm.MoveDashboardBlock(btn.Tag.ToString(), 1);
        }

        private void DashboardAdd_Click(object sender, RoutedEventArgs e)
        {
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

        private void DashboardEdit_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || btn.Tag == null)
                return;
            var id = btn.Tag.ToString();
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

        private void DashboardRemove_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || btn.Tag == null)
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
                MessageBox.Show("Укажите имя снимка.", "PilotBim.Analytics");
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
            if (_cts != null)
                _cts.Cancel();
            _vm.ProgressText = "Отмена запрошена...";
        }
    }
}
