using FluentAssertions;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.Engine.Util;
using OgrGeometry = OSGeo.OGR.Geometry;

namespace OpenGIS.Utils.Tests;

public class CrsUtilTests
{
    [Theory]
    [InlineData(116.404, 39)]
    [InlineData(120.0, 40)]
    [InlineData(0.0, 0)]
    [InlineData(-1.5, 0)]
    [InlineData(-3.0, -1)]
    public void GetDh_Computes3DegreeZone(double longitude, int expected)
    {
        CrsUtil.GetDh(longitude).Should().Be(expected);
    }

    [Theory]
    [InlineData(116.404, 20)]
    [InlineData(120.0, 21)]
    [InlineData(0.0, 1)]
    [InlineData(6.0, 2)]
    [InlineData(-6.0, 0)]
    public void GetDh6_Computes6DegreeZone(double longitude, int expected)
    {
        CrsUtil.GetDh6(longitude).Should().Be(expected);
    }

    [Theory]
    [InlineData(4491, 13)] // CGCS2000 / Gauss-Kruger zone 13（6度带）
    [InlineData(4501, 23)] // CGCS2000 / Gauss-Kruger zone 23（6度带）
    [InlineData(4513, 25)] // CGCS2000 / 3-degree Gauss-Kruger zone 25
    [InlineData(4524, 36)] // CGCS2000 / 3-degree Gauss-Kruger zone 36
    [InlineData(4533, 45)] // CGCS2000 / 3-degree Gauss-Kruger zone 45
    public void GetDhFromWkid_ReturnsZoneNumber(int wkid, int expected)
    {
        CrsUtil.GetDhFromWkid(wkid).Should().Be(expected);
    }

    [Theory]
    [InlineData(4326)] // 地理坐标系，无带号概念
    [InlineData(4503)] // CGCS2000 / Gauss-Kruger CM 81E：中央经线码，不携带带号
    [InlineData(4545)] // CGCS2000 / 3-degree Gauss-Kruger CM 108E：同上
    public void GetDhFromWkid_ThrowsForWkidWithoutZone(int wkid)
    {
        var act = () => CrsUtil.GetDhFromWkid(wkid);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(25, 4513)]
    [InlineData(36, 4524)]
    [InlineData(45, 4533)]
    public void GetProjectedWkid_Returns3DegreeWkid(int zone, int expected)
    {
        CrsUtil.GetProjectedWkid(zone).Should().Be(expected);
    }

    [Theory]
    [InlineData(23)]
    [InlineData(24)] // EPSG 未收录 3度带 24 带码
    [InlineData(46)]
    public void GetProjectedWkid_ThrowsForInvalidZone(int zone)
    {
        var act = () => CrsUtil.GetProjectedWkid(zone);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(13, 4491)]
    [InlineData(23, 4501)]
    public void GetProjectedWkid6_Returns6DegreeWkid(int zone, int expected)
    {
        CrsUtil.GetProjectedWkid6(zone).Should().Be(expected);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    public void GetProjectedWkid6_ThrowsForInvalidZone(int zone)
    {
        var act = () => CrsUtil.GetProjectedWkid6(zone);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(4326)]
    [InlineData(4490)]
    public void GetTolerance_ReturnsSmallToleranceForGeographic(int wkid)
    {
        CrsUtil.GetTolerance(wkid).Should().Be(0.0000001);
    }

    [Fact]
    public void GetTolerance_ReturnsDefaultForProjected()
    {
        CrsUtil.GetTolerance(32650).Should().Be(LibrarySettings.DefaultTolerance);
    }

    [Fact]
    public void GetTolerance_ReturnsDefaultForUnknownWkid()
    {
        CrsUtil.GetTolerance(0).Should().Be(LibrarySettings.DefaultTolerance);
    }

    [Theory]
    [InlineData(4491, true)]
    [InlineData(4554, true)]
    [InlineData(4513, true)]
    [InlineData(32601, true)]
    [InlineData(32660, true)]
    [InlineData(32701, true)]
    [InlineData(32760, true)]
    [InlineData(4326, false)]
    [InlineData(4490, false)]
    [InlineData(4269, false)]
    [InlineData(1000, false)]
    [InlineData(2000, true)]
    public void IsProjectedCRS_ClassifiesWkid(int wkid, bool expected)
    {
        CrsUtil.IsProjectedCRS(wkid).Should().Be(expected);
    }

    [Theory]
    [InlineData(4326, true)]
    [InlineData(4490, true)]
    [InlineData(2326, false)]
    [InlineData(3826, false)]
    [InlineData(3857, false)]
    [InlineData(32650, false)]
    public void IsGeographicCRS_UsesSpatialReferenceMetadata(int wkid, bool expected)
    {
        CrsUtil.IsGeographicCRS(wkid).Should().Be(expected);
    }

    [Theory]
    [InlineData(2326)]
    [InlineData(3826)]
    [InlineData(3857)]
    [InlineData(32650)]
    public void IsProjectedCRS_RecognizesInternationalAndRegionalCrs(int wkid)
    {
        CrsUtil.IsProjectedCRS(wkid).Should().BeTrue();
    }

    [Fact]
    public void GetCrsInfo_ReturnsMetadataFromSpatialReference()
    {
        var info = CrsUtil.GetCrsInfo(2326);

        info.Wkid.Should().Be(2326);
        info.Name.Should().NotBeNullOrWhiteSpace();
        info.IsProjected.Should().BeTrue();
        info.IsGeographic.Should().BeFalse();
    }

    [Fact]
    public void TransformThrough_AppliesIntermediateCoordinateSystem()
    {
        const string wkt = "POINT (836694.05 819597.91)";

        var expected = CrsUtil.Transform(
            CrsUtil.Transform(wkt, 2326, 4326),
            4326,
            4490);

        var actual = CrsUtil.TransformThrough(wkt, 2326, 4326, 4490);

        actual.Should().Be(expected);
    }

    [Fact]
    public void GetTransformRecommendation_IdentifiesHk1980ToCgcs2000Risk()
    {
        var recommendation = CrsUtil.GetTransformRecommendation(2326, 4490);

        recommendation.RequiresExplicitPath.Should().BeTrue();
        recommendation.IntermediateWkids.Should().ContainSingle().Which.Should().Be(4326);
        recommendation.Message.Should().Contain("4326");
    }

    [Fact]
    public void Transform_ThrowsForInvalidSourceWkid()
    {
        var act = () => CrsUtil.Transform("POINT (116 40)", 0, 4326);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*source*WKID*");
    }

    [Fact]
    public void Transform_ThrowsForInvalidTargetWkid()
    {
        var act = () => CrsUtil.Transform("POINT (116 40)", 4326, 0);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*target*WKID*");
    }

    [Fact]
    public void Transform_ThrowsForInvalidWkidEvenWhenSourceAndTargetMatch()
    {
        var act = () => CrsUtil.Transform("POINT (116 40)", 0, 0);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*WKID*");
    }

    [Fact]
    public void Transform_ThrowsForEmptyGeometry()
    {
        using var geometry = OgrGeometry.CreateFromWkt("POINT EMPTY");

        var act = () => CrsUtil.Transform(geometry!, 4326, 4490);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void GetDh_ThrowsWhenCentroidCannotBeRead()
    {
        using var geometry = OgrGeometry.CreateFromWkt("POLYGON EMPTY");

        var act = () => CrsUtil.GetDh(geometry!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Transform_ReturnsValidWktForGeometry()
    {
        var transformed = CrsUtil.Transform("POINT (1 2)", 4326, 3857);

        transformed.Should().Contain("POINT");
    }
}
