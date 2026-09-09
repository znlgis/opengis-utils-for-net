using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Util;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     10. PostGIS 数据库读写:连接串规范、表存在性探测、统一门面写入/读取、
///     属性过滤下推与空间索引创建。未配置演示库时自动 SKIP,不影响其余示例。
///     启用方式(使用你自己的测试库,切勿连接生产数据):
///         set OGU_POSTGIS_CONN=PG:host=127.0.0.1 port=5432 dbname=test_db user=tester password=secret
/// </summary>
public sealed class PostgisSample : ISample
{
    private const string ParcelTable = "ogu_samples_parcels";
    private const string RoadTable = "ogu_samples_roads";

    public string Title => "10-PostGIS读写(需 OGU_POSTGIS_CONN 环境变量)";

    public void Run()
    {
        var conn = Environment.GetEnvironmentVariable("OGU_POSTGIS_CONN");
        if (string.IsNullOrWhiteSpace(conn))
            throw new SampleSkipException("未设置 OGU_POSTGIS_CONN;类注释里有连接串格式说明");
        if (!conn.StartsWith("PG:", StringComparison.OrdinalIgnoreCase))
            throw new SampleSkipException("连接串必须以 PG: 前缀开头,形如 PG:host=... dbname=... user=... password=...");

        Out.Step("1. 连接串规范:前缀 PG: 触发 PostGIS 路由;键值对不加引号(与 GDAL OCI 风格不同)");
        bool exists;
        try
        {
            exists = PostgisUtil.TableExists(conn, ParcelTable);
        }
        catch (DataSourceException ex)
        {
            throw new SampleSkipException($"无法连接目标库:{Out.Head(ex.Message)}");
        }
        Out.Result($"TableExists({ParcelTable})", exists ? "存在(将覆盖)" : "不存在(将新建)");
        Out.Note("TableExists 连不上时抛 DataSourceException;连接成功但表不存在才返回 false");

        Out.Step("2. 经统一门面写入(与写文件同一 API,path 参数即连接串)");
        var options = new Dictionary<string, object> { ["overwrite"] = true };
        OguLayerUtil.WriteLayer(DataFormatType.POSTGIS, TestData.BuildParcels(), conn, ParcelTable, options: options);
        OguLayerUtil.WriteLayer(DataFormatType.POSTGIS, TestData.BuildRoads(), conn, RoadTable, options: options);
        Out.Result("写入表", $"{ParcelTable}({TestData.BuildParcels().GetFeatureCount()} 要素), {RoadTable}({TestData.BuildRoads().GetFeatureCount()} 要素)");
        Out.Note("表名只允许字母/数字/下划线;overwrite=true 走 DROP+CREATE,重跑安全");

        Out.Step("3. 读取与过滤器下推(过滤在数据库端执行,大表避免全量拉取)");
        var all = OguLayerUtil.ReadLayer(DataFormatType.POSTGIS, conn, layerName: ParcelTable);
        Out.Result("整表读取", $"{all.GetFeatureCount()} 个要素,首个 ID={all.Features[0].GetValue("ID")}");
        var filtered = PostgisUtil.ReadPostGIS(conn, ParcelTable, "ID >= 103");
        Out.Result("ReadPostGIS + ID>=103", string.Join(", ", filtered.Features.Select(f => f.GetValue("ID"))));

        Out.Step("4. CreateSpatialIndex:自动探测几何列建 GIST 索引(IF NOT EXISTS,幂等)");
        PostgisUtil.CreateSpatialIndex(conn, ParcelTable);
        Out.Note("几何列默认按 GDAL PG 驱动的 wkb_geometry,列名不同也可显式传入");
        var spatial = OguLayerUtil.ReadLayer(DataFormatType.POSTGIS, conn, layerName: ParcelTable,
            spatialFilterWkt: TestData.Rect(TestData.BaseLon, TestData.BaseLat, 0.005, 0.005));
        Out.Result("空间过滤读回(经驱动下推,命中刚建的 GIST 索引)", $"{spatial.GetFeatureCount()} 个要素");

        Out.Note("演示表保留在库中便于到 GIS 软件里查看,再次运行会被 overwrite 重建");
    }
}
