using AutoInspect.Helper;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoInspect.Core
{
    public class AlertMonotoring(LogHelper log, AppConfigura configura) : IJob
    {
        public int Step { get; set; } = 1;

        public TaskEnum Id => TaskEnum.AlertMonitoring;

        public async Task Invoke()
        {
            #region 布防
            const string processName = "AlarmMonitoringSystem";
            string? exePath = configura.AlarmMonitoringSystem;
            if(!string.IsNullOrWhiteSpace(exePath) && !File.Exists(exePath))
            {
                Error(exePath);
                return;
            }

            // 获取最新进程
            var procs = Process.GetProcessesByName(processName);
            await CheckProcessAsync(procs);
            #endregion
        }

        private async Task CheckProcessAsync(Process[] processes)
        {
            if (!processes.Any())
            {
                StartProcess();
                await Task.Delay(10000);
                await Invoke();
            }
            else
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

                        var richEdits = SysApi.FindRichTextBox(mainWnd);
                        if (richEdits.Count == 0)
                        {
                            Error($"在窗口 0x{mainWnd.ToInt64():X} 中未找到 RichEdit 子控件。");
                            continue;
                        }

                        // 使用 WM_GETTEXT
                        List<string> abnormalText = new List<string>() { "異常", "失敗" };
                        foreach (var richEdit in richEdits)
                        {
                            string plain = SysApi.GetRichTextBoxPlainText(richEdit);
                            var plainArray = plain.Split('\r');
                            plainArray.Where(x => abnormalText.Any(y => x.Contains(y))).ForEach(x => Error(x));
                            plainArray.Where(x => x.Contains("斷開")).ForEach(x => Error(x));
                        }

                        if (Step == 1)
                        {
                            Info($"正在关闭进程 {proc.ProcessName} (PID={proc.Id})...");
                            proc.Kill();
                            proc.WaitForExit(3000);
                            await Task.Delay(500);
                            StartProcess();
                            await Task.Delay(10000);
                            Step++;
                            await Invoke();
                        }
                    }
                    catch (Exception exProc)
                    {
                        Error($"处理进程 {proc.Id} 时异常: {exProc}");
                    }
                }
            }
        }

        private void Info(string msg)
        {
            log.Info("AlarmMonitoringSystem", msg);
        }

        private void Error(string msg)
        {
            log.Error("AlarmMonitoringSystem", msg);
        }

        private bool StartProcess()
        {
            string exePath = configura.AlarmMonitoringSystem;
            if (System.IO.File.Exists(exePath))
            {
                Info($"重新启动：{exePath}");
                UtilityHelper.StartProcess(exePath);
                return true;
            }
            else
            {
                Error("未找到可执行文件，请确认程序是否已安装。");
            }
            return false;
        }
    }
}
