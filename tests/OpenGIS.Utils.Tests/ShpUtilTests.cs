using FluentAssertions;
using OpenGIS.Utils.Engine.Util;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Tests;

public class ShpUtilTests : IDisposable
{
    private readonly string _testDir;

    public ShpUtilTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ShpUtilTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void GetShapefileBounds_ThrowsDataSourceExceptionWhenDataSourceCannotBeOpened()
    {
        var path = Path.Combine(_testDir, "invalid.shp");
        File.WriteAllText(path, "not a shapefile");

        var act = () => ShpUtil.GetShapefileBounds(path);

        act.Should().Throw<DataSourceException>()
            .WithMessage("*data source*");
    }
}
