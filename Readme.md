# Bridge 🌉

### The ultra-lightweight, native WSL manager for Windows.

Bridge is a high-performance desktop application built with **C# 12**, **WinUI 3** (Windows App SDK 1.8), and **.NET 8**. It provides a sleek, Docker Desktop-inspired interface to manage your Windows Subsystem for Linux (WSL) distributions with zero overhead.

> **Unpackaged app** — Bridge runs as a standalone executable (no MSIX install required). Publish produces a single, self-contained, trimmed `.exe`.

---

## ✨ Features

| Feature | Description |
|---|---|
| **Native Performance** | Built with WinUI 3 for a fluid, Windows 11-native experience. |
| **One-Click Management** | Start, Stop, and Terminate distributions instantly. |
| **Terminal Integration** | Launch your favorite distro directly into Windows Terminal. |
| **Import / Export** | Easily backup or clone your WSL environments to `.tar` files. |
| **Per-Distro Settings** | Configure default start directory and user for each distribution (stored in `%APPDATA%\Bridge\distro-settings.json`). |
| **System Tray** | Minimize to tray — the app stays alive via a Win32 `Shell_NotifyIcon` (no WinForms dependency). |
| **Localization** | UI fully translated in 5 languages: `en-US`, `it-IT`, `es-ES`, `fr-FR`, `zh-CN`. |
| **Deep Dark Mode** | A professional industrial-dark UI designed for developers who live in the dark. |

---

## 🚀 Getting Started

### Prerequisites

- Windows 10 (1809) or Windows 11.
- WSL 2 installed and configured.
- Windows Terminal (recommended).

### Installation

1. Go to the [Releases](https://github.com/Forz70043/bridge/releases) page.
2. Download the latest `Bridge.exe` (self-contained, single-file).
3. Run — no installer required.

---

## 🛠️ Development

### Build from source

1. Clone the repository:
   ```bash
   git clone https://github.com/Forz70043/Bridge.git
   ```
2. Open `Bridge.sln` in **Visual Studio 2022+**.
3. Ensure the **Windows App SDK** workload is installed.
4. Set the solution platform to **x64** (also supports x86 and ARM64).
5. Press **F5** to build and run.

### Publish a release build

The project includes a ready-to-use publish profile (`FolderProfile.pubxml`):

```powershell
dotnet publish -c Release -r win-x64 -p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml
```

This produces a **self-contained, single-file, trimmed** executable in `build\`.

### Generate the app icon

The tray / application icon is generated from `Assets\linux_image_transparent.jpg`. Run from the project root (requires [ImageMagick](https://imagemagick.org/) `magick` on PATH):

```powershell
.\Tools\GenerateIcons.ps1 -source Assets\linux_image_transparent.jpg -outDir Assets\Icons
```

---

## 🏗️ Architecture

| File | Role |
|---|---|
| `WslDistro.cs` | Data model — `Name`, `Status`, `Version`, `IsDefault`, `IsBusy`. Implements `INotifyPropertyChanged`. |
| `WslEngine.cs` | Business logic — wraps `wsl.exe` via `ProcessStartInfo` / `ArgumentList`. |
| `Localizer.cs` | Lazy wrapper around `ResourceLoader` for `.resw` string lookup. |
| `MainWindow.xaml` | Main dashboard — `ListView` styled as DataGrid. |
| `MainWindow.xaml.cs` | UI logic + Win32 tray icon (P/Invoke) + per-distro settings (JSON source-generated serialization). |
| `App.xaml.cs` | Application entry point. |

---

## 📸 Screenshots

| Dashboard | Actions |
|---|---|
| | |

---

## 📜 Roadmap

- [x] Auto-update integration.
- [x] System tray icon with context menu.
- [x] Per-distro settings (default directory / user).
- [x] Localization (5 languages).
- [x] Single-file self-contained publish.
- [ ] Resource monitoring (CPU / RAM usage per distro).
- [ ] Custom mount point management.
- [ ] Snapshot system (Checkpoints).

---

## 🌐 Localization (.resw)

Bridge uses `.resw` files for translations. Resource files live under `Strings\<culture>\Resources.resw` (e.g. `Strings\it-IT\Resources.resw`).

**Supported languages:** `en-US` · `it-IT` · `es-ES` · `fr-FR` · `zh-CN`

### How to add / update strings

1. Add `<data name="My_Key"><value>…</value></data>` in **all 5** `Resources.resw` files.
2. Use `Localizer.Get("My_Key")` or `Localizer.GetFormat("My_Key", args)` in code.
3. Rebuild the project — the `.csproj` automatically includes `Strings\**\*.resw`.
4. The CI workflow `validate-resw.yml` verifies that all locale files contain identical key sets on every push.

> **Rule:** never hardcode UI strings. A missing key is displayed as the key name at runtime (no crash).

---

## 🤝 Contributing

Contributions are what make the open-source community such an amazing place to learn, inspire, and create. Any contributions you make are greatly appreciated.

1. Fork the Project.
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`).
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`).
4. Push to the Branch (`git push origin feature/AmazingFeature`).
5. Open a Pull Request.

---

## 📄 License

Distributed under the **MIT License**. See [LICENSE](LICENSE) for more information.

---

Created with ❤️ by [Forz70043](https://github.com/Forz70043)
