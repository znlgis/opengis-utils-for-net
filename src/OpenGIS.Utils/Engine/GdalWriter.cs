using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.IO;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;
using OSGeo.OGR;
using OSGeo.OSR;
using OgrDataSource = OSGeo.OGR.DataSource;
using SysException = System.Exception;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GDAL 写入器
/// </summary>
public class GdalWriter : ILayerWriter
{
    private static readonly ILogger Logger = OguLogging.CreateLogger<GdalWriter>();

    static GdalWriter()
    {
        // 确保 GDAL 已初始化
        GdalConfiguration.ConfigureGdal();
    }

    /// <summary>
    ///     写入图层
    /// </summary>
    /// <param name="layer">图层对象</param>
    /// <param name="path">输出路径</param>
    /// <param name="layerName">图层名称，如果为 null 则使用图层的 Name 属性</param>
    /// <param name="options">附加选项字典，可以包含驱动名称、编码等</param>
    /// <exception cref="ArgumentNullException">当图层为 null 时抛出</exception>
    /// <exception cref="ArgumentException">当路径为空时抛出</exception>
    /// <exception cref="SysException">当驱动不可用或创建数据源失败时抛出</exception>
    public void Write(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        ValidateCollections(layer);

        // 推断驱动名称
        string driverName = InferDriverName(path, options);
        var driver = Ogr.GetDriverByName(driverName);

        if (driver == null)
            throw new DataSourceException($"Driver '{driverName}' not available");

        // 确保目录存在
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        // 删除已存在的文件
        if (File.Exists(path) || Directory.Exists(path))
        {
            try
            {
                if (driver.DeleteDataSource(path) != 0)
                    throw new DataSourceException($"Failed to delete existing data source: {path}");
            }
            catch (SysException ex)
            {
                if (ex is DataSourceException)
                    throw;

                throw new DataSourceException($"Failed to delete existing data source: {path}", ex);
            }
        }

        OgrDataSource? dataSource = null;
        try
        {
            // 创建数据源
            dataSource = driver.CreateDataSource(path, new string[] { });

            if (dataSource == null)
                throw new DataSourceException($"Failed to create data source: {path}");

            // 创建图层
            var ogrGeomType = OgrTypeMapper.MapToOgrGeometryType(layer.GeometryType);
            var layerOptions = BuildLayerOptions(options);
            using var spatialReference = CreateSpatialReference(layer.Wkid);
            var ogrLayer = dataSource.CreateLayer(
                layerName ?? layer.Name ?? "layer",
                spatialReference,
                ogrGeomType,
                layerOptions);

            if (ogrLayer == null)
                throw new DataSourceException("Failed to create layer");

            // 创建字段
            foreach (var field in layer.Fields)
            {
                using var fieldDefn = CreateOgrFieldDefn(field);
                if (ogrLayer.CreateField(fieldDefn, 1) != 0)
                    throw new SysException($"Failed to create field '{field.Name}'");
            }

            // 预计算字段索引映射，避免在要素循环内重复调用 GetFieldIndex
            var fieldIndexMap = new Dictionary<string, int>(layer.Fields.Count);
            foreach (var field in layer.Fields)
            {
                var index = ogrLayer.GetLayerDefn().GetFieldIndex(field.Name);
                if (index >= 0) fieldIndexMap[field.Name] = index;
            }

            // 写入要素
            int failedCount = 0;
            foreach (var oguFeature in layer.Features)
            {
                if (string.IsNullOrWhiteSpace(oguFeature.Wkt))
                {
                    failedCount++;
                    Logger.LogWarning("跳过空几何要素 (Fid={Fid})", oguFeature.Fid);
                    continue;
                }

                Feature? ogrFeature = null;
                OSGeo.OGR.Geometry? geometry = null;

                try
                {
                    // 创建要素
                    ogrFeature = new Feature(ogrLayer.GetLayerDefn());

                    if (oguFeature.Fid != 0 && ogrFeature.SetFID(oguFeature.Fid) != 0)
                        throw new SysException($"设置要素 FID 失败 (Fid={oguFeature.Fid})");

                    // 设置几何
                    geometry = OSGeo.OGR.Geometry.CreateFromWkt(oguFeature.Wkt);
                    if (geometry == null)
                        throw new SysException($"无法解析要素几何 (Fid={oguFeature.Fid})");

                    if (ogrFeature.SetGeometry(geometry) != 0)
                        throw new SysException($"设置要素几何失败 (Fid={oguFeature.Fid})");

                    // 设置属性
                    foreach (var field in layer.Fields)
                    {
                        if (fieldIndexMap.TryGetValue(field.Name, out var fieldIndex))
                        {
                            var value = oguFeature.GetValue(field.Name);
                            SetFieldValue(ogrFeature, fieldIndex, value, field.DataType);
                        }
                    }

                    // 添加要素到图层
                    if (ogrLayer.CreateFeature(ogrFeature) != 0)
                    {
                        failedCount++;
                        Logger.LogWarning("创建要素失败 (Fid={Fid})", oguFeature.Fid);
                    }
                }
                catch (SysException ex)
                {
                    failedCount++;
                    Logger.LogWarning(ex, "写入要素时出错 (Fid={Fid})", oguFeature.Fid);
                }
                finally
                {
                    geometry?.Dispose();
                    ogrFeature?.Dispose();
                }
            }

            // 同步到磁盘
            dataSource.SyncToDisk();

            if (failedCount > 0)
                throw new SysException($"写入图层时 {failedCount} 个要素失败: {path}");
        }
        finally
        {
            dataSource?.Dispose();
        }
    }

    private static string[] BuildLayerOptions(Dictionary<string, object>? options)
    {
        if (options == null || !options.TryGetValue("encoding", out var encodingObj) || encodingObj == null)
            return Array.Empty<string>();

        var encoding = encodingObj as Encoding ?? Encoding.GetEncoding(encodingObj.ToString() ?? "UTF-8");
        return new[] { $"ENCODING={encoding.WebName}" };
    }

    /// <summary>
    ///     追加要素到已存在的图层
    /// </summary>
    /// <param name="layer">图层对象</param>
    /// <param name="path">输出路径</param>
    /// <param name="layerName">图层名称</param>
    /// <param name="options">附加选项</param>
    /// <exception cref="ArgumentNullException">当图层为 null 时抛出</exception>
    /// <exception cref="ArgumentException">当路径为空时抛出</exception>
    /// <exception cref="DataSourceException">当数据源或图层无法打开时抛出</exception>
    public void Append(OguLayer layer, string path, string? layerName = null,
        Dictionary<string, object>? options = null)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        ValidateCollections(layer);

        using var dataSource = Ogr.Open(path, 1);
        if (dataSource == null)
            throw new DataSourceException($"Failed to open data source for appending: {path}");

        var targetLayerName = layerName ?? layer.Name;
        var ogrLayer = string.IsNullOrWhiteSpace(targetLayerName)
            ? dataSource.GetLayerByIndex(0)
            : dataSource.GetLayerByName(targetLayerName);
        if (ogrLayer == null)
            throw new DataSourceException($"Layer '{targetLayerName ?? ""}' not found");

        var layerDefinition = ogrLayer.GetLayerDefn();
        var fieldIndexMap = new Dictionary<string, int>(layer.Fields.Count);
        foreach (var field in layer.Fields)
        {
            var index = layerDefinition.GetFieldIndex(field.Name);
            if (index < 0)
                throw new DataSourceException($"Field '{field.Name}' not found in target layer");
            fieldIndexMap[field.Name] = index;
        }

        var failedCount = 0;
        foreach (var oguFeature in layer.Features)
        {
            if (string.IsNullOrWhiteSpace(oguFeature.Wkt))
            {
                failedCount++;
                Logger.LogWarning("跳过空几何要素 (Fid={Fid})", oguFeature.Fid);
                continue;
            }

            Feature? ogrFeature = null;
            OSGeo.OGR.Geometry? geometry = null;
            try
            {
                ogrFeature = new Feature(layerDefinition);
                if (oguFeature.Fid != 0 && ogrFeature.SetFID(oguFeature.Fid) != 0)
                    throw new SysException($"设置要素 FID 失败 (Fid={oguFeature.Fid})");

                geometry = OSGeo.OGR.Geometry.CreateFromWkt(oguFeature.Wkt);
                if (geometry == null)
                    throw new SysException($"无法解析要素几何 (Fid={oguFeature.Fid})");
                if (ogrFeature.SetGeometry(geometry) != 0)
                    throw new SysException($"设置要素几何失败 (Fid={oguFeature.Fid})");

                foreach (var field in layer.Fields)
                {
                    var value = oguFeature.GetValue(field.Name);
                    SetFieldValue(ogrFeature, fieldIndexMap[field.Name], value, field.DataType);
                }

                if (ogrLayer.CreateFeature(ogrFeature) != 0)
                {
                    failedCount++;
                    Logger.LogWarning("追加要素失败 (Fid={Fid})", oguFeature.Fid);
                }
            }
            catch (SysException ex)
            {
                failedCount++;
                Logger.LogWarning(ex, "追加要素时出错 (Fid={Fid})", oguFeature.Fid);
            }
            finally
            {
                geometry?.Dispose();
                ogrFeature?.Dispose();
            }
        }

        dataSource.SyncToDisk();
        if (failedCount > 0)
            throw new SysException($"追加到图层时 {failedCount} 个要素失败: {path}");
    }

    private static void ValidateCollections(OguLayer layer)
    {
        if (layer.Fields == null)
            throw new ArgumentException("Layer fields collection cannot be null", nameof(layer));

        if (layer.Features == null)
            throw new ArgumentException("Layer features collection cannot be null", nameof(layer));
    }

    private string InferDriverName(string path, Dictionary<string, object>? options)
    {
        // 从选项中获取驱动名称
        if (options != null && options.TryGetValue("driver", out var driverObj))
            return driverObj.ToString() ?? "ESRI Shapefile";

        // 根据扩展名推断
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".shp" => "ESRI Shapefile",
            ".gdb" => "FileGDB",
            ".gpkg" => "GPKG",
            ".kml" => "KML",
            ".dxf" => "DXF",
            ".geojson" or ".json" => "GeoJSON",
            _ => "ESRI Shapefile"
        };
    }

    private FieldDefn CreateOgrFieldDefn(OguField field)
    {
        var ogrType = OgrTypeMapper.MapToOgrFieldType(field.DataType);
        var fieldDefn = new FieldDefn(field.Name, ogrType);

        if (field.Length.HasValue && field.Length.Value > 0) fieldDefn.SetWidth(field.Length.Value);

        if (field.Precision.HasValue && field.Precision.Value > 0) fieldDefn.SetPrecision(field.Precision.Value);

        return fieldDefn;
    }

    private static SpatialReference? CreateSpatialReference(int? wkid)
    {
        if (!wkid.HasValue)
            return null;

        var spatialReference = new SpatialReference(null);
        if (spatialReference.ImportFromEPSG(wkid.Value) != 0)
        {
            spatialReference.Dispose();
            throw new SysException($"Failed to import spatial reference EPSG:{wkid.Value}");
        }

        return spatialReference;
    }

    private void SetFieldValue(Feature feature, int fieldIndex, object? value, FieldDataType dataType)
    {
        if (value == null)
        {
            feature.UnsetField(fieldIndex);
            return;
        }

        switch (dataType)
        {
            case FieldDataType.INTEGER:
                feature.SetField(fieldIndex, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                break;
            case FieldDataType.LONG:
                feature.SetField(fieldIndex, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case FieldDataType.DOUBLE:
            case FieldDataType.FLOAT:
                feature.SetField(fieldIndex, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case FieldDataType.DATE:
            case FieldDataType.DATETIME:
                if (value is DateTime dt)
                {
                    var seconds = dt.Second + (float)dt.Millisecond / 1000;
                    feature.SetField(fieldIndex, dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, seconds, 0);
                }
                break;
            default:
                feature.SetField(fieldIndex, value.ToString());
                break;
        }
    }
}
