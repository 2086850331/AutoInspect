using AutoInspect;
using AutoInspect.Core;
using AutoInspect.Helper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel;
using System.Reflection;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) => {
                services.AddSingleton<AppConfigura>();
                services.AddSingleton<AppUpdate>();
                services.AddSingleton<LogHelper>();
                var jobTypes = Assembly.GetExecutingAssembly().GetTypes()
                    .Where(t => typeof(IJob).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);
                foreach (var type in jobTypes)
                {
                    services.AddScoped(typeof(IJob), type);
                }
            });

var build = host.Build();
LogHelper log = build.Services.GetRequiredService<LogHelper>();
AppConfigura configura = build.Services.GetRequiredService<AppConfigura>();
AppUpdate appUpdate = build.Services.GetRequiredService<AppUpdate>();
build.StartAsync();

await appUpdate.RunUpdaterAndExitAsync();

// 获取所有枚举字段
var fields = typeof(TaskEnum).GetFields(BindingFlags.Public | BindingFlags.Static);

// 按值从大到小排序
var sortedFields = fields
    .Select(f => new
    {
        Name = f.Name,
        Value = (int)f.GetValue(null)!,
        Description = f.GetCustomAttribute<DescriptionAttribute>()?.Description ?? f.Name
    })
    .OrderByDescending(f => f.Value);

log.Info("system","请选择要执行的功能：");
// 输出到控制台
foreach (var item in sortedFields)
{
    log.Info("system", $"{item.Value} = {item.Name} ({item.Description})");
}
string executeValue = configura.ExecuteTask ?? Console.ReadLine() ?? string.Empty;
log.Info("system", $"输入值：{executeValue}");

// 转成 int
if (!int.TryParse(executeValue, out int number))
{
    Console.WriteLine("输入非法");
    return;
}

// 转二进制表示
executeValue = new string(executeValue.Where(c => c == '0' || c == '1').ToArray());
string binary = "0b" + executeValue;
Console.WriteLine($"二进制表示: {binary}");
number = Convert.ToInt32(executeValue, 2);

// 转成枚举类型
TaskEnum selectedItems = (TaskEnum)number;

var jobs = build.Services.GetServices<IJob>();

foreach (var job in jobs)
{
    try
    {
        // 判断是否包含用户选择的任务
        if ((job.Id & selectedItems) != 0)
        {
            log.Info("system", $"执行 {job.GetType().FullName}");
            await job.Invoke();
        }
        else
        {
            log.Info("system", $"跳过 {job.GetType().FullName}");
        }
    }
    catch (Exception ex)
    {
        log.Error("system", $"执行 {job.GetType().FullName} 失败; 错误信息:{ex.ToString()}");
    }
}
log.Info("system", "按任意键退出...");

try
{

    string? serviceHost = configura.ServiceHost;
    if (!string.IsNullOrWhiteSpace(serviceHost))
    {
        await UploadClient.UploadFileAsync($"http://{serviceHost}/upload", log);
    }
    log.Info("system", "日志上传完成。");
}
catch (Exception e)
{
    log.Error("system", $"Failura to file upload：{e.Message}");
}