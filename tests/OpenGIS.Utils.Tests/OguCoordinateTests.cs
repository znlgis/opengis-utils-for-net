using FluentAssertions;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Tests;

public class OguCoordinateTests
{
    [Fact]
    public void ToWkt_With2DCoordinate()
    {
        var coord = new OguCoordinate { X = 120.5, Y = 30.2 };

        var wkt = coord.ToWkt();

        wkt.Should().Be("POINT (120.5 30.2)");
    }

    [Fact]
    public void ToWkt_With3DCoordinate()
    {
        var coord = new OguCoordinate { X = 120.5, Y = 30.2, Z = 100.0 };

        var wkt = coord.ToWkt();

        wkt.Should().Be("POINT Z (120.5 30.2 100)");
    }

    [Fact]
    public void FromWkt_Parses2DPoint()
    {
        var coord = OguCoordinate.FromWkt("POINT (120.5 30.2)");

        coord.X.Should().Be(120.5);
        coord.Y.Should().Be(30.2);
        coord.Z.Should().BeNull();
    }

    [Fact]
    public void FromWkt_Parses3DPoint()
    {
        var coord = OguCoordinate.FromWkt("POINT Z (120.5 30.2 100)");

        coord.X.Should().Be(120.5);
        coord.Y.Should().Be(30.2);
        coord.Z.Should().Be(100.0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromWkt_ThrowsOnEmptyInput(string? wkt)
    {
        var act = () => OguCoordinate.FromWkt(wkt!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromWkt_ThrowsOnNonPointGeometry()
    {
        var act = () => OguCoordinate.FromWkt("LINESTRING (0 0, 1 1)");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*POINT*");
    }

    [Fact]
    public void FromWkt_ParsesCaseInsensitive()
    {
        var coord = OguCoordinate.FromWkt("point (10 20)");

        coord.X.Should().Be(10.0);
        coord.Y.Should().Be(20.0);
    }
}
