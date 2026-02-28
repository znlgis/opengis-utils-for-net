using FluentAssertions;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Tests;

public class OguFeatureTests
{
    [Fact]
    public void Constructor_InitializesEmptyAttributes()
    {
        var feature = new OguFeature();

        feature.Attributes.Should().NotBeNull().And.BeEmpty();
        feature.Fid.Should().Be(0);
        feature.Wkt.Should().BeNull();
    }

    [Fact]
    public void SetValue_GetValue_RoundTrip()
    {
        var feature = new OguFeature();

        feature.SetValue("Name", "Test");
        var value = feature.GetValue("Name");

        value.Should().Be("Test");
    }

    [Fact]
    public void SetValue_OverwritesExistingValue()
    {
        var feature = new OguFeature();
        feature.SetValue("Name", "Original");

        feature.SetValue("Name", "Updated");

        feature.GetValue("Name").Should().Be("Updated");
    }

    [Fact]
    public void GetValue_ReturnsNullForMissingField()
    {
        var feature = new OguFeature();

        var value = feature.GetValue("NonExistent");

        value.Should().BeNull();
    }

    [Fact]
    public void HasAttribute_ReturnsTrueWhenPresent()
    {
        var feature = new OguFeature();
        feature.SetValue("Name", "Test");

        feature.HasAttribute("Name").Should().BeTrue();
    }

    [Fact]
    public void HasAttribute_ReturnsFalseWhenMissing()
    {
        var feature = new OguFeature();

        feature.HasAttribute("Missing").Should().BeFalse();
    }

    [Fact]
    public void GetAttribute_ReturnsOguFieldValueWhenPresent()
    {
        var feature = new OguFeature();
        feature.SetValue("Name", "Test");

        var attr = feature.GetAttribute("Name");

        attr.Should().NotBeNull();
        attr!.Value.Should().Be("Test");
    }

    [Fact]
    public void GetAttribute_ReturnsNullWhenMissing()
    {
        var feature = new OguFeature();

        var attr = feature.GetAttribute("Missing");

        attr.Should().BeNull();
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var feature = new OguFeature { Fid = 42, Wkt = "POINT (1 2)" };
        feature.SetValue("Name", "Test");
        feature.SetValue("Value", 3.14);

        var clone = feature.Clone();

        clone.Fid.Should().Be(42);
        clone.Wkt.Should().Be("POINT (1 2)");
        clone.GetValue("Name").Should().Be("Test");
        clone.GetValue("Value").Should().Be(3.14);

        // Verify deep copy
        clone.SetValue("Name", "Modified");
        feature.GetValue("Name").Should().Be("Test");
    }

    [Fact]
    public void ToJson_FromJson_RoundTrip()
    {
        var feature = new OguFeature { Fid = 1, Wkt = "POINT (10 20)" };
        feature.SetValue("Name", "Test");

        var json = feature.ToJson();
        var restored = OguFeature.FromJson(json);

        restored.Should().NotBeNull();
        restored!.Fid.Should().Be(1);
        restored.Wkt.Should().Be("POINT (10 20)");
    }
}
