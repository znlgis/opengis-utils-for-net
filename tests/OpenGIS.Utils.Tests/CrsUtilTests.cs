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
    [InlineData(4491, 24)]
    [InlineData(4500, 33)]
    [InlineData(4513, 46)]
    [InlineData(4554, 87)]
    public void GetDhFromWkid_Returns3DegreeZone(int wkid, int expected)
    {
        CrsUtil.GetDhFromWkid(wkid).Should().Be(expected);
    }

    [Fact]
    public void GetDhFromWkid_ThrowsForUnknownWkid()
    {
        var act = () => CrsUtil.GetDhFromWkid(4326);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(24, 4491)]
    [InlineData(45, 4512)]
    public void GetProjectedWkid_Returns3DegreeWkid(int zone, int expected)
    {
        CrsUtil.GetProjectedWkid(zone).Should().Be(expected);
    }

    [Theory]
    [InlineData(23)]
    [InlineData(46)]
    public void GetProjectedWkid_ThrowsForInvalidZone(int zone)
    {
        var act = () => CrsUtil.GetProjectedWkid(zone);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(13, 4513)]
    [InlineData(23, 4523)]
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
