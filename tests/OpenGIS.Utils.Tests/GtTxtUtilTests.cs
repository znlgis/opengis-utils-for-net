using System.Globalization;
using FluentAssertions;
using OpenGIS.Utils.DataSource;

namespace OpenGIS.Utils.Tests;

public class GtTxtUtilTests
{
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
