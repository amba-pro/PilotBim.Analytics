using System.Windows;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.ViewModels;
using UiResources = PilotBim.Analytics.Properties.Resources;

namespace PilotBim.Analytics.Views
{
    public partial class DashboardFilterEditorWindow : Window
    {
        private readonly DashboardFilterEditorViewModel _vm;

        internal DashboardFilterEditorWindow(DashboardFilterEditorViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = _vm;
            Title = UiResources.DashboardFilters_WindowTitle;
        }

        internal DashboardLevelFilterDefinition Result { get; private set; }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string error;
            var built = _vm.TryBuild(out error);
            if (built == null)
                return;
            Result = built;
            DialogResult = true;
            Close();
        }
    }
}
