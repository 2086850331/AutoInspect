using System.ComponentModel;
using System.Reflection;

namespace Service.Client.UploadFileMerge
{
    public class BittMap
    {
        public static byte MapOldToNew(byte oldValue)
        {
            // 新顺序映射表
            var orderList = new Dictionary<string, int>
        {
            { "布防", 0},
            { "直播",1},
            { "电箱",2},
            { "出入口",3},
            { "网站",4 },
            { "API",5},
            { "硬盘空间（C或D盘）",6},
            { "性能（CPU）",6},
            { "性能（内存）",6},
            { "服务器访问IP",7}
        };

            byte result = 0;

            foreach (TaskEnum item in Enum.GetValues(typeof(TaskEnum)))
            {
                string desc = GetDescription(item);

                bool isEnabled = ((byte)item & oldValue) != 0;
                if (!isEnabled) continue;

                int newIndex = desc switch
                {
                    "布防监控" => orderList["布防"],
                    "多摄像头" => orderList["直播"],
                    "电箱" => orderList["电箱"],
                    "出入口" => orderList["出入口"],
                    "IIS Web/Api错误扫描" => orderList["网站"],
                    "磁盘空间和系统环境" => orderList["硬盘空间（C或D盘）"],
                    "系统任务" => orderList["API"],
                    "DDOS分析" => orderList["服务器访问IP"],
                    _ => -1
                };

                if (newIndex >= 0)
                    result |= (byte)(1 <<  (7 - newIndex));
            }

            return result;
        }

        private static string GetDescription(Enum value)
        {
            return value.GetType()
                        .GetField(value.ToString())
                        .GetCustomAttribute<DescriptionAttribute>()?
                        .Description ?? value.ToString();
        }
    }
}
