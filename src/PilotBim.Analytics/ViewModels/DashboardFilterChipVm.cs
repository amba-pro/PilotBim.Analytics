using System.ComponentModel;
using System.Runtime.CompilerServices;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Properties;
using PilotBim.Analytics.Services;

namespace PilotBim.Analytics.ViewModels
{
    internal sealed class DashboardFilterChipVm : INotifyPropertyChanged
    {
        public DashboardFilterChipVm(DashboardLevelFilterDefinition filter, DashboardFieldCatalog catalog, bool editMode)
        {
            Id = filter != null ? filter.Id : string.Empty;
            Title = filter != null && !string.IsNullOrWhiteSpace(filter.Title)
                ? filter.Title
                : (filter != null ? filter.FieldId : string.Empty);
            ValueDisplay = DashboardLevelFilterDisplay.ValueText(filter, catalog);
            FieldUnavailable = DashboardLevelFilterDisplay.IsFieldUnavailable(filter, catalog);
            NotApplied = filter != null && filter.Disabled;
            CanManage = editMode;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; private set; }
        public string Title { get; private set; }
        public string ValueDisplay { get; private set; }
        public bool FieldUnavailable { get; private set; }
        public bool NotApplied { get; private set; }
        public bool CanManage { get; private set; }

        public string StatusText
        {
            get
            {
                if (FieldUnavailable)
                    return Resources.DashboardFilters_FieldUnavailable;
                if (NotApplied)
                    return Resources.DashboardFilters_NotApplied;
                return null;
            }
        }

        public bool ShowStatus
        {
            get { return !string.IsNullOrEmpty(StatusText); }
        }

        public string Summary
        {
            get
            {
                if (string.IsNullOrEmpty(ValueDisplay))
                    return Title;
                return Title + ": " + ValueDisplay;
            }
        }

        private void Raise([CallerMemberName] string name = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
