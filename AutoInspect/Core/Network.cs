using AutoInspect.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoInspect.Core
{
    public class Network(LogHelper log) : IJob
    {
        public TaskEnum Id => TaskEnum.Network;

        public async Task Invoke()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var netstatOutput = RunNetstat();

            var connections = ParseNetstat(netstatOutput);

            Info($"总连接数: {connections.Count}");

            AnalyzeConnections(connections);

            await Task.CompletedTask;
        }

        // 调用 netstat -nao
        static string[] RunNetstat()
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-nao",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process proc = Process.Start(psi);
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            return output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        }

        // 解析 netstat 输出为对象
        class Connection
        {
            public string Protocol { get; set; } = null!;
            public string LocalAddress { get; set; } = null!;
            public string ForeignAddress { get; set; } = null!;
            public string State { get; set; } = null!;
            public int PID { get; set; }
        }

        List<Connection> ParseNetstat(string[] lines)
        {
            var connections = new List<Connection>();
            var regex = new Regex(@"^(TCP|UDP)\s+([\d\.:]+)\s+([\d\.:]+)\s+(\S+)?\s*(\d+)?$", RegexOptions.IgnoreCase);

            foreach (var line in lines)
            {
                var match = regex.Match(line.Trim());
                if (match.Success)
                {
                    connections.Add(new Connection
                    {
                        Protocol = match.Groups[1].Value,
                        LocalAddress = match.Groups[2].Value,
                        ForeignAddress = match.Groups[3].Value,
                        State = match.Groups[4].Success ? match.Groups[4].Value : "",
                        PID = match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : 0
                    });
                }
            }

            return connections;
        }

        // 简单分析连接，找出可能异常
        void AnalyzeConnections(List<Connection> connections)
        {
            var foreignIPCounts = connections
                .GroupBy(c => c.ForeignAddress.Split(':')[0])
                .Select(g => new { IP = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(100);

            Info("连接最多的外部 IP:");
            foreach (var item in foreignIPCounts)
            {
                Info($"{item.IP} - {item.Count} 次连接");
            }

            var suspiciousStates = connections
                .Where(c => c.State == "TIME_WAIT" || c.State == "CLOSE_WAIT")
                .GroupBy(c => c.ForeignAddress.Split(':')[0])
                .Select(g => new { IP = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(10);

            Error("可能异常的连接状态 (TIME_WAIT / CLOSE_WAIT):");
            foreach (var item in suspiciousStates)
            {
                Error($"{item.IP} - {item.Count} 次连接");
            }

            foreach (var item in foreignIPCounts)
            {
                if (item.Count > 50) // 超过50次连接可以认为有风险
                {
                    Error($"[警告] {item.IP} 连接次数异常，可能存在攻击");
                }
            }
        }

        private void Info(string msg)
        {
            log.Info("Network", msg);
        }

        private void Error(string msg)
        {
            log.Error("Network", msg);
        }
    }
}
