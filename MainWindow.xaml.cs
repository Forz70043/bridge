using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Bridge
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private System.Collections.ObjectModel.ObservableCollection<WslDistro> _visibleDistros = new System.Collections.ObjectModel.ObservableCollection<WslDistro>();
        private Dictionary<string, WslDistro> _map = new Dictionary<string, WslDistro>(StringComparer.OrdinalIgnoreCase);
        private Microsoft.UI.Xaml.DispatcherTimer _refreshTimer;
        private bool _isLoading = false;
        private string _searchQuery = string.Empty;

        public MainWindow()
        {
            //InitializeComponent();
            this.InitializeComponent();
            // Start periodic refresh to detect external changes to WSL state
            _refreshTimer = new Microsoft.UI.Xaml.DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += async (s, e) => await LoadDistros();
            _refreshTimer.Start();

            DistroList.ItemsSource = _visibleDistros; // set once to avoid rebind flicker

            _ = LoadDistros(); // initial load

            this.Closed += MainWindow_Closed;
        }

        /**
         * Loads the list of WSL distributions asynchronously and updates the UI.
         * It uses a flag to prevent overlapping loads if the timer ticks while a load is already in progress.
         */
        private async Task LoadDistros()
        {
            if (_isLoading) return;
            _isLoading = true;
            try
            {
                // show loading ring
                Windows.System.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => { LoadingRing.IsActive = true; LoadingRing.Visibility = Visibility.Visible; });

                WslEngine engine = new WslEngine();
                var newList = await engine.GetDistrosAsync();

                // update collection in-place to avoid full rebind flicker
                UpdateCollection(newList);
            }
            finally
            {
                _isLoading = false;
                // hide loading ring
                Windows.System.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => { LoadingRing.IsActive = false; LoadingRing.Visibility = Visibility.Collapsed; });
            }
        }

        private void UpdateCollection(IEnumerable<WslDistro> newList)
        {
            var names = new HashSet<string>(newList.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);

            // Update or add entries
            foreach (var d in newList)
            {
                if (_map.TryGetValue(d.Name, out var existing))
                {
                    // update properties in-place
                    existing.Status = d.Status;
                    existing.Version = d.Version;
                    existing.IsDefault = d.IsDefault;
                }
                else
                {
                    // add new
                    _map[d.Name] = d;
                    if (!OnlyRunningToggle.IsOn || string.Equals(d.Status, "Running", StringComparison.OrdinalIgnoreCase))
                    {
                        Windows.System.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => _visibleDistros.Add(d));
                    }
                }
            }

            // Remove entries that are no longer present
            var toRemove = _map.Keys.Except(names, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var n in toRemove)
            {
                var item = _map[n];
                _map.Remove(n);
                Windows.System.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => _visibleDistros.Remove(item));
            }

            // If filter changed, ensure visible collection matches filter
            ApplyFilter();
        }

        /**
         * Applies the filter based on the "Only Running" toggle state.
         * If the toggle is on, it filters the list to show only running distributions.
         * Otherwise, it shows all distributions.
         */
        private void ApplyFilter()
        {
            // Build desired list from the master map, then update _visibleDistros in-place to preserve binding
            var query = (_searchQuery ?? string.Empty).Trim();
            var baseList = (OnlyRunningToggle != null && OnlyRunningToggle.IsOn)
                ? _map.Values.Where(d => string.Equals(d.Status, "Running", StringComparison.OrdinalIgnoreCase))
                : _map.Values;

            var desired = string.IsNullOrEmpty(query)
                ? baseList.ToList()
                : baseList.Where(d => d.Name != null && d.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var desiredNames = new HashSet<string>(desired.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);

            Windows.System.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                // Remove items that shouldn't be visible
                for (int i = _visibleDistros.Count - 1; i >= 0; i--)
                {
                    if (!desiredNames.Contains(_visibleDistros[i].Name))
                    {
                        _visibleDistros.RemoveAt(i);
                    }
                }

                // Add missing desired items (preserve order from desired)
                foreach (var d in desired)
                {
                    if (!_visibleDistros.Any(v => string.Equals(v.Name, d.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        _visibleDistros.Add(d);
                    }
                }
            });
        }

        /**
         * Event handler for the "Only Running" toggle button.
         * It calls ApplyFilter to update the displayed list of distributions based on the toggle state.
         */
        private void OnlyRunning_Toggled(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        // Search box handler: update query and reapply filter
        private void SearchBox_TextChanged(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            _searchQuery = tb?.Text ?? string.Empty;
            ApplyFilter();
        }

        /**
         * Event handler for the "Start" button click.
         * It retrieves the associated WSL distribution from the button's DataContext and starts a terminal for that distribution.
         */
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            var distro = (sender as Button).DataContext as WslDistro;
            new WslEngine().StartTerminal(distro.Name);
        }

        /**
         * Event handler for the "Stop" button click.
         * It retrieves the associated WSL distribution from the button's DataContext, terminates it, and then refreshes the list to reflect the new status.
         */
        private async void Stop_Click(object sender, RoutedEventArgs e)
        {
            var distro = (sender as Button).DataContext as WslDistro;
            await new WslEngine().TerminateDistro(distro.Name);
            await LoadDistros(); // Update the list after stopping the distribution to reflect the new status
        }

        /**
         * Event handler for the "Export" button click.
         * It retrieves the associated WSL distribution from the button's DataContext and exports it to a .tar file in a predefined location.
         * Note: In a real application, you would likely want to use a SaveFileDialog to allow the user to choose the export location and filename.
         */
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var distro = (sender as Button).DataContext as WslDistro;
            // Qui potresti aggiungere una 'SaveFileDialog' per scegliere dove salvare il .tar
            new WslEngine().ExportDistro(distro.Name, $"C:\\Backups\\{distro.Name}.tar");
        }

        // Added missing event handler referenced from XAML: Terminal_Click
        private async void Terminal_Click(object sender, RoutedEventArgs e)
        {
            var distro = (sender as Button).DataContext as WslDistro;
            if (distro != null)
            {
                new WslEngine().StartTerminal(distro.Name);

                // Give WSL a short moment to change state, then refresh the list
                await Task.Delay(800);
                await LoadDistros();
            }
        }

        // Added missing event handler referenced from XAML: Delete_Click
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            _ = Delete_Click_Impl(sender, e);
        }

        // Async implementation for per-item delete with confirmation
        private async Task Delete_Click_Impl(object sender, RoutedEventArgs e)
        {
            var distro = (sender as Button).DataContext as WslDistro;
            if (distro == null) return;

            var ok = await ConfirmAsync("Unregister distribution", $"Sei sicuro di voler rimuovere la distro '{distro.Name}'? Queste operazioni non sono reversibili.", "Unregister", "Annulla");
            if (!ok) return;
            try
            {
                // show per-item spinner
                distro.IsBusy = true;

                await new WslEngine().UnregisterDistro(distro.Name);
                System.Diagnostics.Debug.WriteLine($"Delete completed for distro: {distro.Name}");
                _map.Remove(distro.Name);
                ShowToast($"Distro {distro.Name} rimossa", TimeSpan.FromSeconds(4));
            }
            finally
            {
                distro.IsBusy = false;
                await LoadDistros();
            }
        }

        /**
         * Event handler for the window's Closed event.
         * It stops the refresh timer to prevent it from trying to update the UI after the window has been closed.
         */
        private void MainWindow_Closed(object sender, WindowEventArgs e)
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer = null;
            }
        }

        // Top app bar actions (map to selected items)
        private async void TopDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = DistroList.SelectedItems.Cast<WslDistro>().ToList();
            if (!selected.Any()) return;

            var names = string.Join(", ", selected.Select(s => s.Name));
            var ok = await ConfirmAsync("Unregister distributions", $"Sei sicuro di voler rimuovere le distro: {names}? Queste operazioni non sono reversibili.", "Unregister", "Annulla");
            if (!ok) return;

            foreach (var d in selected)
            {
                await new WslEngine().UnregisterDistro(d.Name);
                System.Diagnostics.Debug.WriteLine($"Top delete completed for: {d.Name}");
                _map.Remove(d.Name);
                ShowToast($"Distro {d.Name} rimossa", TimeSpan.FromSeconds(4));
            }

            await LoadDistros();
        }

        private void TopPlay_Click(object sender, RoutedEventArgs e)
        {
            var selected = DistroList.SelectedItems.Cast<WslDistro>().ToList();
            foreach (var d in selected)
            {
                new WslEngine().StartTerminal(d.Name);
            }
        }

        private async void TopStop_Click(object sender, RoutedEventArgs e)
        {
            var selected = DistroList.SelectedItems.Cast<WslDistro>().ToList();
            if (!selected.Any()) return;

            var names = string.Join(", ", selected.Select(s => s.Name));
            var ok = await ConfirmAsync("Terminate distributions", $"Sei sicuro di voler terminare le distro: {names}?", "Termina", "AnNulla");
            if (!ok) return;

            foreach (var d in selected)
            {
                await new WslEngine().TerminateDistro(d.Name);
                ShowToast($"Distro {d.Name} terminata", TimeSpan.FromSeconds(3));
            }
            await LoadDistros();
        }

        // Utility: show a confirmation dialog and return true if primary button pressed
        private async Task<bool> ConfirmAsync(string title, string content, string primaryText = "OK", string secondaryText = "Cancel")
        {
            var dlg = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryText,
                CloseButtonText = secondaryText,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dlg.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        // Simple in-app toast messages (transient)
        private void ShowToast(string text, TimeSpan duration)
        {
            try
            {
                var dq = Windows.System.DispatcherQueue.GetForCurrentThread();
                dq.TryEnqueue(() =>
                {
                    // Find the ToastPanel at runtime instead of relying on the generated field
                    var root = this.Content as FrameworkElement;
                    var toastPanel = root?.FindName("ToastPanel") as StackPanel;
                    if (toastPanel == null) return;

                    var tb = new Border
                    {
                        Background = new SolidColorBrush(Microsoft.UI.Colors.DimGray),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 8),
                        Child = new TextBlock { Text = text, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) }
                    };

                    toastPanel.Children.Insert(0, tb);

                    // remove after delay on background thread, then marshal removal to UI thread
                    Task.Run(async () =>
                    {
                        await Task.Delay(duration);
                        dq.TryEnqueue(() =>
                        {
                            if (toastPanel.Children.Contains(tb))
                            {
                                toastPanel.Children.Remove(tb);
                            }
                        });
                    });
                });
            }
            catch
            {
                // ignore errors in toast display
            }
        }
    }
}
