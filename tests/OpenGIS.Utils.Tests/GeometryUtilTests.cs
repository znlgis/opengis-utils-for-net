using FluentAssertions;
using OpenGIS.Utils.Geometry;
using OgrGeometry = OSGeo.OGR.Geometry;

namespace OpenGIS.Utils.Tests;

public class GeometryUtilTests
{
    [Fact]
    public void Union_ThrowsWhenGeometryCollectionContainsNull()
    {
        using var point = OgrGeometry.CreateFromWkt("POINT (0 0)");
        var geometries = new[] { point!, null! };

        var act = () => GeometryUtil.Union(geometries);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*null*geometry*");
    }

    [Fact]
    public void Geometry2Wkt_ReturnsValidWkt()
    {
        using var point = OgrGeometry.CreateFromWkt("POINT (1 2)");

        GeometryUtil.Geometry2Wkt(point!).Should().Be("POINT (1 2)");
    }

    [Fact]
    public void Geojson2Geometry_ParsesBareGeometry()
    {
        using var geometry = GeometryUtil.Geojson2Geometry("""{"type": "Point", "coordinates": [116.4, 39.9]}""");

        GeometryUtil.Geometry2Wkt(geometry).Should().Be("POINT (116.4 39.9)");
    }

    [Fact]
    public void Geojson2Geometry_ParsesFeatureAndExtractsGeometry()
    {
        using var geometry = GeometryUtil.Geojson2Geometry("""
        {
          "type": "Feature",
          "properties": { "name": "demo" },
          "geometry": { "type": "LineString", "coordinates": [[0, 0], [1, 1]] }
        }
        """);
        using var expectedSource = OgrGeometry.CreateFromWkt("LINESTRING (0 0,1 1)");

        // 与 GDAL 规范化 WKT 比较，避免绑定版本间输出格式差异
        GeometryUtil.Geometry2Wkt(geometry).Should().Be(GeometryUtil.Geometry2Wkt(expectedSource!));
    }

    [Fact]
    public void Geojson2Wkt_RoundTripsWithWkt2Geojson()
    {
        const string wkt = "POLYGON ((0 0,4 0,4 3,0 3,0 0))";
        using var expectedSource = OgrGeometry.CreateFromWkt(wkt);

        GeometryUtil.Geojson2Wkt(GeometryUtil.Wkt2Geojson(wkt)).Should().Be(GeometryUtil.Geometry2Wkt(expectedSource!));
    }

    [Fact]
    public void Geojson2Geometry_ThrowsForUnparseableInput()
    {
        var act = () => GeometryUtil.Geojson2Geometry("""{"not": "geojson"}""");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*GeoJSON*");
    }

    [Fact]
    public void Geojson2Geometry_ThrowsForFeatureCollectionWithoutFeatures()
    {
        var act = () => GeometryUtil.Geojson2Geometry("""{"type": "FeatureCollection", "features": []}""");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*GeoJSON*");
    }

    [Fact]
    public void Geojson2Geometry_ThrowsForNullOrWhitespace()
    {
        var act = () => GeometryUtil.Geojson2Geometry("   ");

        act.Should().Throw<ArgumentException>();
    }
}
