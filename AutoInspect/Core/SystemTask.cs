using AutoInspect.Helper;
using Dapper;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoInspect.Core
{
    public class SystemTask(AppConfigura configura, LogHelper log) : IJob
    {
        private readonly SqlConnection conn = new SqlConnection(configura.DbConnectionString!);
        public TaskEnum Id => TaskEnum.SystemTask;

        public async Task Invoke()
        {
            const string sql = $@"WITH LatestLogs AS
            (
                SELECT 
                    tt.F_Info AS Info,
                    tt.F_Status AS Status,
                    t.F_TaskName AS Name,
                    tt.F_CreateDate,
                    ROW_NUMBER() OVER(PARTITION BY t.F_TaskName ORDER BY tt.F_CreateDate DESC) AS rn
                FROM dbo.TB_TaskLog tt
                INNER JOIN dbo.TB_Task t ON t.F_TaskId = tt.F_TaskId
                WHERE tt.F_CreateDate >= DATEADD(DAY, -7, GETDATE())
            )
            , Top10PerTask AS
            (
                SELECT *
                FROM LatestLogs
                WHERE rn <= 10
            )
            , TaskCheck AS
            (
                SELECT Name
                FROM Top10PerTask
                GROUP BY Name
                HAVING SUM(CASE WHEN Status <> 0 THEN 1 ELSE 0 END) = 0
            )
            , LatestInfo AS
            (
                SELECT Name, Info, F_CreateDate,
                       ROW_NUMBER() OVER(PARTITION BY Name ORDER BY F_CreateDate DESC) AS rn2
                FROM Top10PerTask
                WHERE Name IN (SELECT Name FROM TaskCheck)
            )
            SELECT Name, Info, 0
            FROM LatestInfo
            WHERE rn2 = 1
            ORDER BY Name;
            ";
            await conn.OpenAsync();
            var result = await conn.QueryAsync<TaskLogItem>(sql);
            conn.Close();
            if (result != null && result.Count() > 0)
            {
                foreach (var item in result)
                {
                    Error($"任务名称: {item.Name}, 状态: {item.Status}, 信息: {item.Info}");
                }
            }
            else
            {
                Info("最近7天内没有失败的任务日志。");
            }
        }

        private void Info(string msg) => log.Info("SystemTask", msg);
        private void Error(string msg) => log.Error("SystemTask", msg);
    }

    public record TaskLogItem(string Info, int Status, string Name) { 
        public TaskLogItem():this(string.Empty, default(int), string.Empty) { }
    }
}
