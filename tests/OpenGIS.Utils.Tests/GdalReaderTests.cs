using FluentAssertions;
using OpenGIS.Utils.Engine;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Tests;

public class GdalReaderTests : IDisposable
{
    private readonly string _testDir;

    public GdalReaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "GdalReaderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Read_PreservesSpatialReferenceAndSourceFid()
    {
        var path = Path.Combine(_testDir, "source.geojson");
        File.WriteAllText(path, """
        {
          "type": "FeatureCollection",
          "crs": {
            "type": "name",
            "properties": { "name": "EPSG:4326" }
          },
          "features": [
            {
              "type": "Feature",
              "id": 42,
              "properties": { "name": "origin" },
              "geometry": { "type": "Point", "coordinates": [ 116.4, 39.9 ] }
            }
          ]
        }
        """);

        var layer = new GdalReader().Read(path);

        layer.Wkid.Should().Be(4326);
        layer.Features.Should().ContainSingle();
        layer.Features[0].Fid.Should().Be(42);
    }

    [Fact]
    public void Read_ThrowsDataSourceExceptionWhenPathCannotBeOpened()
    {
        var path = Path.Combine(_testDir, "missing.geojson");

        var act = () => new GdalReader().Read(path);

        act.Should().Throw<DataSourceException>()
            .WithMessage($"*{path}*");
    }

    [Fact]
    public void Read_ThrowsFormatParseExceptionWhenSpatialFilterIsInvalid()
    {
        var path = Path.Combine(_testDir, "source.geojson");
        File.WriteAllText(path, """
        {
          "type": "FeatureCollection",
          "features": []
        }
        """);

        var act = () => new GdalReader().Read(path, spatialFilterWkt: "NOT A GEOMETRY");

        act.Should().Throw<FormatParseException>()
            .WithMessage("*spatial filter*WKT*");
    }

    [Fact]
    public void Read_ThrowsFormatParseExceptionWhenAttributeFilterIsInvalid()
    {
        var path = Path.Combine(_testDir, "source.geojson");
        File.WriteAllText(path, """
        {
          "type": "FeatureCollection",
          "features": []
        }
        """);

        var act = () => new GdalReader().Read(path, attributeFilter: "NOT A SQL FILTER");

        act.Should().Throw<FormatParseException>()
            .WithMessage("*attribute filter*");
    }

    [Fact]
    public void GetLayerNames_ThrowsDataSourceExceptionWhenPathCannotBeOpened()
    {
        var path = Path.Combine(_testDir, "missing.geojson");

        var act = () => new GdalReader().GetLayerNames(path);

        act.Should().Throw<DataSourceException>()
            .WithMessage($"*{path}*");
    }
}
