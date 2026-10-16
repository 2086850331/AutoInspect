using AutoInspect.Helper;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoInspect.Core
{
    public class ElectricBoxAlarm(LogHelper log, AppConfigura appConfigura) : IJob
    {
        public int Step { get; set; } = 1;

        public TaskEnum Id => TaskEnum.ElectricBoxAlarm;

        public async Task Invoke()
        {
            try
            {
                string processName = "ElectricBoxAlarmSystem";
                string exePath = appConfigura.ElectricBoxAlarmSystem;
                if (!string.IsNullOrWhiteSpace(exePath) && !File.Exists(exePath))
                {
                    Error(exePath);
                    return;
                }

                var procs = Process.GetProcessesByName(processName);
                if (procs == null || procs.Length == 0)
                {
                    Error($"未找到进程: {processName}");
                    StartProcess();
                    await Task.Delay(10000);
                    await Invoke();
                    return;
                }

                foreach (Process proc in procs)
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
                        List<string> abnormalText = new List<string>() { "異常", "連接失敗" };
                        List<string> abnormal = new();
                        List<string> offline = new();
                        foreach (var richEdit in richEdits)
                        {
                            string plain = SysApi.GetRichTextBoxPlainText(richEdit);
                            var plainArray = plain.Split('\r');
                            abnormal.AddRange(plainArray.Where(x => abnormalText.Any(y => x.Contains(y))));
                            offline.AddRange(plainArray.Where(x => x.Contains("已離線")));
                        }

                        abnormal.ForEach(x => Error(x));
                        if (offline.Any() && !abnormal.Any() && Step == 1)
                        {
                            Info("电箱存在离线设备，但未发现异常。重启程序");
                            Info($"正在关闭进程 {proc.ProcessName} (PID={proc.Id})...");
                            proc.Kill();
                            proc.WaitForExit(3000);
                            await Task.Delay(500);
                            StartProcess();
                            Step++;
                            await Task.Delay(5000);
                            await Invoke();
                        }
                    }
                    catch (Exception exProc)
                    {
                        Error($"处理进程 {proc.Id} 时异常: {exProc}");
                    }
                }
            }
            catch (Exception ex)
            {
                Error($"Invoke 出错: {ex}");
            }
        }

        private bool StartProcess()
        {
            string exePath = appConfigura.ElectricBoxAlarmSystem;
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
        private void Info(string msg)
        {
            log.Info("ElectricBoxAlarmSystem_TCP", msg);
        }

        private void Error(string msg)
        {
            log.Error("ElectricBoxAlarmSystem_TCP", msg);
        }
    }
}
