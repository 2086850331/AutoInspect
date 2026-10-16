using AutoInspect.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoInspect.Core
{
    public class CameraMultiple(LogHelper log, AppConfigura configura) : IJob
    {
        private readonly JsonDocumentOptions options = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        public TaskEnum Id => TaskEnum.CameraMultipleFormsPlaySystem;

        public async Task Invoke()
        {
            try
            {
                const string processName = "CameraMultipleFormsPlaySystem";
                string exePath = configura.CameraMultipleFormsPlaySystem;
                if (!string.IsNullOrWhiteSpace(exePath) && !File.Exists(exePath))
                {
                    Error(exePath);
                    return;
                }

                string cfgPath = Path.Combine(Path.GetDirectoryName(exePath)!, "PushFlowConfig.json");
                if (!File.Exists(cfgPath))
                {
                    Error($"未找到配置文件：{cfgPath}");
                    return;
                }

                using var fs = File.OpenRead(cfgPath);
                using var doc = JsonDocument.Parse(fs, options);
                if (!doc.RootElement.TryGetProperty("CameraConfigList", out var listElem) || listElem.ValueKind != JsonValueKind.Array)
                {
                    Error("配置文件中未找到 CameraConfigList 或格式不正确。");
                    return;
                }

                foreach (var camElem in listElem.EnumerateArray())
                {
                    try
                    {
                        string ip = camElem.GetProperty("TitleName").GetString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(ip)) continue;

                        bool outside = false;
                        if (camElem.TryGetProperty("OutsideCamera", out var outsideElem) && outsideElem.ValueKind == JsonValueKind.True)
                            outside = true;

                        string processNameToCheck;
                        if (outside)
                        {
                            processNameToCheck = "python";
                        }
                        else
                        {
                            processNameToCheck = processName;
                        }

                        var procs = Process.GetProcessesByName(processNameToCheck);
                        bool foundWindow = false;
                        foreach (var proc in procs)
                        {
                            try
                            {
                                IntPtr hwnd = FindWindowByPidAndTitle(proc.Id, ip);
                                if (hwnd == IntPtr.Zero) continue;
                                foundWindow = true;

                                using var bmp = CaptureWindowBitmap(hwnd);
                                if (bmp == null)
                                {
                                    Error($"摄像头 {ip}：找到窗口但截屏失败。");
                                    break;
                                }

                                bool noVideo = IsImageLikelyNoVideo(bmp);
                                if (noVideo)
                                {
                                    Error($"摄像头 {ip}：窗口存在但未接收到画面（截图分析）。");
                                }
                                else
                                {
                                    Info($"摄像头 {ip}：健康（窗口存在且有画面）。");
                                }

                                break; // 已找到匹配窗口并处理，跳出进程循环
                            }
                            catch (Exception exProcWindow)
                            {
                                Error($"处理进程 {proc.ProcessName} (PID={proc.Id}) 下摄像头 {ip} 时异常: {exProcWindow.Message}");
                            }
                        }

                        if (!foundWindow)
                        {
                            Error($"摄像头 {ip}：未在进程 '{processNameToCheck}' 下找到标题为 \"{ip}\" 的窗口。");
                        }
                    }
                    catch (Exception exItem)
                    {
                        Error($"处理 CameraConfigList 项异常: {exItem.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Error($"CameraMultiple 执行异常: {ex}");
            }

            await Task.CompletedTask;
        }

        private void Info(string msg) => log.Info("CameraMultipleFormsPlaySystem_Nginx", msg);
        private void Error(string msg) => log.Error("CameraMultipleFormsPlaySystem_Nginx", msg);

        // ----- 窗口标题查找 -----
        private IntPtr FindWindowByPidAndTitle(int pid, string title)
        {
            IntPtr found = IntPtr.Zero;
            SysApi.EnumWindows(new SysApi.EnumWindowsProc((IntPtr hWnd, IntPtr lParam) =>
            {
                try
                {
                    if (!SysApi.IsWindowVisible(hWnd)) return true;
                    _ = SysApi.GetWindowTextLength(hWnd); // 保证 API 可调用
                    _ = SysApi.GetWindowThreadProcessId(hWnd, out uint windowPid);
                    if ((int)windowPid != pid) return true;

                    // 获取窗口标题
                    int len = SysApi.GetWindowTextLength(hWnd);
                    if (len <= 0) return true;
                    var sb = new System.Text.StringBuilder(len + 1);
                    SysApi.SendMessage(hWnd, SysApi.WM_GETTEXT, new IntPtr(sb.Capacity), sb);
                    string wndTitle = sb.ToString();
                    if (string.Equals(wndTitle, title, StringComparison.OrdinalIgnoreCase))
                    {
                        found = hWnd;
                        return false; // 停止枚举
                    }
                }
                catch { }
                return true;
            }), IntPtr.Zero);

            return found;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;

            public override string ToString() => $"{{Left={Left}, Top={Top}, Right={Right}, Bottom={Bottom}}}";
        }

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h,
                                  IntPtr hdcSrc, int xSrc, int ySrc, CopyPixelOperation rop);

        private Bitmap? CaptureWindowBitmap(IntPtr hwnd)
        {
            if (!GetWindowRect(hwnd, out RECT rect)) return null;
            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;

            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr hdcDest = g.GetHdc();
                IntPtr hdcSrc = GetWindowDC(hwnd);
                BitBlt(hdcDest, 0, 0, w, h, hdcSrc, 0, 0, CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt);
                g.ReleaseHdc(hdcDest);
                ReleaseDC(hwnd, hdcSrc);
            }
            return bmp;
        }

        private bool IsImageLikelyNoVideo(Bitmap bmp)
        {
            try
            {
                int w = bmp.Width;
                int h = bmp.Height;
                // 采样步长，避免逐像素处理过慢
                int stepX = Math.Max(1, w / 100);
                int stepY = Math.Max(1, h / 100);

                double sum = 0;
                double sumSq = 0;
                long count = 0;

                for (int y = 0; y < h; y += stepY)
                {
                    for (int x = 0; x < w; x += stepX)
                    {
                        Color c = bmp.GetPixel(x, y);
                        // 灰度
                        double g = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                        sum += g;
                        sumSq += g * g;
                        count++;
                    }
                }

                if (count == 0) return true;
                double mean = sum / count;
                double variance = (sumSq / count) - (mean * mean);
                // 若灰度方差非常小，说明画面几乎静止或纯色（可能无视频）
                // 阈值可根据实际情况调整；此处用经验值
                return variance < 250.0;
            }
            catch
            {
                // 分析失败时保守判断为无画面
                return true;
            }
        }
    }
}
