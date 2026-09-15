using FluentAssertions;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Engine.Util;
using OSGeo.OGR;

namespace OpenGIS.Utils.Tests;

/// <summary>
///     PostgreSQL 写路径回归：多部件几何列类型提升（COPY 曾因 "Geometry type does not match
///     column type" 整批拒收、零行入库）与 Real 字段 float8 化（曾因 DBF 派生 numeric(24,15)
///     对普通大数值报 "numeric field overflow"）。
///     仅在设置 OGU_POSTGIS_CONN 时执行；未配置时跳过（与其它真实环境测试同一约定）。
/// </summary>
[Collection("CultureSensitive")]
public class PostgisWriteTests : IDisposable
{
    private static readonly string? Conn = Environment.GetEnvironmentVariable("OGU_POSTGIS_CONN");
    private readonly List<string> _createdTables = new();

    public void Dispose()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        using var ds = Ogr.Open(Conn, 1);
        foreach (var table in _createdTables)
            ds?.ExecuteSQL($"DROP TABLE IF EXISTS \"{table}\"", null, null)?.Dispose();
    }

    private static bool Skip() => string.IsNullOrWhiteSpace(Conn);

    [Fact]
    public void WritePostGIS_MixedSingleAndMultipartPolygons_LoadsAllRows()
    {
        if (Skip()) return; // 未配置真实 PostGIS 环境

        var table = "ogu_test_promote_" + Guid.NewGuid().ToString("N")[..12];
        _createdTables.Add(table);

        var layer = new OguLayer
        {
            Name = table,
            GeometryType = GeometryType.POLYGON, // 声明单部件，但数据含多部件 —— 历史必炸场景
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "kind", DataType = FieldDataType.STRING, Length = 10 });

        var single = new OguFeature { Fid = 0, Wkt = "POLYGON ((0 0, 1 0, 1 1, 0 1, 0 0))" };
        single.SetValue("kind", "single");
        layer.AddFeature(single);
        var multi = new OguFeature
        {
            Fid = 1,
            Wkt = "MULTIPOLYGON (((10 10, 11 10, 11 11, 10 11, 10 10)),((20 20, 21 20, 21 21, 20 21, 20 20)))"
        };
        multi.SetValue("kind", "multi");
        layer.AddFeature(multi);

        var act = () => PostgisUtil.WritePostGIS(layer, Conn!, table);
        act.Should().NotThrow("列类型应自动提升为 MultiPolygon，单部件要素 WKT 应被包裹为 MULTI");

        var back = PostgisUtil.ReadPostGIS(Conn!, table, null);
        back.GetFeatureCount().Should().Be(2);
        back.Features.Select(f => f.GetValue("kind")?.ToString()).Should().Contain(["single", "multi"]);
    }
}
