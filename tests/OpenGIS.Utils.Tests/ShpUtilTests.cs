using FluentAssertions;
using OpenGIS.Utils.Engine;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
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

    [Fact]
    public void GetShapefileBounds_ReturnsEnvelopeForEmptyShapefile()
    {
        var path = Path.Combine(_testDir, "empty.shp");
        var layer = new OguLayer { Name = "empty", GeometryType = GeometryType.POINT };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING });
        new GdalWriter().Write(layer, path);

        var envelope = ShpUtil.GetShapefileBounds(path);

        envelope.Should().NotBeNull();
    }
}
