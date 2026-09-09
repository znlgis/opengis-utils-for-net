# OpenGIS Utils for .NET

[English](#english) | [中文](#中文)

---

## English

### Overview

**OpenGIS Utils for .NET** (OGU4Net) is a comprehensive GIS development toolkit for .NET based on [MaxRev.Gdal.Universal](https://github.com/MaxRev-Dev/gdal.netcore). It provides a unified layer model and convenient format conversion capabilities to simplify reading, processing, and exporting GIS data.

This project is a complete port of [opengis-utils-for-java](https://github.com/znlgis/opengis-utils-for-java) to C# .NET Standard 2.0.

### Key Features

- 🎯 **Unified Layer Model**: Simple and consistent `OguLayer`, `OguFeature`, and `OguField` abstractions that hide underlying GIS library differences
- 🔄 **Format Conversion**: Seamless conversion between Shapefile, GeoJSON, FileGDB, PostGIS, GeoPackage, KML, DXF, and TXT formats
- 🌐 **Coordinate System Support**: CRS metadata and transformations backed by the GDAL/PROJ database, covering EPSG coordinate systems worldwide while retaining CGCS2000 helpers
- 📐 **Geometry Processing**: Rich set of spatial operations including buffer, intersection, union, topology validation, and more
- 🔧 **GDAL-Based Architecture**: All operations powered by GDAL/OGR for maximum compatibility and performance
- 📦 **Cross-Platform**: Runs on Windows, Linux, and macOS via .NET Standard 2.0
- 🛠️ **Utility Classes**: Encoding detection, ZIP compression, natural sorting, and numeric formatting

### Installation

```bash
dotnet add package OpenGIS.Utils
```

Or via NuGet Package Manager:

```
Install-Package OpenGIS.Utils
```

### Quick Start

#### Basic Layer Operations

```csharp
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Engine.Enums;

// Create a new layer
var layer = new OguLayer
{
    Name = "My Layer",
    GeometryType = GeometryType.POINT,
    Wkid = 4326
};

// Add fields
layer.AddField(new OguField
{
    Name = "ID",
    DataType = FieldDataType.INTEGER
});

layer.AddField(new OguField
{
    Name = "Name",
    DataType = FieldDataType.STRING,
    Length = 50
});

// Add features
var feature = new OguFeature
{
    Fid = 1,
    Wkt = "POINT (116.404 39.915)"
};
feature.SetValue("ID", 1);
feature.SetValue("Name", "Beijing");
layer.AddFeature(feature);

// Validate layer
layer.Validate();

Console.WriteLine($"Layer '{layer.Name}' has {layer.GetFeatureCount()} features");
```

#### Geometry Operations

```csharp
using OpenGIS.Utils.Geometry;

// WKT to GeoJSON conversion
string wkt = "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))";
string geojson = GeometryUtil.Wkt2Geojson(wkt);

// GeoJSON string back to WKT (bare geometry, Feature, or FeatureCollection)
string roundTripped = GeometryUtil.Geojson2Wkt(geojson);

// Buffer operation
string buffered = GeometryUtil.BufferWkt(wkt, 5.0);

// Spatial relationship
string point = "POINT (5 5)";
bool contains = GeometryUtil.ContainsWkt(wkt, point);

// Area and length
double area = GeometryUtil.AreaWkt(wkt);
double length = GeometryUtil.LengthWkt(wkt);

// Topology validation
var geom = GeometryUtil.Wkt2Geometry(wkt);  // Returns OSGeo.OGR.Geometry
var validationResult = GeometryUtil.IsValid(geom);
if (!validationResult.IsValid)
{
    Console.WriteLine($"Geometry is invalid: {validationResult.ErrorMessage}");
}

// Note: Direct GeoJSON string parsing is not supported
// Use WKT format or load GeoJSON from files using GdalReader
```

#### Reading and Writing Data

```csharp
using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine.Enums;

// Read from Shapefile
var layer = OguLayerUtil.ReadLayer(
    DataFormatType.SHP,
    "data/cities.shp"
);

// Write to GeoJSON
OguLayerUtil.WriteLayer(
    DataFormatType.GEOJSON,
    layer,
    "output/cities.geojson"
);

// Read with spatial filter
var filtered = OguLayerUtil.ReadLayer(
    DataFormatType.SHP,
    "data/cities.shp",
    spatialFilterWkt: "POLYGON ((...))"
);
```

`GdalReader` 可通过 `options["encoding"]` 指定 Shapefile 属性编码。属性过滤器使用驱动支持的表达式语法，空间过滤器使用 WKT。无效过滤器、无法解析的日期字段、打不开的数据源和不存在的图层都会通过类型化异常报告，不会静默忽略。

`GdalWriter.Append` 向已有数据源追加要素，不会删除原数据源；输入字段必须能够映射到目标图层。`Write` 会重新创建已存在的数据源。两种操作都会在处理完输入后，以 `DataSourceException` 汇总报告失败要素；目标驱动支持时会尽量保留非零 FID。

PostGIS 连接串必须能被 GDAL PostgreSQL 驱动识别，且整体不能加引号：写 `PG:host=127.0.0.1 port=5432 dbname=postgres user=postgres password=postgres`；写成 `PG:"host=..."` 会让 GDAL 把引号当成参数名的一部分而连接失败。`OguLayerUtil.WriteLayer(DataFormatType.POSTGIS, ...)` 与 `PostgisUtil.WritePostGIS` 按 `PG:` 前缀选择 PostgreSQL 驱动。`PostgisUtil.TableExists` 在数据源打不开时抛 `DataSourceException`，只有连接成功且库中不含该表才返回 `false`。

`PostgisUtil.CreateSpatialIndex` 通过 OGR 创建 GIST 索引，几何列名默认为 GDAL 建表时使用的 `wkb_geometry`，传 `null` 时从 `geometry_columns` 自动探测实际列名；DDL 使用 `CREATE INDEX IF NOT EXISTS`，重复调用不会失败。数据库用户必须拥有创建索引的权限，表名和几何列名仅允许字母、数字和下划线。默认写入不覆盖已存在的同名表；需要覆盖时在 options 中传 `overwrite = true`（等价于图层创建选项 `OVERWRITE=YES`），文件驱动同样接受该选项。

`GdalReader` supports an optional `options["encoding"]` value for encoded
Shapefile attributes. Attribute filters use the driver's expression syntax;
spatial filters use WKT. Invalid filters, unreadable date fields, missing
data sources, and missing layers are reported with typed exceptions rather
than being silently ignored.

`GdalWriter.Append` writes into an existing data source without deleting it.
The input fields must exist in the target layer. `Write` recreates an existing
data source, while both methods report failed features through
`DataSourceException` after processing the input. Non-zero feature IDs are
preserved when the target driver accepts them.

For PostGIS, the connection string must be understood by the GDAL PostgreSQL
driver and must not be quoted: write
`PG:host=127.0.0.1 port=5432 dbname=postgres user=postgres password=postgres`.
A quoted form such as `PG:"host=..."` makes GDAL treat the quote as part of the
option name and fail to connect. `OguLayerUtil.WriteLayer(DataFormatType.POSTGIS, ...)`
and `PostgisUtil.WritePostGIS` select the PostgreSQL driver from the `PG:`
prefix. `PostgisUtil.TableExists` throws `DataSourceException` when the data
source cannot be opened and returns `false` only when the connection succeeds
and the table is absent.

`PostgisUtil.CreateSpatialIndex` creates a GIST index through OGR. The geometry
column defaults to `wkb_geometry`, the name GDAL uses when creating tables, and
passing `null` detects the actual column from `geometry_columns`; the DDL uses
`CREATE INDEX IF NOT EXISTS`, so repeated calls do not fail. The database user
must have permission to create indexes, and table/geometry column names are
restricted to letters, digits, and underscores. Writes do not replace an
existing table of the same name by default; pass `overwrite = true` in options
(equivalent to the `OVERWRITE=YES` layer creation option) when replacement is
intended. File-based drivers accept the same option.

#### Coordinate Transformation

```csharp
using OpenGIS.Utils.Engine.Util;

// Transform coordinates from WGS84 to CGCS2000
string wkt = "POINT (116.404 39.915)";
string transformed = CrsUtil.Transform(wkt, 4326, 4490);

// CRS type detection is resolved by GDAL/PROJ rather than a local WKID list.
bool isProjected = CrsUtil.IsProjectedCRS(2326); // Hong Kong 1980 Grid
bool isGeographic = CrsUtil.IsGeographicCRS(4326); // WGS 84
var crsInfo = CrsUtil.GetCrsInfo(3826); // Taiwan TWD97 / TM2 zone 121

// Get zone number from longitude
int zone = CrsUtil.GetDh(116.404);  // 3-degree zone
int zone6 = CrsUtil.GetDh6(116.404); // 6-degree zone

// Get projected coordinate system WKID
int wkid = CrsUtil.GetProjectedWkid(39);  // CGCS2000 3-degree zone 39
```

`CrsUtil.Transform` uses the default transformation pipeline selected by
GDAL/PROJ. When a source and target datum have a known business-safe
intermediate path, make that path explicit. In particular, the tested
HK1980 Grid to CGCS2000 path should go through WGS 84 to avoid the
low-accuracy direct pipeline in affected PROJ environments:

```csharp
string cgcs2000 = CrsUtil.TransformThrough(
    wkt,
    2326, // HK1980 Grid
    4326, // WGS 84 intermediate
    4490  // CGCS2000
);

var recommendation = CrsUtil.GetTransformRecommendation(2326, 4490);
```

`GetDh`, `GetDh6`, `GetDhFromWkid`, `GetProjectedWkid`, and
`GetProjectedWkid6` are retained as compatibility helpers for China's
3-degree and 6-degree zoning rules. Zone numbers map to the EPSG
zone-numbered CGCS2000 codes verified against the PROJ database: 3-degree
zones 25-45 ↔ 4513-4533 and 6-degree zones 13-23 ↔ 4491-4501. The
central-meridian codes (4502-4512, 4534-4554) carry no zone number, so
`GetDhFromWkid` rejects them with `ArgumentException`; 3-degree zone 24 has
no EPSG code and is rejected by `GetProjectedWkid`. They are not
general-purpose methods for inferring zones from international CRS
identifiers.

The `CrsUtil.Transform` geometry overload returns the input OGR geometry when
the source and target WKIDs are equal; otherwise it returns a new geometry.
The caller owns the returned native geometry and must dispose it. Invalid WKIDs
raise `ArgumentException`; conversion failures raise the documented runtime
exception.

#### TXT coordinate files

`GtTxtUtil.LoadTxt` rejects malformed coordinate rows with
`FormatParseException` instead of silently dropping them. New code can use
`TryParseTxtLine` when parsing individual rows without exceptions:

```csharp
if (GtTxtUtil.TryParseTxtLine(line, out var coordinate))
{
    Console.WriteLine(coordinate!.ToWkt());
}
```

The older `ParseTxtLine` method remains available and returns `null` for blank
or invalid input for compatibility.

#### Utility Functions

```csharp
using OpenGIS.Utils.Utils;

// Encoding detection
var encoding = EncodingUtil.GetFileEncoding("data.txt");
var content = File.ReadAllText("data.txt", encoding);

// Natural sorting
var files = new[] { "file1.txt", "file10.txt", "file2.txt" };
var sorted = SortUtil.NaturalSort(files, f => f);
// Result: file1.txt, file2.txt, file10.txt

// Number formatting (avoid scientific notation)
string formatted = NumUtil.GetPlainString(0.00000123);

// ZIP compression
ZipUtil.Zip("folder/", "output.zip");
ZipUtil.Unzip("output.zip", "extracted/");
```

#### GDAL Configuration

GDAL is automatically configured on first use:

```csharp
using OpenGIS.Utils.Configuration;

// Get GDAL version
string version = GdalConfiguration.GetGdalVersion();
Console.WriteLine($"GDAL Version: {version}");

// Check driver availability
bool hasFileGDB = GdalConfiguration.IsDriverAvailable("FileGDB");
Console.WriteLine($"FileGDB Support: {hasFileGDB}");

// List all supported drivers
var drivers = GdalConfiguration.GetSupportedDrivers();
foreach (var driver in drivers)
{
    Console.WriteLine($"- {driver}");
}
```

#### Logging (Optional)

By default the library produces no log output. To receive internal diagnostics
(e.g. features that fail to write, coordinate lines that fail to parse — cases
that were previously silently ignored), configure a logger factory at startup:

```csharp
using Microsoft.Extensions.Logging;
using OpenGIS.Utils.Configuration;

OguLogging.LoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
```

GDAL's `SHAPE_ENCODING` setting is process-wide. The reader serializes the
encoding-sensitive open/read operation so concurrent reads using different
encodings do not overwrite one another's configuration, and restores the
previous value once the read completes so a declared encoding never leaks
into later reads or writes. This protects correctness at the cost of
serializing those operations.

### API Reference

Public APIs are documented with XML documentation comments including:
- Detailed parameter descriptions
- Return value explanations  
- Exception conditions
- Usage examples for key methods

#### Core Classes

- **`OguLayer`** - Unified GIS layer with fields and features
- **`OguFeature`** - Feature with geometry (WKT) and attributes
- **`OguField`** - Field definition with data type and constraints
- **`OguFieldValue`** - Type-safe field value container with conversion methods

#### Geometry Processing

- **`GeometryUtil`** - Comprehensive spatial operations:
  - Format conversion (WKT ↔ GeoJSON)
  - Spatial analysis (buffer, intersection, union, difference)
  - Spatial relationships (contains, intersects, touches, etc.)
  - Measurements (area, length, centroid)
  - Topology validation

#### Data I/O

- **`OguLayerUtil`** - High-level data reading/writing
- **`GdalReader`** / **`GdalWriter`** - GDAL-based I/O operations
- **`ShpUtil`** - Shapefile-specific utilities (encoding, repair)

#### Coordinate Systems

- **`CrsUtil`** - Coordinate transformation and zone calculations
  - WGS84, CGCS2000, and custom CRS support
  - EPSG code handling
  - Zone number calculations for Chinese coordinate systems

#### Utilities

- **`EncodingUtil`** - Automatic encoding detection (UTF-8, GBK, GB2312)
- **`ZipUtil`** - ZIP compression and extraction
- **`SortUtil`** - Natural sorting for filenames with numbers
- **`NumUtil`** - Number formatting without scientific notation

### Project Structure

```
OpenGIS.Utils/
├── Engine/
│   ├── Enums/             # Geometry types, field types, format types
│   ├── IO/                # Reader/Writer interfaces
│   ├── Model/
│   │   └── Layer/         # OguLayer, OguFeature, OguField, etc.
│   └── Util/              # CrsUtil, ShpUtil, OgrUtil, etc.
├── Exception/             # Custom exception types
├── Geometry/              # GeometryUtil for spatial operations
├── Utils/                 # ZipUtil, EncodingUtil, SortUtil, NumUtil
├── DataSource/            # OguLayerUtil for unified data access
└── Configuration/         # GdalConfiguration, LibrarySettings
```

### Dependencies

- **[MaxRev.Gdal.Core](https://github.com/MaxRev-Dev/gdal.netcore)** 3.13.3+ - GDAL/OGR bindings
- **[MaxRev.Gdal.Universal](https://github.com/MaxRev-Dev/gdal.netcore)** 3.13.3+ - Cross-platform GDAL runtime
- **[System.Text.Json](https://www.nuget.org/packages/System.Text.Json)** 10.0.11 - JSON serialization
- **[System.Text.Encoding.CodePages](https://www.nuget.org/packages/System.Text.Encoding.CodePages)** 10.0.11 - Encoding support (GBK, GB2312)
- **[SharpZipLib](https://github.com/icsharpcode/SharpZipLib)** 1.4.2 - ZIP compression
- **[Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)** 10.0.11 - Logging

### Requirements

- **.NET Standard 2.0** or higher
- Compatible with .NET Core 2.0+, .NET 5+, .NET Framework 4.6.1+

### Documentation

Public APIs include XML documentation with:
- **Parameter descriptions** - Clear explanation of each parameter
- **Return values** - What the method returns
- **Exceptions** - When and why exceptions are thrown
- **Examples** - Code samples for common use cases
- **Remarks** - Implementation details and important notes

IntelliSense in Visual Studio and other IDEs will display this documentation automatically.

### License

Licensed under [LGPL-2.1-or-later](LICENSE), consistent with the Java version.

### Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

### Links

- **GitHub**: https://github.com/znlgis/opengis-utils-for-net
- **Java Version**: https://github.com/znlgis/opengis-utils-for-java
- **NuGet**: [Coming Soon]

---

## 中文

### 项目概述

**OpenGIS Utils for .NET** (OGU4Net) 是基于 [MaxRev.Gdal.Universal](https://github.com/MaxRev-Dev/gdal.netcore) 的 .NET GIS 二次开发工具库。提供统一的图层模型和便捷的格式转换功能，简化 GIS 数据的读取、处理和导出操作。

本项目是 [opengis-utils-for-java](https://github.com/znlgis/opengis-utils-for-java) 的完整 C# .NET Standard 2.0 移植版本。

### 主要特性

- 🎯 **统一图层模型**：简洁一致的 `OguLayer`、`OguFeature`、`OguField` 抽象，屏蔽底层 GIS 库差异
- 🔄 **格式转换**：Shapefile、GeoJSON、FileGDB、PostGIS、GeoPackage、KML、DXF、TXT 等格式无缝转换
- 🌐 **坐标系支持**：基于 GDAL/PROJ 数据库识别全球 EPSG 坐标系并执行转换，同时保留 CGCS2000 专用辅助方法
- 📐 **几何处理**：丰富的空间操作，包括缓冲区、交集、并集、拓扑验证等
- 🔧 **GDAL 架构**：所有操作均由 GDAL/OGR 提供支持，确保最大兼容性和性能
- 📦 **跨平台**：通过 .NET Standard 2.0 支持 Windows、Linux 和 macOS
- 🛠️ **实用工具**：编码检测、ZIP 压缩、自然排序、数字格式化

### 安装

```bash
dotnet add package OpenGIS.Utils
```

或通过 NuGet 包管理器：

```
Install-Package OpenGIS.Utils
```

### 快速开始

#### 基本图层操作

```csharp
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Engine.Enums;

// 创建新图层
var layer = new OguLayer
{
    Name = "我的图层",
    GeometryType = GeometryType.POINT,
    Wkid = 4326
};

// 添加字段
layer.AddField(new OguField
{
    Name = "ID",
    DataType = FieldDataType.INTEGER
});

layer.AddField(new OguField
{
    Name = "名称",
    DataType = FieldDataType.STRING,
    Length = 50
});

// 添加要素
var feature = new OguFeature
{
    Fid = 1,
    Wkt = "POINT (116.404 39.915)"
};
feature.SetValue("ID", 1);
feature.SetValue("名称", "北京");
layer.AddFeature(feature);

// 验证图层
layer.Validate();

Console.WriteLine($"图层 '{layer.Name}' 有 {layer.GetFeatureCount()} 个要素");
```

#### 几何操作

```csharp
using OpenGIS.Utils.Geometry;

// WKT 转 GeoJSON
string wkt = "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))";
string geojson = GeometryUtil.Wkt2Geojson(wkt);

// 缓冲区分析
string buffered = GeometryUtil.BufferWkt(wkt, 5.0);

// 空间关系判断
string point = "POINT (5 5)";
bool contains = GeometryUtil.ContainsWkt(wkt, point);

// 面积和长度
double area = GeometryUtil.AreaWkt(wkt);
double length = GeometryUtil.LengthWkt(wkt);

// 拓扑验证
var geom = GeometryUtil.Wkt2Geometry(wkt);  // 返回 OSGeo.OGR.Geometry
var validationResult = GeometryUtil.IsValid(geom);
if (!validationResult.IsValid)
{
    Console.WriteLine($"几何对象无效: {validationResult.ErrorMessage}");
}

// GeoJSON 字符串可直接解析回几何（裸几何 / Feature / FeatureCollection，集合取第一个要素的几何）；
// 整文件批量加载仍推荐 GdalReader / OguLayerUtil.ReadLayer(GEOJSON, path)
string roundTripped = GeometryUtil.Geojson2Wkt(geojson);
```

#### 数据读写

```csharp
using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine.Enums;

// 从 Shapefile 读取
var layer = OguLayerUtil.ReadLayer(
    DataFormatType.SHP,
    "data/cities.shp"
);

// 写入到 GeoJSON
OguLayerUtil.WriteLayer(
    DataFormatType.GEOJSON,
    layer,
    "output/cities.geojson"
);

// 使用空间过滤读取
var filtered = OguLayerUtil.ReadLayer(
    DataFormatType.SHP,
    "data/cities.shp",
    spatialFilterWkt: "POLYGON ((...))"
);
```

#### 坐标转换

```csharp
using OpenGIS.Utils.Engine.Util;

// WGS84 转 CGCS2000
string wkt = "POINT (116.404 39.915)";
string transformed = CrsUtil.Transform(wkt, 4326, 4490);

// CRS 类型和元数据由 GDAL/PROJ 动态识别，覆盖港澳台及国外 EPSG 坐标系
bool isProjected = CrsUtil.IsProjectedCRS(2326); // 香港 HK1980 Grid
bool isGeographic = CrsUtil.IsGeographicCRS(4326); // WGS 84
var crsInfo = CrsUtil.GetCrsInfo(3826); // 台湾 TWD97 / TM2 zone 121

// 根据经度获取带号
int zone = CrsUtil.GetDh(116.404);  // 3度带
int zone6 = CrsUtil.GetDh6(116.404); // 6度带

// 获取投影坐标系 WKID
int wkid = CrsUtil.GetProjectedWkid(39);  // CGCS2000 3度带第39带
```

`CrsUtil.Transform` 使用 GDAL/PROJ 选择的默认转换管道。对于已知存在业务风险的路径，
应显式指定中间坐标系。香港 HK1980 Grid（EPSG:2326）迁移到 CGCS2000（EPSG:4490）时，
请经 WGS 84（EPSG:4326）中转，以避免部分 PROJ 环境选择低精度直转管道：

```csharp
string cgcs2000 = CrsUtil.TransformThrough(wkt, 2326, 4326, 4490);
var recommendation = CrsUtil.GetTransformRecommendation(2326, 4490);
```

`GetDh`、`GetDh6`、`GetDhFromWkid`、`GetProjectedWkid` 和 `GetProjectedWkid6` 仍保留用于
中国 3 度带和 6 度带规则。带号与 EPSG 码按 PROJ 数据库核实过的 CGCS2000 带号系列码互推：
3度带 25-45 带 ↔ 4513-4533，6度带 13-23 带 ↔ 4491-4501。中央经线系列码（4502-4512、4534-4554）
不携带带号，`GetDhFromWkid` 对其抛出 `ArgumentException`；EPSG 未收录 3度带 24 带码，
`GetProjectedWkid(24)` 同样抛出。这些方法不用于从国际坐标系标识推断通用分带。

当源和目标 WKID 相同时，`CrsUtil.Transform` 的 Geometry 重载返回传入的 OGR 几何对象；发生转换时返回新的几何对象。调用方负责释放返回的原生几何对象。无效 WKID 抛出 `ArgumentException`，转换失败抛出文档中说明的运行时异常。

#### TXT 坐标文件

`GtTxtUtil.LoadTxt` 遇到无法解析的坐标行时抛出 `FormatParseException`，不再静默丢弃输入。单独解析坐标行时可使用不抛异常的 `TryParseTxtLine`：

```csharp
if (GtTxtUtil.TryParseTxtLine(line, out var coordinate))
{
    Console.WriteLine(coordinate!.ToWkt());
}
```

旧的 `ParseTxtLine` 方法仍然保留，空行或无效输入继续返回 `null`，以维持兼容性。

#### 实用工具函数

```csharp
using OpenGIS.Utils.Utils;

// 编码检测
var encoding = EncodingUtil.GetFileEncoding("data.txt");
var content = File.ReadAllText("data.txt", encoding);

// 自然排序
var files = new[] { "file1.txt", "file10.txt", "file2.txt" };
var sorted = SortUtil.NaturalSort(files, f => f);
// 结果: file1.txt, file2.txt, file10.txt

// 数字格式化（避免科学计数法）
string formatted = NumUtil.GetPlainString(0.00000123);

// ZIP 压缩
ZipUtil.Zip("folder/", "output.zip");
ZipUtil.Unzip("output.zip", "extracted/");
```

#### GDAL 配置

GDAL 在首次使用时自动配置：

```csharp
using OpenGIS.Utils.Configuration;

// 获取 GDAL 版本
string version = GdalConfiguration.GetGdalVersion();
Console.WriteLine($"GDAL 版本: {version}");

// 检查驱动可用性
bool hasFileGDB = GdalConfiguration.IsDriverAvailable("FileGDB");
Console.WriteLine($"FileGDB 支持: {hasFileGDB}");

// 列出所有支持的驱动
var drivers = GdalConfiguration.GetSupportedDrivers();
foreach (var driver in drivers)
{
    Console.WriteLine($"- {driver}");
}
```

#### 日志（可选）

默认情况下本库不产生任何日志输出。如需接收内部诊断信息（例如写入失败的要素、
解析失败的坐标行——这些情况过去被静默忽略），可在应用启动时配置日志工厂：

```csharp
using Microsoft.Extensions.Logging;
using OpenGIS.Utils.Configuration;

OguLogging.LoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
```

GDAL 的 `SHAPE_ENCODING` 是进程级配置。读取器会串行化设置编码、打开和读取数据源的过程，避免不同编码的并发读取互相覆盖；读取结束后自动恢复之前的配置值，声明过的编码不会泄漏并污染同进程后续的读写。代价是这些编码敏感的读取操作不能并行执行。

### API 参考

所有公共 API 都包含完整的 XML 文档注释，包括：
- 详细的参数说明
- 返回值解释
- 异常条件说明
- 关键方法的使用示例

#### 核心类

- **`OguLayer`** - 统一的 GIS 图层，包含字段和要素
- **`OguFeature`** - 要素，包含几何（WKT）和属性
- **`OguField`** - 字段定义，包含数据类型和约束
- **`OguFieldValue`** - 类型安全的字段值容器，提供转换方法

#### 几何处理

- **`GeometryUtil`** - 全面的空间操作：
  - 格式转换（WKT ↔ GeoJSON）
  - 空间分析（缓冲区、交集、并集、差集）
  - 空间关系（包含、相交、接触等）
  - 测量（面积、长度、质心）
  - 拓扑验证

#### 数据 I/O

- **`OguLayerUtil`** - 高级数据读写
- **`GdalReader`** / **`GdalWriter`** - 基于 GDAL 的 I/O 操作
- **`ShpUtil`** - Shapefile 专用工具（编码、修复）

#### 坐标系统

- **`CrsUtil`** - 坐标转换和分带计算
  - 支持 WGS84、CGCS2000 和自定义坐标系
  - EPSG 代码处理
  - 中国坐标系统的带号计算

#### 实用工具

- **`EncodingUtil`** - 自动编码检测（UTF-8、GBK、GB2312）
- **`ZipUtil`** - ZIP 压缩和解压
- **`SortUtil`** - 文件名自然排序（支持数字）
- **`NumUtil`** - 数字格式化（避免科学计数法）

### 项目结构

```
OpenGIS.Utils/
├── Engine/
│   ├── Enums/             # 几何类型、字段类型、格式类型
│   ├── IO/                # 读写器接口
│   ├── Model/
│   │   └── Layer/         # OguLayer、OguFeature、OguField 等
│   └── Util/              # CrsUtil、ShpUtil、OgrUtil 等
├── Exception/             # 自定义异常类型
├── Geometry/              # GeometryUtil 空间操作
├── Utils/                 # ZipUtil、EncodingUtil、SortUtil、NumUtil
├── DataSource/            # OguLayerUtil 统一数据访问
└── Configuration/         # GdalConfiguration、LibrarySettings
```

### 依赖项

- **[MaxRev.Gdal.Core](https://github.com/MaxRev-Dev/gdal.netcore)** 3.13.3.557 - GDAL/OGR 绑定
- **[MaxRev.Gdal.Universal](https://github.com/MaxRev-Dev/gdal.netcore)** 3.13.3.557 - 跨平台 GDAL 运行时
- **[System.Text.Json](https://www.nuget.org/packages/System.Text.Json)** 10.0.11 - JSON 序列化
- **[System.Text.Encoding.CodePages](https://www.nuget.org/packages/System.Text.Encoding.CodePages)** 10.0.11 - 编码支持（GBK、GB2312）
- **[SharpZipLib](https://github.com/icsharpcode/SharpZipLib)** 1.4.2 - ZIP 压缩
- **[Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)** 10.0.11 - 日志

### 环境要求

- **.NET Standard 2.0** 或更高版本
- 兼容 .NET Core 2.0+、.NET 5+、.NET Framework 4.6.1+

### 文档说明

所有公共 API 都包含完整的 XML 文档注释：
- **参数说明** - 每个参数的清晰解释
- **返回值** - 方法返回的内容
- **异常** - 何时以及为什么会抛出异常
- **示例** - 常见用例的代码示例
- **备注** - 实现细节和重要说明

在 Visual Studio 和其他 IDE 中，IntelliSense 会自动显示这些文档。

### 许可证

采用 [LGPL-2.1-or-later](LICENSE) 许可证，与 Java 版本保持一致。

### 贡献

欢迎贡献！请随时提交 Pull Request。

### 链接

- **GitHub**: https://github.com/znlgis/opengis-utils-for-net
- **Java 版本**: https://github.com/znlgis/opengis-utils-for-java
- **NuGet**: [即将推出]
