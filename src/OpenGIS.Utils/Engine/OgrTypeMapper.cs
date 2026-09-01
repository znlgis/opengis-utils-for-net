using OpenGIS.Utils.Engine.Enums;
using OSGeo.OGR;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     OGR 类型与库内部枚举之间的映射工具
/// </summary>
internal static class OgrTypeMapper
{
    /// <summary>
    ///     将 OGR 字段类型映射为库内部字段类型
    /// </summary>
    public static FieldDataType MapOgrFieldType(FieldType ogrType)
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

    /// <summary>
    ///     将库内部字段类型映射为 OGR 字段类型
    /// </summary>
    public static FieldType MapToOgrFieldType(FieldDataType dataType)
    {
        return dataType switch
        {
            FieldDataType.INTEGER => FieldType.OFTInteger,
            FieldDataType.LONG => FieldType.OFTInteger64,
            FieldDataType.DOUBLE or FieldDataType.FLOAT => FieldType.OFTReal,
            FieldDataType.STRING => FieldType.OFTString,
            FieldDataType.DATE => FieldType.OFTDate,
            FieldDataType.DATETIME => FieldType.OFTDateTime,
            FieldDataType.BINARY => FieldType.OFTBinary,
            _ => FieldType.OFTString
        };
    }

    /// <summary>
    ///     将 OGR 几何类型映射为库内部几何类型
    /// </summary>
    public static GeometryType MapOgrGeometryType(wkbGeometryType geomType)
    {
        var flatType = FlattenWkbType((int)geomType);

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

    /// <summary>
    ///     将库内部几何类型映射为 OGR 几何类型
    /// </summary>
    public static wkbGeometryType MapToOgrGeometryType(GeometryType geomType)
    {
        return geomType switch
        {
            GeometryType.POINT => wkbGeometryType.wkbPoint,
            GeometryType.LINESTRING => wkbGeometryType.wkbLineString,
            GeometryType.POLYGON => wkbGeometryType.wkbPolygon,
            GeometryType.MULTIPOINT => wkbGeometryType.wkbMultiPoint,
            GeometryType.MULTILINESTRING => wkbGeometryType.wkbMultiLineString,
            GeometryType.MULTIPOLYGON => wkbGeometryType.wkbMultiPolygon,
            GeometryType.GEOMETRYCOLLECTION => wkbGeometryType.wkbGeometryCollection,
            _ => wkbGeometryType.wkbUnknown
        };
    }

    /// <summary>
    ///     去除 WKB 类型中的 Z/M/ZM 维度位，返回基础几何类型
    /// </summary>
    public static wkbGeometryType FlattenWkbType(int geomType)
    {
        // Remove 25D bit
        var flatType = (int)((uint)geomType & 0x7FFFFFFFu);
        // Handle Z (1000-1999), M (2000-2999), ZM (3000-3999) offsets
        if (flatType >= 1000 && flatType < 2000)
            return (wkbGeometryType)(flatType - 1000);
        if (flatType >= 2000 && flatType < 3000)
            return (wkbGeometryType)(flatType - 2000);
        if (flatType >= 3000 && flatType < 4000)
            return (wkbGeometryType)(flatType - 3000);
        return (wkbGeometryType)flatType;
    }
}
