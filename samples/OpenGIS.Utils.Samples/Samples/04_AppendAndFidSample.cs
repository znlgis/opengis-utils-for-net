using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     04. 写入模式细节:Write(重建) vs Append(追加)、非零 FID 保留、overwrite 覆盖选项、
///     以及字段不匹配时的失败聚合报告。做增量更新/同步类功能前必须弄清这一节。
/// </summary>
public sealed class AppendAndFidSample : ISample
{
    public string Title => "04-追加写入与 FID 保留(Write/Append/overwrite)";

    public void Run()
    {
        // GeoPackage 完整保留显式 FID,是演示该语义最稳妥的载体
        var gpkgPath = SampleOutput.File("04_append", "demo.gpkg");
        const string layerName = "demo_parcels";

        Out.Step("1. WriteLayer 首次写入:Fid 100..105 显式指定并原样落盘");
        OguLayerUtil.WriteLayer(DataFormatType.GEOPACKAGE, TestData.BuildParcels(), gpkgPath, layerName);
        Out.Result("读回 FID", string.Join(", ", ReadFids(gpkgPath, layerName)));

        Out.Step("2. GdalWriter.Append:向已有图层追加 3 个新要素(Fid 200..202),不重写旧数据");
        new GdalWriter().Append(TestData.BuildParcelAppends(), gpkgPath, layerName);
        Out.Result("追加后 FID", string.Join(", ", ReadFids(gpkgPath, layerName)));

        Out.Step("3. 文件驱动 WriteLayer 恒先删后建:直接重写即整层替换(9 个要素缩减回 2 个)");
        // overwrite=true 会向驱动追加 OVERWRITE=YES 选项,主要面向"不删文件重复写入"的数据库目标(如 PostGIS);
        // 对文件目标 WriteLayer 本就整体重建,效果一致
        OguLayerUtil.WriteLayer(DataFormatType.GEOPACKAGE, TestData.BuildParcels(2), gpkgPath, layerName,
            options: new Dictionary<string, object> { ["overwrite"] = true });
        var afterOverwrite = ReadFids(gpkgPath, layerName);
        Out.Result("覆盖后 FID", $"{string.Join(", ", afterOverwrite)}(共 {afterOverwrite.Count} 个)");

        Out.Step("4. Append 字段校验:新图层带目标层不存在的字段 → 失败要素聚合为 DataSourceException");
        var mismatched = TestData.BuildParcelAppends(1);
        mismatched.AddField(new OguField { Name = "EXTRA_FIELD", DataType = FieldDataType.STRING });
        mismatched.Features[0].SetValue("EXTRA_FIELD", "目标层没有这个字段");
        try
        {
            new GdalWriter().Append(mismatched, gpkgPath, layerName);
            Out.Note("未抛异常(驱动宽容处理),按环境实际行为为准");
        }
        catch (DataSourceException ex)
        {
            Out.Result("捕获 DataSourceException", Out.Head(ex.Message));
        }
        Out.Note("异常消息里列出了写入失败的要素与原因,便于批量修复后重试");

        Out.Step("5. 对照:显式 FID 是否保留取决于驱动 — SHP 按记录序号重编号(写 100.. 读回 0..3)");
        var shpPath = SampleOutput.File("04_append", "parcels_shp", "parcels.shp");
        OguLayerUtil.WriteLayer(DataFormatType.SHP, TestData.BuildParcels(4), shpPath);
        var shpFids = OguLayerUtil.ReadLayer(DataFormatType.SHP, shpPath).Features.Select(f => f.Fid);
        Out.Result("SHP 读回 FID", string.Join(", ", shpFids));
        Out.Note("需要跨会话稳定的要素号时,选 GPKG/PostGIS 这类支持显式 FID 的格式,或把主键放进属性字段(本例 ID 列即为此设计)");
    }

    private static List<int> ReadFids(string gpkgPath, string layerName)
    {
        var layer = OguLayerUtil.ReadLayer(DataFormatType.GEOPACKAGE, gpkgPath, layerName: layerName);
        return layer.Features.Select(f => f.Fid).OrderBy(x => x).ToList();
    }
}
