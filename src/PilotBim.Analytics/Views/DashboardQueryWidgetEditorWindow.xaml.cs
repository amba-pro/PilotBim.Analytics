using System.Windows;
using System.Windows.Controls;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.ViewModels;
using UiResources = PilotBim.Analytics.Properties.Resources;

namespace PilotBim.Analytics.Views
{
    public partial class DashboardQueryWidgetEditorWindow : Window
    {
        private readonly DashboardQueryWidgetEditorViewModel _vm;

        internal DashboardQueryWidgetEditorWindow(DashboardQueryWidgetEditorViewModel viewModel, bool isEdit)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = _vm;
            Title = isEdit ? UiResources.QueryEditor_EditTitle : UiResources.QueryEditor_WindowTitle;
        }

        internal DashboardWidgetDefinition Result { get; private set; }

        private void AddFilter_Click(object sender, RoutedEventArgs e)
        {
            _vm.AddFilter();
        }

        private void RemoveFilter_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var row = button != null ? button.Tag as DashboardQueryFilterRowViewModel : null;
            _vm.RemoveFilter(row);
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            var ignored = _vm.RunPreviewAsync();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Result = _vm.TrySave();
            if (Result == null)
                return;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
