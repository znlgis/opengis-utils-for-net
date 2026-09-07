using System.Globalization;
using FluentAssertions;
using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Tests;

[Collection("CultureSensitive")]
public class GtTxtUtilTests
{
    [Fact]
    public void SaveTxt_ThrowsFormatParseExceptionForInvalidFeatureWkt()
    {
        var layer = new OguLayer { Name = "points", GeometryType = GeometryType.POINT };
        layer.AddFeature(new OguFeature { Fid = 1, Wkt = "NOT A GEOMETRY" });
        var path = Path.Combine(Path.GetTempPath(), $"GtTxtUtilTests_{Guid.NewGuid():N}.txt");

        try
        {
            var act = () => GtTxtUtil.SaveTxt(layer, path);

            act.Should().Throw<FormatParseException>()
                .WithMessage("*WKT*Fid=1*");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void ParseTxtLine_Parses2DCoordinate()
    {
        var coord = GtTxtUtil.ParseTxtLine("J1 1 500000.123 4400000.456");

        coord.Should().NotBeNull();
        coord!.PointNumber.Should().Be("J1");
        coord.RingNumber.Should().Be("1");
        coord.X.Should().Be(500000.123);
        coord.Y.Should().Be(4400000.456);
    }

    [Fact]
    public void ParseTxtLine_ParsesZAndRemark()
    {
        var coord = GtTxtUtil.ParseTxtLine("J2 1 100.5 200.5 12.5 界址点");

        coord.Should().NotBeNull();
        coord!.Z.Should().Be(12.5);
        coord.Remark.Should().Be("界址点");
    }

    [Fact]
    public void ParseTxtLine_ReturnsNullForHeaderOrInvalidLine()
    {
        GtTxtUtil.ParseTxtLine("点号 圈号 X Y Z 备注").Should().BeNull();
        GtTxtUtil.ParseTxtLine("   ").Should().BeNull();
    }

    [Fact]
    public void ParseTxtLine_IsCultureInvariant()
    {
        // 在使用逗号作为小数分隔符的区域设置下，
        // 坐标文件中的 '.' 仍必须被正确解析。
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var coord = GtTxtUtil.ParseTxtLine("J1 1 500000.123 4400000.456 12.5");

            coord.Should().NotBeNull();
            coord!.X.Should().Be(500000.123);
            coord.Y.Should().Be(4400000.456);
            coord.Z.Should().Be(12.5);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ParseTxtLine_ParsesSignedAndScientificCoordinates()
    {
        var coord = GtTxtUtil.ParseTxtLine("J3 1 -1.25e+3 -4.5E-2 -6.75e1");

        coord.Should().NotBeNull();
        coord!.X.Should().Be(-1250);
        coord.Y.Should().Be(-0.045);
        coord.Z.Should().Be(-67.5);
    }

    [Fact]
    public void TryParseTxtLine_ReturnsFalseForInvalidLine()
    {
        var success = GtTxtUtil.TryParseTxtLine("J3 1 invalid 2", out var coordinate);

        success.Should().BeFalse();
        coordinate.Should().BeNull();
    }

    [Fact]
    public void SaveTxt_ThrowsWhenFeaturesCollectionIsNull()
    {
        var layer = new OguLayer { Name = "points", Features = null! };
        var path = Path.Combine(Path.GetTempPath(), $"GtTxtUtilTests_{Guid.NewGuid():N}.txt");

        try
        {
            var act = () => GtTxtUtil.SaveTxt(layer, path);

            act.Should().Throw<ArgumentException>().WithMessage("*features*");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LoadTxt_ThrowsFormatParseExceptionForInvalidCoordinateLine()
    {
        var path = Path.Combine(Path.GetTempPath(), $"GtTxtUtilTests_{Guid.NewGuid():N}.txt");
        File.WriteAllLines(path, new[]
        {
            "J1 1 100.0 200.0",
            "J2 1 invalid 300.0"
        });

        try
        {
            var act = () => GtTxtUtil.LoadTxt(path);

            act.Should().Throw<FormatParseException>()
                .WithMessage("*line*J2*invalid*");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void SaveTxt_LoadTxt_RoundTripsWithHeaderLine()
    {
        // 回归：SaveTxt 输出的列头行（点号/圈号/X/Y/Z/备注）应能被 LoadTxt 跳过
        var layer = new OguLayer { Name = "points", GeometryType = GeometryType.POINT };
        layer.AddField(new OguField { Name = "点号", DataType = FieldDataType.STRING });
        layer.AddFeature(new OguFeature { Fid = 1, Wkt = "POINT (100.5 200.25 12.5)" });
        layer.AddFeature(new OguFeature { Fid = 2, Wkt = "POINT (101.5 201.25)" });
        var path = Path.Combine(Path.GetTempPath(), $"GtTxtUtilTests_{Guid.NewGuid():N}.txt");

        try
        {
            GtTxtUtil.SaveTxt(layer, path);

            var loaded = GtTxtUtil.LoadTxt(path);

            loaded.GetFeatureCount().Should().Be(2);
            loaded.Features[1].GetValue("X").Should().Be(101.5);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void FormatTxtLine_RoundTripsWithParse()
    {
        var line = GtTxtUtil.ParseTxtLine("J1 2 500000.5 4400000.25 3.5 角点");

        line.Should().NotBeNull();

        var formatted = GtTxtUtil.FormatTxtLine(line!, 0);
        var reparsed = GtTxtUtil.ParseTxtLine(formatted.Replace('\t', ' '));

        reparsed.Should().NotBeNull();
        reparsed!.X.Should().Be(line!.X);
        reparsed.Y.Should().Be(line.Y);
        reparsed.Z.Should().Be(line.Z);
    }
}
