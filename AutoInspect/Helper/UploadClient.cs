using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoInspect.Helper;
public static class UploadClient
{
    private const long MaxBytes = 50 * 1024 * 1024;
    private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

    // 最大重试次数
    private const int MaxRetries = 5;

    public static async Task UploadFileAsync(string url, LogHelper log, string? xForwardedFor = null)
    {
        string filePath = log._logFile;
        if (!File.Exists(filePath)) throw new FileNotFoundException("文件不存在", filePath);

        var fi = new FileInfo(filePath);
        if (fi.Length > MaxBytes) throw new InvalidOperationException($"文件太大：{fi.Length} 字节");

        // 设置 Header
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            http.DefaultRequestHeaders.Remove("X-Forwarded-For");
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", xForwardedFor);
        }

        int attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(fs);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(streamContent, "file", Path.GetFileName(filePath));

                using var resp = await http.PostAsync(url, content);
                string respBody = await resp.Content.ReadAsStringAsync();

                if (resp.StatusCode == (HttpStatusCode)429)
                    throw new InvalidOperationException("被限流：6小时内只能发送一次。");

                if (resp.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                    throw new InvalidOperationException("文件过大（服务器拒绝）。");

                resp.EnsureSuccessStatusCode();

                log.Info("system", $"第 {attempt} 次尝试成功。返回值：{respBody}");
                var json = JsonSerializer.Deserialize<JsonElement>(respBody);
                return;
            }
            catch (Exception ex) when (attempt <= MaxRetries && IsTransientError(ex))
            {
                int delaySeconds = (int)Math.Pow(2, attempt);
                log.Error("system", $"第 {attempt} 次上传失败（{ex.Message}），将在 {delaySeconds} 分钟后重试...");
                await Task.Delay(TimeSpan.FromMinutes(delaySeconds));
            }
            catch (Exception ex)
            {
                log.Error("system", $"上传彻底失败（尝试 {attempt} 次）: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// 判断是否为值得重试的“暂时性”错误
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        if (ex is HttpRequestException || ex is TaskCanceledException || ex is IOException)
            return true;

        return false;
    }
}
