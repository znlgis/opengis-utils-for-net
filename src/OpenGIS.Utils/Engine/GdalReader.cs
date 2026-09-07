using System;
using System.Collections.Generic;
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
///     GDAL 读取器
/// </summary>
public class GdalReader : ILayerReader
{
    private static readonly ILogger Logger = OguLogging.CreateLogger<GdalReader>();
    private static readonly object GlobalConfigLock = new();

    static GdalReader()
    {
        // 确保 GDAL 已初始化
        GdalConfiguration.ConfigureGdal();
    }

    /// <summary>
    ///     读取图层
    /// </summary>
    /// <param name="path">数据源路径</param>
    /// <param name="layerName">图层名称，如果为 null 则读取第一个图层</param>
    /// <param name="attributeFilter">属性过滤条件（SQL WHERE 子句）</param>
    /// <param name="spatialFilterWkt">空间过滤几何（WKT格式）</param>
    /// <param name="options">附加选项</param>
    /// <returns>图层对象</returns>
    /// <exception cref="ArgumentException">当路径为空时抛出</exception>
    /// <exception cref="SysException">当无法打开数据源或找不到图层时抛出</exception>
    public OguLayer Read(string path, string? layerName = null, string? attributeFilter = null,
        string? spatialFilterWkt = null, Dictionary<string, object>? options = null)
    {
        lock (GlobalConfigLock)
        {
            return ReadCore(path, layerName, attributeFilter, spatialFilterWkt, options);
        }
    }

    private OguLayer ReadCore(string path, string? layerName, string? attributeFilter,
        string? spatialFilterWkt, Dictionary<string, object>? options)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        // 应用编码选项（如 SHAPE_ENCODING），确保 GBK 等编码正确读取
        ApplyEncodingOption(options);

        OgrDataSource? dataSource = null;
        try
        {
            dataSource = Ogr.Open(path, 0); // 0 = read-only

            if (dataSource == null)
                throw new DataSourceException($"Failed to open data source: {path}");

            // 选择图层
            Layer ogrLayer;
            if (!string.IsNullOrWhiteSpace(layerName))
            {
                ogrLayer = dataSource.GetLayerByName(layerName);
                if (ogrLayer == null)
                    throw new DataSourceException($"Layer '{layerName}' not found");
            }
            else
            {
                if (dataSource.GetLayerCount() == 0)
                    throw new DataSourceException("No layers found in data source");
                ogrLayer = dataSource.GetLayerByIndex(0);
            }

            return ReadOgrLayer(ogrLayer, attributeFilter, spatialFilterWkt);
        }
        finally
        {
            dataSource?.Dispose();
        }
    }

    /// <summary>
    ///     获取图层名称列表
    /// </summary>
    /// <param name="path">数据源路径</param>
    /// <returns>图层名称列表</returns>
    /// <exception cref="ArgumentException">当路径为空时抛出</exception>
    /// <exception cref="DataSourceException">当无法打开数据源时抛出</exception>
    public IList<string> GetLayerNames(string path)
    {
        lock (GlobalConfigLock)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            var layerNames = new List<string>();

            using var dataSource = Ogr.Open(path, 0);
            if (dataSource == null)
                throw new DataSourceException($"Failed to open data source: {path}");

            var layerCount = dataSource.GetLayerCount();
            for (int i = 0; i < layerCount; i++)
            {
                using var layer = dataSource.GetLayerByIndex(i);
                if (layer != null) layerNames.Add(layer.GetName());
            }

            return layerNames;
        }
    }

    private OguLayer ReadOgrLayer(Layer ogrLayer, string? attributeFilter, string? spatialFilterWkt)
    {
        var layer = new OguLayer { Name = ogrLayer.GetName() };

        using (var spatialReference = ogrLayer.GetSpatialRef())
        {
            layer.Wkid = GetWkid(spatialReference);
        }

        // 读取字段定义
        var layerDefn = ogrLayer.GetLayerDefn();
        var fieldCount = layerDefn.GetFieldCount();

        for (int i = 0; i < fieldCount; i++)
        {
            var fieldDefn = layerDefn.GetFieldDefn(i);
            var field = new OguField
            {
                Name = fieldDefn.GetName(),
                DataType = OgrTypeMapper.MapOgrFieldType(fieldDefn.GetFieldType()),
                Length = fieldDefn.GetWidth(),
                Precision = fieldDefn.GetPrecision()
            };
            layer.AddField(field);
        }

        // 确定几何类型
        var geomType = ogrLayer.GetGeomType();
        layer.GeometryType = OgrTypeMapper.MapOgrGeometryType(geomType);

        // 应用属性过滤
        if (!string.IsNullOrWhiteSpace(attributeFilter))
        {
            try
            {
                ogrLayer.SetAttributeFilter(attributeFilter);
            }
            catch (SysException ex)
            {
                throw new FormatParseException($"Invalid attribute filter: {attributeFilter}", ex);
            }
        }

        // 应用空间过滤
        if (!string.IsNullOrWhiteSpace(spatialFilterWkt))
        {
            try
            {
                using var filterGeom = OSGeo.OGR.Geometry.CreateFromWkt(spatialFilterWkt);
                if (filterGeom == null)
                    throw new FormatParseException($"Invalid spatial filter WKT: {spatialFilterWkt}");

                ogrLayer.SetSpatialFilter(filterGeom);
            }
            catch (FormatParseException)
            {
                throw;
            }
            catch (SysException ex)
            {
                throw new FormatParseException($"Invalid spatial filter WKT: {spatialFilterWkt}", ex);
            }
        }

        // 预计算字段索引映射，避免在要素循环内重复调用 GetFieldIndex
        var fieldIndexMap = new Dictionary<string, int>(layer.Fields.Count);
        foreach (var field in layer.Fields)
        {
            var index = ogrLayer.GetLayerDefn().GetFieldIndex(field.Name);
            if (index >= 0) fieldIndexMap[field.Name] = index;
        }

        // 读取要素
        ogrLayer.ResetReading();

        Feature? ogrFeature;
        while ((ogrFeature = ogrLayer.GetNextFeature()) != null)
            using (ogrFeature)
            {
                var sourceFid = ogrFeature.GetFID();
                var feature = new OguFeature
                {
                    Fid = sourceFid >= int.MinValue && sourceFid <= int.MaxValue
                        ? (int)sourceFid
                        : throw new SysException($"Source FID is outside the supported Int32 range: {sourceFid}")
                };

                // 读取几何
                var geometry = ogrFeature.GetGeometryRef();
                if (geometry != null)
                {
                    geometry.ExportToWkt(out string wkt);
                    feature.Wkt = wkt;
                }

                // 读取属性
                foreach (var field in layer.Fields)
                {
                    if (fieldIndexMap.TryGetValue(field.Name, out var fieldIndex))
                    {
                        var value = GetFieldValue(ogrFeature, fieldIndex, field.DataType, field.Name);
                        feature.SetValue(field.Name, value);
                    }
                }

                layer.AddFeature(feature);
            }

        return layer;
    }

    private static int? GetWkid(SpatialReference? spatialReference)
    {
        if (spatialReference == null)
            return null;

        var authorityCode = spatialReference.GetAuthorityCode(null);
        return int.TryParse(authorityCode, out var wkid) ? wkid : null;
    }

    private static void ApplyEncodingOption(Dictionary<string, object>? options)
    {
        if (options == null || !options.TryGetValue("encoding", out var encodingObj) || encodingObj == null)
            return;

        var encoding = encodingObj as Encoding ?? Encoding.GetEncoding(encodingObj.ToString() ?? "UTF-8");
        OSGeo.GDAL.Gdal.SetConfigOption("SHAPE_ENCODING", encoding.WebName);
    }

    private object? GetFieldValue(Feature feature, int fieldIndex, FieldDataType dataType, string fieldName)
    {
        if (!feature.IsFieldSet(fieldIndex))
            return null;

        return dataType switch
        {
            FieldDataType.INTEGER => feature.GetFieldAsInteger(fieldIndex),
            FieldDataType.LONG => feature.GetFieldAsInteger64(fieldIndex),
            FieldDataType.DOUBLE or FieldDataType.FLOAT => feature.GetFieldAsDouble(fieldIndex),
            FieldDataType.STRING => feature.GetFieldAsString(fieldIndex),
            FieldDataType.DATE or FieldDataType.DATETIME => GetDateTimeValue(feature, fieldIndex, fieldName),
            _ => feature.GetFieldAsString(fieldIndex)
        };
    }

    private DateTime? GetDateTimeValue(Feature feature, int fieldIndex, string fieldName)
    {
        try
        {
            feature.GetFieldAsDateTime(fieldIndex, out int year, out int month, out int day,
                out int hour, out int minute, out float second, out int tzFlag);
            var wholeSeconds = (int)Math.Truncate(second);
            var fractionalTicks = (long)Math.Round(
                (second - wholeSeconds) * TimeSpan.TicksPerSecond,
                MidpointRounding.AwayFromZero);
            if (fractionalTicks == TimeSpan.TicksPerSecond)
            {
                wholeSeconds++;
                fractionalTicks = 0;
            }

            return new DateTime(year, month, day, hour, minute, wholeSeconds).AddTicks(fractionalTicks);
        }
        catch (SysException ex)
        {
            Logger.LogDebug(ex, "解析日期时间字段失败 (fieldIndex={FieldIndex})", fieldIndex);
            throw new FormatParseException($"Invalid date field '{fieldName}'", ex);
        }
    }
}
