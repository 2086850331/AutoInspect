using AutoInspect.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoInspect.Core
{
    public class SerialPortReader : IJob
    {
        private readonly LogHelper log;
        private readonly AppConfigura configura;

        public SerialPortReader(LogHelper log, AppConfigura configura)
        {
            this.log = log;
            this.configura = configura;
        }

        public TaskEnum Id => TaskEnum.SerialPortReader;

        public async Task Invoke()
        {
            const string processName = "SerialPortReader";
            string exePath = configura.ElectricBoxAlarmSystem;
            if (!string.IsNullOrWhiteSpace(exePath) && !File.Exists(exePath))
            {
                Error(exePath);
                await Task.CompletedTask;
                return;
            }
            //var procs = Process.GetProcessesByName(processName);
            //await CheckProcessAsync(procs);
        }

        private async Task CheckProcessAsync(Process[] processes)
        {
            foreach (var proc in processes)
            {
                try
                {
                    Info($"检测到进程 PID={proc.Id}, 名称={proc.ProcessName}");
                    IntPtr mainWnd = SysApi.FindTopLevelWindowByPid(proc.Id);
                    if (mainWnd == IntPtr.Zero)
                    {
                        Error($"未找到进程 {proc.Id} 的顶层窗口句柄。");
                        continue;
                    }

                    var listViews = SysApi.FindListView(mainWnd);
                    if (listViews.Count == 0)
                    {
                        Error($"在窗口 0x{mainWnd.ToInt64():X} 中未找到 ListView 子控件。");
                        continue;
                    }

                    List<string> abnormalText = new List<string>() { "異常", "失敗" };
                    List<string> matched = new();

                    foreach (var lv in listViews)
                    {
                        var texts = SysApi.GetListViewFRemarkTexts(lv);
                        if (texts == null || texts.Count == 0) continue;

                        foreach (var t in texts)
                        {
                            if (abnormalText.Any(k => t.Contains(k)))
                            {
                                matched.Add(t);
                            }
                        }
                    }

                    matched.ForEach(x => Error(x));
                }
                catch (Exception exProc)
                {
                    Error($"处理进程 {proc.Id} 时异常: {exProc}");
                }
            }
            await Task.CompletedTask;
        }

        private void Info(string msg)
        {
            log.Info("SerialPortReader", msg);
        }

        private void Error(string msg)
        {
            log.Error("SerialPortReader", msg);
        }
    }
}
