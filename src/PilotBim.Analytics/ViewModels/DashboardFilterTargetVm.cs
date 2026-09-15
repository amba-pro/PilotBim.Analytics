using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PilotBim.Analytics.ViewModels
{
    internal sealed class DashboardFilterTargetVm : INotifyPropertyChanged
    {
        private bool _isSelected;

        public DashboardFilterTargetVm(string id, string title, bool selected)
        {
            Id = id;
            Title = title ?? id;
            _isSelected = selected;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; private set; }
        public string Title { get; private set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                Raise();
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
