using System.Text;
using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Utils;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     08. 通用工具族:编码检测转码 EncodingUtil、自然排序 SortUtil、
///     数值格式化 NumUtil、压缩包 ZipUtil——围绕 GIS 数据交换的日常杂活。
/// </summary>
public sealed class UtilitySample : ISample
{
    private static readonly Encoding Gbk = Encoding.GetEncoding("GBK");

    public string Title => "08-通用工具(编码/自然排序/数值/ZIP)";

    public void Run()
    {
        EncodingTools();
        NaturalOrder();
        NumberFormatting();
        ZipWorkflow();
    }

    private static void EncodingTools()
    {
        Out.Step("1. EncodingUtil:探测文件编码并统一转码(拿到第三方数据第一步)");
        var gbkFile = SampleOutput.File("08_util", "legacy_gbk.txt");
        File.WriteAllText(gbkFile, "第一条记录,坐标成果表,GBK编码中文。\n第二条:北偏东36度。\n", Gbk);
        var utf8File = SampleOutput.File("08_util", "modern_utf8.txt");
        File.WriteAllText(utf8File, "同样的中文内容,UTF-8 编码存储。\n", new UTF8Encoding(true));

        Out.Result("GBK 文件探测", EncodingUtil.GetFileEncoding(gbkFile).WebName);
        Out.Result("UTF-8 文件探测", EncodingUtil.GetFileEncoding(utf8File).WebName);
        EncodingUtil.ConvertFileEncoding(gbkFile, Encoding.UTF8);
        Out.Result("转码后再探测", EncodingUtil.GetFileEncoding(gbkFile).WebName);
        Out.Result("转码后内容(UTF8 读)", Out.Head(File.ReadAllText(gbkFile, Encoding.UTF8)));
        Out.Note("纯 ASCII 文件会被判定为 UTF-8,属正常启发式行为;Shapefile 中文属性编码请用 03 的 encoding 选项");
    }

    private static void NaturalOrder()
    {
        Out.Step("2. SortUtil:自然排序(文件名里的数字按数值而非字典序比较)");
        var files = new[] { "parcels_10.geojson", "parcels_2.geojson", "parcels_1.geojson", "parcels_21.geojson" };
        Out.Result("默认序", string.Join(", ", files.OrderBy(x => x, StringComparer.Ordinal)));
        Out.Result("NaturalSort", string.Join(", ", SortUtil.NaturalSort(files, f => f)));
        Out.Result("CompareString(\"a2\", \"a10\")", SortUtil.CompareString("a2", "a10"));
        Out.Note("批量导入按期号/图幅号命名的数据时,自然排序保证处理顺序符合直觉");
    }

    private static void NumberFormatting()
    {
        Out.Step("3. NumUtil:防科学计数法 + 定点格式化(坐标写属性/报表必备)");
        Out.Result("ToString() 直出", (0.000000123).ToString());
        Out.Result("GetPlainString", NumUtil.GetPlainString(0.000000123));
        Out.Result("大数 GetPlainString", NumUtil.GetPlainString(1.23456789e15));
        Out.Result("Round(3.14159, 4)", NumUtil.Round(3.14159, 4));
        Out.Result("FormatNumber(123456.7891, 2)", NumUtil.FormatNumber(123456.7891, 2));
        Out.Note("double 直接拼进 WKT/属性表可能出现 1E-07,落库前过一遍 GetPlainString");
    }

    private static void ZipWorkflow()
    {
        Out.Step("4. ZipUtil:整套 Shapefile 打包分发 / 解包接收(中文条目名用 GBK 编码)");
        var srcDir = SampleOutput.Dir("08_util", "dist");
        // SHP 按默认方式写(DBF 字节为 UTF-8,见 03 §1);ZipUtil 的编码参数只管压缩包内的
        // 中文条目名(文件夹/文件名),与 DBF 属性编码是两回事,不要混淆
        OguLayerUtil.WriteLayer(DataFormatType.SHP, TestData.BuildParcels(3),
            Path.Combine(srcDir, "交付", "parcels.shp"));
        File.WriteAllText(Path.Combine(srcDir, "交付", "说明.txt"), "本包为合成示例数据,不含真实成果。", Gbk);

        var zipPath = SampleOutput.File("08_util", "dist.zip");
        ZipUtil.Zip(srcDir, zipPath, Gbk);
        Out.Result("压缩包大小", $"{new FileInfo(zipPath).Length} 字节");

        var fileNames = new List<string>();
        using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
        {
            fileNames.AddRange(archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).Select(e => e.FullName));
        }
        Out.Result("包内文件(中文条目名经 GBK 编码,老解压工具不乱码)", string.Join(" | ", fileNames));

        var extracted = SampleOutput.Dir("08_util", "received");
        ZipUtil.Unzip(zipPath, extracted, Gbk);
        var receivedShp = Path.Combine(extracted, "交付", "parcels.shp");
        var layer = OguLayerUtil.ReadLayer(DataFormatType.SHP, receivedShp);
        Out.Result("解包后直接读 SHP", $"{layer.GetFeatureCount()} 个要素,首个 NAME={layer.Features[0].GetValue("NAME")}");

        Out.Step("4b. CompressFiles:只挑散文件打包(不必整目录)");
        var pickZip = SampleOutput.File("08_util", "picked.zip");
        ZipUtil.CompressFiles(Directory.GetFiles(srcDir, "*.txt", SearchOption.AllDirectories), pickZip);
        Out.Result("挑选打包", $"{new FileInfo(pickZip).Length} 字节");
    }
}
