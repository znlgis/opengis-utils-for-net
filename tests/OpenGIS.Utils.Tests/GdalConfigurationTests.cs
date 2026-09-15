using FluentAssertions;
using OpenGIS.Utils.Configuration;
using OSGeo.GDAL;

namespace OpenGIS.Utils.Tests;

public class GdalConfigurationTests
{
    [Fact]
    public void ConfigureGdal_PointsGdalDataAtDirectoryContainingDxfTemplate()
    {
        // DXF 写驱动依赖 GDAL_DATA 下的 header.dxf。MaxRev 的自动探测会命中不含该文件的
        // 输出目录，且 GDAL 的 CPLFindFile 按线程缓存查找路径快照，一旦在错误目录上初始化
        // 便无法通过后续修正 GDAL_DATA 恢复（CI 上表现为间歇性
        // "Failed to find template header file header.dxf"）。
        GdalConfiguration.ConfigureGdal();

        var gdalData = Environment.GetEnvironmentVariable("GDAL_DATA");
        gdalData.Should().NotBeNullOrEmpty();
        File.Exists(Path.Combine(gdalData!, "header.dxf")).Should().BeTrue(
            "GDAL_DATA 必须指向包含 header.dxf 的目录，否则 DXF 驱动无法创建数据源");
    }

    [Fact]
    public void ConfigureGdal_ResolvesDxfTemplateThroughFinder()
    {
        // 直接验证 GDAL 的查找器能解析 header.dxf，覆盖 TLS 快照被冻结的场景
        GdalConfiguration.ConfigureGdal();

        var resolved = Gdal.FindFile("gdal", "header.dxf");
        resolved.Should().NotBeNullOrEmpty();
        File.Exists(resolved).Should().BeTrue();
    }
}
