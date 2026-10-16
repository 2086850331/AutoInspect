using HtmlAgilityPack;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace Service.Client.UploadFileMerge
{
    public static class FileHelper
    {
        public static Dictionary<string, HashSet<DataModel>> ParseHtml(string html)
        {
            var result = new Dictionary<string, HashSet<DataModel>>();

            var doc = new HtmlDocument();
            doc.Load(html);

            // 查所有 class='module' 且有 id 的 div
            var modules = doc.DocumentNode.SelectNodes("//div[contains(@class,'module') and @id]");
            if (modules == null) return result;

            foreach (var module in modules)
            {
                string id = module.GetAttributeValue("id", "");

                var table = module.SelectSingleNode(".//table/tbody");
                if (table == null)
                {
                    result[id] = new HashSet<DataModel>();
                    continue;
                }

                var set = new HashSet<DataModel>();

                var rows = table.SelectNodes("./tr");
                if (rows != null)
                {
                    foreach (var tr in rows)
                    {
                        var tds = tr.SelectNodes("./td");
                        if (tds == null || tds.Count < 2) continue;

                        // 最后两个 td
                        string type = tds[tds.Count - 2].InnerText.Trim();
                        string message = tds[tds.Count - 1].InnerText.Trim();

                        set.Add(new DataModel
                        {
                            Type = type,
                            Message = message.Replace("\r", string.Empty).Replace("\n", string.Empty)
                        });
                    }
                }

                result[id] = set;
            }

            return result;
        }

        public static void WriteExcel(this Dictionary<string, Dictionary<string, HashSet<DataModel>>> data, string filePath)
        {
            List<int> pointer = new List<int>();

            // 固定顺序
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

            // 创建 EPPlus 包
            using (var package = new ExcelPackage())
            {
                var workbook = package.Workbook;
                var sheet = workbook.Worksheets.Add("Sheet1");
                int rowIndex = 1; // EPPlus 行列索引从1开始
                sheet.Column(3).Width = 20;
                sheet.Column(2).Width = 20;

                // 列头样式
                var headerStyle = sheet.Cells[rowIndex, 1, rowIndex, 8].Style;
                headerStyle.Fill.PatternType = ExcelFillStyle.Solid;
                headerStyle.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0x98, 0xD7, 0xB6)); // 设置背景色
                headerStyle.Border.BorderAround(ExcelBorderStyle.Thin);

                // 数据单元格样式
                var dataStyle = sheet.Cells.Style;
                dataStyle.Border.BorderAround(ExcelBorderStyle.Thin);

                foreach (var serverItem in data)
                {
                    pointer.Clear();
                    string serverName = serverItem.Key;
                    var innerDict = serverItem.Value;

                    // 填充服务器名称
                    sheet.Cells[rowIndex, 1].Value = "服务器名称：";
                    sheet.Cells[rowIndex, 2].Value = serverName;  // 第二列是服务器名称
                    // 设置列头样式
                    foreach (var item in Enumerable.Range(1, 8))
                    {
                        var cellStyle = sheet.Cells[rowIndex, item].Style;
                        cellStyle.Fill.PatternType = ExcelFillStyle.Solid;
                        cellStyle.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0x98, 0xD7, 0xB6)); // 设置列头背景色
                        cellStyle.Border.BorderAround(ExcelBorderStyle.Thin);  // 设置列头边框
                    }

                    // fallback：内层第 11 项，用于解析输入值：xxxx
                    var fallbackItem = innerDict.Values.Last();

                    for (int i = 0; i < orderList.Count; i++)
                    {
                        int dataIndex = i;
                        dataIndex = orderList.Skip(i).Take(1).First().Value;
                        string keyName = orderList.Skip(i).Take(1).First().Key;

                        rowIndex++; // 增加行索引，开始填充数据行

                        sheet.Cells[rowIndex, 1].Value = i + 1;   // 填充序号
                        sheet.Cells[rowIndex, 2].Value = keyName;  // 填充描述

                        string thirdCol = "";
                        string fourthCol = "";

                        HashSet<DataModel> set = innerDict.Skip(dataIndex).Take(1).First().Value;
                        if (dataIndex == 6)
                        {
                            if (i == 6)
                            {
                                thirdCol = string.Join(";", set.Where(x => x.Message.Contains("盘空间剩余")).Select(x => x.Message.Split("; ")[0]));
                                foreach (int index in Enumerable.Range(0, set.Where(x => x.Message.Contains("盘空间剩余")).Count() - 1))
                                    pointer.Add(6);
                            }
                            else
                            {
                                thirdCol = string.Join(";", set.Skip(pointer.Count(x => x == 6)).Take(1).Select(x => x.Message));
                            }

                            fourthCol = "OK";
                            pointer.Add(6);
                        }
                        else if (set != null && set.Count > 0)
                        {
                            var errors = set
                                .Where(x => x.Type == "ERROR")
                                .Select(x => x.Message)
                                .ToList();

                            if (errors.Count > 0)
                            {
                                thirdCol = string.Join("；", errors);
                                thirdCol = thirdCol.Length > 5000 ? thirdCol.Substring(0, 5000) : thirdCol;
                                fourthCol = "OK";
                            }
                        }

                        // 如果第三列仍为空，从 fallback 中找 "输入值：xxx"
                        if (string.IsNullOrEmpty(thirdCol))
                        {
                            var fallbackString = fallbackItem.Where(x => x.Message.Contains("输入值："));

                            if (fallbackString is not null && fallbackString.Any())
                            {
                                byte number = 0b00000000;
                                foreach (var fallbackStr in fallbackString)
                                {
                                    byte reMap = BittMap.MapOldToNew(Convert.ToByte(fallbackStr.Message.Split('：').Last(), 2));
                                    number |= reMap;
                                }

                                string numberStr = Convert.ToString(number, 2).PadLeft(8, '0');
                                if (numberStr.Length > i)
                                {
                                    if (numberStr[i] == '1')
                                    {
                                        // 第三第四列都空
                                        thirdCol = "";
                                        fourthCol = "OK";
                                    }
                                    else if (numberStr[i] == '0')
                                    {
                                        thirdCol = "没有在用";
                                        fourthCol = "";
                                    }
                                }
                            }
                        }

                        // 写入第三列和第四列数据
                        sheet.Cells[rowIndex, 3].Value = thirdCol;
                        sheet.Cells[rowIndex, 4].Value = fourthCol;

                        // 填充其他列数据
                        foreach (var item in Enumerable.Range(5, 8 - 5))
                        {
                            if (item == 6 && fourthCol == "OK")
                                sheet.Cells[rowIndex, item].Value = "黄超";
                            else if (item == 7 && fourthCol == "OK")
                                sheet.Cells[rowIndex, item].Value = DateTime.Now.ToString("yyyy/MM/dd");
                        }

                        // 填充所有列样式
                        foreach (var item in Enumerable.Range(1, 8))
                        {
                            sheet.Cells[rowIndex, item].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        }
                    }

                    rowIndex++; // 增加行索引
                    rowIndex++; // 增加行索引
                }

                // 自动列宽
                for (int col = 1; col <= 8; col++)
                {
                    if (col != 3)  // 跳过第三列
                    {
                        sheet.Column(col).AutoFit();  // 自动调整其他列的宽度
                    }
                }

                // 保存文件
                var fileInfo = new FileInfo(filePath);
                package.SaveAs(fileInfo);
            }
        }
    }
}
