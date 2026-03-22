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
                Arguments = "-l -v",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.Unicode
            };

            using var process = Process.Start(psi);
            string output = await process.StandardOutput.ReadToEndAsync();

            // Group the output into lines and skip the first line (header)
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Skip(1);

            foreach (var line in lines)
            {
                // Clean up the line and split it into parts
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 3)
                {
                    bool isDefault = line.Trim().StartsWith("*");

                    // If the line starts with "*", it indicates the default distribution, so we need to adjust the indices accordingly
                    int offset = isDefault ? 1 : 0;

                    distros.Add(new WslDistro
                    {
                        IsDefault = isDefault,
                        Name = parts[0 + offset],
                        Status = parts[1 + offset],
                        Version = parts[2 + offset]
                    });
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
            // Build wsl arguments: distro, optional user, optional start directory
            var args = new List<string>();
            args.Add($"-d \"{distroName}\"");
            if (!string.IsNullOrWhiteSpace(user)) args.Add($"-u \"{user}\"");
            if (!string.IsNullOrWhiteSpace(startDir)) args.Add($"--cd \"{startDir}\"");

            var wslArgs = string.Join(" ", args);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start wsl {wslArgs}",
                CreateNoWindow = true
            };

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
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"--terminate {distroName}",
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            await process.WaitForExitAsync();
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
                Arguments = $"--export {distroName} \"{filePath}\"",
                CreateNoWindow = false,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Unicode
            };

            using var process = Process.Start(psi);
            var output = new StringBuilder();

            if (process == null)
            {
                throw new InvalidOperationException("Failed to start wsl.exe");
            }

            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            output.AppendLine(await outTask);
            output.AppendLine(await errTask);

            return output.ToString();
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
                Arguments = $"--import {distroName} \"{installLocation}\" \"{filePath}\"",
                CreateNoWindow = false,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Unicode
            };

            using var process = Process.Start(psi);
            var output = new StringBuilder();

            if (process == null)
            {
                throw new InvalidOperationException("Failed to start wsl.exe");
            }

            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            output.AppendLine(await outTask);
            output.AppendLine(await errTask);

            return output.ToString();
        }

        /**
         * Unregisters (deletes) the specified WSL distribution.
         * It executes the "wsl.exe --unregister <distroName>" command.
         */
        public async Task UnregisterDistro(string distroName)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"--unregister {distroName}",
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            await process.WaitForExitAsync();
        }
    }
}
