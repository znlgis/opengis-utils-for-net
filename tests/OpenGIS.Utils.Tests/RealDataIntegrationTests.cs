using FluentAssertions;
using OpenGIS.Utils.RealDataHarness;

namespace OpenGIS.Utils.Tests;

/// <summary>
///     通用真实数据集成测试：通过环境变量 OGU_REAL_DATA_DIR 指定包含 Shapefile 的数据目录，
///     harness 自动发现并执行六维度检查。未设置或目录无效时自动跳过（CI 无数据环境不失败）。
/// </summary>
/// <remarks>
///     加入 CultureSensitive 集合以与其他修改进程级 GDAL 配置（SHAPE_ENCODING 等）
///     的测试类串行执行，避免并行时编码配置互相污染。
/// </remarks>
[Collection("CultureSensitive")]
public class RealDataIntegrationTests
{
    private static readonly string? DataDir = Environment.GetEnvironmentVariable("OGU_REAL_DATA_DIR");

    [Fact]
    public void RealData_AllChecks_HaveNoFailures()
    {
        if (string.IsNullOrWhiteSpace(DataDir) || RealDataChecks.ValidateEnvironment(DataDir) != null)
            return; // 未通过 OGU_REAL_DATA_DIR 提供有效数据目录时跳过

        var workDir = Path.Combine(Path.GetTempPath(), "OguRealDataXunit_" + Guid.NewGuid().ToString("N"));
        try
        {
            var results = RealDataChecks.RunAll(DataDir, workDir);

            results.Should().NotBeEmpty("至少应产出检查结果");

            var failures = results.Where(r => r.Status == CheckStatus.Fail).ToList();
            failures.Should().BeEmpty(
                "存在失败项: " + string.Join("; ", failures.Select(f => f.ToString())));

            // 关键不变量：发现的每个图层均读出且要素数与 DBF 头一致
            var discovered = RealDataChecks.DiscoverLayers(DataDir);
            var countPasses = results
                .Count(r => r.Dimension == "1-读取链路" && r.Check == "要素数量" && r.Status == CheckStatus.Pass);
            countPasses.Should().Be(discovered.Count,
                "每个发现的图层都应通过要素数量交叉校验");
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, true);
        }
    }
}
