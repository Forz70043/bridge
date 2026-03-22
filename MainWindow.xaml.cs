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
using WinRT.Interop;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Text.Json;
using System.Collections.Concurrent;

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

        // Per-distro settings storage
        private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bridge", "distro-settings.json");
        private static readonly ConcurrentDictionary<string, DistroSettings> _settingsCache = new ConcurrentDictionary<string, DistroSettings>(StringComparer.OrdinalIgnoreCase);

        private class DistroSettings
        {
            public string DefaultDir { get; set; } = string.Empty;
            public string DefaultUser { get; set; } = string.Empty;
        }

        private DistroSettings LoadSettingsFor(string distroName)
        {
            try
            {
                if (_settingsCache.TryGetValue(distroName, out var cached)) return cached;
                if (!File.Exists(SettingsPath)) return new DistroSettings();
                var all = JsonSerializer.Deserialize<Dictionary<string, DistroSettings>>(File.ReadAllText(SettingsPath)) ?? new Dictionary<string, DistroSettings>(StringComparer.OrdinalIgnoreCase);
                if (all.TryGetValue(distroName, out var s)) { _settingsCache[distroName] = s; return s; }
                return new DistroSettings();
            }
            catch { return new DistroSettings(); }
        }

        private void SaveSettingsFor(string distroName, DistroSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath) ?? Path.GetTempPath());
                Dictionary<string, DistroSettings> all = new Dictionary<string, DistroSettings>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(SettingsPath))
                {
                    all = JsonSerializer.Deserialize<Dictionary<string, DistroSettings>>(File.ReadAllText(SettingsPath)) ?? all;
                }
                all[distroName] = settings;
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
                _settingsCache[distroName] = settings;
            }
            catch { }
        }

        // Handler for per-distro settings button
        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            var distro = (sender as Button).DataContext as WslDistro;
            if (distro == null) return;

            var current = LoadSettingsFor(distro.Name);

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "Directory di avvio (Start directory):", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
            var dirBox = new TextBox { Text = string.IsNullOrEmpty(current.DefaultDir) ? "" : current.DefaultDir };
            panel.Children.Add(dirBox);

            panel.Children.Add(new TextBlock { Text = "Default user (es. root):", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), Margin = new Thickness(0,8,0,0) });
            var userBox = new TextBox { Text = string.IsNullOrEmpty(current.DefaultUser) ? "" : current.DefaultUser };
            panel.Children.Add(userBox);

            panel.Children.Add(new TextBlock { Text = "Suggerimenti:", Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray), Margin = new Thickness(0,8,0,0) });
            panel.Children.Add(new TextBlock { Text = "- Directory dove aprire la shell della distro (es. /home/utente)", Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray) });
            panel.Children.Add(new TextBlock { Text = "- Puoi impostare variabili d'ambiente (future feature)", Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray) });

            var dlg = new ContentDialog
            {
                Title = $"Settings - {distro.Name}",
                Content = panel,
                PrimaryButtonText = "Salva",
                CloseButtonText = "Annulla",
                XamlRoot = this.Content.XamlRoot
            };

            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

            current.DefaultDir = dirBox.Text.Trim();
            current.DefaultUser = userBox.Text.Trim();
            SaveSettingsFor(distro.Name, current);

            ShowToast($"Settings salvati per {distro.Name}", TimeSpan.FromSeconds(3));
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
            if (distro == null) return;
            var settings = LoadSettingsFor(distro.Name);
            var startDir = string.IsNullOrWhiteSpace(settings.DefaultDir) ? null : settings.DefaultDir;
            var user = string.IsNullOrWhiteSpace(settings.DefaultUser) ? null : settings.DefaultUser;
            new WslEngine().StartTerminal(distro.Name, startDir, user);
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
            if (distro == null) return;

            // default folder under Documents\WSLBackups
            var defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WSLBackups");
            Directory.CreateDirectory(defaultFolder);
            var filePath = Path.Combine(defaultFolder, $"{distro.Name}.tar");

            _ = Task.Run(async () =>
            {
                try
                {
                    Windows.System.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => { distro.IsBusy = true; });
                    await new WslEngine().ExportDistro(distro.Name, filePath);
                    Windows.System.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => ShowToast(Localizer.GetFormat("Export_Completed", filePath), TimeSpan.FromSeconds(4)));
                }
                catch (Exception ex)
                {
                    Windows.System.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => ShowToast(Localizer.GetFormat("Export_Error", distro.Name, ex.Message), TimeSpan.FromSeconds(5)));
                }
                finally
                {
                    Windows.System.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => { distro.IsBusy = false; });
                    await LoadDistros();
                }
            });
        }

        // Added missing event handler referenced from XAML: Terminal_Click
        private async void Terminal_Click(object sender, RoutedEventArgs e)
        {
            var distro = (sender as Button).DataContext as WslDistro;
            if (distro != null)
            {
                var settings = LoadSettingsFor(distro.Name);
                var startDir = string.IsNullOrWhiteSpace(settings.DefaultDir) ? null : settings.DefaultDir;
                var user = string.IsNullOrWhiteSpace(settings.DefaultUser) ? null : settings.DefaultUser;
                new WslEngine().StartTerminal(distro.Name, startDir, user);

                // Give WSL a short moment to change state, then refresh the list
                await Task.Delay(800);
                await LoadDistros();
            }
        }

        // New: Export selected distros (top command)
        private async void TopExport_Click(object sender, RoutedEventArgs e)
        {
            var selected = DistroList.SelectedItems.Cast<WslDistro>().ToList();
            if (!selected.Any())
            {
                ShowToast("Seleziona almeno una distro da esportare", TimeSpan.FromSeconds(3));
                return;
            }

            // Use FolderPicker for better UX
            var picker = new Windows.Storage.Pickers.FolderPicker();
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);
            // FolderPicker requires at least one FileTypeFilter item
            picker.FileTypeFilter.Add("*");

            StorageFolder picked = null;
            try
            {
                picked = await picker.PickSingleFolderAsync();
            }
            catch
            {
                ShowToast("Folder picker non disponibile", TimeSpan.FromSeconds(3));
                return;
            }

            if (picked == null)
            {
                // user cancelled
                return;
            }

            var folderPath = picked.Path;
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                ShowToast("Cartella non valida", TimeSpan.FromSeconds(3));
                return;
            }

            Directory.CreateDirectory(folderPath);

            try
            {
                TopOperationRing.IsActive = true;
                TopOperationRing.Visibility = Visibility.Visible;
                TopOperationText.Text = "Export in corso...";
                TopOperationText.Visibility = Visibility.Visible;

                foreach (var d in selected)
                {
                    d.IsBusy = true;
                    var filePath = Path.Combine(folderPath, $"{d.Name}.tar");
                    try
                    {
                        var output = await new WslEngine().ExportDistro(d.Name, filePath);
                        ShowToast($"Export completato: {d.Name}", TimeSpan.FromSeconds(3));
                        System.Diagnostics.Debug.WriteLine(output);
                    }
                    catch (Exception ex)
                    {
                        ShowToast($"Errore export {d.Name}: {ex.Message}", TimeSpan.FromSeconds(5));
                    }
                    finally
                    {
                        d.IsBusy = false;
                    }
                }
            }
            finally
            {
                TopOperationRing.IsActive = false;
                TopOperationRing.Visibility = Visibility.Collapsed;
                TopOperationText.Visibility = Visibility.Collapsed;
                await LoadDistros();
            }
        }

        // New: Import a distro (top command)
        private async void TopImport_Click(object sender, RoutedEventArgs e)
        {
            // Use FileOpenPicker to select a .tar file, then ask for name and install folder
            var picker = new FileOpenPicker();
            // WinUI3 requires initializing picker with window handle
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".tar");

            StorageFile file = null;
            try
            {
                file = await picker.PickSingleFileAsync();
            }
            catch
            {
                ShowToast("Picker non disponibile", TimeSpan.FromSeconds(3));
                return;
            }

            if (file == null)
            {
                // user cancelled
                return;
            }

            // Ask for distro name and install folder
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "Nome della nuova distro:", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
            var nameBox = new TextBox { Text = Path.GetFileNameWithoutExtension(file.Name) };
            panel.Children.Add(nameBox);

            panel.Children.Add(new TextBlock { Text = "Cartella di installazione:", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), Margin = new Thickness(0,8,0,0) });
            var installBox = new TextBox { Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WSL", nameBox.Text) };
            panel.Children.Add(installBox);

            var dlg = new ContentDialog
            {
                Title = "Import distro",
                Content = panel,
                PrimaryButtonText = "Importa",
                CloseButtonText = "Annulla",
                XamlRoot = this.Content.XamlRoot
            };

            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

            var tarPath = file.Path;
            var name = nameBox.Text.Trim();
            var installFolder = installBox.Text.Trim();

            if (string.IsNullOrEmpty(tarPath) || !File.Exists(tarPath))
            {
                ShowToast("File .tar non valido o inesistente", TimeSpan.FromSeconds(4));
                return;
            }
            if (string.IsNullOrEmpty(name))
            {
                ShowToast("Nome distro non valido", TimeSpan.FromSeconds(3));
                return;
            }
            if (string.IsNullOrEmpty(installFolder)) { ShowToast("Cartella di installazione non valida", TimeSpan.FromSeconds(3)); return; }
            Directory.CreateDirectory(installFolder);

            try
            {
                TopOperationRing.IsActive = true;
                TopOperationRing.Visibility = Visibility.Visible;
                TopOperationText.Text = $"Import {name} in corso...";
                TopOperationText.Visibility = Visibility.Visible;

                var output = await new WslEngine().ImportDistro(name, installFolder, tarPath);
                ShowToast($"Import completato: {name}", TimeSpan.FromSeconds(4));
                System.Diagnostics.Debug.WriteLine(output);
            }
            catch (Exception ex)
            {
                ShowToast($"Errore import {name}: {ex.Message}", TimeSpan.FromSeconds(6));
            }
            finally
            {
                TopOperationRing.IsActive = false;
                TopOperationRing.Visibility = Visibility.Collapsed;
                TopOperationText.Visibility = Visibility.Collapsed;
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
                ShowToast(Localizer.GetFormat("Distro_Removed", d.Name), TimeSpan.FromSeconds(4));
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
        // Test button for toast messages
        private void TestToast_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("Notifica di test: operazione completata", TimeSpan.FromSeconds(3));
            ShowToast("Seconda notifica", TimeSpan.FromSeconds(4));
            ShowToast("Terza notifica (più lunga)", TimeSpan.FromSeconds(6));
        }

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
