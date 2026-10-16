using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Client.Helper
{
    public class LogHelper
    {
        public readonly string _logFile;
        private readonly object _lock = new object();

        public LogHelper(string logDirectory = "Logs")
        {
            Directory.CreateDirectory(logDirectory);
            _logFile = Path.Combine(logDirectory, $"log_{DateTime.Now:yyyyMMdd_HH}.txt");
            if (!Directory.Exists(Path.GetDirectoryName(_logFile)))
                Directory.CreateDirectory(Path.GetDirectoryName(_logFile)!);
            if (!File.Exists(_logFile))
                File.Create(_logFile).Dispose();
        }

        public void WriteLog(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logLine = $"[{timestamp}] {message}";

            lock (_lock)
            {
                // 写入 HTML 模板
                File.AppendAllText(_logFile, logLine + Environment.NewLine);
            }
        }
    }

}
