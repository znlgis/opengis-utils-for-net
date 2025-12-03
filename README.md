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
- 🌐 **Coordinate System Support**: Comprehensive CRS transformation using GDAL/OGR with built-in CGCS2000 support
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

#### Coordinate Transformation

```csharp
using OpenGIS.Utils.Engine.Util;

// Transform coordinates from WGS84 to CGCS2000
string wkt = "POINT (116.404 39.915)";
string transformed = CrsUtil.Transform(wkt, 4326, 4490);

// Get zone number from longitude
int zone = CrsUtil.GetDh(116.404);  // 3-degree zone
int zone6 = CrsUtil.GetDh6(116.404); // 6-degree zone

// Get projected coordinate system WKID
int wkid = CrsUtil.GetProjectedWkid(39);  // CGCS2000 3-degree zone 39
```

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

### API Reference

All public APIs are fully documented with XML documentation comments including:
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

- **[MaxRev.Gdal.Core](https://github.com/MaxRev-Dev/gdal.netcore)** 3.12.0+ - GDAL/OGR bindings
- **[MaxRev.Gdal.Universal](https://github.com/MaxRev-Dev/gdal.netcore)** 3.12.0+ - Cross-platform GDAL runtime
- **[System.Text.Json](https://www.nuget.org/packages/System.Text.Json)** 10.0.0 - JSON serialization
- **[System.Text.Encoding.CodePages](https://www.nuget.org/packages/System.Text.Encoding.CodePages)** 10.0.0 - Encoding support (GBK, GB2312)
- **[SharpZipLib](https://github.com/icsharpcode/SharpZipLib)** 1.4.2 - ZIP compression
- **[Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)** 10.0.0 - Logging

### Requirements

- **.NET Standard 2.0** or higher
- Compatible with .NET Core 2.0+, .NET 5+, .NET Framework 4.6.1+

### Documentation

All public APIs include comprehensive XML documentation with:
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
- 🌐 **坐标系支持**：基于 GDAL/OGR 的全面坐标系转换，内置 CGCS2000 支持
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

// 注意：不支持直接解析 GeoJSON 字符串
// 请使用 WKT 格式或通过 GdalReader 从文件加载 GeoJSON
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

// 根据经度获取带号
int zone = CrsUtil.GetDh(116.404);  // 3度带
int zone6 = CrsUtil.GetDh6(116.404); // 6度带

// 获取投影坐标系 WKID
int wkid = CrsUtil.GetProjectedWkid(39);  // CGCS2000 3度带第39带
```

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

- **[MaxRev.Gdal.Core](https://github.com/MaxRev-Dev/gdal.netcore)** 3.12.0+ - GDAL/OGR 绑定
- **[MaxRev.Gdal.Universal](https://github.com/MaxRev-Dev/gdal.netcore)** 3.12.0+ - 跨平台 GDAL 运行时
- **[System.Text.Json](https://www.nuget.org/packages/System.Text.Json)** 10.0.0 - JSON 序列化
- **[System.Text.Encoding.CodePages](https://www.nuget.org/packages/System.Text.Encoding.CodePages)** 10.0.0 - 编码支持（GBK、GB2312）
- **[SharpZipLib](https://github.com/icsharpcode/SharpZipLib)** 1.4.2 - ZIP 压缩
- **[Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)** 10.0.0 - 日志

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