using FluentAssertions;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.Engine.Util;

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
}
