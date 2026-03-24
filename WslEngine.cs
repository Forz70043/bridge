using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace Bridge
{
    internal class WslEngine
    {
        /**
         * Retrieves the list of WSL distributions installed on the system.
         * It executes the "wsl.exe -l -v" command and parses its output.
         * 
         * @return A list of WslDistro objects representing each distribution.
         */
        public async Task<List<WslDistro>> GetDistrosAsync()
        {
            var distros = new List<WslDistro>();

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.Unicode
            };
            psi.ArgumentList.Add("-l");
            psi.ArgumentList.Add("-v");

            var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start wsl.exe to list WSL distributions. Ensure WSL is installed and accessible.");
            }

            using (process)
            {
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Group the output into lines and skip the first line (header)
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Skip(1);

                foreach (var line in lines)
                {
                    // Clean up the line and split it into parts
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 3)
                    {
                        bool isDefault = line.Trim().StartsWith("*");

                        // If the line starts with "*", skip the leading "*" token
                        int offset = isDefault ? 1 : 0;

                        // Need at least offset + name + status + version tokens
                        if (parts.Length < offset + 3) continue;

                        // Version is the last token, status is second-to-last; name may contain spaces
                        string version = parts[parts.Length - 1];
                        string status = parts[parts.Length - 2];
                        string name = string.Join(" ", parts, offset, parts.Length - 2 - offset);

                        distros.Add(new WslDistro
                        {
                            IsDefault = isDefault,
                            Name = name,
                            Status = status,
                            Version = version
                        });
                    }
                }
            }

            return distros;
        }

        /**
         * Starts a new terminal window for the specified WSL distribution.
         * It uses 'cmd.exe' to launch 'wsl.exe' with the appropriate arguments.
         * 
         * @param distroName The name of the WSL distribution to start.
         */
        public void StartTerminal(string distroName, string? startDir = null, string? user = null)
        {
            // Build wsl arguments using ArgumentList to avoid fragile manual quoting and injection issues
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("start");
            psi.ArgumentList.Add("wsl");
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(distroName);

            if (!string.IsNullOrWhiteSpace(user))
            {
                psi.ArgumentList.Add("-u");
                psi.ArgumentList.Add(user);
            }

            if (!string.IsNullOrWhiteSpace(startDir))
            {
                psi.ArgumentList.Add("--cd");
                psi.ArgumentList.Add(startDir);
            }

            Process.Start(psi);
        }

        /**
         * Terminates the specified WSL distribution.
         * It executes the "wsl.exe --terminate <distroName>" command.
         * 
         * @param distroName The name of the WSL distribution to terminate.
         */
        public async Task TerminateDistro(string distroName)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.Unicode
            };
            psi.ArgumentList.Add("--terminate");
            psi.ArgumentList.Add(distroName);
            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start wsl.exe to terminate the specified WSL distribution.");
            }

            var errTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stderr = await errTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"wsl --terminate failed (exit code {process.ExitCode}): {stderr.Trim()}");
            }
        }

        /**
         * Exports the specified WSL distribution to a tar file.
         * It executes the "wsl.exe --export <distroName> <filePath>" command.
         *
         * @param distroName The name of the WSL distribution to export.
         * @param filePath The path where the exported tar file will be saved.
         */
        public async Task<string> ExportDistro(string distroName, string filePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Unicode,
                StandardErrorEncoding = Encoding.Unicode
            };
            psi.ArgumentList.Add("--export");
            psi.ArgumentList.Add(distroName);
            psi.ArgumentList.Add(filePath);

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start wsl.exe to export the specified WSL distribution.");
            }

            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var stdout = await outTask;
            var stderr = await errTask;

            if (process.ExitCode != 0)
            {
                var msg = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : stdout.Trim();
                throw new InvalidOperationException($"wsl --export failed (exit code {process.ExitCode}): {msg}");
            }

            return $"{stdout}\n{stderr}".Trim();
        }

        /**
         * Imports a distro from a tar file.
         * It executes "wsl.exe --import <name> <installLocation> <tarPath>"
         * 
         * @param distroName The name to assign to the imported distribution.
         * @param installLocation The folder where WSL will store the distro files.
         * @param filePath The path to the .tar file to import.
         */
        public async Task<string> ImportDistro(string distroName, string installLocation, string filePath)
        {
            // ensure folder exists
            Directory.CreateDirectory(installLocation);

            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Unicode,
                StandardErrorEncoding = Encoding.Unicode
            };
            psi.ArgumentList.Add("--import");
            psi.ArgumentList.Add(distroName);
            psi.ArgumentList.Add(installLocation);
            psi.ArgumentList.Add(filePath);

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start wsl.exe to import the specified WSL distribution.");
            }

            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var stdout = await outTask;
            var stderr = await errTask;

            if (process.ExitCode != 0)
            {
                var msg = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : stdout.Trim();
                throw new InvalidOperationException($"wsl --import failed (exit code {process.ExitCode}): {msg}");
            }

            return $"{stdout}\n{stderr}".Trim();
        }

        /**
         * Unregisters (deletes) the specified WSL distribution.
         * It executes the "wsl.exe --unregister <distroName>" command.
         */
        public async Task UnregisterDistro(string distroName)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.Unicode
            };
            psi.ArgumentList.Add("--unregister");
            psi.ArgumentList.Add(distroName);
            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start wsl.exe to unregister the specified WSL distribution.");
            }

            var errTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stderr = await errTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"wsl --unregister failed (exit code {process.ExitCode}): {stderr.Trim()}");
            }
        }
    }
}
