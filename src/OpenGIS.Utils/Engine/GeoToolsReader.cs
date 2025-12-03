using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.IO;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Engine.Util;
using GeometryType = OpenGIS.Utils.Engine.Enums.GeometryType;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GeoTools 读取器（基于 NetTopologySuite）
/// </summary>
public class GeoToolsReader : ILayerReader
{
    /// <summary>
    ///     读取图层
    /// </summary>
    public OguLayer Read(string path, string? layerName = null, string? attributeFilter = null,
        string? spatialFilterWkt = null, Dictionary<string, object>? options = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("File not found", path);

        var extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".shp" => ReadShapefile(path, attributeFilter, spatialFilterWkt, options),
            ".geojson" or ".json" => ReadGeoJson(path, attributeFilter, spatialFilterWkt),
            _ => throw new FormatException($"Unsupported file format: {extension}")
        };
    }

    /// <summary>
    ///     获取图层名称列表
    /// </summary>
    public IList<string> GetLayerNames(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        // Shapefile 和 GeoJSON 通常只有一个图层
        if (File.Exists(path)) return new List<string> { Path.GetFileNameWithoutExtension(path) };

        return new List<string>();
    }

    private OguLayer ReadShapefile(string path, string? attributeFilter, string? spatialFilterWkt,
        Dictionary<string, object>? options)
    {
        var encoding = ShpUtil.GetShapefileEncoding(path);

        using var reader = new ShapefileDataReader(path, GeometryFactory.Default, encoding);

        var layer = new OguLayer
        {
            Name = Path.GetFileNameWithoutExtension(path),
            GeometryType = GetGeometryType(reader.ShapeHeader.ShapeType)
        };

        // 读取字段定义
        var fields = new List<OguField>();
        var dbaseHeader = reader.DbaseHeader;

        for (int i = 0; i < dbaseHeader.NumFields; i++)
        {
            var fieldDescriptor = dbaseHeader.Fields[i];
            var field = new OguField
            {
                Name = fieldDescriptor.Name,
                DataType = MapDbaseFieldType(fieldDescriptor.DbaseType),
                Length = fieldDescriptor.Length,
                Precision = fieldDescriptor.DecimalCount
            };
            fields.Add(field);
            layer.AddField(field);
        }

        // 读取要素
        int fid = 1;
        while (reader.Read())
        {
            var geometry = reader.Geometry;
            if (geometry == null) continue;

            var feature = new OguFeature { Fid = fid++, Wkt = geometry.AsText() };

            // 读取属性
            for (int i = 0; i < fields.Count; i++)
            {
                var fieldName = fields[i].Name;
                var value = reader.GetValue(i);
                feature.SetValue(fieldName, value);
            }

            layer.AddFeature(feature);
        }

        return layer;
    }

    private OguLayer ReadGeoJson(string path, string? attributeFilter, string? spatialFilterWkt)
    {
        var geoJsonReader = new GeoJsonReader();
        var featureCollection = geoJsonReader.Read<FeatureCollection>(File.ReadAllText(path));

        var layer = new OguLayer { Name = Path.GetFileNameWithoutExtension(path), GeometryType = GeometryType.UNKNOWN };

        if (featureCollection == null || featureCollection.Count == 0)
            return layer;

        // 从第一个要素推断几何类型
        var firstGeometry = featureCollection.FirstOrDefault()?.Geometry;
        if (firstGeometry != null) layer.GeometryType = MapNtsGeometryType(firstGeometry.GeometryType);

        // 从第一个要素推断字段
        var firstFeature = featureCollection.FirstOrDefault();
        if (firstFeature?.Attributes != null)
            foreach (var attrName in firstFeature.Attributes.GetNames())
            {
                var value = firstFeature.Attributes[attrName];
                var field = new OguField { Name = attrName, DataType = InferFieldDataType(value) };
                layer.AddField(field);
            }

        // 读取所有要素
        int fid = 1;
        foreach (var ntsFeature in featureCollection)
        {
            if (ntsFeature.Geometry == null) continue;

            var feature = new OguFeature { Fid = fid++, Wkt = ntsFeature.Geometry.AsText() };

            // 读取属性
            if (ntsFeature.Attributes != null)
                foreach (var attrName in ntsFeature.Attributes.GetNames())
                    feature.SetValue(attrName, ntsFeature.Attributes[attrName]);

            layer.AddFeature(feature);
        }

        return layer;
    }

    private GeometryType GetGeometryType(ShapeGeometryType shapeType)
    {
        return shapeType switch
        {
            ShapeGeometryType.Point or ShapeGeometryType.PointM or ShapeGeometryType.PointZ => GeometryType.POINT,
            ShapeGeometryType.LineString or ShapeGeometryType.LineStringM or ShapeGeometryType.LineStringZ =>
                GeometryType.LINESTRING,
            ShapeGeometryType.Polygon or ShapeGeometryType.PolygonM or ShapeGeometryType.PolygonZ => GeometryType
                .POLYGON,
            ShapeGeometryType.MultiPoint or ShapeGeometryType.MultiPointM or ShapeGeometryType.MultiPointZ =>
                GeometryType.MULTIPOINT,
            _ => GeometryType.UNKNOWN
        };
    }

    private GeometryType MapNtsGeometryType(string geometryType)
    {
        return geometryType.ToUpperInvariant() switch
        {
            "POINT" => GeometryType.POINT,
            "LINESTRING" => GeometryType.LINESTRING,
            "POLYGON" => GeometryType.POLYGON,
            "MULTIPOINT" => GeometryType.MULTIPOINT,
            "MULTILINESTRING" => GeometryType.MULTILINESTRING,
            "MULTIPOLYGON" => GeometryType.MULTIPOLYGON,
            "GEOMETRYCOLLECTION" => GeometryType.GEOMETRYCOLLECTION,
            _ => GeometryType.UNKNOWN
        };
    }

    private FieldDataType MapDbaseFieldType(char dbaseType)
    {
        return dbaseType switch
        {
            'C' => FieldDataType.STRING,
            'N' or 'F' => FieldDataType.DOUBLE,
            'L' => FieldDataType.BOOLEAN,
            'D' => FieldDataType.DATE,
            _ => FieldDataType.STRING
        };
    }

    private FieldDataType InferFieldDataType(object? value)
    {
        if (value == null) return FieldDataType.STRING;

        return value switch
        {
            int => FieldDataType.INTEGER,
            long => FieldDataType.LONG,
            double or float or decimal => FieldDataType.DOUBLE,
            bool => FieldDataType.BOOLEAN,
            DateTime => FieldDataType.DATETIME,
            _ => FieldDataType.STRING
        };
    }
}
