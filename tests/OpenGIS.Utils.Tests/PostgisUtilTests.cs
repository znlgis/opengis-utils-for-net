using FluentAssertions;
using OpenGIS.Utils.Engine.Util;

namespace OpenGIS.Utils.Tests;

public class PostgisUtilTests
{
    [Fact]
    public void CreateSpatialIndex_RejectsUnsafeTableIdentifier()
    {
        var act = () => PostgisUtil.CreateSpatialIndex(
            "PG:dbname=test",
            "roads; DROP TABLE users");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*letters*digits*underscores*");
    }

    [Fact]
    public void CreateSpatialIndex_RejectsUnsafeGeometryIdentifier()
    {
        var act = () => PostgisUtil.CreateSpatialIndex(
            "PG:dbname=test",
            "roads",
            "geom); DROP TABLE users");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("geomColumn");
    }
}
