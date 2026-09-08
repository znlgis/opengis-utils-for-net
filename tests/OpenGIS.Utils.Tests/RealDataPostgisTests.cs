using FluentAssertions;
using OpenGIS.Utils.RealDataHarness;

namespace OpenGIS.Utils.Tests;

/// <summary>
///     真实 PostGIS 往返集成测试：需要同时提供 OGU_REAL_DATA_DIR（含 Shapefile 的数据目录）
///     与 OGU_POSTGIS_CONN（GDAL PostgreSQL 驱动可识别的连接串）才会执行；
///     两个变量都未设置时跳过（CI 无数据库环境不失败），只设置了其中一个或环境不可用时直接失败。
/// </summary>
/// <remarks>
///     测试会在数据库中创建 ogu_test_ 前缀的临时表，harness 在 finally 中全部 DROP。
///     加入 CultureSensitive 集合以与其他修改进程级 GDAL 配置的测试类串行执行。
/// </remarks>
[Collection("CultureSensitive")]
public class RealDataPostgisTests
{
    private static readonly string? DataDir = Environment.GetEnvironmentVariable("OGU_REAL_DATA_DIR");
    private static readonly string? PostgisConn = Environment.GetEnvironmentVariable("OGU_POSTGIS_CONN");

    [Fact]
    public void Postgis_RoundTrip_HaveNoFailures()
    {
        if (string.IsNullOrWhiteSpace(DataDir) && string.IsNullOrWhiteSpace(PostgisConn))
            return; // 未配置真实环境时跳过

        var dataDir = DataDir ?? string.Empty;
        var conn = PostgisConn ?? string.Empty;

        // 一旦显式配置了环境变量，环境不可用就必须失败：静默跳过会让"测试通过"变成假信号
        dataDir.Should().NotBeNullOrWhiteSpace("设置了 OGU_POSTGIS_CONN 就必须同时提供 OGU_REAL_DATA_DIR");
        conn.Should().NotBeNullOrWhiteSpace("设置了 OGU_REAL_DATA_DIR 就必须同时提供 OGU_POSTGIS_CONN");
        RealDataChecks.ValidateEnvironment(dataDir)
            .Should().BeNull($"数据目录 {dataDir} 不可用");
        RealDataChecks.ValidatePostgis(conn)
            .Should().BeNull("PostGIS 连接不可用");

        var workDir = Path.Combine(Path.GetTempPath(), "OguRealDataPgXunit_" + Guid.NewGuid().ToString("N"));
        try
        {
            var results = RealDataChecks.RunAll(dataDir, workDir, conn);
            var postgis = results.Where(r => r.Dimension == RealDataChecks.PostgisDimension).ToList();

            postgis.Should().NotBeEmpty("连接可用时 PostGIS 维度至少应产出检查结果");

            var failures = postgis.Where(r => r.Status == CheckStatus.Fail).ToList();
            failures.Should().BeEmpty(
                "存在失败项: " + string.Join("; ", failures.Select(f => f.ToString())));

            // 关键不变量：每个图层都要走完建表—索引—覆盖写入这条链路
            var layerCount = RealDataChecks.DiscoverLayers(dataDir).Count;
            foreach (var check in new[]
                     {
                         "CreateSpatialIndex(默认列名)",
                         "CreateSpatialIndex(null 自动探测)",
                         "CreateSpatialIndex(重复调用幂等)",
                         "overwrite=true 覆盖写入"
                     })
                postgis.Count(r => r.Check == check && r.Status == CheckStatus.Pass)
                    .Should().Be(layerCount, $"每个图层都应通过 {check}");

            postgis.Should().Contain(
                r => r.Check == "OguLayerUtil.WriteLayer(POSTGIS)" && r.Status == CheckStatus.Pass,
                "DataFormatType.POSTGIS 必须路由到 PostgreSQL 驱动，而不是按扩展名回退到 Shapefile");

            postgis.Should().Contain(
                r => r.Check == "TableExists(不可达连接)" && r.Status == CheckStatus.Pass,
                "连不上数据库与表不存在是两种结果，必须抛异常而不是静默返回 false");
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, true);
        }
    }
}
