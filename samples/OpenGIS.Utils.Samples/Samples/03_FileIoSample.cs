using System.Text;
using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Util;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     03. 文件格式读写:统一门面 OguLayerUtil 一行读写任意格式,
///     涵盖属性编码声明与探测、属性/空间过滤、多图层容器探测、格式转换与 Shapefile 专用工具 ShpUtil。
/// </summary>
public sealed class FileIoSample : ISample
{
    public string Title => "03-文件读写(SHP/GeoJSON/GPKG/KML/转换/ShpUtil)";

    public void Run()
    {
        var gbk = Encoding.GetEncoding("GBK");

        ShapefileRoundTrip(gbk);
        GeoJsonRoundTrip();
        GeoPackageSingleLayer();
        KmlViaLayerNameDiscovery();
        FormatConversion();
        ShpUtilFamily(gbk);
    }

    private static void ShapefileRoundTrip(Encoding gbk)
    {
        Out.Step("1. Shapefile 写入与读回(DBF 中文属性,编码声明必须与文件实际字节一致)");
        var shpPath = SampleOutput.File("03_files", "parcels.shp");
        OguLayerUtil.WriteLayer(DataFormatType.SHP, TestData.BuildParcels(), shpPath);

        Out.Result("生成的配套文件", string.Join(", ",
            Directory.GetFiles(Path.GetDirectoryName(shpPath)!, "parcels.*").Select(Path.GetFileName)));
        Out.Note("DBF 字段名上限 10 字节,GREEN_RATIO 会被驱动截断为 GREEN_RATI(GDAL Warning 6,属预期行为)");

        var read = OguLayerUtil.ReadLayer(DataFormatType.SHP, shpPath);
        Out.Result("读回要素数", read.GetFeatureCount());
        Out.Result("首个要素 NAME(默认 UTF-8 往返无损)", read.Features[0].GetValue("NAME"));
        Out.Result("坐标系 WKID", read.Wkid);

        // options["encoding"] 读写两侧都生效:写侧决定 DBF 属性字节编码并自动生成配套 .cpg;
        // 读侧声明 DBF 字节按什么编码转成 .NET 字符串(未声明时 GDAL 依据 .cpg 自动判断)。
        // 故意用错误编码读 UTF-8 文件,直观展示"声明与实际不符"的经典乱码事故
        var wrong = OguLayerUtil.ReadLayer(DataFormatType.SHP, shpPath,
            options: new Dictionary<string, object> { ["encoding"] = "GB2312" });
        Out.Result("同一 UTF-8 文件强制按 GB2312 声明 → 乱码", wrong.Features[0].GetValue("NAME"));

        // 正向演示:按 GBK 写 → 驱动生成 .cpg → 不带任何选项读回也正确;
        // 声明式读取结束后自动恢复进程级 GDAL 配置(SHAPE_ENCODING),不会污染后续小节
        var gbkPath = SampleOutput.File("03_files", "parcels_gbk.shp");
        OguLayerUtil.WriteLayer(DataFormatType.SHP, TestData.BuildParcels(3), gbkPath,
            options: new Dictionary<string, object> { ["encoding"] = gbk });
        Out.Result("GBK 写入生成的配套文件", string.Join(", ",
            Directory.GetFiles(Path.GetDirectoryName(gbkPath)!, "parcels_gbk.*").Select(Path.GetFileName)));
        Out.Result("GBK 文件免声明读回(按 .cpg 自动解码)",
            OguLayerUtil.ReadLayer(DataFormatType.SHP, gbkPath).Features[0].GetValue("NAME"));
        Out.Note("处理外部来的 GBK 老 shapefile 时:读侧声明 encoding 或核对 .cpg(见 §7 ShpUtil)");

        // 同名 API 均有 Async 版本,IO 密集程序可直接 await
        var asyncRead = OguLayerUtil.ReadLayerAsync(DataFormatType.SHP, shpPath)
            .GetAwaiter().GetResult();
        Out.Result("ReadLayerAsync 要素数", asyncRead.GetFeatureCount());

        Out.Step("2. 属性过滤(SQL 风格)与空间过滤(WKT)");
        var attrFiltered = OguLayerUtil.ReadLayer(DataFormatType.SHP, shpPath,
            attributeFilter: "ID >= 103");
        Out.Result("ID >= 103", string.Join(", ", attrFiltered.Features.Select(f => f.GetValue("ID"))));
        var spatialFiltered = OguLayerUtil.ReadLayer(DataFormatType.SHP, shpPath,
            spatialFilterWkt: TestData.Rect(TestData.BaseLon - 0.005, TestData.BaseLat - 0.005, 0.013, 0.013));
        Out.Result("与左侧小窗口相交的要素数", $"{spatialFiltered.GetFeatureCount()} / {read.GetFeatureCount()}");
    }

    private static void GeoJsonRoundTrip()
    {
        Out.Step("3. GeoJSON 读写(注意:GeoJSON 规范只允许 WGS84/CGCS2000 经纬度)");
        var geojsonPath = SampleOutput.File("03_files", "roads.geojson");
        OguLayerUtil.WriteLayer(DataFormatType.GEOJSON, TestData.BuildRoads(), geojsonPath);
        var roads = OguLayerUtil.ReadLayer(DataFormatType.GEOJSON, geojsonPath);
        Out.Result("要素数", roads.GetFeatureCount());
        Out.Result("首条道路", $"{roads.Features[0].GetValue("ROAD_NAME")} ({FirstWktWord(roads)})");
    }

    // 取首个要素 WKT 的类型词(如 LINESTRING),快速确认几何形态
    private static string FirstWktWord(Engine.Model.Layer.OguLayer layer) =>
        layer.Features[0].Wkt!.Split(' ')[0];

    private static void GeoPackageSingleLayer()
    {
        Out.Step("4. GeoPackage 读写(注意:WriteLayer 对已存在目标先删后建,整库重建而非追加图层)");
        var gpkgPath = SampleOutput.File("03_files", "demo.gpkg");
        OguLayerUtil.WriteLayer(DataFormatType.GEOPACKAGE, TestData.BuildParcels(), gpkgPath, "demo_parcels");

        // 同一文件第二次 WriteLayer 会替换整个容器(想增量扩充同一图层用 GdalWriter.Append,见 04 示例)
        var names = OguLayerUtil.GetLayerNames(DataFormatType.GEOPACKAGE, gpkgPath);
        Out.Result("容器内图层", string.Join(", ", names));
        var back = OguLayerUtil.ReadLayer(DataFormatType.GEOPACKAGE, gpkgPath, layerName: "demo_parcels");
        Out.Result("指定图层读取", $"{back.Name}: {back.GetFeatureCount()} 个要素");
    }

    private static void KmlViaLayerNameDiscovery()
    {
        Out.Step("5. KML 写出 + 用 GetLayerNames 探测读回(KML 内部图层名由驱动决定)");
        var kmlPath = SampleOutput.File("03_files", "parcels.kml");
        OguLayerUtil.WriteLayer(DataFormatType.KML, TestData.BuildParcels(3), kmlPath);
        var kmlLayers = OguLayerUtil.GetLayerNames(DataFormatType.KML, kmlPath);
        Out.Result("KML 内图层名", string.Join(", ", kmlLayers));
        var read = OguLayerUtil.ReadLayer(DataFormatType.KML, kmlPath, layerName: kmlLayers[0]);
        Out.Result("读回要素数", read.GetFeatureCount());
    }

    private static void FormatConversion()
    {
        Out.Step("6. ConvertFormat:一步完成 输入格式 → 输出格式(不必先读入内存)");
        var input = SampleOutput.File("03_files", "roads.geojson");
        var output = SampleOutput.File("03_files", "roads_converted.gpkg");
        OguLayerUtil.ConvertFormat(input, DataFormatType.GEOJSON, output, DataFormatType.GEOPACKAGE,
            layerName: "demo_roads");
        var converted = OguLayerUtil.ReadLayer(DataFormatType.GEOPACKAGE, output, layerName: "demo_roads");
        Out.Result("geojson → gpkg 转换后要素数", converted.GetFeatureCount());
    }

    private static void ShpUtilFamily(Encoding gbk)
    {
        Out.Step("7. ShpUtil:Shapefile 家族专用工具(cpg 伴生文件/编码探测/范围/修复)");
        var miniShp = SampleOutput.File("03_shputil", "mini.shp");
        ShpUtil.WriteShapefile(TestData.BuildParcels(4), miniShp); // DBF 实际字节为 UTF-8
        ShpUtil.CreateCpgFile(miniShp, gbk);                        // 故意先声明成 GBK
        Out.Result("GetShapefileEncoding(读 .cpg 声明)", ShpUtil.GetShapefileEncoding(miniShp).WebName);
        Out.Note("cpg 只是『声明』:与实际 DBF 字节不符时,依赖它的软件(含本库自动探测路径)就会乱码 —— 改回与写入一致的声明");
        ShpUtil.CreateCpgFile(miniShp, Encoding.UTF8);
        Out.Result("改声明为 UTF-8 后", ShpUtil.GetShapefileEncoding(miniShp).WebName);

        var bounds = ShpUtil.GetShapefileBounds(miniShp);
        Out.Result("GetShapefileBounds", $"X[{bounds.MinX:0.####}, {bounds.MaxX:0.####}] Y[{bounds.MinY:0.####}, {bounds.MaxY:0.####}]");
        var mini = ShpUtil.ReadShapefile(miniShp); // 不传编码 → 按 cpg 自动探测
        Out.Result("ReadShapefile 首个 NAME(按 cpg 自动解码)", mini.Features[0].GetValue("NAME"));
        ShpUtil.RepairShapefile(miniShp);
        Out.Result("RepairShapefile 后可用文件", string.Join(", ",
            Directory.GetFiles(Path.GetDirectoryName(miniShp)!, "mini.*").Select(Path.GetFileName)));
    }
}
