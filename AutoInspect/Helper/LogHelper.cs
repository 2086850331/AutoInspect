using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoInspect.Helper
{
    public class LogHelper
    {
        public readonly string _logFile;
        private readonly object _lock = new object();

        public LogHelper(AppConfigura configuration, string logDirectory = "Logs")
        {
            Directory.CreateDirectory(logDirectory);
            _logFile = Path.Combine(logDirectory, $"{configuration.SystemName}_log_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            File.Copy("Files/Template/log.html", _logFile);
        }

        public void Info(string moduleId, string message)
            => WriteLog(moduleId, "INFO", message, ConsoleColor.White);

        public void Error(string moduleId, string message)
            => WriteLog(moduleId, "ERROR", message, ConsoleColor.Red);

        private void WriteLog(string moduleId, string level, string message, ConsoleColor color)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logLine = $"{timestamp} [{level}] {message}";

            // 控制台输出
            Console.ForegroundColor = color;
            Console.WriteLine(logLine);
            Console.ResetColor();

            lock (_lock)
            {
                // 写入 HTML 模板
                AppendHtmlLog(moduleId, timestamp, level, message);
            }
        }

        private void AppendHtmlLog(string moduleId, string time, string level, string message)
        {
            if (!File.Exists(_logFile)) return; // 模板不存在则跳过

            string html = File.ReadAllText(_logFile);
            string row = $"<tr class='{level}'><td class='time'>{time}</td><td class='level'>{level}</td><td class='message{level}'>{System.Net.WebUtility.HtmlEncode(message)}</td></tr>";

            // 定位模块 tbody
            string marker = $"<div class=\"module\" id=\"{moduleId}\">";
            int tbodyIndex = html.IndexOf($"<div class=\"module\" id=\"{moduleId}\">");
            if (tbodyIndex == -1) return;

            int insertPos = html.IndexOf("</tbody>", tbodyIndex);
            if (insertPos == -1) return;

            html = html.Insert(insertPos, row);
            File.WriteAllText(_logFile, html);
        }
    }

}
