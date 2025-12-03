using System;
using System.Collections.Generic;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.IO;
using OpenGIS.Utils.Engine.Model.Layer;
using OSGeo.OGR;
using OgrDataSource = OSGeo.OGR.DataSource;
using SysException = System.Exception;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GDAL 读取器
/// </summary>
public class GdalReader : ILayerReader
{
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
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        OgrDataSource? dataSource = null;
        try
        {
            dataSource = Ogr.Open(path, 0); // 0 = read-only

            if (dataSource == null)
                throw new SysException($"Failed to open data source: {path}");

            // 选择图层
            Layer ogrLayer;
            if (!string.IsNullOrWhiteSpace(layerName))
            {
                ogrLayer = dataSource.GetLayerByName(layerName);
                if (ogrLayer == null)
                    throw new SysException($"Layer '{layerName}' not found");
            }
            else
            {
                if (dataSource.GetLayerCount() == 0)
                    throw new SysException("No layers found in data source");
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
    public IList<string> GetLayerNames(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        var layerNames = new List<string>();

        using var dataSource = Ogr.Open(path, 0);
        if (dataSource == null)
            return layerNames;

        var layerCount = dataSource.GetLayerCount();
        for (int i = 0; i < layerCount; i++)
        {
            using var layer = dataSource.GetLayerByIndex(i);
            if (layer != null) layerNames.Add(layer.GetName());
        }

        return layerNames;
    }

    private OguLayer ReadOgrLayer(Layer ogrLayer, string? attributeFilter, string? spatialFilterWkt)
    {
        var layer = new OguLayer { Name = ogrLayer.GetName() };

        // 读取字段定义
        var layerDefn = ogrLayer.GetLayerDefn();
        var fieldCount = layerDefn.GetFieldCount();

        for (int i = 0; i < fieldCount; i++)
        {
            var fieldDefn = layerDefn.GetFieldDefn(i);
            var field = new OguField
            {
                Name = fieldDefn.GetName(),
                DataType = MapOgrFieldType(fieldDefn.GetFieldType()),
                Length = fieldDefn.GetWidth(),
                Precision = fieldDefn.GetPrecision()
            };
            layer.AddField(field);
        }

        // 确定几何类型
        var geomType = ogrLayer.GetGeomType();
        layer.GeometryType = MapOgrGeometryType(geomType);

        // 应用属性过滤
        if (!string.IsNullOrWhiteSpace(attributeFilter))
            ogrLayer.SetAttributeFilter(attributeFilter);

        // 应用空间过滤
        if (!string.IsNullOrWhiteSpace(spatialFilterWkt))
            try
            {
                using var filterGeom = OSGeo.OGR.Geometry.CreateFromWkt(spatialFilterWkt);
                if (filterGeom != null) ogrLayer.SetSpatialFilter(filterGeom);
            }
            catch
            {
                // 忽略空间过滤错误
            }

        // 读取要素
        int fid = 1;
        ogrLayer.ResetReading();

        Feature? ogrFeature;
        while ((ogrFeature = ogrLayer.GetNextFeature()) != null)
            using (ogrFeature)
            {
                var feature = new OguFeature { Fid = fid++ };

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
                    var fieldIndex = ogrFeature.GetFieldIndex(field.Name);
                    if (fieldIndex >= 0)
                    {
                        var value = GetFieldValue(ogrFeature, fieldIndex, field.DataType);
                        feature.SetValue(field.Name, value);
                    }
                }

                layer.AddFeature(feature);
            }

        return layer;
    }

    private object? GetFieldValue(Feature feature, int fieldIndex, FieldDataType dataType)
    {
        if (!feature.IsFieldSet(fieldIndex))
            return null;

        return dataType switch
        {
            FieldDataType.INTEGER => feature.GetFieldAsInteger(fieldIndex),
            FieldDataType.LONG => feature.GetFieldAsInteger64(fieldIndex),
            FieldDataType.DOUBLE or FieldDataType.FLOAT => feature.GetFieldAsDouble(fieldIndex),
            FieldDataType.STRING => feature.GetFieldAsString(fieldIndex),
            FieldDataType.DATE or FieldDataType.DATETIME => GetDateTimeValue(feature, fieldIndex),
            _ => feature.GetFieldAsString(fieldIndex)
        };
    }

    private DateTime? GetDateTimeValue(Feature feature, int fieldIndex)
    {
        try
        {
            feature.GetFieldAsDateTime(fieldIndex, out int year, out int month, out int day,
                out int hour, out int minute, out float second, out int tzFlag);
            return new DateTime(year, month, day, hour, minute, (int)second);
        }
        catch
        {
            return null;
        }
    }

    private FieldDataType MapOgrFieldType(FieldType ogrType)
    {
        return ogrType switch
        {
            FieldType.OFTInteger => FieldDataType.INTEGER,
            FieldType.OFTInteger64 => FieldDataType.LONG,
            FieldType.OFTReal => FieldDataType.DOUBLE,
            FieldType.OFTString => FieldDataType.STRING,
            FieldType.OFTDate => FieldDataType.DATE,
            FieldType.OFTDateTime => FieldDataType.DATETIME,
            FieldType.OFTBinary => FieldDataType.BINARY,
            _ => FieldDataType.STRING
        };
    }

    private GeometryType MapOgrGeometryType(wkbGeometryType geomType)
    {
        var flatType = wkbFlatten((int)geomType);

        return flatType switch
        {
            wkbGeometryType.wkbPoint => GeometryType.POINT,
            wkbGeometryType.wkbLineString => GeometryType.LINESTRING,
            wkbGeometryType.wkbPolygon => GeometryType.POLYGON,
            wkbGeometryType.wkbMultiPoint => GeometryType.MULTIPOINT,
            wkbGeometryType.wkbMultiLineString => GeometryType.MULTILINESTRING,
            wkbGeometryType.wkbMultiPolygon => GeometryType.MULTIPOLYGON,
            wkbGeometryType.wkbGeometryCollection => GeometryType.GEOMETRYCOLLECTION,
            _ => GeometryType.UNKNOWN
        };
    }

    private wkbGeometryType wkbFlatten(int geomType)
    {
        return (wkbGeometryType)(geomType & ~0x80000000);
    }
}
