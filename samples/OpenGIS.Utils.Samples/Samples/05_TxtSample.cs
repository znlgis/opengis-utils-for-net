using System.Text;
using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     05. 测绘 TXT 坐标文件格式 GtTxtUtil:SaveTxt/LoadTxt 整体往返、
///     单行解析(ParseTxtLine / TryParseTxtLine 两种错误风格)、行格式化与 OguCoordinate。
///     TXT 坐标成果是国内测绘交换的"最后一公里"格式,字段约定为中文(点号/圈号/备注)。
/// </summary>
public sealed class TxtSample : ISample
{
    private static readonly Encoding Gbk = Encoding.GetEncoding("GBK");

    public string Title => "05-TXT坐标文件(SaveTxt/LoadTxt/单行解析)";

    public void Run()
    {
        RoundTrip();
        LineLevelApi();
        BadDataBehavior();
    }

    private static void RoundTrip()
    {
        Out.Step("1. 图层 → TXT(GBK) → 图层:往返不丢信息");
        var txtPath = SampleOutput.File("05_txt", "survey.txt");
        var metadata = new OguLayerMetadata
        {
            DataSource = "OpenGIS.Utils.Samples 合成数据",
            CoordinateSystemName = "CGCS2000 / 3-degree Gauss-Kruger zone 36",
            ZoneDivision = "3度带",
            ProjectionType = "高斯-克吕格",
            MeasureUnit = "米"
        };
        GtTxtUtil.SaveTxt(TestData.BuildSurveyPoints(), txtPath, metadata, Gbk);

        Out.Note("文件头(元数据 + 表头):");
        foreach (var line in File.ReadLines(txtPath, Gbk).Take(8))
            Console.WriteLine($"      | {line}");

        var loaded = GtTxtUtil.LoadTxt(txtPath, Gbk);
        Out.Result("往返要素数", loaded.GetFeatureCount());
        Out.Result("字段集", string.Join(", ", loaded.Fields.Select(f => f.Name)));
        var p1 = loaded.Features[1];
        Out.Result("第 2 个点", $"点号={p1.GetValue("点号")} X={p1.GetValue("X")} Y={p1.GetValue("Y")}");
        Out.Note("X/Y/Z 由几何 WKT 自动展开为属性,中文点号经 GBK 往返无损");
    }

    private static void LineLevelApi()
    {
        Out.Step("2. 单行级 API:适合逐行清洗原始记录");
        // 行格式:点号 [圈号] X Y [Z] [备注],空白分隔
        var coord = GtTxtUtil.ParseTxtLine("J1 1 500100.500 3798000.250 452.5 示例注记");
        Out.Result("ParseTxtLine", coord == null ? "null(不应发生)" :
            $"点号={coord.PointNumber} 圈号={coord.RingNumber} X={coord.X} Y={coord.Y} Z={coord.Z} 备注={coord.Remark}");
        if (coord != null)
        {
            Out.Result("ToWkt", coord.ToWkt());
            Out.Result("FromWkt 往返 X", OguCoordinate.FromWkt(coord.ToWkt()).X);

            var formatted = GtTxtUtil.FormatTxtLine(coord, 36);
            Out.Result("FormatTxtLine(zone=36),制表符显示为空格", formatted.Replace('\t', ' '));
            Out.Result("ParseTxtLine 再读回格式化行", GtTxtUtil.ParseTxtLine(formatted.Replace('\t', ' '))?.PointNumber);
            Out.Note("当前实现中 zoneNumber 参数不参与数值变换(X/Y 原样输出),带号信息经 SaveTxt 的『分带:』元数据头承载");
        }

        var ok = GtTxtUtil.TryParseTxtLine("J2 1 500200 3798100", out var shortLine);
        Out.Result("TryParseTxtLine(缺 Z 的简行)", $"ok={ok} X={shortLine?.X}");
        Out.Result("TryParseTxtLine(坏行)",
            GtTxtUtil.TryParseTxtLine("这一行完全不是坐标", out var bad) ? "意外成功" : "false(坐标=null,不抛异常)");
        Out.Note("Try 风格返回 false 供循环内廉价跳过;ParseTxtLine 对坏行返回 null(兼容旧行为)");
    }

    private static void BadDataBehavior()
    {
        Out.Step("3. 整文件加载遇坏行:抛 FormatParseException(带行号与坏内容)");
        var badPath = SampleOutput.File("05_txt", "bad.txt");
        File.WriteAllLines(badPath, new[]
        {
            "J1 1 500100.0 3798000.0",
            "J2 1 invalid 3798100.0"
        }, Gbk);
        try
        {
            GtTxtUtil.LoadTxt(badPath, Gbk);
        }
        catch (FormatParseException ex)
        {
            Out.Result("捕获 FormatParseException", Out.Head(ex.Message));
        }
        Out.Note("逐行容错清洗请改用 TryParseTxtLine;确认干净后再 LoadTxt 整文件读入");
    }
}
