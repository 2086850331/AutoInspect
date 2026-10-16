using AutoInspect.Helper;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoInspect.Core
{
    public class IISInspect(LogHelper log) : IJob
    {
        private readonly List<string> AbnormalTextList = new List<string>() { "異常", "异常", "中止", "失敗", "失败" };
        public TaskEnum Id => TaskEnum.IIS;

        public async Task Invoke()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            using var serverManager = new ServerManager();
            var apps = serverManager.Sites
                .SelectMany(site => site.Applications)
                .Where(app => app.Path != "/" && (app.Path.Contains("_Web", StringComparison.OrdinalIgnoreCase)
                                               || app.Path.Contains("_Api", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (apps.Count == 0)
            {
                Info("未发现包含 _Web 或 _Api 的应用程序。");
                return;
            }

            foreach (var app in apps)
            {
                string physicalPath = app.VirtualDirectories["/"]?.PhysicalPath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(physicalPath))
                    continue;

                Info($"检查应用：{app.Path}  ({physicalPath})");

                string fileManagePath = Path.Combine(physicalPath, "FileManage");
                var fileManageFiles = Directory.GetDirectories(fileManagePath);
                for (int i = 0; i < 7; i++)
                {

                    #region Logs部分
                    DateTime date = DateTime.Today.AddDays(-i);
                    string logPath = Path.Combine(physicalPath, "Log", date.ToString("yyyy-MM-dd"), "Error.log");
                    Info($"检查{date.ToString("yyyy-MM-dd")}的Logs：{logPath}");


                    if (Directory.Exists(Path.GetDirectoryName(logPath)) && File.Exists(logPath))
                    {
                        string cpLogPath = Path.Combine(physicalPath, "Log", date.ToString("yyyy-MM-dd"), "Error.cp");
                        File.Copy(logPath, cpLogPath);

                        string content = File.ReadAllText(cpLogPath).Trim();
                        if (!string.IsNullOrEmpty(content))
                        {
                            Error($"{date:yyyy-MM-dd} 的日志文件：{cpLogPath}:[{(content.Length > 5000 ? content.Substring(0, 5000) + "...(内容过长已截断)..." : content)}]");
                        }
                        File.Delete(cpLogPath);
                    }
                    #endregion

                    #region FileManage部分
                    foreach (var fileBase in fileManageFiles)
                    {
                        string? fileManageFilePath = Directory.GetFiles(fileBase).Where(x => x.Contains(date.ToString("yyyyMMdd"))).FirstOrDefault();
                        if (fileManageFilePath != null)
                        {
                            string cpLogPath = Path.Combine(fileManageFilePath) + ".cp";
                            File.Copy(fileManageFilePath, cpLogPath);

                            var content = await File.ReadAllLinesAsync(cpLogPath);
                            var abnormalContent = content.Where(x => AbnormalTextList.Any(y => x.Contains(y))).Take(20);
                            if (abnormalContent.Count() > 0)
                            {
                                Error($"{date:yyyy-MM-dd} 的日志文件：{cpLogPath}:[{string.Join(";<br>", abnormalContent)}]");
                            }
                            File.Delete(cpLogPath);
                        }
                    }
                    #endregion

                }

                Console.WriteLine();
            }

            Info("检查完成。");
            await Task.CompletedTask;
        }

        private void Info(string msg)
        {
            log.Info("IIS", msg);
        }

        private void Error(string msg)
        {
            log.Error("IIS", msg);
        }
    }
}
