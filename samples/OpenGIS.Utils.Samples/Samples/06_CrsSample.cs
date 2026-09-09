using OpenGIS.Utils.Engine.Util;
using OpenGIS.Utils.Geometry;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     06. 坐标系工具 CrsUtil:EPSG 元数据查询、坐标转换、高斯分带带号推算,
///     以及本项目最有代表性的实战主题——HK1980 等历史坐标系必须经 WGS84 中转(TransformThrough)。
/// </summary>
public sealed class CrsSample : ISample
{
    public string Title => "06-坐标系与投影转换(CrsUtil/HK1980中转/高斯分带)";

    public void Run()
    {
        CrsMetadata();
        BasicTransform();
        HongKong1980Path();
        GaussZones();
        ToleranceAndChecks();
    }

    private static void CrsMetadata()
    {
        Out.Step("1. GetCrsInfo:查询权威元数据(PROJ 数据库,涵盖全球 EPSG 码)");
        foreach (var wkid in new[] { 4326, 4490, 2326, 3857 })
        {
            var info = CrsUtil.GetCrsInfo(wkid);
            Out.Result($"EPSG:{wkid}",
                $"{info.Name} | 地理={info.IsGeographic} 投影={info.IsProjected} | {info.AuthorityName}:{info.AuthorityCode}");
        }
    }

    private static void BasicTransform()
    {
        Out.Step("2. Transform:地理坐标 ↔ 高斯投影(WKT 版与 Geometry 版)");
        // 虚构点:东经 108.9°、北纬 34.3°(示例城中心);带号是纯数学推算,可放心用
        var wktGeo = $"POINT ({TestData.BaseLon} {TestData.BaseLat})";
        var zone = CrsUtil.GetDh(TestData.BaseLon);
        Out.Result("经度推算 3 度带带号", zone);
        // 带号 → EPSG 码务必先查权威名:"CGCS2000 / 3-degree Gauss-Kruger zone 36" 的真实码是 4524
        const int projectedWkid = 4524;
        Out.Result("EPSG:4524 权威名", CrsUtil.GetCrsInfo(projectedWkid).Name);

        var wktProj = CrsUtil.Transform(wktGeo, 4490, projectedWkid);
        Out.Result("4490 → 4524(假东 36500000 含带号)", wktProj);
        Out.Result("投影 → 4490 往返", CrsUtil.Transform(wktProj, projectedWkid, 4490));

        using var geomGeo = GeometryUtil.Wkt2Geometry(wktGeo);
        using var geomProj = CrsUtil.Transform(geomGeo, 4490, projectedWkid);
        Out.Result("Geometry 版转换", GeometryUtil.Geometry2Wkt(geomProj));
        Out.Note("src==tgt 时 Transform 直接返回入参对象本身,注意不要重复 Dispose");
    }

    private static void HongKong1980Path()
    {
        Out.Step("3. HK1980 实战:GetTransformRecommendation + TransformThrough 两步中转");
        // HK1980 网格与 CGCS2000 之间没有直连网格平移参数,必须经 WGS84 中转,
        // 库把这条"推荐路径"做成 API,避免学习者拿到一个看起来对但偏移上百米的坐标。
        var rec = CrsUtil.GetTransformRecommendation(2326, 4490);
        Out.Result("2326→4490 需要显式路径", rec.RequiresExplicitPath);
        Out.Result("建议中转 WKID", string.Join(" → ", new[] { 2326 }.Concat(rec.IntermediateWkids).Concat(new[] { 4490 })));
        Out.Result("说明", rec.Message);

        // 取一个 HK1980 网格内的虚构点(香港附近纬度,仅演示数值形态)
        const string wkt2326 = "POINT (806888.4 826454.2)";
        var wkt4490 = CrsUtil.TransformThrough(wkt2326, 2326, 4326, 4490);
        Out.Result("TransformThrough(2326→4326→4490)", wkt4490);
        var rec2 = CrsUtil.GetTransformRecommendation(4326, 4490);
        Out.Result("4326→4490 建议", $"RequiresExplicitPath={rec2.RequiresExplicitPath}(常规配对可直接 Transform)");
        Out.Note("遇到转换结果整体偏移或抛坐标操作异常时,先查 GetTransformRecommendation 再决定走 Transform 还是 TransformThrough");
    }

    private static void GaussZones()
    {
        Out.Step("4. 高斯分带带号推算:经度/几何/WKID 三种入口,3度带与6度带两套");
        Out.Result("GetDh(经度, 3度带)", CrsUtil.GetDh(TestData.BaseLon));
        Out.Result("GetDh6(经度, 6度带)", CrsUtil.GetDh6(TestData.BaseLon));
        using var geom = GeometryUtil.Wkt2Geometry(TestData.Rect(TestData.BaseLon, TestData.BaseLat, 0.01, 0.01));
        Out.Result("GetDh(几何, 取质心X)", CrsUtil.GetDh(geom));

        Out.Step("4b. 带号 ↔ EPSG 码互推(映射已与 EPSG 权威名逐一核对)");
        foreach (var wkid in new[] { 4524, 4545, 4497 })
            Out.Result($"EPSG:{wkid}", CrsUtil.GetCrsInfo(wkid).Name);
        Out.Note("同一带号分『含带号前缀(假东=带号×10⁶+5×10⁵)』与『CM 中央经线不含带号』两套码:3度带 36 带即 4524 与 4545 之别;带号助手只覆盖前者(3度带 4513-4533、6度带 4491-4501)");
        Out.Result("GetProjectedWkid(36) → 3度带 36 带", CrsUtil.GetProjectedWkid(36));
        Out.Result("GetDhFromWkid(4524)", CrsUtil.GetDhFromWkid(4524));
        Out.Result("GetProjectedWkid6(19) → 6度带 19 带", CrsUtil.GetProjectedWkid6(19));
        Out.Result("GetDhFromWkid(4497)", CrsUtil.GetDhFromWkid(4497));
        // CM 系列码(4502-4512 / 4534-4554)不含带号信息,无法反推带号,库明确抛异常而非静默给错值
        try
        {
            CrsUtil.GetDhFromWkid(4545);
        }
        catch (ArgumentException ex)
        {
            Out.Result("GetDhFromWkid(4545)(CM 码,无带号可推)", Out.Head(ex.Message));
        }
    }

    private static void ToleranceAndChecks()
    {
        Out.Step("5. 判定与容差");
        Out.Result("IsGeographicCRS(4490)", CrsUtil.IsGeographicCRS(4490));
        Out.Result("IsProjectedCRS(4524)", CrsUtil.IsProjectedCRS(4524));
        Out.Result("GetTolerance(4490 经纬度)", CrsUtil.GetTolerance(4490));
        Out.Result("GetTolerance(4524 投影米)", CrsUtil.GetTolerance(4524));
        Out.Note("拓扑校验、近似相等判断的容差应随坐标系量纲选择:度=1e-7 级,米=默认值级");

        try
        {
            CrsUtil.GetCrsInfo(999999);
        }
        catch (ArgumentException ex)
        {
            Out.Result("非法 WKID", ex.Message);
        }
    }
}
