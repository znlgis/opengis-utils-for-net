using FluentAssertions;
using OpenGIS.Utils.Engine;

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
}
