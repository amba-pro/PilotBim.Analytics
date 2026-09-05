using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Export;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using PilotBim.Analytics.ViewModels;
using PilotDataObject = Ascon.Pilot.SDK.IDataObject;

namespace PilotBim.Analytics.Views
{
    public partial class InventoryWindow : Window
    {
        private readonly InventoryService _inventory;
        private readonly InventoryWindowViewModel _vm;
        private CancellationTokenSource _cts;
        private string _lastReportText;

        public InventoryWindow(InventoryService inventory)
        {
            InitializeComponent();
            _inventory = inventory;
            _vm = new InventoryWindowViewModel();
            DataContext = _vm;
            ShowPanel("Overview");
        }

        private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowPanel(_vm.SelectedNavKey);
        }

        private void ShowPanel(string key)
        {
            PanelOverview.Visibility = key == "Overview" ? Visibility.Visible : Visibility.Collapsed;
            PanelTypes.Visibility = key == "Types" ? Visibility.Visible : Visibility.Collapsed;
            PanelAttributes.Visibility = key == "Attributes" ? Visibility.Visible : Visibility.Collapsed;
            PanelSystemFields.Visibility = key == "SystemFields" ? Visibility.Visible : Visibility.Collapsed;
            PanelStates.Visibility = key == "States" ? Visibility.Visible : Visibility.Collapsed;
            PanelPersons.Visibility = key == "Persons" ? Visibility.Visible : Visibility.Collapsed;
            PanelOrganisations.Visibility = key == "Organisations" ? Visibility.Visible : Visibility.Collapsed;
            PanelStructure.Visibility = key == "Structure" ? Visibility.Visible : Visibility.Collapsed;
            PanelDocuments.Visibility = key == "Documents" ? Visibility.Visible : Visibility.Collapsed;
            PanelHistory.Visibility = key == "History" ? Visibility.Visible : Visibility.Collapsed;
            PanelBim.Visibility = key == "Bim" ? Visibility.Visible : Visibility.Collapsed;
            PanelCapabilities.Visibility = key == "Capabilities" ? Visibility.Visible : Visibility.Collapsed;
            PanelDiagnostics.Visibility = key == "Diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void Scan_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.IsBusy)
                return;

            if (_vm.ScanMode == ScanMode.Full)
            {
                var confirm = MessageBox.Show(
                    "Полный режим может занять много времени и нагрузить Pilot. Продолжить?",
                    "Полный scan",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            _cts = new CancellationTokenSource();
            _vm.IsBusy = true;
            _vm.ProgressText = "Scanning...";
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

                _vm.Report = report;
                _lastReportText = new InventoryReportService().BuildTextReport(report);
                _vm.ProgressText = report.Cancelled
                    ? "Сканирование отменено."
                    : "Готово. Status=" + report.FinalStatus;
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

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
                _cts.Cancel();
            _vm.ProgressText = "Отмена запрошена...";
        }

        private void CopyReport_Click(object sender, RoutedEventArgs e)
        {
            EnsureReportText();
            if (string.IsNullOrEmpty(_lastReportText))
            {
                MessageBox.Show("Сначала выполните сканирование.", "PilotBim.Analytics");
                return;
            }

            Clipboard.SetText(_lastReportText);
            _vm.ProgressText = "Отчёт скопирован в буфер обмена.";
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            EnsureReportText();
            if (string.IsNullOrEmpty(_lastReportText))
            {
                MessageBox.Show("Сначала выполните сканирование.", "PilotBim.Analytics");
                return;
            }

            var path = new InventoryReportExporter().SaveReport(_lastReportText);
            _vm.ProgressText = "Сохранено: " + path;
            MessageBox.Show("Отчёт сохранён:\n" + path, "PilotBim.Analytics");
        }

        private void EnsureReportText()
        {
            if (_vm.Report != null && string.IsNullOrEmpty(_lastReportText))
                _lastReportText = new InventoryReportService().BuildTextReport(_vm.Report);
        }

        private void TypesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _vm.SelectedType = TypesGrid.SelectedItem as TypeInventoryRecord;
        }

        private void BimElements_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid != null)
                _vm.SelectedBimElement = grid.SelectedItem as BimElementSample;
        }

        private void LoadStructureRoot_Click(object sender, RoutedEventArgs e)
        {
            var root = _inventory.GetRootObject();
            if (root != null)
            {
                _vm.StructureRoots.Clear();
                _vm.StructureRoots.Add(ToNode(root));
                return;
            }

            _inventory.LoadChildren(SystemObjectIds.RootObjectId, children =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var node = new StructureNodeModel
                    {
                        Id = SystemObjectIds.RootObjectId,
                        Name = "Root",
                        TypeName = "Root",
                        ChildrenCount = children != null ? children.Count : 0,
                        ChildrenLoaded = true
                    };
                    if (children != null)
                    {
                        foreach (var c in children.OrderBy(x => x.DisplayName))
                            node.Children.Add(ToNode(c));
                    }
                    _vm.StructureRoots.Clear();
                    _vm.StructureRoots.Add(node);
                }));
            });
        }

        private StructureNodeModel ToNode(PilotDataObject obj)
        {
            var node = new StructureNodeModel
            {
                Id = obj.Id,
                ParentId = obj.ParentId,
                Name = obj.DisplayName,
                TypeName = obj.Type != null ? (obj.Type.Title ?? obj.Type.Name) : null,
                TypeId = obj.Type != null ? obj.Type.Id : 0,
                StateText = obj.ObjectStateInfo != null ? obj.ObjectStateInfo.State.ToString() : null,
                ChildrenCount = obj.Children != null ? obj.Children.Count : 0,
                ChildrenLoaded = false
            };
            if (node.ChildrenCount > 0)
                node.Children.Add(new StructureNodeModel { Name = "...", Id = Guid.Empty });
            return node;
        }

        private void StructureTree_Expanded(object sender, RoutedEventArgs e)
        {
            var item = e.OriginalSource as TreeViewItem;
            if (item == null)
                return;
            var node = item.DataContext as StructureNodeModel;
            if (node == null || node.ChildrenLoaded || node.Id == Guid.Empty)
                return;

            node.ChildrenLoaded = true;
            _inventory.LoadChildren(node.Id, children =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    node.Children.Clear();
                    if (children == null)
                        return;
                    foreach (var c in children.OrderBy(x => x.DisplayName))
                        node.Children.Add(ToNode(c));
                }));
            });
        }
    }
}
