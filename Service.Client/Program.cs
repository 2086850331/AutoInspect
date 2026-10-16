using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using OfficeOpenXml;
using Service.Client;
using Service.Client.Helper;
using Service.Client.UploadFileMerge;
using SixLabors.ImageSharp;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://*:5031");

// Add services to the container.

// 配置：限制 multipart 大小（防御性设置，真正的大小校验在代码里）
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5_000_000 * 10; // 防止客户端一次上传超大数据（字节）
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder => builder
            .SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped<LogHelper>();
builder.Services.AddScoped<MergeService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

app.MapOpenApi();
//app.UseHttpsRedirection();
app.UseCors("AllowAll");//启用Cors
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// 设置许可证为免费的非商业许可证
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

const int MaxBytes = 50 * 1024 * 1024; // 最大接收字节50M
DateTime RateLimitWindow = DateTime.Now.AddHours(-6);
var basePath = Path.Combine(AppContext.BaseDirectory, "Files", "Update");
Directory.CreateDirectory(basePath);
var cache = app.Services.GetRequiredService<IMemoryCache>();

app.MapPost("/upload", async (HttpRequest request, IConfiguration configuration, IMemoryCache cache, LogHelper log, MergeService merge) => {
    try
    {
        log.WriteLog($"[{request.HttpContext.Connection.RemoteIpAddress}]:接收到上传请求;");

        // 获取客户端地址（优先 X-Forwarded-For）
        string? remoteIp = request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (string.IsNullOrEmpty(remoteIp))
        {
            remoteIp = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
        // 若 X-Forwarded-For 带有多个 IP, 取第一个
        if (remoteIp?.Contains(',') == true)
        {
            remoteIp = remoteIp.Split(',')[0].Trim();
        }

        // 速率限制检查
        //cache.TryGetValue<List<RequestModel>>(cacheRequestKey, out List<RequestModel>? requests);
        //requests = requests ?? new List<RequestModel>();
        //if (requests.Count(x => x.ip == remoteIp && x.timestamp >= RateLimitWindow) > 100)
        //{
        //    return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        //}

        // 读取表单
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { success = false, message = "请求必须为 multipart/form-data。" });
        }

        var form = await request.ReadFormAsync();
        var file = form.Files.FirstOrDefault();
        if (file == null)
        {
            return Results.BadRequest(new { success = false, message = "未包含文件字段，请在 multipart 的 file 字段上传。" });
        }

        // 大小检查
        if (file.Length > MaxBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        log.WriteLog($"[{request.HttpContext.Connection.RemoteIpAddress}]:接收到上传请求; 文件大小:{file.Length}");

        // 读取内容
        string tempFolder = configuration.GetValue<string>("TempPath") ?? "Template";
        Directory.CreateDirectory(tempFolder);

        // 生成唯一文件名
        string fileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        string savePath = Path.Combine(tempFolder, fileName);
        using (var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(fs);
        }

        //设置速率限制记录
        //requests.Add(new RequestModel (remoteIp!, DateTime.Now));
        //cache.Set(cacheRequestKey, requests.Where(x => x.timestamp >= RateLimitWindow).ToList());

        await merge.HandlerAsync(tempFolder);
        return Results.Ok(new { success = true, message = "上传成功", saved = savePath });
    }
    catch (Exception ex)
    {
        // 服务器内部错误
        return Results.Problem(detail: ex.ToString(), statusCode: StatusCodes.Status500InternalServerError);
    }

});
app.MapGet("/file/merge", async (MergeService merge, IConfiguration configuration) => await merge.HandlerAsync(configuration.GetValue<string>("TempPath") ?? "Template"));
app.MapGet("/get", () => DateTime.Now);
app.MapGet("/get/file/list", () =>
{
    var files = Directory.GetFiles(basePath);
    var list = files.Select(file =>
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(file);
        var hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        return new
        {
            FileName = Path.GetFileName(file),
            Hash = hash
        };
    });
    return Results.Json(list);
});
app.MapGet("/get/file/download", (HttpContext context, string fileName) =>
{
    var filePath = Path.Combine(basePath, fileName);
    if (!System.IO.File.Exists(filePath))
        return Results.NotFound("文件不存在");

    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var fileKey = $"download:{fileName}";
    var ipKey = $"download:{fileName}:{ip}";

    if (cache.TryGetValue(ipKey, out _))
        return Results.BadRequest("该IP在4小时内已下载过此文件");

    var todayKey = $"{fileKey}:{DateTime.UtcNow:yyyyMMdd}";
    var downloadCount = cache.GetOrCreate(todayKey, e =>
    {
        e.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
        return 0;
    });

    if (downloadCount >= 100)
        return Results.BadRequest("该文件今日下载次数已达上限（100次）");

    cache.Set(ipKey, true, TimeSpan.FromHours(4)); // 4小时内禁止再次下载
    cache.Set(todayKey, (int)downloadCount + 1);

    var contentType = "application/octet-stream";
    return Results.File(filePath, contentType, fileName);
});

await app.RunAsync();
