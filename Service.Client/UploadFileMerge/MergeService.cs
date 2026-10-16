using System.Text.Json;
using System.Threading.Tasks;

namespace Service.Client.UploadFileMerge
{
    public class MergeService(IConfiguration configuration)
    {
        public async Task HandlerAsync(string folderPath)
        {
            //旧数据归类
            await HandlerExpiredFilesAsync(folderPath);

            Dictionary<string, string> NameMap = configuration.GetSection("NameMap").Get<List<NameMapItem>>()?.ToDictionary(x => x.text, x => x.value) ?? new();
            Dictionary<string, Dictionary<string, HashSet<DataModel>>> reValue = new();// 写入Excel的总结果集
            foreach (var filePath in Directory.GetFiles(folderPath, "*.html"))
            {
                var htmlContent = FileHelper.ParseHtml(filePath);
                string fileNameIdentify = Path.GetFileNameWithoutExtension(filePath).Split('_').Skip(1).Take(1).First(); // 取第二个下划线部分xxx_yyy_aaa_iii.html的yyy

                string key = NameMap.ContainsKey(fileNameIdentify) ? NameMap[fileNameIdentify] : fileNameIdentify;
                var value = reValue.ContainsKey(key) ? reValue[key] : new Dictionary<string, HashSet<DataModel>>();

                foreach (var kvp in htmlContent)
                {
                    if (!value.TryAdd(kvp.Key, kvp.Value))
                    {
                        foreach (var item in kvp.Value)
                        {
                            value[kvp.Key].Add(item);
                        }
                    }

                }

                if (!reValue.TryAdd(key, value))
                    reValue[key] = value;
            }
            string outPutPath = Path.Combine(folderPath, "merge.xlsx");
            if (reValue.Count > 0)
                reValue.WriteExcel(outPutPath);
            await Task.CompletedTask;
        }

        public async Task HandlerExpiredFilesAsync(string folderPath)
        {
            string[] files = Directory.GetFiles(folderPath, "*.html");
            var groupBy = files.GroupBy(x => Path.GetFileNameWithoutExtension(x).Split('_')[^2]).OrderBy(x => x.Key).ToList();
            for (int i = 0; i < groupBy.Count - 1; i++)
            {
                var group = groupBy[i];
                if(!Directory.Exists(Path.Combine(folderPath, group.Key)))
                    Directory.CreateDirectory(Path.Combine(folderPath, group.Key));

                foreach(var file in group)
                {
                    File.Move(file, Path.Combine(folderPath, group.Key, Path.GetFileName(file)));
                }
                await HandlerAsync(Path.Combine(folderPath, group.Key));
            }
        }
    }
}
