using FluentAssertions;
using OpenGIS.Utils.Utils;

namespace OpenGIS.Utils.Tests;

public class NumUtilTests
{
    [Fact]
    public void GetPlainString_WithNormalNumber()
    {
        var result = NumUtil.GetPlainString(123.456);

        result.Should().Be("123.456");
    }

    [Fact]
    public void GetPlainString_WithVerySmallNumber_NoScientificNotation()
    {
        var result = NumUtil.GetPlainString(0.000000001);

        result.Should().NotContain("E").And.NotContain("e");
    }

    [Fact]
    public void GetPlainString_WithNaN()
    {
        var result = NumUtil.GetPlainString(double.NaN);

        result.Should().Be("NaN");
    }

    [Fact]
    public void GetPlainString_WithPositiveInfinity()
    {
        var result = NumUtil.GetPlainString(double.PositiveInfinity);

        // InvariantCulture produces "∞" or "Infinity"
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void GetPlainString_WithNegativeInfinity()
    {
        var result = NumUtil.GetPlainString(double.NegativeInfinity);

        result.Should().NotBeEmpty();
    }

    [Fact]
    public void GetPlainString_DecimalOverload()
    {
        var result = NumUtil.GetPlainString(123.456m);

        result.Should().Be("123.456");
    }

    [Fact]
    public void GetPlainString_DecimalOverload_LargeNumber()
    {
        var result = NumUtil.GetPlainString(123456789.123456789m);

        result.Should().NotBeEmpty();
        result.Should().NotContain("E").And.NotContain("e");
    }

    [Theory]
    [InlineData(1.555, 2, 1.56)]
    [InlineData(1.545, 2, 1.55)]
    [InlineData(2.5, 0, 3.0)]
    [InlineData(-1.5, 0, -2.0)]
    public void Round_WithVariousValues(double value, int decimals, double expected)
    {
        var result = NumUtil.Round(value, decimals);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(3.14159, 2, "3.14")]
    [InlineData(3.14159, 4, "3.1416")]
    [InlineData(100.0, 0, "100")]
    public void FormatNumber_FormatsCorrectly(double value, int decimals, string expected)
    {
        var result = NumUtil.FormatNumber(value, decimals);

        result.Should().Be(expected);
    }

    [Fact]
    public void GetPlainString_WithZero()
    {
        var result = NumUtil.GetPlainString(0.0);

        result.Should().Be("0");
    }
}
