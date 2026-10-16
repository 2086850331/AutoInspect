using AutoInspect.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoInspect.Core
{
    public class DiskAndSystemEnvironment(LogHelper log) : IJob
    {
        public TaskEnum Id => TaskEnum.DiskAndEnvironment;

        public async Task Invoke()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                Info($"{drive.Name.Split(':', 2)[0]}盘空间剩余: {UtilityHelper.FormatSize(drive.AvailableFreeSpace)}; 总容量: {UtilityHelper.FormatSize(drive.TotalSize)}");
            }

            double cpuUsage = UtilityHelper.GetCpuUsage();
            Info($"CPU 占用: {cpuUsage:0.0}%");

            var memUsage = UtilityHelper.GetMemoryUsage();
            Info($"内存占用: {memUsage.used / 1024 / 1024} MB / {memUsage.total / 1024 / 1024} MB ({(double)memUsage.used / memUsage.total:P1})");

            Console.WriteLine();
            await Task.CompletedTask;
        }

        private void Info(string msg)
        {
            log.Info("DiskEnvivronment", msg);
        }

        private void Error(string msg)
        {
            log.Error("DiskEnvivronment", msg);
        }
    }
}
