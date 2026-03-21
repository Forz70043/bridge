using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace Bridge
{
    /**
     * Represents a WSL distribution with its properties.
     * Implements INotifyPropertyChanged so UI can update properties in-place without re-binding the whole list.
     */
    internal class WslDistro : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _status = string.Empty;
        private string _version = string.Empty;

        public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(); } } }
        public string Status { get => _status; set { if (_status != value) { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); } } } // Running, Stopped, etc.
        public string Version { get => _version; set { if (_version != value) { _version = value; OnPropertyChanged(); } } }
        public bool IsDefault { get; set; }
        public string StatusIcon => Status == "Running" ? "🟢" : "🔴";
        public SolidColorBrush StatusColor => Status == "Running"
            ? new SolidColorBrush(Colors.LimeGreen)
            : new SolidColorBrush(Colors.Gray);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
