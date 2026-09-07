using FluentAssertions;
using OpenGIS.Utils.Engine;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Tests;

public class GdalWriterTests : IDisposable
{
    private readonly string _testDir;

    public GdalWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "GdalWriterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Write_ThrowsWhenFeatureHasNoGeometry()
    {
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING });
        var feature = new OguFeature { Fid = 7 };
        feature.SetValue("name", "missing geometry");
        layer.AddFeature(feature);

        var path = Path.Combine(_testDir, "points.geojson");
        var act = () => new GdalWriter().Write(layer, path);

        act.Should().Throw<System.Exception>().WithMessage("*1 个要素失败*");
    }

    [Fact]
    public void Write_PreservesLayerSpatialReference()
    {
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING });
        var feature = new OguFeature { Fid = 7, Wkt = "POINT (116.4 39.9)" };
        feature.SetValue("name", "origin");
        layer.AddFeature(feature);

        var path = Path.Combine(_testDir, "points.geojson");
        new GdalWriter().Write(layer, path);

        var writtenLayer = new GdalReader().Read(path);
        writtenLayer.Wkid.Should().Be(4326);
        writtenLayer.Features.Should().ContainSingle().Which.Fid.Should().Be(7);
    }

    [Fact]
    public void Write_ThrowsWhenFeatureGeometryIsInvalid()
    {
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT
        };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING });
        layer.AddFeature(new OguFeature { Fid = 1, Wkt = "NOT A GEOMETRY" });

        var act = () => new GdalWriter().Write(layer, Path.Combine(_testDir, "invalid.geojson"));

        act.Should().Throw<System.Exception>().WithMessage("*1 个要素失败*");
    }

    [Fact]
    public void Write_ThrowsDataSourceExceptionWhenDriverIsUnavailable()
    {
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT
        };
        layer.AddFeature(new OguFeature { Fid = 1, Wkt = "POINT (0 0)" });

        var options = new Dictionary<string, object>
        {
            ["driver"] = "driver-that-does-not-exist"
        };

        var act = () => new GdalWriter().Write(layer, Path.Combine(_testDir, "points.data"), options: options);

        act.Should().Throw<DataSourceException>()
            .WithMessage("*driver-that-does-not-exist*");
    }
}
