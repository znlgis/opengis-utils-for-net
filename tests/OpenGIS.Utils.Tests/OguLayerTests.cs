using FluentAssertions;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Tests;

public class OguLayerTests
{
    private static OguLayer CreateValidLayer()
    {
        var layer = new OguLayer
        {
            Name = "TestLayer",
            Wkid = 4326,
            GeometryType = GeometryType.POINT
        };
        layer.Fields.Add(new OguField { Name = "Name", DataType = FieldDataType.STRING });
        layer.Fields.Add(new OguField { Name = "Value", DataType = FieldDataType.DOUBLE });

        var feature = new OguFeature { Fid = 1, Wkt = "POINT (1 2)" };
        feature.SetValue("Name", "A");
        feature.SetValue("Value", 1.5);
        layer.Features.Add(feature);

        return layer;
    }

    [Fact]
    public void Constructor_InitializesEmptyFieldsAndFeatures()
    {
        var layer = new OguLayer();

        layer.Fields.Should().NotBeNull().And.BeEmpty();
        layer.Features.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Validate_ThrowsWhenNameIsEmpty()
    {
        var layer = new OguLayer { Name = "" };
        layer.Fields.Add(new OguField { Name = "F1" });

        var act = () => layer.Validate();

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void Validate_ThrowsWhenFieldsIsEmpty()
    {
        var layer = new OguLayer { Name = "Test" };

        var act = () => layer.Validate();

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*at least one field*");
    }

    [Fact]
    public void Validate_ThrowsOnDuplicateFieldNames()
    {
        var layer = new OguLayer { Name = "Test" };
        layer.Fields.Add(new OguField { Name = "Dup" });
        layer.Fields.Add(new OguField { Name = "Dup" });

        var act = () => layer.Validate();

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*duplicated*");
    }

    [Fact]
    public void Validate_ThrowsWhenFieldIsNull()
    {
        var layer = new OguLayer { Name = "Test" };
        layer.Fields.Add(null!);

        var act = () => layer.Validate();

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*field*null*");
    }

    [Fact]
    public void Validate_ThrowsWhenFeatureIsNull()
    {
        var layer = new OguLayer { Name = "Test" };
        layer.Fields.Add(new OguField { Name = "F1" });
        layer.Features.Add(null!);

        var act = () => layer.Validate();

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*feature*null*");
    }

    [Fact]
    public void Validate_ThrowsWhenFeatureAttributesAreNull()
    {
        var layer = new OguLayer { Name = "Test" };
        layer.Fields.Add(new OguField { Name = "F1" });
        layer.Features.Add(new OguFeature { Attributes = null! });

        var act = () => layer.Validate();

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*attributes*null*");
    }

    [Fact]
    public void Validate_ThrowsWhenFeaturesCollectionIsNull()
    {
        var layer = new OguLayer { Name = "Test", Features = null! };
        layer.Fields.Add(new OguField { Name = "F1" });

        var act = () => layer.Validate();

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*features*null*");
    }

    [Fact]
    public void Validate_ThrowsWhenFeatureHasUndefinedAttribute()
    {
        var layer = new OguLayer { Name = "Test" };
        layer.Fields.Add(new OguField { Name = "F1" });
        var feature = new OguFeature { Fid = 1 };
        feature.SetValue("Unknown", "val");
        layer.Features.Add(feature);

        var act = () => layer.Validate();

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*not defined*");
    }

    [Fact]
    public void Validate_PassesWithValidData()
    {
        var layer = CreateValidLayer();

        var act = () => layer.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Filter_ReturnsMatchingFeatures()
    {
        var layer = CreateValidLayer();
        var f2 = new OguFeature { Fid = 2 };
        f2.SetValue("Name", "B");
        f2.SetValue("Value", 3.0);
        layer.Features.Add(f2);

        var result = layer.Filter(f => f.Fid == 2);

        result.Should().HaveCount(1);
        result[0].Fid.Should().Be(2);
    }

    [Fact]
    public void Filter_ThrowsWhenFeaturesCollectionIsNull()
    {
        var layer = new OguLayer { Features = null! };

        var act = () => layer.Filter(_ => true);

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*features*null*");
    }

    [Fact]
    public void GetFeatureCount_ReturnsCorrectCount()
    {
        var layer = CreateValidLayer();

        layer.GetFeatureCount().Should().Be(1);
    }

    [Fact]
    public void GetField_ThrowsWhenFieldsCollectionIsNull()
    {
        var layer = new OguLayer { Fields = null! };

        var act = () => layer.GetField("Name");

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*fields*null*");
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var layer = CreateValidLayer();
        layer.Metadata = new OguLayerMetadata
        {
            DataSource = "test.shp",
            CoordinateSystemName = "WGS84"
        };
        layer.Metadata.ExtendedProperties["key"] = "value";

        var clone = layer.Clone();

        clone.Name.Should().Be(layer.Name);
        clone.Wkid.Should().Be(layer.Wkid);
        clone.GeometryType.Should().Be(layer.GeometryType);
        clone.Fields.Should().HaveCount(layer.Fields.Count);
        clone.Features.Should().HaveCount(layer.Features.Count);
        clone.Metadata.Should().NotBeNull();
        clone.Metadata!.DataSource.Should().Be("test.shp");
        clone.Metadata.ExtendedProperties["key"].Should().Be("value");

        // Verify deep copy: modifying clone should not affect original
        clone.Name = "Modified";
        clone.Name.Should().NotBe(layer.Name);
    }

    [Fact]
    public void Clone_ThrowsWhenFieldsCollectionIsNull()
    {
        var layer = new OguLayer { Name = "Test", Fields = null! };

        var act = () => layer.Clone();

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*fields*null*");
    }

    [Fact]
    public void Clone_ThrowsWhenFeaturesCollectionIsNull()
    {
        var layer = new OguLayer { Name = "Test", Features = null! };

        var act = () => layer.Clone();

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*features*null*");
    }

    [Fact]
    public void AddField_AddsFieldSuccessfully()
    {
        var layer = new OguLayer { Name = "Test" };
        var field = new OguField { Name = "NewField", DataType = FieldDataType.STRING };

        layer.AddField(field);

        layer.Fields.Should().HaveCount(1);
        layer.Fields[0].Name.Should().Be("NewField");
    }

    [Fact]
    public void AddField_ThrowsOnDuplicateName()
    {
        var layer = new OguLayer { Name = "Test" };
        layer.Fields.Add(new OguField { Name = "Existing" });

        var act = () => layer.AddField(new OguField { Name = "Existing" });

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public void AddField_ThrowsWhenFieldIsNull()
    {
        var layer = new OguLayer { Name = "Test" };

        var act = () => layer.AddField(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddFeature_AddsFeature()
    {
        var layer = new OguLayer { Name = "Test" };
        var feature = new OguFeature { Fid = 1 };

        layer.AddFeature(feature);

        layer.Features.Should().HaveCount(1);
        layer.Features[0].Fid.Should().Be(1);
    }

    [Fact]
    public void AddFeature_ThrowsWhenFeatureIsNull()
    {
        var layer = new OguLayer { Name = "Test" };

        var act = () => layer.AddFeature(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Clone_ClonesMutableValues()
    {
        var layer = CreateValidLayer();
        var bytes = new byte[] { 1, 2, 3 };
        layer.Features[0].SetValue("Bytes", bytes);
        layer.Fields.Add(new OguField { Name = "Bytes", DataType = FieldDataType.BINARY, DefaultValue = bytes });
        layer.Metadata = new OguLayerMetadata();
        layer.Metadata.ExtendedProperties["Bytes"] = bytes;

        var clone = layer.Clone();
        ((byte[])clone.Features[0].GetValue("Bytes")!)[0] = 9;
        ((byte[])clone.Fields[^1].DefaultValue!)[1] = 9;
        ((byte[])clone.Metadata!.ExtendedProperties["Bytes"])[2] = 9;

        ((byte[])layer.Features[0].GetValue("Bytes")!).Should().Equal(1, 2, 3);
        ((byte[])layer.Fields[^1].DefaultValue!).Should().Equal(1, 2, 3);
        ((byte[])layer.Metadata!.ExtendedProperties["Bytes"]).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void RemoveFeature_RemovesExistingFeature()
    {
        var layer = CreateValidLayer();

        var result = layer.RemoveFeature(1);

        result.Should().BeTrue();
        layer.Features.Should().BeEmpty();
    }

    [Fact]
    public void RemoveFeature_ReturnsFalseForNonexistentFid()
    {
        var layer = CreateValidLayer();

        var result = layer.RemoveFeature(999);

        result.Should().BeFalse();
        layer.Features.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveFeature_ThrowsWhenFeaturesCollectionIsNull()
    {
        var layer = new OguLayer { Features = null! };

        var act = () => layer.RemoveFeature(1);

        act.Should().Throw<LayerValidationException>()
            .WithMessage("*features*null*");
    }

    [Fact]
    public void GetField_ReturnsCorrectField()
    {
        var layer = CreateValidLayer();

        var field = layer.GetField("Name");

        field.Should().NotBeNull();
        field!.Name.Should().Be("Name");
    }

    [Fact]
    public void GetField_ReturnsNullForMissingField()
    {
        var layer = CreateValidLayer();

        var field = layer.GetField("NonExistent");

        field.Should().BeNull();
    }

    [Fact]
    public void ToJson_FromJson_RoundTrip()
    {
        var layer = CreateValidLayer();

        var json = layer.ToJson();
        var restored = OguLayer.FromJson(json);

        restored.Should().NotBeNull();
        restored!.Name.Should().Be(layer.Name);
        restored.Wkid.Should().Be(layer.Wkid);
        restored.GeometryType.Should().Be(layer.GeometryType);
        restored.Fields.Should().HaveCount(layer.Fields.Count);
        restored.Features.Should().HaveCount(layer.Features.Count);
    }
}
