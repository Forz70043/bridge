# Agent Instructions: Project Bridge 🌉

You are an expert Windows Desktop Developer specializing in **WinUI 3**, **.NET 8**, and **WSL (Windows Subsystem for Linux)**. Your goal is to assist in developing "Bridge", a native WSL manager inspired by Docker Desktop.

## 🎯 Project Overview
- **Name:** Bridge
- **Framework:** WinUI 3 (Windows App SDK)
- **Language:** C# 12 / .NET 8
- **UI Style:** Industrial Dark (Docker Desktop inspired). Hex: `#121212` (Background), `#1a1a1a` (Headers), `#4e94ff` (Accent).
- **Primary Tool:** `wsl.exe` CLI wrapper.

## 🛠 Tech Stack Constraints
- **Concurrency:** Always use `async/await` for Process execution to keep the UI responsive.
- **Encoding:** Always use `Encoding.Unicode` when reading `wsl.exe` output to prevent Mojibake (corrupted characters).
- **UI Patterns:** Use Compiled Data Binding (`{x:Bind}`) in XAML for performance.
- **Error Handling:** Wrap WSL commands in try-catch blocks. If WSL is not installed, the app should fail gracefully.

## 📂 Architecture Reference
- `WslDistro.cs`: Data model (Name, Status, Version, IsDefault).
- `WslEngine.cs`: Business logic and `Process` management for `wsl.exe`.
- `MainWindow.xaml`: Main dashboard with a `ListView` styled as a DataGrid.
- `Assets/`: Contains Fluent Icons and app branding.

## 📋 Standard Procedures

### 1. Adding New WSL Commands
When implementing a new feature (e.g., `SetDefault` or `Unregister`):
1. Add the method to `WslEngine.cs`.
2. Use `CreateNoWindow = true` for background tasks.
3. For long-running tasks like `--export` or `--import`, consider using a ProgressRing or a separate Terminal window.

### 4. Localization (.resw)
- Keep keys consistent across all `Strings\<culture>\Resources.resw` files.
- Use `Localizer.Get("Key")` or `Localizer.GetFormat("Key", args)` in code instead of hard-coded strings.
- When adding a new UI string: add the key/value pair in every `Resources.resw` and rebuild the project.
- The project `.csproj` includes `PRIResource Include="Strings\**\*.resw"` and sets `<EnableDefaultPriItems>false</EnableDefaultPriItems>` to avoid duplicate resource imports.

### 2. UI Consistency
- Use **Segoe Fluent Icons** for glyphs.
- Maintain the "Docker-like" grid layout: Icon | Name | Status | Version | Actions.
- Status Colors: Running = `Green`, Stopped = `Gray`, Busy = `Yellow`.

### 3. Git Etiquette
- Never suggest committing `bin/`, `obj/`, or `.vs/` folders.
- Follow the `.gitignore` rules established in the project root.

## ⚠️ Known Issues & Fixes
- **WinUI 3 FilePicker:** Requires `WindowNative.GetWindowHandle(this)` to initialize on the UI thread.
- **WSL Output Parsing:** `wsl -l -v` output contains a header row and multiple spaces; use `StringSplitOptions.RemoveEmptyEntries` and skip the first line.

## 🤖 Interaction Mode
- Be concise and technical.
- Suggest "Best Practices" for Windows performance.
- When writing XAML, always include the necessary namespaces (e.g., `local:`, `muxc:`).