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
    public void Read_ThrowsFormatParseExceptionWhenGpkgAttributeFilterIsInvalid()
    {
        // GPKG(SQLite) 把过滤器编译推迟到首次 GetNextFeature，非法过滤同样要包装成 FormatParseException，
        // 不得泄漏 OGR 原生异常（回归：曾抛裸 ApplicationException）
        var path = Path.Combine(_testDir, "deferred-filter.gpkg");
        var layer = new OguLayer { Name = "points", GeometryType = GeometryType.POINT };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING, Length = 50 });
        var feature = new OguFeature { Fid = 1, Wkt = "POINT (0 0)" };
        feature.SetValue("name", "value");
        layer.AddFeature(feature);
        new GdalWriter().Write(layer, path);

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

    [Fact]
    public void Read_RestoresPreviousShapeEncodingConfigOption()
    {
        // 声明式读取不得把 SHAPE_ENCODING 遗留在进程级，污染后续读写（回归：读后从不恢复）
        var path = CreateEncodedShapefile("restore-encoding", Encoding.UTF8, "value");
        OSGeo.GDAL.Gdal.SetConfigOption("SHAPE_ENCODING", "CUSTOM");
        try
        {
            new GdalReader().Read(path, options: new Dictionary<string, object> { ["encoding"] = Encoding.UTF8 });

            OSGeo.GDAL.Gdal.GetConfigOption("SHAPE_ENCODING", null).Should().Be("CUSTOM");
        }
        finally
        {
            OSGeo.GDAL.Gdal.SetConfigOption("SHAPE_ENCODING", null);
        }
    }

    [Fact]
    public void Read_ClearsShapeEncodingConfigOptionWhenPreviouslyUnset()
    {
        var path = CreateEncodedShapefile("clear-encoding", Encoding.UTF8, "value");
        OSGeo.GDAL.Gdal.SetConfigOption("SHAPE_ENCODING", null);

        new GdalReader().Read(path, options: new Dictionary<string, object> { ["encoding"] = Encoding.GetEncoding("GBK") });

        OSGeo.GDAL.Gdal.GetConfigOption("SHAPE_ENCODING", null).Should().BeNull();
    }

    [Fact]
    public void Write_EncodesShapefileDbfAndCpgPerEncodingOption()
    {
        // encoding 对 shapefile 驱动是数据集级创建选项：DBF 应按 GBK 落盘并自动生成配套 .cpg
        // （回归：曾作为图层创建选项传入被驱动忽略，DBF 恒为 UTF-8 且 .cpg 内容与实际字节不符）
        OSGeo.GDAL.Gdal.SetConfigOption("SHAPE_ENCODING", null);
        var path = CreateEncodedShapefile("gbk-bytes", Encoding.GetEncoding("GBK"), "示例地块");

        var dbfBytes = File.ReadAllBytes(Path.ChangeExtension(path, ".dbf"));
        ContainsSequence(dbfBytes, Encoding.GetEncoding("GBK").GetBytes("示例地块")).Should().BeTrue();
        File.Exists(Path.ChangeExtension(path, ".cpg")).Should().BeTrue();

        // 不声明编码读取时应依据 .cpg 自动识别，读回正确中文
        var layer = new GdalReader().Read(path);
        layer.Features.Single().GetValue("name").Should().Be("示例地块");
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var matched = true;
            for (var j = 0; j < needle.Length; j++)
                if (haystack[i + j] != needle[j])
                {
                    matched = false;
                    break;
                }

            if (matched)
                return true;
        }

        return false;
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
