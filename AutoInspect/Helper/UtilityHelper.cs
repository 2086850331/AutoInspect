using Hardware.Info;
using Microsoft.TeamFoundation.Common;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoInspect.Helper
{
    public class UtilityHelper
    {
        public static string? FindInstallPathFromRegistry(string displayName)
        {
            string[] registryRoots =
            {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

            foreach (var root in registryRoots)
            {
                using var key = Registry.LocalMachine.OpenSubKey(root);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    using var sub = key.OpenSubKey(subKeyName);
                    var name = sub?.GetValue("DisplayName") as string;
                    var installLoc = sub?.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(name) && name.Contains(displayName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(installLoc))
                        {
                            string path = Path.Combine(installLoc, $"{displayName}.exe");
                            return path;
                        }
                    }
                }
            }

            return null;
        }
        public static void StartProcess(string exePath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });
        }

        // 格式化容量
        public static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        // 获取 CPU 占用（快照）
        public static double GetCpuUsage()
        {
            var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            cpuCounter.NextValue(); // 第一次调用总是 0
            Thread.Sleep(1000);
            float cpuUsage = cpuCounter.NextValue();
            return cpuUsage;
        }

        // 获取内存占用
        public static (ulong used, ulong total, ulong available) GetMemoryUsage()
        {
            var hi = new HardwareInfo();
            hi.RefreshMemoryStatus();

            ulong total = hi.MemoryStatus.TotalPhysical;
            ulong available = hi.MemoryStatus.AvailablePhysical;
            ulong used = total - available;
            return (used, total, available);
        }

    }
}
