using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Client.UploadFileMerge
{
    [Flags]
    public enum TaskEnum
    {
        /// <summary>
        /// IIS Web/Api错误扫描
        /// </summary>
        [Description("IIS Web/Api错误扫描")]
        IIS = 0b10000000,

        /// <summary>
        /// 布防监控
        /// </summary>
        [Description("布防监控")]
        AlertMonitoring = 0b01000000,

        /// <summary>
        /// 磁盘空间和系统环境
        /// </summary>
        [Description("磁盘空间和系统环境")]
        DiskAndEnvironment = 0b00100000,

        /// <summary>
        /// DDOS分析
        /// </summary>
        [Description("DDOS分析")]
        Network = 0b00010000,

        /// <summary>
        /// 电箱
        /// </summary>
        [Description("电箱")]
        ElectricBoxAlarm = 0b00001000,

        /// <summary>
        /// 多摄像头
        /// </summary>
        [Description("多摄像头")]
        CameraMultipleFormsPlaySystem = 0b00000100,

        /// <summary>
        /// 出入口
        /// </summary>
        [Description("出入口")]
        SerialPortReader = 0b00000010,

        /// <summary>
        /// 系统任务
        /// </summary>
        [Description("系统任务")]
        SystemTask = 0b00000001
    }
}
