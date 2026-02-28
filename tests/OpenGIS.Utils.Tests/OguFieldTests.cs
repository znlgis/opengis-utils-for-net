using FluentAssertions;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Tests;

public class OguFieldTests
{
    [Fact]
    public void Clone_CreatesCompleteClone()
    {
        var field = new OguField
        {
            Name = "TestField",
            Alias = "Test Alias",
            DataType = FieldDataType.DOUBLE,
            Length = 50,
            Precision = 10,
            Scale = 2,
            IsNullable = false,
            DefaultValue = 0.0
        };

        var clone = field.Clone();

        clone.Name.Should().Be("TestField");
        clone.Alias.Should().Be("Test Alias");
        clone.DataType.Should().Be(FieldDataType.DOUBLE);
        clone.Length.Should().Be(50);
        clone.Precision.Should().Be(10);
        clone.Scale.Should().Be(2);
        clone.IsNullable.Should().BeFalse();
        clone.DefaultValue.Should().Be(0.0);

        // Verify it's a different instance
        clone.Name = "Modified";
        field.Name.Should().Be("TestField");
    }

    [Fact]
    public void ToJson_FromJson_RoundTrip()
    {
        var field = new OguField
        {
            Name = "Population",
            Alias = "Pop",
            DataType = FieldDataType.INTEGER,
            Length = 10,
            IsNullable = true
        };

        var json = field.ToJson();
        var restored = OguField.FromJson(json);

        restored.Should().NotBeNull();
        restored!.Name.Should().Be("Population");
        restored.Alias.Should().Be("Pop");
        restored.DataType.Should().Be(FieldDataType.INTEGER);
        restored.Length.Should().Be(10);
        restored.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var field = new OguField();

        field.Name.Should().Be(string.Empty);
        field.Alias.Should().BeNull();
        field.Length.Should().BeNull();
        field.Precision.Should().BeNull();
        field.Scale.Should().BeNull();
        field.IsNullable.Should().BeTrue();
        field.DefaultValue.Should().BeNull();
    }
}
