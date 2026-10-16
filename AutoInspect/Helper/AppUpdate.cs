using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoInspect.Helper
{
    public class AppUpdate(AppConfigura configura, LogHelper log)
    {
        private readonly HttpClient _httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(20) };
        private readonly string _updatePath = Path.Combine(AppContext.BaseDirectory, "Files", "Update");
        private readonly string _serviceHost = "http://" + configura.ServiceHost!.TrimEnd('/');

        public async Task RunUpdaterAndExitAsync()
        {
            bool isUpate = await DownloadFilesAsync();
            if (!isUpate) return;

            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            string exeDir = Path.GetDirectoryName(exePath)!;
            string batPath = Path.Combine(exeDir, "Files", "update.bat");

            if (!File.Exists(batPath))
            {
                log.Error("system", "update.bat does not exist.");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = batPath,             // full path of bat script
                Verb = "runas",                 // request admin privileges
                UseShellExecute = true,         // must be true to use Verb
                CreateNoWindow = true           // hide window if desired
            };

            Process.Start(psi);
            Environment.Exit(0);
        }

        public async Task<bool> DownloadFilesAsync()
        {
            bool result = false;
            Directory.CreateDirectory(_updatePath);

            // Get file list from server
            string listUrl = $"{_serviceHost}/get/file/list";
            var response = await _httpClient.GetStringAsync(listUrl);

            // [{"fileName":"xxx","hash":"xxxx"}]
            var serverFiles = JsonSerializer.Deserialize<List<FileInfoDto>>(response) ?? new List<FileInfoDto>();

            // Get local files and hashes
            var localFiles = Directory.GetFiles(Environment.CurrentDirectory)
                .Select(path => new FileInfoDto
                {
                    fileName = Path.GetFileName(path),
                    hash = ComputeFileHash(path)
                })
                .ToDictionary(f => f.fileName, f => f.hash);

            foreach (var file in serverFiles)
            {
                string localFilePath = Path.Combine(_updatePath, file.fileName);
                bool needDownload = false;

                if (!localFiles.TryGetValue(file.fileName, out var localHash))
                {
                    log.Info("system", $"[INFO] Missing local file {file.fileName}, preparing to download...");
                    needDownload = true;
                }
                else if (!string.Equals(localHash, file.hash, StringComparison.OrdinalIgnoreCase))
                {
                    log.Info("system", $"[INFO] File {file.fileName} hash mismatch, preparing to update...");
                    needDownload = true;
                }

                if (needDownload)
                {
                    await DownloadFileAsync(file.fileName, localFilePath);
                    result = true;
                }
            }

            log.Info("system", "[INFO] File synchronization completed.");
            return result;
        }

        // Download a single file
        private async Task DownloadFileAsync(string fileName, string savePath)
        {
            string downloadUrl = $"{_serviceHost}/get/file/download?fileName={Uri.EscapeDataString(fileName)}";
            log.Info("system", $"[INFO] Downloading {fileName} ...");

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                log.Error("system", $"[ERROR] Failed to download: {fileName} ({response.StatusCode})");
                return;
            }

            await using var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            using var contentStream = await response.Content.ReadAsStreamAsync();

            long? totalBytes = response.Content.Headers.ContentLength;
            long totalRead = 0;
            byte[] buffer = new byte[64 * 1024];
            int bytesRead;

            const int progressBarLength = 30; // progress bar length

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fs.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (totalBytes.HasValue)
                {
                    double progress = (double)totalRead / totalBytes.Value;
                    int filledLength = (int)(progress * progressBarLength);
                    string bar = new string('#', filledLength) + new string('-', progressBarLength - filledLength);
                    Console.Write($"\r[INFO] Download progress: [{bar}] {progress * 100:F2}%");
                }
                else
                {
                    Console.Write($"\r[INFO] Downloaded: {totalRead} bytes");
                }
            }

            Console.WriteLine(); // new line
            log.Info("system", $"[INFO] Download completed: {fileName}");
        }

        // Compute SHA256 hash
        private static string ComputeFileHash(string path)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // DTO
        private class FileInfoDto
        {
            public string fileName { get; set; } = string.Empty;
            public string hash { get; set; } = string.Empty;
        }
    }
}
