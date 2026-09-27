using FluentAssertions;
using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Tests;

public class GdalWriterTests : IDisposable
{
    private readonly string _testDir;

    public GdalWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "GdalWriterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Write_SkipsFeaturesWithoutGeometry()
    {
        // 真实数据常含 NullShape 记录：空几何要素应被跳过而非让整层写入判为失败
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING });
        var withGeom = new OguFeature { Fid = 1, Wkt = "POINT (116.4 39.9)" };
        withGeom.SetValue("name", "valid");
        layer.AddFeature(withGeom);
        var feature = new OguFeature { Fid = 7 };
        feature.SetValue("name", "missing geometry");
        layer.AddFeature(feature);

        var path = Path.Combine(_testDir, "points.geojson");
        var act = () => new GdalWriter().Write(layer, path);

        act.Should().NotThrow();
        var read = OguLayerUtil.ReadLayer(DataFormatType.GEOJSON, path);
        read.Features.Should().ContainSingle()
            .Which.GetValue("name").Should().Be("valid");
    }

    [Fact]
    public void Write_ThrowsArgumentExceptionWhenFeaturesCollectionIsNull()
    {
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Features = null!
        };

        var act = () => new GdalWriter().Write(layer, Path.Combine(_testDir, "points.geojson"));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*features*");
    }

    [Fact]
    public void Append_ThrowsArgumentExceptionWhenFieldsCollectionIsNull()
    {
        var path = Path.Combine(_testDir, "points.gpkg");
        new GdalWriter().Write(CreatePointLayer(1, "first", "POINT (0 0)"), path);

        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Fields = null!
        };

        var act = () => new GdalWriter().Append(layer, path);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*fields*");
    }

    [Fact]
    public void Write_PreservesLayerSpatialReference()
    {
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING });
        var feature = new OguFeature { Fid = 7, Wkt = "POINT (116.4 39.9)" };
        feature.SetValue("name", "origin");
        layer.AddFeature(feature);

        var path = Path.Combine(_testDir, "points.geojson");
        new GdalWriter().Write(layer, path);

        var writtenLayer = new GdalReader().Read(path);
        writtenLayer.Wkid.Should().Be(4326);
        writtenLayer.Features.Should().ContainSingle().Which.Fid.Should().Be(7);
    }

    [Fact]
    public void Write_ThrowsWhenFeatureGeometryIsInvalid()
    {
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT
        };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING });
        layer.AddFeature(new OguFeature { Fid = 1, Wkt = "NOT A GEOMETRY" });

        var act = () => new GdalWriter().Write(layer, Path.Combine(_testDir, "invalid.geojson"));

        act.Should().Throw<System.Exception>().WithMessage("*1 个要素失败*");
    }

    [Fact]
    public void Write_ThrowsDataSourceExceptionWhenDriverIsUnavailable()
    {
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT
        };
        layer.AddFeature(new OguFeature { Fid = 1, Wkt = "POINT (0 0)" });

        var options = new Dictionary<string, object>
        {
            ["driver"] = "driver-that-does-not-exist"
        };

        var act = () => new GdalWriter().Write(layer, Path.Combine(_testDir, "points.data"), options: options);

        act.Should().Throw<DataSourceException>()
            .WithMessage("*driver-that-does-not-exist*");
    }

    [Fact]
    public void Write_ThrowsDataSourceExceptionWhenExistingDataSourceCannotBeDeleted()
    {
        var path = Path.Combine(_testDir, "blocked.geojson");
        Directory.CreateDirectory(path);

        var act = () => new GdalWriter().Write(CreatePointLayer(1, "point", "POINT (0 0)"), path);

        act.Should().Throw<DataSourceException>()
            .WithMessage("*delete*data source*");
    }

    [Fact]
    public void Write_ThrowsDataSourceExceptionWhenFieldCreationFails()
    {
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Fields = new List<OguField>
            {
                new() { Name = "name", DataType = FieldDataType.STRING },
                new() { Name = "name", DataType = FieldDataType.STRING }
            }
        };
        layer.AddFeature(new OguFeature { Fid = 1, Wkt = "POINT (0 0)" });

        var act = () => new GdalWriter().Write(layer, Path.Combine(_testDir, "duplicate-fields.gpkg"));

        act.Should().Throw<DataSourceException>()
            .WithMessage("*field*name*");
    }

    [Fact]
    public void Append_AddsFeaturesToExistingLayer()
    {
        var path = Path.Combine(_testDir, "points.gpkg");
        var writer = new GdalWriter();
        var initialLayer = CreatePointLayer(1, "first", "POINT (0 0)");
        writer.Write(initialLayer, path);

        var appendedLayer = CreatePointLayer(2, "second", "POINT (1 1)");
        writer.Append(appendedLayer, path);

        var result = new GdalReader().Read(path);
        result.Features.Should().HaveCount(2);
        result.Features.Should().Contain(feature => feature.Fid == 1);
        result.Features.Should().Contain(feature => feature.Fid == 2);
    }

    [Fact]
    public void Append_ThrowsDataSourceExceptionWhenFeatureWriteFails()
    {
        var path = Path.Combine(_testDir, "append-invalid.gpkg");
        var writer = new GdalWriter();
        writer.Write(CreatePointLayer(1, "first", "POINT (0 0)"), path);

        var appendedLayer = CreatePointLayer(2, "invalid", "NOT A GEOMETRY");
        var act = () => writer.Append(appendedLayer, path);

        act.Should().Throw<DataSourceException>()
            .WithMessage("*1 个要素失败*");
    }

    [Fact]
    public void Write_ThrowsDataSourceExceptionWhenFieldValueCannotBeConverted()
    {
        var layer = new OguLayer { Name = "points", GeometryType = GeometryType.POINT };
        layer.AddField(new OguField { Name = "value", DataType = FieldDataType.INTEGER });
        var feature = new OguFeature { Fid = 1, Wkt = "POINT (0 0)" };
        feature.SetValue("value", "not-an-integer");
        layer.AddFeature(feature);

        var act = () => new GdalWriter().Write(layer, Path.Combine(_testDir, "invalid-value.gpkg"));

        act.Should().Throw<DataSourceException>()
            .WithMessage("*1 个要素失败*");
    }

    [Fact]
    public void Write_ThrowsDataSourceExceptionWhenLongValueCannotBeConverted()
    {
        var layer = new OguLayer { Name = "points", GeometryType = GeometryType.POINT };
        layer.AddField(new OguField { Name = "value", DataType = FieldDataType.LONG });
        var feature = new OguFeature { Fid = 1, Wkt = "POINT (0 0)" };
        feature.SetValue("value", "not-a-long");
        layer.AddFeature(feature);

        var act = () => new GdalWriter().Write(layer, Path.Combine(_testDir, "invalid-long.gpkg"));

        act.Should().Throw<DataSourceException>().WithMessage("*1 个要素失败*");
    }

    [Fact]
    public void Append_MapsFieldsWithoutCaseSensitivity()
    {
        var path = Path.Combine(_testDir, "case-insensitive.gpkg");
        var writer = new GdalWriter();
        writer.Write(CreatePointLayerWithField(1, "first", "POINT (0 0)", "Name"), path);

        var appendedLayer = CreatePointLayerWithField(2, "second", "POINT (1 1)", "name");
        var act = () => writer.Append(appendedLayer, path);

        act.Should().NotThrow();
        var result = new GdalReader().Read(path);
        result.Features.Should().HaveCount(2);
        result.Features[1].GetValue("Name").Should().Be("second");
    }

    [Fact]
    public void Write_PreservesDateTimeFractionalSeconds()
    {
        var layer = new OguLayer
        {
            Name = "events",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "occurred", DataType = FieldDataType.DATETIME });
        var expected = new DateTime(2024, 1, 15, 10, 30, 15, 750);
        var feature = new OguFeature { Fid = 1, Wkt = "POINT (0 0)" };
        feature.SetValue("occurred", expected);
        layer.AddFeature(feature);

        var path = Path.Combine(_testDir, "events.gpkg");
        new GdalWriter().Write(layer, path);

        var result = new GdalReader().Read(path);
        result.Features.Should().ContainSingle();
        result.Features[0].GetValue("occurred").Should().BeOfType<DateTime>()
            .Which.Should().Be(expected);
    }

    [Fact]
    public void Write_PreservesZeroBasedFidsInGpkg()
    {
        // GPKG 支持显式写入 fid=0：源 Fid=0/1 的要素应原样保留，
        // 不再触发"跳过 Fid=0 → 驱动自动分配 1 → 与显式 Fid=1 链式相撞"的回退路径
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING });
        var feature0 = new OguFeature { Fid = 0, Wkt = "POINT (0 0)" };
        feature0.SetValue("name", "zero");
        layer.AddFeature(feature0);
        var feature1 = new OguFeature { Fid = 1, Wkt = "POINT (1 1)" };
        feature1.SetValue("name", "one");
        layer.AddFeature(feature1);

        var path = Path.Combine(_testDir, "fid-preserve.gpkg");
        new GdalWriter().Write(layer, path);

        var result = new GdalReader().Read(path);
        result.Features.Should().HaveCount(2);
        result.Features.Select(f => f.Fid).Should().BeEquivalentTo(new[] { 0, 1 });
        result.Features.Single(f => f.Fid == 0).GetValue("name").Should().Be("zero");
        result.Features.Single(f => f.Fid == 1).GetValue("name").Should().Be("one");
    }

    [Fact]
    public void Write_RetriesWithAutoFidWhenDuplicateSourceFids()
    {
        // 回退机制回归：源存在重复 FID 时，后一个要素与已写入的显式 FID 撞 UNIQUE 约束，
        // 应回退为自动分配 FID（max+1）而不是写入失败
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING });
        var first = new OguFeature { Fid = 5, Wkt = "POINT (0 0)" };
        first.SetValue("name", "first");
        layer.AddFeature(first);
        var second = new OguFeature { Fid = 5, Wkt = "POINT (1 1)" };
        second.SetValue("name", "second");
        layer.AddFeature(second);

        var path = Path.Combine(_testDir, "fid-duplicate.gpkg");
        new GdalWriter().Write(layer, path);

        var result = new GdalReader().Read(path);
        result.Features.Should().HaveCount(2);
        result.Features.Select(f => f.Fid).Should().BeEquivalentTo(new[] { 5, 6 });
    }

    [Fact]
    public void Write_DefersZeroFidToEndForOpenFileGdb()
    {
        // OpenFileGDB 要求正整数 FID（写入 0 会被驱动拒绝）：Fid=0 的要素延迟到最后写入
        // （自动分配 max+1），其余要素 FID 保真，且不产生链式冲突回退
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING });
        var zero = new OguFeature { Fid = 0, Wkt = "POINT (0 0)" };
        zero.SetValue("name", "zero");
        layer.AddFeature(zero);
        var one = new OguFeature { Fid = 1, Wkt = "POINT (1 1)" };
        one.SetValue("name", "one");
        layer.AddFeature(one);
        var two = new OguFeature { Fid = 2, Wkt = "POINT (2 2)" };
        two.SetValue("name", "two");
        layer.AddFeature(two);

        var path = Path.Combine(_testDir, "fid-defer.gdb");
        new GdalWriter().Write(layer, path);

        var result = new GdalReader().Read(path);
        result.Features.Should().HaveCount(3);
        // Fid=1/2 保真；Fid=0 延迟写入被分配 max+1=3
        result.Features.Single(f => f.Fid == 1).GetValue("name").Should().Be("one");
        result.Features.Single(f => f.Fid == 2).GetValue("name").Should().Be("two");
        result.Features.Single(f => f.Fid == 3).GetValue("name").Should().Be("zero");
    }

    [Fact]
    public void Write_Uses25DGeometryTypeWhenFeaturesHaveZ()
    {
        // 回归：GeometryType 枚举无 Z 维度，但要素 WKT 含 Z 时图层应声明为 25D，
        // 避免 GPKG 等驱动出现"声明 2D 但包含 Z 几何"的不一致，且 Z 值应保留
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "name", DataType = FieldDataType.STRING });
        var feature = new OguFeature { Fid = 1, Wkt = "POINT (10 20 30)" };
        feature.SetValue("name", "with-z");
        layer.AddFeature(feature);

        var path = Path.Combine(_testDir, "with-z.gpkg");
        new GdalWriter().Write(layer, path);

        var result = new GdalReader().Read(path);
        result.Features.Should().ContainSingle();
        result.Features[0].Wkt.Should().Contain("30");
    }

    [Fact]
    public void Write_RoutesPostgisConnectionStringToPostgresDriver()
    {
        // 回归：PostGIS 连接串没有文件扩展名，驱动推断曾回退到 ESRI Shapefile，
        // GDAL 于是尝试创建名为 "PG:host=..." 的目录并报 shapefile datastore 错误
        var layer = CreatePointLayer(1, "pg", "POINT (10 20)");
        const string conn = "PG:host=127.0.0.1 port=1 dbname=postgres user=postgres password=postgres";

        var act = () => new GdalWriter().Write(layer, conn, "ogu_test_routing");

        act.Should().Throw<System.Exception>()
            .Where(e => !e.Message.Contains("shapefile datastore"));
    }

    [Fact]
    public void Write_DxfWithAttributes_SkippedFieldsDoNotLeakIntoBuiltInColumns()
    {
        // DXF 是固定 schema 驱动：CreateField 失败被跳过的字段绝不能继续占用内建列
        // （曾按源序数映射，把属性值写进 Layer/Color 等内建字段，实体被放到不存在的图层上）
        var layer = new OguLayer
        {
            Name = "attrs",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "id", DataType = FieldDataType.STRING, Length = 20 });
        var f1 = new OguFeature { Fid = 1, Wkt = "POINT (116.4 39.9)" };
        f1.SetValue("id", "ZZZ_NO_SUCH_CAD_LAYER");
        layer.AddFeature(f1);
        var f2 = new OguFeature { Fid = 2, Wkt = "POINT (121.4 31.2)" };
        f2.SetValue("id", "ZZZ_NO_SUCH_CAD_LAYER");
        layer.AddFeature(f2);

        var path = Path.Combine(_testDir, "attrs.dxf");
        var act = () => new GdalWriter().Write(layer, path);
        act.Should().NotThrow();

        var read = OguLayerUtil.ReadLayer(DataFormatType.DXF, path);
        read.Features.Should().HaveCount(2);
        // 修复后内建 Layer 列不会被源属性值污染
        foreach (var feature in read.Features)
        {
            var layerValue = feature.GetValue("Layer")?.ToString();
            layerValue.Should().NotBe("ZZZ_NO_SUCH_CAD_LAYER");
        }
    }

    [Fact]
    public void Write_KmlWithDateFields_SucceedsAndKeepsIsoValues()
    {
        // GDAL 的原生 KML 写驱动对 OFTDate 列会让每个要素 CreateFeature 失败
        // （"Export of geometry to KML failed"）；日期降级为文本列后应整层成功且值保留。
        var layer = new OguLayer
        {
            Name = "dated",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "when", DataType = FieldDataType.DATE });
        layer.AddField(new OguField { Name = "what", DataType = FieldDataType.STRING, Length = 10 });
        var feature = new OguFeature { Fid = 1, Wkt = "POINT (116.4 39.9)" };
        feature.SetValue("when", new DateTime(2004, 7, 15));
        feature.SetValue("what", "tunnel");
        layer.AddFeature(feature);

        var path = Path.Combine(_testDir, "dated.kml");
        var act = () => new GdalWriter().Write(layer, path);
        act.Should().NotThrow();

        var read = OguLayerUtil.ReadLayer(DataFormatType.KML, path);
        read.GetFeatureCount().Should().Be(1);
        read.Features[0].GetValue("when")?.ToString().Should().Be("2004-07-15");
    }

    [Fact]
    public void Write_KmlKeepsFieldValuesAligned()
    {
        // KML 驱动在图层构造时预置 Name/Description 两个内建字段（索引 0/1），创建字段追加其后。
        // 修复前按 0 起始映射索引，属性值整体错位 2 位（code 拿到 pop 的值、rank/pop 丢失），
        // 且源 "Name" 字段与内建 Name 会各输出一个 <name>。
        var layer = new OguLayer
        {
            Name = "aligned",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = "Name", DataType = FieldDataType.STRING });
        layer.AddField(new OguField { Name = "code", DataType = FieldDataType.STRING, Length = 8 });
        layer.AddField(new OguField { Name = "rank", DataType = FieldDataType.INTEGER });
        layer.AddField(new OguField { Name = "pop", DataType = FieldDataType.LONG });
        var feature = new OguFeature { Fid = 1, Wkt = "POINT (116.4 39.9)" };
        feature.SetValue("Name", "Alpha");
        feature.SetValue("code", "A-01");
        feature.SetValue("rank", 3);
        feature.SetValue("pop", 14645468L);
        layer.AddFeature(feature);

        var path = Path.Combine(_testDir, "aligned.kml");
        var act = () => new GdalWriter().Write(layer, path);
        act.Should().NotThrow();

        var read = OguLayerUtil.ReadLayer(DataFormatType.KML, path);
        read.GetFeatureCount().Should().Be(1);
        var back = read.Features[0];
        back.GetValue("Name")?.ToString().Should().Be("Alpha");
        back.GetValue("code")?.ToString().Should().Be("A-01");
        back.GetValue("rank")?.ToString().Should().Be("3");
        back.GetValue("pop")?.ToString().Should().Be("14645468");

        // 修复后 Placemark 只写一个 <name>（源 "Name" 字段；内建 Name 不再被误赋值），
        // 加上 Folder 名共 2 处；修复前为 3 处。
        var nameTagCount = File.ReadAllText(path).Split("<name>").Length - 1;
        nameTagCount.Should().Be(2);
    }

    private static OguLayer CreatePointLayer(int fid, string name, string wkt)
    {
        return CreatePointLayerWithField(fid, name, wkt, "name");
    }

    private static OguLayer CreatePointLayerWithField(int fid, string name, string wkt, string fieldName)
    {
        var layer = new OguLayer
        {
            Name = "points",
            GeometryType = GeometryType.POINT,
            Wkid = 4326
        };
        layer.AddField(new OguField { Name = fieldName, DataType = FieldDataType.STRING });
        var feature = new OguFeature { Fid = fid, Wkt = wkt };
        feature.SetValue("name", name);
        layer.AddFeature(feature);
        return layer;
    }
}
