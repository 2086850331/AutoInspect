using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoInspect.Helper
{
    public class AppConfigura(IConfiguration configuration)
    {
        public string AlarmMonitoringSystem = configuration.GetValue<string>("AlarmMonitoringSystem") ?? throw new Exception("未配置【AlarmMonitoringSystem】");
        public string ElectricBoxAlarmSystem = configuration.GetValue<string>("ElectricBoxAlarmSystem_TCP") ?? throw new Exception("未配置【ElectricBoxAlarmSystem_TCP】");
        public string CameraMultipleFormsPlaySystem = configuration.GetValue<string>("CameraMultipleFormsPlaySystem_Nginx") ?? throw new Exception("未配置【CameraMultipleFormsPlaySystem_Nginx】");
        public string SerialPortReader = configuration.GetValue<string>("SerialPortReader_Branch") ?? throw new Exception("未配置【SerialPortReader_Branch】");
        public string SerialPortSystemTaskReader = configuration.GetValue<string>("SystemTask") ?? throw new Exception("未配置【SystemTask】");
        public string DbConnectionString = configuration.GetValue<string>("DbConnectionString") ?? throw new Exception("未配置【DbConnectionString】");
        public string? ExecuteTask = configuration.GetValue<string>("ExecuteTask");
        public string? SystemName = configuration.GetValue<string>("SystemName");
        public string? ServiceHost = configuration.GetValue<string>("ServiceHost");
    }
}
