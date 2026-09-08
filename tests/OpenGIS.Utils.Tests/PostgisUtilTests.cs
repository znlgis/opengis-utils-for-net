using FluentAssertions;
using OpenGIS.Utils.Engine.Util;
using OpenGIS.Utils.Exception;

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
    public void TableExists_ThrowsWhenDataSourceCannotBeOpened()
    {
        // 连不上数据库与"表不存在"是两种不同结果，静默返回 false 会让调用方误判
        var act = () => PostgisUtil.TableExists(
            "PG:host=127.0.0.1 port=1 dbname=postgres user=postgres password=postgres",
            "ogu_test_missing");

        act.Should().Throw<DataSourceException>();
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
