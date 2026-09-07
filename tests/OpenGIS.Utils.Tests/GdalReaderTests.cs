using FluentAssertions;
using OpenGIS.Utils.Engine;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.Exception;
using System.Text;
using OSGeo.OGR;
using OgrGeometry = OSGeo.OGR.Geometry;

namespace OpenGIS.Utils.Tests;

/// <summary>
///     该测试类会修改进程级 GDAL 配置（SHAPE_ENCODING），
///     加入 CultureSensitive 集合与其他全局状态相关测试串行执行。
/// </summary>
[Collection("CultureSensitive")]
public class GdalReaderTests : IDisposable
{
    private readonly string _testDir;

    public GdalReaderTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _testDir = Path.Combine(Path.GetTempPath(), "GdalReaderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Read_PreservesSpatialReferenceAndSourceFid()
    {
        var path = Path.Combine(_testDir, "source.geojson");
        File.WriteAllText(path, """
        {
          "type": "FeatureCollection",
          "crs": {
            "type": "name",
            "properties": { "name": "EPSG:4326" }
          },
          "features": [
            {
              "type": "Feature",
              "id": 42,
              "properties": { "name": "origin" },
              "geometry": { "type": "Point", "coordinates": [ 116.4, 39.9 ] }
            }
          ]
        }
        """);

        var layer = new GdalReader().Read(path);

        layer.Wkid.Should().Be(4326);
        layer.Features.Should().ContainSingle();
        layer.Features[0].Fid.Should().Be(42);
    }

    [Fact]
    public void Read_ThrowsDataSourceExceptionWhenPathCannotBeOpened()
    {
        var path = Path.Combine(_testDir, "missing.geojson");

        var act = () => new GdalReader().Read(path);

        act.Should().Throw<DataSourceException>()
            .WithMessage($"*{path}*");
    }

    [Fact]
    public void Read_ThrowsFormatParseExceptionWhenSpatialFilterIsInvalid()
    {
        var path = Path.Combine(_testDir, "source.geojson");
        File.WriteAllText(path, """
        {
          "type": "FeatureCollection",
          "features": []
        }
        """);

        var act = () => new GdalReader().Read(path, spatialFilterWkt: "NOT A GEOMETRY");

        act.Should().Throw<FormatParseException>()
            .WithMessage("*spatial filter*WKT*");
    }

    [Fact]
    public void Read_ThrowsFormatParseExceptionWhenAttributeFilterIsInvalid()
    {
        var path = Path.Combine(_testDir, "source.geojson");
        File.WriteAllText(path, """
        {
          "type": "FeatureCollection",
          "features": []
        }
        """);

        var act = () => new GdalReader().Read(path, attributeFilter: "NOT A SQL FILTER");

        act.Should().Throw<FormatParseException>()
            .WithMessage("*attribute filter*");
    }

    [Fact]
    public void GetLayerNames_ThrowsDataSourceExceptionWhenPathCannotBeOpened()
    {
        var path = Path.Combine(_testDir, "missing.geojson");

        var act = () => new GdalReader().GetLayerNames(path);

        act.Should().Throw<DataSourceException>()
            .WithMessage($"*{path}*");
    }

    [Fact]
    public void Read_ReturnsNullForNullDateTimeField()
    {
        // GPKG 驱动无法存储 "not a date"，写入后实际为 null 字段（与 Shapefile 空 'D' 日期一致）
        var path = Path.Combine(_testDir, "invalid-datetime.gpkg");
        GdalConfiguration.ConfigureGdal();
        var driver = Ogr.GetDriverByName("GPKG");
        using (var dataSource = driver!.CreateDataSource(path, Array.Empty<string>()))
        using (var ogrLayer = dataSource!.CreateLayer("events", null, wkbGeometryType.wkbPoint, Array.Empty<string>()))
        using (var fieldDefinition = new FieldDefn("occurred", FieldType.OFTDateTime))
        {
            ogrLayer.CreateField(fieldDefinition, 1);
            using var ogrFeature = new Feature(ogrLayer.GetLayerDefn());
            using var geometry = OgrGeometry.CreateFromWkt("POINT (0 0)");
            ogrFeature.SetGeometry(geometry);
            ogrFeature.SetField(0, "not a date");
            ogrLayer.CreateFeature(ogrFeature);
            dataSource.SyncToDisk();
        }

        var layer = new GdalReader().Read(path);

        layer.GetFeatureCount().Should().Be(1);
        layer.Features[0].GetValue("occurred").Should().BeNull();
    }

    [Fact]
    public void Read_ReturnsNullForEmptyShapefileDateField()
    {
        // 回归：真实 Shapefile 常见空 'D' 日期字段（空白值），读取应为 null 而非抛 FormatParseException
        var path = Path.Combine(_testDir, "empty-date.shp");
        GdalConfiguration.ConfigureGdal();
        var driver = Ogr.GetDriverByName("ESRI Shapefile");
        using (var dataSource = driver!.CreateDataSource(path, Array.Empty<string>()))
        using (var ogrLayer = dataSource!.CreateLayer("events", null, wkbGeometryType.wkbPoint, Array.Empty<string>()))
        using (var fieldDefinition = new FieldDefn("occurred", FieldType.OFTDate))
        {
            ogrLayer.CreateField(fieldDefinition, 1);
            using var ogrFeature = new Feature(ogrLayer.GetLayerDefn());
            using var geometry = OgrGeometry.CreateFromWkt("POINT (0 0)");
            ogrFeature.SetGeometry(geometry);
            ogrFeature.SetField(0, "        ");
            ogrLayer.CreateFeature(ogrFeature);
            dataSource.SyncToDisk();
        }

        var layer = new GdalReader().Read(path);

        layer.GetFeatureCount().Should().Be(1);
        layer.Features[0].GetValue("occurred").Should().BeNull();
    }

    [Fact]
    public async Task Read_HandlesConcurrentEncodingOptions()
    {
        var utf8Path = CreateEncodedShapefile("utf8", Encoding.UTF8, "UTF8 value");
        var gbkPath = CreateEncodedShapefile("gbk", Encoding.GetEncoding("GBK"), "GBK value");

        var reads = Enumerable.Range(0, 20)
            .SelectMany(_ => new[]
            {
                ReadName(utf8Path, Encoding.UTF8, "UTF8 value"),
                ReadName(gbkPath, Encoding.GetEncoding("GBK"), "GBK value")
            });

        var results = await Task.WhenAll(reads);

        results.Should().OnlyContain(result => result.IsCorrect);
    }

    private string CreateEncodedShapefile(string name, Encoding encoding, string value)
    {
        var path = Path.Combine(_testDir, name + ".shp");
        var layer = new OguLayer { Name = name, GeometryType = GeometryType.POINT };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING, Length = 50 });
        var feature = new OguFeature { Fid = 1, Wkt = "POINT (0 0)" };
        feature.SetValue("name", value);
        layer.AddFeature(feature);

        new GdalWriter().Write(layer, path, options: new Dictionary<string, object> { ["encoding"] = encoding });
        return path;
    }

    private static async Task<(string Value, bool IsCorrect)> ReadName(
        string path, Encoding encoding, string expected)
    {
        await Task.Yield();
        var layer = new GdalReader().Read(path, options: new Dictionary<string, object> { ["encoding"] = encoding });
        var value = layer.Features.Single().GetValue("name")?.ToString() ?? string.Empty;
        return (value, value == expected);
    }
}
