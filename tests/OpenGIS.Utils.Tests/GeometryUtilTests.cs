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
}
