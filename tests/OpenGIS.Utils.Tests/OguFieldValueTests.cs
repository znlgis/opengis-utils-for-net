using FluentAssertions;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Tests;

public class OguFieldValueTests
{
    [Fact]
    public void IsNull_WhenValueIsNull_ReturnsTrue()
    {
        var fv = new OguFieldValue(null);

        fv.IsNull.Should().BeTrue();
    }

    [Fact]
    public void IsNull_WhenValueIsNotNull_ReturnsFalse()
    {
        var fv = new OguFieldValue("hello");

        fv.IsNull.Should().BeFalse();
    }

    [Fact]
    public void GetStringValue_ReturnsStringRepresentation()
    {
        var fv = new OguFieldValue(42);

        fv.GetStringValue().Should().Be("42");
    }

    [Fact]
    public void GetStringValue_ReturnsNullWhenValueIsNull()
    {
        var fv = new OguFieldValue(null);

        fv.GetStringValue().Should().BeNull();
    }

    [Theory]
    [InlineData(42, 42)]
    public void GetIntValue_WithInt_ReturnsValue(int input, int expected)
    {
        var fv = new OguFieldValue(input);

        fv.GetIntValue().Should().Be(expected);
    }

    [Fact]
    public void GetIntValue_WithString_ParsesValue()
    {
        var fv = new OguFieldValue("123");

        fv.GetIntValue().Should().Be(123);
    }

    [Fact]
    public void GetIntValue_WithNull_ReturnsNull()
    {
        var fv = new OguFieldValue(null);

        fv.GetIntValue().Should().BeNull();
    }

    [Fact]
    public void GetIntValue_WithUnparsable_ReturnsNull()
    {
        var fv = new OguFieldValue("not_a_number");

        fv.GetIntValue().Should().BeNull();
    }

    [Fact]
    public void GetLongValue_WithLong_ReturnsValue()
    {
        var fv = new OguFieldValue(9999999999L);

        fv.GetLongValue().Should().Be(9999999999L);
    }

    [Fact]
    public void GetLongValue_WithString_ParsesValue()
    {
        var fv = new OguFieldValue("9999999999");

        fv.GetLongValue().Should().Be(9999999999L);
    }

    [Fact]
    public void GetLongValue_WithNull_ReturnsNull()
    {
        var fv = new OguFieldValue(null);

        fv.GetLongValue().Should().BeNull();
    }

    [Fact]
    public void GetDoubleValue_WithDouble_ReturnsValue()
    {
        var fv = new OguFieldValue(3.14);

        fv.GetDoubleValue().Should().Be(3.14);
    }

    [Fact]
    public void GetDoubleValue_WithString_ParsesValue()
    {
        var fv = new OguFieldValue("3.14");

        fv.GetDoubleValue().Should().Be(3.14);
    }

    [Fact]
    public void GetDoubleValue_WithNull_ReturnsNull()
    {
        var fv = new OguFieldValue(null);

        fv.GetDoubleValue().Should().BeNull();
    }

    [Fact]
    public void GetFloatValue_WithFloat_ReturnsValue()
    {
        var fv = new OguFieldValue(2.5f);

        fv.GetFloatValue().Should().Be(2.5f);
    }

    [Fact]
    public void GetFloatValue_WithString_ParsesValue()
    {
        var fv = new OguFieldValue("2.5");

        fv.GetFloatValue().Should().Be(2.5f);
    }

    [Fact]
    public void GetFloatValue_WithNull_ReturnsNull()
    {
        var fv = new OguFieldValue(null);

        fv.GetFloatValue().Should().BeNull();
    }

    [Fact]
    public void GetBoolValue_WithBool_ReturnsValue()
    {
        var fv = new OguFieldValue(true);

        fv.GetBoolValue().Should().BeTrue();
    }

    [Fact]
    public void GetBoolValue_WithString_ParsesValue()
    {
        var fv = new OguFieldValue("true");

        fv.GetBoolValue().Should().BeTrue();
    }

    [Fact]
    public void GetBoolValue_WithNull_ReturnsNull()
    {
        var fv = new OguFieldValue(null);

        fv.GetBoolValue().Should().BeNull();
    }

    [Fact]
    public void GetDateTimeValue_WithDateTime_ReturnsValue()
    {
        var dt = new DateTime(2024, 1, 15, 10, 30, 0);
        var fv = new OguFieldValue(dt);

        fv.GetDateTimeValue().Should().Be(dt);
    }

    [Fact]
    public void GetDateTimeValue_WithString_ParsesValue()
    {
        var fv = new OguFieldValue("2024-01-15");

        fv.GetDateTimeValue().Should().NotBeNull();
        fv.GetDateTimeValue()!.Value.Year.Should().Be(2024);
        fv.GetDateTimeValue()!.Value.Month.Should().Be(1);
        fv.GetDateTimeValue()!.Value.Day.Should().Be(15);
    }

    [Fact]
    public void GetDateTimeValue_WithNull_ReturnsNull()
    {
        var fv = new OguFieldValue(null);

        fv.GetDateTimeValue().Should().BeNull();
    }

    [Fact]
    public void GetDecimalValue_WithDecimal_ReturnsValue()
    {
        var fv = new OguFieldValue(99.99m);

        fv.GetDecimalValue().Should().Be(99.99m);
    }

    [Fact]
    public void GetDecimalValue_WithString_ParsesValue()
    {
        var fv = new OguFieldValue("99.99");

        fv.GetDecimalValue().Should().Be(99.99m);
    }

    [Fact]
    public void GetDecimalValue_WithNull_ReturnsNull()
    {
        var fv = new OguFieldValue(null);

        fv.GetDecimalValue().Should().BeNull();
    }

    [Fact]
    public void ToString_ReturnsStringRepresentation()
    {
        var fv = new OguFieldValue(42);

        fv.ToString().Should().Be("42");
    }

    [Fact]
    public void ToString_ReturnsEmptyStringWhenNull()
    {
        var fv = new OguFieldValue(null);

        fv.ToString().Should().Be(string.Empty);
    }
}
