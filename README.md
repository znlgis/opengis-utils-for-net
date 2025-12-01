# OGU4Net - OpenGIS Utils for .NET

[![.NET Standard](https://img.shields.io/badge/.NET%20Standard-2.0-blue)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
[![License](https://img.shields.io/badge/License-Apache%202.0-green.svg)](LICENSE)
[![Version](https://img.shields.io/badge/Version-1.0.0-orange)](https://github.com/znlgis/opengis-utils-for-net)

[English](#english) | [中文](#中文)

---

<a name="中文"></a>
## 中文说明

### 简介

OGU4Net（OpenGIS Utils for .NET）是一个基于开源GIS库（NetTopologySuite、ProjNET）的.NET GIS二次开发工具库。它提供了统一的图层模型和便捷的格式转换功能，简化了GIS数据的读取、处理和导出操作。本项目是 [opengis-utils-for-java](https://github.com/znlgis/opengis-utils-for-java) 的.NET版本。

### 主要特性

- 🗂️ **统一图层模型**：提供简洁的图层、要素、字段抽象，屏蔽底层GIS库差异
- 📐 **几何处理**：基于NetTopologySuite提供丰富的几何操作和空间分析功能
- 🌐 **坐标系管理**：内置CGCS2000坐标系支持，提供坐标转换功能
- 🔄 **格式转换**：支持WKT、GeoJSON等常见几何格式的相互转换
- 🛠️ **实用工具**：提供ZIP压缩/解压、文件编码检测、自然排序等实用工具

### 快速安装

#### NuGet

```shell
dotnet add package Ogu4Net
```

或者在项目文件中添加：

```xml
<PackageReference Include="Ogu4Net" Version="1.0.0" />
```

### 核心图层模型

本库提供了统一的简化图层模型，位于 `Ogu4Net.Model.Layer` 命名空间中：

| 类名 | 说明 |
|------|------|
| **OguLayer** | 统一的GIS图层定义，包含图层名称、坐标系、几何类型、字段定义和要素集合 |
| **OguFeature** | 统一的要素类，包含要素ID、几何信息（WKT格式）和属性值集合 |
| **OguField** | 统一的字段定义类，包含字段名称、别名、数据类型等信息 |
| **OguFieldValue** | 字段值容器，提供便捷的类型转换方法（GetStringValue、GetIntValue、GetDoubleValue等） |
| **OguCoordinate** | 坐标类，支持二维/三维坐标及点号/圈号（用于国土TXT格式） |
| **OguFeatureFilter** | 委托类型，用于要素过滤 |
| **OguLayerMetadata** | 图层元数据，存储坐标系参数、数据来源、扩展信息等 |

### 使用示例

#### 基本操作

```csharp
using Ogu4Net.Model.Layer;
using Ogu4Net.Enums;

// 从JSON字符串创建OguLayer
OguLayer layer = OguLayer.FromJson(jsonString);

// 验证图层数据完整性
layer.Validate();

// 过滤要素
var filtered = layer.Filter(feature => 
    "北京".Equals(feature.GetValue("city")));

// 获取要素数量
int count = layer.GetFeatureCount();

// 转换为JSON字符串
string json = layer.ToJson();
```

#### 读取要素属性

```csharp
OguFeature feature = layer.Features[0];

// 获取属性值
object value = feature.GetValue("fieldName");

// 获取属性值对象
OguFieldValue fieldValue = feature.GetAttribute("fieldName");
string strValue = fieldValue.GetStringValue();
int? intValue = fieldValue.GetIntValue();
double? doubleValue = fieldValue.GetDoubleValue();

// 设置属性值
feature.SetValue("fieldName", newValue);
```

### 几何格式转换

使用 `GeometryConverter` 进行几何格式转换：

```csharp
using Ogu4Net.Geometry;
using NetTopologySuite.Geometries;

// WKT <-> NTS Geometry
Geometry geom = GeometryConverter.Wkt2Geometry(wkt);
string wkt = GeometryConverter.Geometry2Wkt(geom);

// GeoJSON <-> NTS Geometry
Geometry geom = GeometryConverter.GeoJson2Geometry(geojson);
string geojson = GeometryConverter.Geometry2GeoJson(geom);

// WKT <-> GeoJSON
string geojson = GeometryConverter.Wkt2GeoJson(wkt);
string wkt = GeometryConverter.GeoJson2Wkt(geojson);

// 安全解析
if (GeometryConverter.TryParseWkt(wkt, out var geometry))
{
    // 解析成功
}
```

### 几何空间分析

#### NTS几何工具（NtsGeometryUtil）

```csharp
using Ogu4Net.Geometry;

// 空间关系判断
bool result = NtsGeometryUtil.Intersects(geomA, geomB);
bool result = NtsGeometryUtil.Contains(geomA, geomB);
bool result = NtsGeometryUtil.Within(geomA, geomB);
bool result = NtsGeometryUtil.Touches(geomA, geomB);
bool result = NtsGeometryUtil.Crosses(geomA, geomB);
bool result = NtsGeometryUtil.Overlaps(geomA, geomB);
bool result = NtsGeometryUtil.Disjoint(geomA, geomB);

// 空间分析
Geometry buffer = NtsGeometryUtil.Buffer(geom, distance);
Geometry intersection = NtsGeometryUtil.Intersection(geomA, geomB);
Geometry union = NtsGeometryUtil.Union(geomA, geomB);
Geometry difference = NtsGeometryUtil.Difference(geomA, geomB);
Geometry symDifference = NtsGeometryUtil.SymDifference(geomA, geomB);

// 几何属性
double area = NtsGeometryUtil.Area(geom);
double length = NtsGeometryUtil.Length(geom);
Point centroid = NtsGeometryUtil.Centroid(geom);
Point interiorPoint = NtsGeometryUtil.InteriorPoint(geom);
int dimension = NtsGeometryUtil.Dimension(geom);
int numPoints = NtsGeometryUtil.NumPoints(geom);
GeometryType? geometryType = NtsGeometryUtil.GetGeometryType(geom);
bool isEmpty = NtsGeometryUtil.IsEmpty(geom);

// 几何边界与外包矩形
Geometry boundary = NtsGeometryUtil.Boundary(geom);
Geometry envelope = NtsGeometryUtil.Envelope(geom);

// 凸包
Geometry convexHull = NtsGeometryUtil.ConvexHull(geom);

// 拓扑验证与简化
TopologyValidationResult validResult = NtsGeometryUtil.IsValid(geom);
SimpleGeometryResult simpleResult = NtsGeometryUtil.CheckIsSimple(geom);
Geometry simplified = NtsGeometryUtil.Simplify(geom, tolerance);
Geometry validated = NtsGeometryUtil.Validate(geom);
Geometry densified = NtsGeometryUtil.Densify(geom, distance);

// 几何相等判断
bool equalsExact = NtsGeometryUtil.EqualsExact(geomA, geomB);
bool equalsExactTol = NtsGeometryUtil.EqualsExactTolerance(geomA, geomB, tolerance);
bool equalsNorm = NtsGeometryUtil.EqualsNorm(geomA, geomB);
bool equalsTopo = NtsGeometryUtil.EqualsTopo(geomA, geomB);

// 空间关系模式
bool relateResult = NtsGeometryUtil.RelatePattern(geomA, geomB, "T*T***FF*");
string relate = NtsGeometryUtil.Relate(geomA, geomB);

// 距离计算
double distance = NtsGeometryUtil.Distance(geomA, geomB);
bool withinDistance = NtsGeometryUtil.IsWithinDistance(geomA, geomB, maxDistance);

// 多边形操作
Geometry splitResult = NtsGeometryUtil.SplitPolygon(polygon, line);
Geometry polygonized = NtsGeometryUtil.Polygonize(geom);
```

### 坐标系工具（CrsUtil）

位于 `Ogu4Net.Common` 命名空间中：

```csharp
using Ogu4Net.Common;

// 坐标转换（WKT字符串）
string transformedWkt = CrsUtil.Transform(wkt, sourceWkid, targetWkid);

// 坐标转换（NTS Geometry）
Geometry transformed = CrsUtil.Transform(geometry, sourceWkid, targetWkid);

// 图层投影转换
OguLayer reprojected = CrsUtil.Reproject(layer, targetWkid);

// 获取带号
int zoneNumber = CrsUtil.GetZoneNumber(geometry);
int zoneNumber = CrsUtil.GetZoneNumber(wkt);
int zoneNumber = CrsUtil.GetZoneNumberFromWkid(projectedWkid);

// 获取几何对应的WKID
int wkid = CrsUtil.GetWkid(geometry);

// 获取投影坐标系WKID
int projectedWkid = CrsUtil.GetProjectedWkid(zoneNumber);
int projectedWkid = CrsUtil.GetProjectedWkidFromGeometry(geometry);

// 判断坐标系类型
bool isProjected = CrsUtil.IsProjectedCrs(wkid);

// 获取容差
double tolerance = CrsUtil.GetTolerance(wkid);

// 获取支持的坐标系列表
var crsList = CrsUtil.GetSupportedCrsList();
```

### API模块概览

| 命名空间 | 说明 |
|----------|------|
| `Ogu4Net.Model.Layer` | 图层模型类（OguLayer、OguFeature、OguField等） |
| `Ogu4Net.Model` | 数据模型类（DbConnBaseModel、GdbGroupModel、TopologyValidationResult等） |
| `Ogu4Net.Enums` | 枚举类型（GeometryType、FieldDataType、GisEngineType、DataFormatType等） |
| `Ogu4Net.Geometry` | 几何处理工具（NtsGeometryUtil、GeometryConverter） |
| `Ogu4Net.Common` | 通用工具类（CrsUtil、ZipUtil、EncodingUtil、SortUtil、NumUtil） |

### 实用工具类

#### ZipUtil - ZIP压缩解压工具

```csharp
using Ogu4Net.Common;

// 压缩文件夹
ZipUtil.Zip(folderPath, "output.zip");
ZipUtil.Zip(folderPath, "output.zip", Encoding.UTF8);

// 解压文件
ZipUtil.Unzip("input.zip", destPath);
ZipUtil.Unzip("input.zip", destPath, Encoding.UTF8);
```

#### EncodingUtil - 文件编码检测工具

```csharp
using Ogu4Net.Common;

// 自动检测文件编码
Encoding charset = EncodingUtil.GetFileEncoding(filePath);
```

#### SortUtil - 自然排序工具

```csharp
using Ogu4Net.Common;

// 包含数字的字符串自然排序
int result = SortUtil.CompareString("第5章", "第10章");  // 返回 -1

// 获取自然排序比较器
var comparer = SortUtil.GetNaturalComparer();
list.Sort(comparer);
```

#### NumUtil - 数字格式化工具

```csharp
using Ogu4Net.Common;

// 去除科学计数法显示
string plainString = NumUtil.GetPlainString(1.234E10);  // 返回 "12340000000"
```

### 依赖说明

本库主要依赖以下开源库：

| 依赖库 | 版本 | 说明 |
|--------|------|------|
| **NetTopologySuite** | 2.5.0 | .NET拓扑套件，提供几何对象和空间操作 |
| **NetTopologySuite.IO.GeoJSON** | 4.0.0 | NTS的GeoJSON读写支持 |
| **ProjNET** | 2.0.0 | .NET坐标系转换库 |
| **Newtonsoft.Json** | 13.0.3 | JSON序列化库 |
| **SharpZipLib** | 1.4.2 | ZIP压缩解压库 |
| **UTF.Unknown** | 2.5.1 | 文件编码检测库 |

### 环境要求

- **.NET Standard 2.0** 兼容的运行时（.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+）

---

<a name="english"></a>
## English

### Introduction

OGU4Net (OpenGIS Utils for .NET) is a .NET GIS development toolkit based on open-source GIS libraries (NetTopologySuite, ProjNET). It provides a unified layer model and convenient format conversion functions to simplify GIS data reading, processing, and exporting operations. This project is the .NET port of [opengis-utils-for-java](https://github.com/znlgis/opengis-utils-for-java).

### Features

- 🗂️ **Unified Layer Model**: Provides simple layer, feature, and field abstractions, hiding the differences of underlying GIS libraries
- 📐 **Geometry Processing**: Rich geometry operations and spatial analysis based on NetTopologySuite
- 🌐 **CRS Management**: Built-in CGCS2000 coordinate system support with coordinate transformation capabilities
- 🔄 **Format Conversion**: Supports mutual conversion between WKT, GeoJSON and other common geometry formats
- 🛠️ **Utility Tools**: Provides ZIP compression/decompression, file encoding detection, natural sorting, and other utilities

### Installation

#### NuGet

```shell
dotnet add package Ogu4Net
```

Or add to your project file:

```xml
<PackageReference Include="Ogu4Net" Version="1.0.0" />
```

### Layer Model

The library provides a unified simplified layer model in the `Ogu4Net.Model.Layer` namespace:

| Class | Description |
|-------|-------------|
| **OguLayer** | Unified GIS layer definition with name, CRS, geometry type, fields, and features |
| **OguFeature** | Unified feature class containing ID, geometry (WKT format), and attributes |
| **OguField** | Unified field definition with name, alias, and data type |
| **OguFieldValue** | Field value container with convenient type conversion methods |
| **OguCoordinate** | Coordinate class supporting 2D/3D coordinates with point/ring numbers |
| **OguFeatureFilter** | Delegate for feature filtering |
| **OguLayerMetadata** | Layer metadata storing CRS parameters, data source, and extended info |

### Quick Start

```csharp
using Ogu4Net.Model.Layer;
using Ogu4Net.Enums;

// Create OguLayer from JSON
OguLayer layer = OguLayer.FromJson(jsonString);

// Validate layer data integrity
layer.Validate();

// Filter features
var filtered = layer.Filter(feature =>
    "Beijing".Equals(feature.GetValue("city")));

// Convert to JSON
string json = layer.ToJson();
```

### Format Conversion

```csharp
using Ogu4Net.Geometry;

// WKT <-> NTS Geometry
Geometry geom = GeometryConverter.Wkt2Geometry(wkt);
string wkt = GeometryConverter.Geometry2Wkt(geom);

// GeoJSON <-> NTS Geometry  
Geometry geom = GeometryConverter.GeoJson2Geometry(geojson);
string geojson = GeometryConverter.Geometry2GeoJson(geom);
```

### Requirements

- **.NET Standard 2.0** compatible runtime (.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+)

### Dependencies

| Library | Version | Description |
|---------|---------|-------------|
| **NetTopologySuite** | 2.5.0 | .NET Topology Suite for geometry objects and spatial operations |
| **NetTopologySuite.IO.GeoJSON** | 4.0.0 | GeoJSON I/O support for NTS |
| **ProjNET** | 2.0.0 | .NET coordinate transformation library |
| **Newtonsoft.Json** | 13.0.3 | JSON serialization library |
| **SharpZipLib** | 1.4.2 | ZIP compression/decompression library |
| **UTF.Unknown** | 2.5.1 | File encoding detection library |

### API Overview

| Namespace | Description |
|-----------|-------------|
| `Ogu4Net.Model.Layer` | Layer model classes (OguLayer, OguFeature, OguField, etc.) |
| `Ogu4Net.Model` | Data model classes (DbConnBaseModel, GdbGroupModel, TopologyValidationResult, etc.) |
| `Ogu4Net.Enums` | Enumerations (GeometryType, FieldDataType, GisEngineType, DataFormatType) |
| `Ogu4Net.Geometry` | Geometry utilities (NtsGeometryUtil, GeometryConverter) |
| `Ogu4Net.Common` | Common utilities (CrsUtil, ZipUtil, EncodingUtil, SortUtil, NumUtil) |

### License

This project is licensed under the Apache License 2.0.

### Contributing

Contributions are welcome! Please feel free to submit a Pull Request.