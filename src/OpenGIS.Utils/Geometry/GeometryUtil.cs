using System;
using System.Collections.Generic;
using System.Linq;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.Engine.Model;
using OSGeo.OGR;
using OguGeometryType = OpenGIS.Utils.Engine.Enums.GeometryType;
using OguTopologyErrorType = OpenGIS.Utils.Engine.Enums.TopologyValidationErrorType;
using OgrGeometry = OSGeo.OGR.Geometry;

namespace OpenGIS.Utils.Geometry;

/// <summary>
///     几何处理工具类（基于 GDAL/OGR）
/// </summary>
public static class GeometryUtil
{
    static GeometryUtil()
    {
        // 确保 GDAL 已初始化
        GdalConfiguration.ConfigureGdal();
    }

    #region Format Conversions

    /// <summary>
    ///     WKT 转 Geometry
    /// </summary>
    public static OgrGeometry Wkt2Geometry(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            throw new ArgumentException("WKT cannot be null or empty", nameof(wkt));

        var geom = OgrGeometry.CreateFromWkt(wkt);
        if (geom == null)
            throw new ArgumentException("Invalid WKT format", nameof(wkt));

        return geom;
    }

    /// <summary>
    ///     Geometry 转 WKT
    /// </summary>
    public static string Geometry2Wkt(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        geom.ExportToWkt(out string wkt);
        return wkt;
    }

    /// <summary>
    ///     GeoJSON 转 Geometry
    /// </summary>
    /// <remarks>
    ///     GDAL/OGR doesn't support direct GeoJSON string parsing.
    ///     This is a breaking change from the NetTopologySuite implementation.
    ///     Users should either use WKT format or load GeoJSON from files.
    /// </remarks>
    public static OgrGeometry Geojson2Geometry(string geojson)
    {
        if (string.IsNullOrWhiteSpace(geojson))
            throw new ArgumentException("GeoJSON cannot be null or empty", nameof(geojson));

        // GDAL/OGR doesn't support direct GeoJSON string parsing
        // Users should either:
        // 1. Use WKT format with Wkt2Geometry()
        // 2. Load GeoJSON from file using GdalReader
        throw new NotSupportedException(
            "Direct GeoJSON string parsing is not supported by GDAL/OGR. " +
            "Please use Wkt2Geometry() for WKT format, or load GeoJSON from a file using GdalReader.");
    }

    /// <summary>
    ///     Geometry 转 GeoJSON
    /// </summary>
    public static string Geometry2Geojson(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.ExportToJson(null);
    }

    /// <summary>
    ///     WKT 转 GeoJSON
    /// </summary>
    public static string Wkt2Geojson(string wkt)
    {
        var geom = Wkt2Geometry(wkt);
        return Geometry2Geojson(geom);
    }

    /// <summary>
    ///     GeoJSON 转 WKT
    /// </summary>
    public static string Geojson2Wkt(string geojson)
    {
        var geom = Geojson2Geometry(geojson);
        return Geometry2Wkt(geom);
    }

    #endregion

    #region Spatial Relationships

    /// <summary>
    ///     判断两个几何对象是否相交
    /// </summary>
    public static bool Intersects(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null) return false;
        return a.Intersects(b);
    }

    /// <summary>
    ///     判断 a 是否包含 b
    /// </summary>
    public static bool Contains(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null) return false;
        return a.Contains(b);
    }

    /// <summary>
    ///     判断 a 是否在 b 内部
    /// </summary>
    public static bool Within(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null) return false;
        return a.Within(b);
    }

    /// <summary>
    ///     判断两个几何对象是否接触
    /// </summary>
    public static bool Touches(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null) return false;
        return a.Touches(b);
    }

    /// <summary>
    ///     判断两个几何对象是否交叉
    /// </summary>
    public static bool Crosses(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null) return false;
        return a.Crosses(b);
    }

    /// <summary>
    ///     判断两个几何对象是否重叠
    /// </summary>
    public static bool Overlaps(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null) return false;
        return a.Overlaps(b);
    }

    /// <summary>
    ///     判断两个几何对象是否不相交
    /// </summary>
    public static bool Disjoint(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null) return true;
        return a.Disjoint(b);
    }

    #endregion

    #region Spatial Analysis

    /// <summary>
    ///     缓冲区分析
    /// </summary>
    public static OgrGeometry Buffer(OgrGeometry geom, double distance)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Buffer(distance, 30);
    }

    /// <summary>
    ///     交集
    /// </summary>
    public static OgrGeometry Intersection(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));

        return a.Intersection(b);
    }

    /// <summary>
    ///     并集
    /// </summary>
    public static OgrGeometry Union(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));

        return a.Union(b);
    }

    /// <summary>
    ///     多个几何对象合并
    /// </summary>
    public static OgrGeometry Union(IEnumerable<OgrGeometry> geometries)
    {
        if (geometries == null)
            throw new ArgumentNullException(nameof(geometries));

        // Use as array or convert once to avoid multiple enumerations
        var geomArray = geometries as OgrGeometry[] ?? geometries.ToArray();
        
        if (geomArray.Length == 0)
            throw new ArgumentException("Geometry list cannot be empty", nameof(geometries));

        if (geomArray.Length == 1)
            return geomArray[0];

        OgrGeometry result = geomArray[0];
        for (int i = 1; i < geomArray.Length; i++)
            result = result.Union(geomArray[i]);
        
        return result;
    }

    /// <summary>
    ///     差集
    /// </summary>
    public static OgrGeometry Difference(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));

        return a.Difference(b);
    }

    /// <summary>
    ///     对称差集
    /// </summary>
    public static OgrGeometry SymDifference(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));

        return a.SymmetricDifference(b);
    }

    #endregion

    #region Geometry Properties

    /// <summary>
    ///     计算面积
    /// </summary>
    public static double Area(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Area();
    }

    /// <summary>
    ///     计算长度
    /// </summary>
    public static double Length(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Length();
    }

    /// <summary>
    ///     获取质心
    /// </summary>
    public static OgrGeometry Centroid(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Centroid();
    }

    /// <summary>
    ///     获取内部点
    /// </summary>
    public static OgrGeometry InteriorPoint(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.PointOnSurface();
    }

    /// <summary>
    ///     获取几何维度
    /// </summary>
    public static int Dimension(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.GetDimension();
    }

    /// <summary>
    ///     获取点数量
    /// </summary>
    public static int NumPoints(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.GetPointCount();
    }

    /// <summary>
    ///     获取几何类型
    /// </summary>
    public static OguGeometryType GetGeometryType(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        var geometryType = geom.GetGeometryType();
        return geometryType switch
        {
            wkbGeometryType.wkbPoint or wkbGeometryType.wkbPoint25D or wkbGeometryType.wkbPointM
                or wkbGeometryType.wkbPointZM => OguGeometryType.POINT,
            wkbGeometryType.wkbLineString or wkbGeometryType.wkbLineString25D or wkbGeometryType.wkbLineStringM
                or wkbGeometryType.wkbLineStringZM => OguGeometryType.LINESTRING,
            wkbGeometryType.wkbPolygon or wkbGeometryType.wkbPolygon25D or wkbGeometryType.wkbPolygonM
                or wkbGeometryType.wkbPolygonZM => OguGeometryType.POLYGON,
            wkbGeometryType.wkbMultiPoint or wkbGeometryType.wkbMultiPoint25D or wkbGeometryType.wkbMultiPointM
                or wkbGeometryType.wkbMultiPointZM => OguGeometryType.MULTIPOINT,
            wkbGeometryType.wkbMultiLineString or wkbGeometryType.wkbMultiLineString25D
                or wkbGeometryType.wkbMultiLineStringM
                or wkbGeometryType.wkbMultiLineStringZM => OguGeometryType.MULTILINESTRING,
            wkbGeometryType.wkbMultiPolygon or wkbGeometryType.wkbMultiPolygon25D or wkbGeometryType.wkbMultiPolygonM
                or wkbGeometryType.wkbMultiPolygonZM => OguGeometryType.MULTIPOLYGON,
            wkbGeometryType.wkbGeometryCollection or wkbGeometryType.wkbGeometryCollection25D
                or wkbGeometryType.wkbGeometryCollectionM
                or wkbGeometryType.wkbGeometryCollectionZM => OguGeometryType.GEOMETRYCOLLECTION,
            _ => OguGeometryType.UNKNOWN
        };
    }

    /// <summary>
    ///     判断是否为空几何
    /// </summary>
    public static bool IsEmpty(OgrGeometry geom)
    {
        if (geom == null) return true;
        return geom.IsEmpty();
    }

    #endregion

    #region Geometry Operations

    /// <summary>
    ///     获取边界
    /// </summary>
    public static OgrGeometry Boundary(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Boundary();
    }

    /// <summary>
    ///     获取外包矩形
    /// </summary>
    public static OgrGeometry Envelope(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        var envelope = new Envelope();
        geom.GetEnvelope(envelope);

        // Create a polygon from the envelope
        var ring = new OgrGeometry(wkbGeometryType.wkbLinearRing);
        ring.AddPoint_2D(envelope.MinX, envelope.MinY);
        ring.AddPoint_2D(envelope.MaxX, envelope.MinY);
        ring.AddPoint_2D(envelope.MaxX, envelope.MaxY);
        ring.AddPoint_2D(envelope.MinX, envelope.MaxY);
        ring.AddPoint_2D(envelope.MinX, envelope.MinY);

        var polygon = new OgrGeometry(wkbGeometryType.wkbPolygon);
        polygon.AddGeometry(ring);

        return polygon;
    }

    /// <summary>
    ///     凸包
    /// </summary>
    public static OgrGeometry ConvexHull(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.ConvexHull();
    }

    /// <summary>
    ///     简化几何
    /// </summary>
    public static OgrGeometry Simplify(OgrGeometry geom, double tolerance)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Simplify(tolerance);
    }

    /// <summary>
    ///     密化几何（增加顶点）
    /// </summary>
    public static OgrGeometry Densify(OgrGeometry geom, double distanceTolerance)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        // OGR's Segmentize modifies the geometry in place, so clone first
        var cloned = geom.Clone();
        cloned.Segmentize(distanceTolerance);
        return cloned;
    }

    #endregion

    #region Topology Validation

    /// <summary>
    ///     验证几何有效性
    /// </summary>
    /// <remarks>
    ///     Note: GDAL/OGR provides less detailed validation error information
    ///     compared to NetTopologySuite. Error types and locations are not
    ///     available in this implementation.
    /// </remarks>
    public static TopologyValidationResult IsValid(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        var result = new TopologyValidationResult { IsValid = geom.IsValid() };

        if (!result.IsValid)
        {
            result.ErrorMessage = "Geometry is not valid (detailed error information not available in GDAL)";
            result.ErrorType = OguTopologyErrorType.ERROR;
        }

        return result;
    }

    /// <summary>
    ///     判断是否为简单几何
    /// </summary>
    public static SimpleGeometryResult IsSimple(OgrGeometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        var result = new SimpleGeometryResult { IsSimple = geom.IsSimple() };

        if (!result.IsSimple) result.Reason = "Geometry has self-intersections or other complexity";

        return result;
    }

    #endregion

    #region Geometry Equality

    /// <summary>
    ///     精确比较（坐标完全相同）
    /// </summary>
    public static bool EqualsExact(OgrGeometry a, OgrGeometry b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Equals(b);
    }

    /// <summary>
    ///     精确比较（带容差）
    /// </summary>
    /// <remarks>
    ///     Note: OGR doesn't have direct tolerance-based geometry comparison.
    ///     This implementation uses distance as an approximation, which may differ
    ///     from NetTopologySuite's original behavior.
    /// </remarks>
    public static bool EqualsExactTolerance(OgrGeometry a, OgrGeometry b, double tolerance)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        // Check if geometries are within the tolerance distance
        return a.Distance(b) <= tolerance;
    }

    /// <summary>
    ///     拓扑相等
    /// </summary>
    public static bool EqualsTopo(OgrGeometry a, OgrGeometry b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Equals(b);
    }

    #endregion

    #region Distance Calculations

    /// <summary>
    ///     计算两个几何对象之间的距离
    /// </summary>
    public static double Distance(OgrGeometry a, OgrGeometry b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));

        return a.Distance(b);
    }

    /// <summary>
    ///     判断两个几何对象之间的距离是否在指定范围内
    /// </summary>
    public static bool IsWithinDistance(OgrGeometry a, OgrGeometry b, double maxDistance)
    {
        if (a == null || b == null) return false;
        return a.Distance(b) <= maxDistance;
    }

    #endregion

    #region WKT-based Methods

    /// <summary>
    ///     基于 WKT 的相交判断
    /// </summary>
    public static bool IntersectsWkt(string wktA, string wktB)
    {
        var geomA = Wkt2Geometry(wktA);
        var geomB = Wkt2Geometry(wktB);
        return Intersects(geomA, geomB);
    }

    /// <summary>
    ///     基于 WKT 的包含判断
    /// </summary>
    public static bool ContainsWkt(string wktA, string wktB)
    {
        var geomA = Wkt2Geometry(wktA);
        var geomB = Wkt2Geometry(wktB);
        return Contains(geomA, geomB);
    }

    /// <summary>
    ///     基于 WKT 的缓冲区分析
    /// </summary>
    public static string BufferWkt(string wkt, double distance)
    {
        var geom = Wkt2Geometry(wkt);
        var buffered = Buffer(geom, distance);
        return Geometry2Wkt(buffered);
    }

    /// <summary>
    ///     基于 WKT 的交集
    /// </summary>
    public static string IntersectionWkt(string wktA, string wktB)
    {
        var geomA = Wkt2Geometry(wktA);
        var geomB = Wkt2Geometry(wktB);
        var result = Intersection(geomA, geomB);
        return Geometry2Wkt(result);
    }

    /// <summary>
    ///     基于 WKT 的并集
    /// </summary>
    public static string UnionWkt(IEnumerable<string> wktList)
    {
        var geometries = wktList.Select(Wkt2Geometry);
        var result = Union(geometries);
        return Geometry2Wkt(result);
    }

    /// <summary>
    ///     基于 WKT 的面积计算
    /// </summary>
    public static double AreaWkt(string wkt)
    {
        var geom = Wkt2Geometry(wkt);
        return Area(geom);
    }

    /// <summary>
    ///     基于 WKT 的长度计算
    /// </summary>
    public static double LengthWkt(string wkt)
    {
        var geom = Wkt2Geometry(wkt);
        return Length(geom);
    }

    /// <summary>
    ///     基于 WKT 的质心计算
    /// </summary>
    public static string CentroidWkt(string wkt)
    {
        var geom = Wkt2Geometry(wkt);
        var centroid = Centroid(geom);
        return Geometry2Wkt(centroid);
    }

    /// <summary>
    ///     基于 WKT 的简化
    /// </summary>
    public static string SimplifyWkt(string wkt, double tolerance)
    {
        var geom = Wkt2Geometry(wkt);
        var simplified = Simplify(geom, tolerance);
        return Geometry2Wkt(simplified);
    }

    #endregion
}
