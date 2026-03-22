# Agent Instructions: Project Bridge 🌉

You are an expert Windows Desktop Developer specializing in **WinUI 3**, **.NET 8**, and **WSL (Windows Subsystem for Linux)**. Your goal is to assist in developing "Bridge", a native WSL manager inspired by Docker Desktop.

## 🎯 Project Overview
- **Name:** Bridge
- **Framework:** WinUI 3 (Windows App SDK 1.8)
- **Language:** C# 12 / .NET 8
- **Target:** `net8.0-windows10.0.19041.0` — min version `10.0.17763.0`
- **Platforms:** x86 · x64 · ARM64
- **UI Style:** Industrial Dark (Docker Desktop inspired)
  - Background `#121212` · Headers `#1a1a1a` · Accent `#4e94ff`
- **Primary Tool:** `wsl.exe` CLI wrapper (no WSL SDK)
- **Branch:** `develop` · Remote: `https://github.com/Forz70043/bridge`
- **Author:** Alfonso Pisicchio — https://github.com/Forz70043

---

## 📂 Architecture

| File | Role |
|---|---|
| `WslDistro.cs` | Data model — `Name`, `Status`, `Version`, `IsDefault`, `IsBusy`. Implements `INotifyPropertyChanged`. `StatusColor` uses **static cached** `SolidColorBrush` instances. |
| `WslEngine.cs` | Business logic — wraps `wsl.exe` via `ProcessStartInfo`. Always uses `ArgumentList` (never raw `Arguments` string). Always `CreateNoWindow = true`. Always `Encoding.Unicode` on stdout. |
| `Localizer.cs` | Lazy wrapper around `ResourceLoader.GetForViewIndependentUse("Resources")`. Use `Localizer.Get("Key")` and `Localizer.GetFormat("Key", args)`. |
| `MainWindow.xaml` | Main dashboard — `ListView` styled as DataGrid. Header: Icon · Name · Status · Version · Actions. |
| `MainWindow.xaml.cs` | UI logic + **Win32 tray icon** (P/Invoke, no WinForms). Contains nested P/Invoke section (`NOTIFYICONDATA`, `Shell_NotifyIcon`, `SetWindowLongPtr`, etc.). |
| `App.xaml.cs` | Application entry point — creates `MainWindow` on launch. |
| `Assets/linux_image_transparent.jpg` | Source image for tray icon (loaded at runtime via `System.Drawing.Bitmap.GetHicon()`). |
| `Assets/Icons/app.ico` | Generated ICO file (fallback for tray icon). Generate via `Tools/GenerateIcons.ps1`. |
| `Tools/GenerateIcons.ps1` | Generates `Assets/Icons/app.ico` from `linux_image_transparent.jpg` using ImageMagick (`magick`). |
| `Strings/<culture>/Resources.resw` | Localization files — **5 locales**: `en-US`, `it-IT`, `es-ES`, `fr-FR`, `zh-CN`. |

---

## 🛠 Tech Stack Constraints

### Concurrency
- Always use `async/await` for `Process` execution.
- Load guard uses `SemaphoreSlim(1,1)` with `WaitAsync(0)` — never a plain `bool` flag.
- Never call `Dispatcher.Invoke` from a background thread; use `DispatcherQueue.TryEnqueue`.

### Process / WslEngine Rules
- Always use `ArgumentList` — never build argument strings manually.
- Always `CreateNoWindow = true` for background commands.
- Always `Encoding.Unicode` on `StandardOutputEncoding`.
- Validate exit codes and surface errors via `ShowToast`.

### UI Patterns
- Compiled Data Binding `{x:Bind}` for all `DataTemplate` bindings.
- `SolidColorBrush` instances that are bound frequently must be **static readonly** fields in the model — never `new` on each property access.
- Status Colors: Running = `LimeGreen` · Stopped = `Gray` · Busy = `Yellow`.
- Use **Segoe Fluent Icons** glyphs in XAML.

### Nullable / Safety
- Project has `<Nullable>enable</Nullable>`.
- Always use null-conditional `?.` when casting from `sender` in event handlers: `(sender as Button)?.DataContext as WslDistro`.
- `StorageFile?` / `StorageFolder?` for picker results.

### Error Handling
- Wrap all WSL commands in `try/catch`. If WSL is not installed, fail gracefully.
- Surface user-visible errors via `ShowToast(message, duration)`.

---

## 🔔 Tray Icon (Win32 — no WinForms)

The tray icon is implemented entirely via Win32 P/Invoke in `MainWindow.xaml.cs`:

- `Shell_NotifyIcon` (`NIM_ADD` / `NIM_DELETE`) with `NOTIFYICONDATA`.
- Window subclassing via `SetWindowLongPtr(GWLP_WNDPROC)` to receive `WM_USER+1` tray callbacks.
- **Close button (X) hides the window** (`ShowWindow(SW_HIDE)`) — the app stays alive in the tray.
- **Double-click** on tray icon restores the window (`ShowWindow(SW_RESTORE)` + `Activate()`).
- **Right-click** shows a native Win32 popup menu (`CreatePopupMenu` / `TrackPopupMenuEx`).
- Tray icon is loaded from `Assets/linux_image_transparent.jpg` at runtime via `System.Drawing.Bitmap.GetHicon()`. Falls back to `Assets/Icons/app.ico`, then `IDI_APPLICATION`.
- **Do NOT enable `<UseWindowsForms>true`** — WinForms is explicitly excluded.

### Tray menu items (current)
| ID | Label key | Action |
|---|---|---|
| `ID_ABOUT` | `Tray_About` | Opens About dialog |
| `ID_EXIT` | `Tray_Exit` | Removes tray icon → `CoreApplication.Exit()` |

---

## 🔧 Per-distro Settings

Each distro can have a `DefaultDir` (start directory) and `DefaultUser` stored in:
`%APPDATA%\Bridge\distro-settings.json`

Managed by `LoadSettingsFor(distroName)` / `SaveSettingsFor(distroName, settings)` in `MainWindow.xaml.cs`.
Always apply settings when calling `WslEngine.StartTerminal(name, startDir, user)`.

---

## 🌐 Localization (.resw)

### Rules
- **Never hardcode UI strings.** Always use `Localizer.Get("Key")` or `Localizer.GetFormat("Key", args)`.
- All 5 locale files must contain **identical key sets**. The CI validates this (see `validate-resw.yml`).
- When adding a new key: add it in every `Resources.resw` file and rebuild.
- The `.csproj` uses `<PRIResource Include="Strings\**\*.resw" />` with `<EnableDefaultPriItems>false</EnableDefaultPriItems>`.
- `Localizer` uses lazy initialization — a missing PRI file returns the key name as fallback (no crash).
- Resource map name is `"Resources"` (NOT `"Strings/Resources"`).

### Supported locales
`en-US` · `it-IT` · `es-ES` · `fr-FR` · `zh-CN`

### CI — validate-resw
`.github/workflows/validate-resw.yml` runs on every push/PR that touches `Strings/**`.
It parses all `Resources.resw` files with `System.Xml.XmlDocument` and fails if any file is missing a key that exists in another file.

---

## 📋 Standard Procedures

### Adding a new WSL command
1. Add the async method to `WslEngine.cs`.
2. Use `ArgumentList` — never raw argument strings.
3. `CreateNoWindow = true`, `Encoding.Unicode` on stdout.
4. Check exit code; throw `InvalidOperationException` on failure.
5. For long-running tasks (`--export`, `--import`): set `distro.IsBusy = true`, show `ProgressRing`, restore in `finally`.

### Adding a new UI string
1. Add `<data name="MyKey">` in **all 5** `Resources.resw` files.
2. Use `Localizer.Get("MyKey")` in C# code.
3. Rebuild to regenerate the PRI resource.
4. The CI `validate-resw.yml` will verify synchronization on push.

### Adding a new tray menu item
1. Define a new `const uint ID_XXX` in `ShowTrayContextMenu`.
2. Call `AppendMenu(hMenu, MF_STRING, new UIntPtr(ID_XXX), Localizer.Get("Tray_XXX"))`.
3. Add the `case ID_XXX:` branch in the `switch` block.
4. Add the `Tray_XXX` key to all 5 `.resw` files.

### Generating the app icon
Run in PowerShell from the project root (requires ImageMagick `magick` on PATH):
```powershell
.\Tools\GenerateIcons.ps1 -source Assets\linux_image_transparent.jpg -outDir Assets\Icons
```
This produces `Assets\Icons\app.ico` used by the tray icon loader fallback.

---

## ⚠️ Known Issues & Fixes

| Area | Issue | Fix |
|---|---|---|
| WinUI 3 FilePicker | Must be initialized on the UI thread | `InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this))` |
| WSL output parsing | `wsl -l -v` has header row and multi-space columns | Skip line 0; split with `StringSplitOptions.RemoveEmptyEntries` |
| Tray icon — image | `System.Drawing.Bitmap` loads JPG at runtime for HICON | If `linux_image_transparent.jpg` is missing from output, check `CopyToOutputDirectory` in `.csproj` |
| App icon (.ico) | `app.ico` must be generated manually before first build | Run `Tools\GenerateIcons.ps1` (ImageMagick required) |
| WinUI 3 HWND | `SetWindowLongPtr` / `CallWindowProc` require the real HWND | Always obtain via `WindowNative.GetWindowHandle(this)` before subclassing |
| NuGet NU1701 | `EO.WebBrowser 26.1.7` targets .NET Framework, not net8.0 | Remove the package if unused, or replace with a net8.0-compatible alternative |
| resw duplicate keys | PowerShell `Set-Content` after XML save can leave orphan `</root>` | Always validate with `[xml]` parse + duplicate-key check after bulk edits |

---

## 🤖 Interaction Mode
- Be concise and technical.
- Always use `async/await`; never block the UI thread.
- When writing XAML, always include the necessary namespaces (`local:`, `muxc:`).
- Suggest Windows best practices: compiled bindings, cached brushes, `SemaphoreSlim` for guards.
- When adding any UI string, always update **all 5** `.resw` files in the same change.