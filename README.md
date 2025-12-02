# OpenGIS Utils for .NET

[English](#english) | [中文](#中文)

---

## English

### Overview

**OpenGIS Utils for .NET** (OGU4Net) is a comprehensive GIS development toolkit for .NET based on [NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite) and [MaxRev.Gdal.Universal](https://github.com/MaxRev-Dev/gdal.netcore). It provides a unified layer model and convenient format conversion capabilities to simplify reading, processing, and exporting GIS data.

This project is a complete port of [opengis-utils-for-java](https://github.com/znlgis/opengis-utils-for-java) to C# .NET Standard 2.0.

### Key Features

- 🎯 **Unified Layer Model**: Simple and consistent `OguLayer`, `OguFeature`, and `OguField` abstractions that hide underlying GIS library differences
- 🔄 **Format Conversion**: Seamless conversion between Shapefile, GeoJSON, FileGDB, PostGIS, GeoPackage, KML, DXF, and TXT formats
- 🌐 **Coordinate System Support**: Comprehensive CRS transformation using GDAL/OGR with built-in CGCS2000 support
- 📐 **Geometry Processing**: Rich set of spatial operations including buffer, intersection, union, topology validation, and more
- 🔧 **Dual Engine Architecture**: NetTopologySuite for lightweight operations, GDAL for enterprise formats
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
var geom = GeometryUtil.Wkt2Geometry(wkt);
var validationResult = GeometryUtil.IsValid(geom);
if (!validationResult.IsValid)
{
    Console.WriteLine($"Geometry is invalid: {validationResult.ErrorMessage}");
}
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
└── Configuration/         # GdalConfiguration, LibrarySettings
```

### Dependencies

- **[NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite)** 2.5.0 - Geometry model and operations
- **[NetTopologySuite.IO.GeoJSON](https://www.nuget.org/packages/NetTopologySuite.IO.GeoJSON)** 4.0.0 - GeoJSON support
- **[NetTopologySuite.IO.ShapeFile](https://www.nuget.org/packages/NetTopologySuite.IO.ShapeFile)** 2.1.0 - Shapefile support
- **[MaxRev.Gdal.Core](https://github.com/MaxRev-Dev/gdal.netcore)** 3.9.2+ - GDAL/OGR bindings
- **[MaxRev.Gdal.Universal](https://github.com/MaxRev-Dev/gdal.netcore)** 3.9.2+ - Cross-platform GDAL runtime
- **[System.Text.Json](https://www.nuget.org/packages/System.Text.Json)** 8.0.5 - JSON serialization
- **[System.Text.Encoding.CodePages](https://www.nuget.org/packages/System.Text.Encoding.CodePages)** 7.0.0 - Encoding support (GBK, GB2312)
- **[SharpZipLib](https://github.com/icsharpcode/SharpZipLib)** 1.4.2 - ZIP compression
- **[Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)** 7.0.0 - Logging

### Requirements

- **.NET Standard 2.0** or higher
- Compatible with .NET Core 2.0+, .NET 5+, .NET Framework 4.6.1+

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

**OpenGIS Utils for .NET** (OGU4Net) 是基于 [NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite) 和 [MaxRev.Gdal.Universal](https://github.com/MaxRev-Dev/gdal.netcore) 的 .NET GIS 二次开发工具库。提供统一的图层模型和便捷的格式转换功能，简化 GIS 数据的读取、处理和导出操作。

本项目是 [opengis-utils-for-java](https://github.com/znlgis/opengis-utils-for-java) 的完整 C# .NET Standard 2.0 移植版本。

### 主要特性

- 🎯 **统一图层模型**：简洁一致的 `OguLayer`、`OguFeature`、`OguField` 抽象，屏蔽底层 GIS 库差异
- 🔄 **格式转换**：Shapefile、GeoJSON、FileGDB、PostGIS、GeoPackage、KML、DXF、TXT 等格式无缝转换
- 🌐 **坐标系支持**：基于 GDAL/OGR 的全面坐标系转换，内置 CGCS2000 支持
- 📐 **几何处理**：丰富的空间操作，包括缓冲区、交集、并集、拓扑验证等
- 🔧 **双引擎架构**：NetTopologySuite 用于轻量级操作，GDAL 用于企业级格式
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
var geom = GeometryUtil.Wkt2Geometry(wkt);
var validationResult = GeometryUtil.IsValid(geom);
if (!validationResult.IsValid)
{
    Console.WriteLine($"几何对象无效: {validationResult.ErrorMessage}");
}
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
└── Configuration/         # GdalConfiguration、LibrarySettings
```

### 依赖项

- **[NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite)** 2.5.0 - 几何模型和操作
- **[NetTopologySuite.IO.GeoJSON](https://www.nuget.org/packages/NetTopologySuite.IO.GeoJSON)** 4.0.0 - GeoJSON 支持
- **[NetTopologySuite.IO.ShapeFile](https://www.nuget.org/packages/NetTopologySuite.IO.ShapeFile)** 2.1.0 - Shapefile 支持
- **[MaxRev.Gdal.Core](https://github.com/MaxRev-Dev/gdal.netcore)** 3.9.2+ - GDAL/OGR 绑定
- **[MaxRev.Gdal.Universal](https://github.com/MaxRev-Dev/gdal.netcore)** 3.9.2+ - 跨平台 GDAL 运行时
- **[System.Text.Json](https://www.nuget.org/packages/System.Text.Json)** 8.0.5 - JSON 序列化
- **[System.Text.Encoding.CodePages](https://www.nuget.org/packages/System.Text.Encoding.CodePages)** 7.0.0 - 编码支持（GBK、GB2312）
- **[SharpZipLib](https://github.com/icsharpcode/SharpZipLib)** 1.4.2 - ZIP 压缩
- **[Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)** 7.0.0 - 日志

### 环境要求

- **.NET Standard 2.0** 或更高版本
- 兼容 .NET Core 2.0+、.NET 5+、.NET Framework 4.6.1+

### 许可证

采用 [LGPL-2.1-or-later](LICENSE) 许可证，与 Java 版本保持一致。

### 贡献

欢迎贡献！请随时提交 Pull Request。

### 链接

- **GitHub**: https://github.com/znlgis/opengis-utils-for-net
- **Java 版本**: https://github.com/znlgis/opengis-utils-for-java
- **NuGet**: [即将推出]