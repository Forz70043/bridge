using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Bridge
{
    /**
     * Represents a WSL distribution with its properties.
     * Implements INotifyPropertyChanged so UI can update properties in-place without re-binding the whole list.
     */
    internal class WslDistro : INotifyPropertyChanged
    {
        private static readonly SolidColorBrush BrushRunning = new SolidColorBrush(Colors.LimeGreen);
        private static readonly SolidColorBrush BrushStopped = new SolidColorBrush(Colors.Gray);

        private string _name = string.Empty;
        private string _status = string.Empty;
        private string _version = string.Empty;
        private bool _isDefault = false;
        private bool _isBusy = false;

        public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(); } } }
        public string Status { get => _status; set { if (_status != value) { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); } } }
        public string Version { get => _version; set { if (_version != value) { _version = value; OnPropertyChanged(); } } }
        public bool IsDefault { get => _isDefault; set { if (_isDefault != value) { _isDefault = value; OnPropertyChanged(); } } }
        public bool IsBusy { get => _isBusy; set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(BusyVisibility)); } } }
        public string StatusIcon => Status == "Running" ? "🟢" : "🔴";
        public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

        public SolidColorBrush StatusColor => string.Equals(Status, "Running", StringComparison.OrdinalIgnoreCase)
            ? BrushRunning
            : BrushStopped;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
