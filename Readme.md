# Bridge 🌉

### The ultra-lightweight, native WSL manager for Windows.

Bridge is a high-performance desktop application built with C# and WinUI 3. It provides a sleek, Docker Desktop-inspired interface to manage your Windows Subsystem for Linux (WSL) distributions with zero overhead.

## ✨ Features

- Native Performance: Built with WinUI 3 for a fluid, Windows 11-native experience.

- One-Click Management: Start, Stop, and Terminate distributions instantly.

- Terminal Integration: Launch your favorite distro directly into Windows Terminal.

- Import/Export: Easily backup or clone your WSL environments to .tar files.

- Deep Dark Mode: A professional UI designed for developers who live in the dark.

## 🚀 Getting Started
Prerequisites

- Windows 10 (1809) or Windows 11.

- WSL2 installed and configured.

- Windows Terminal (Recommended).

Installation

- Go to the Releases page.

- Download the latest Bridge_Installer.msix or .exe.

- Run the installer and follow the instructions.

## 🛠️ Development

If you want to build Bridge from source:

1. Clone the repository:
        Bash

        git clone https://github.com/Forz70043/Bridge.git

2. Open Bridge.sln in Visual Studio 2022.

3. Ensure the Windows App SDK workload is installed.

4. Set the solution platform to x64.

5. Press F5 to build and run.

## 📸 Screenshots
Dashboard	Actions
	
## 📜 Roadmap

    [✅] Auto-update integration.

    [ ] Resource monitoring (CPU/RAM usage per distro).

    [ ] Custom mount point management.

    [ ] Snapshot system (Checkpoints).

## 🤝 Contributing

Contributions are what make the open-source community such an amazing place to learn, inspire, and create. Any contributions you make are greatly appreciated.

Fork the Project.

Create your Feature Branch (git checkout -b feature/AmazingFeature).

Commit your Changes (git commit -m 'Add some AmazingFeature').

Push to the Branch (git push origin feature/AmazingFeature).

Open a Pull Request.

## 📄 License

Distributed under the MIT License. See LICENSE for more information.

Created with ❤️ by Forz70043

## 🌐 Localization (.resw)

Bridge uses `.resw` files for translations (Windows-style). Resource files live under `Strings\<culture>\Resources.resw` (for example `Strings\it-IT\Resources.resw`).

How to update / add strings:
- Open the file `Strings\<culture>\Resources.resw` for the target language.
- Add the new key `<data name="My_Key">` with the value `<value>...</value>` in every supported language folder.
- Keep keys synchronized across all `Strings` folders. Do not leave missing keys — a missing key will be displayed as the key name at runtime.
- Currently supported languages: `it-IT`, `en-US`, `es-ES`, `fr-FR`, `zh-CN`.
- After updating `.resw` files, rebuild the project (F5). The `Bridge.csproj` is already configured to include `Strings\\**\\*.resw` automatically.

Best practices:
- Always use `Localizer.Get("Key")` or `Localizer.GetFormat("Key", args)` in code instead of hard-coded strings.
- Add clear comments when a string requires formatting (e.g. `{0}`).
- Build and verify the UI in the target languages after changes.
