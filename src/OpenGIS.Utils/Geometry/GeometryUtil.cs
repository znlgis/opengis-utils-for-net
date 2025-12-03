using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Densify;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Valid;
using NetTopologySuite.Simplify;
using OpenGIS.Utils.Engine.Model;
using OguGeometryType = OpenGIS.Utils.Engine.Enums.GeometryType;
using OguTopologyErrorType = OpenGIS.Utils.Engine.Enums.TopologyValidationErrorType;

namespace OpenGIS.Utils.Geometry;

/// <summary>
///     几何处理工具类
/// </summary>
public static class GeometryUtil
{
    private static readonly WKTReader _wktReader = new();
    private static readonly WKTWriter _wktWriter = new();
    private static readonly GeoJsonReader _geoJsonReader = new();
    private static readonly GeoJsonWriter _geoJsonWriter = new();

    #region Format Conversions

    /// <summary>
    ///     WKT 转 Geometry
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Wkt2Geometry(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            throw new ArgumentException("WKT cannot be null or empty", nameof(wkt));

        return _wktReader.Read(wkt);
    }

    /// <summary>
    ///     Geometry 转 WKT
    /// </summary>
    public static string Geometry2Wkt(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return _wktWriter.Write(geom);
    }

    /// <summary>
    ///     GeoJSON 转 Geometry
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Geojson2Geometry(string geojson)
    {
        if (string.IsNullOrWhiteSpace(geojson))
            throw new ArgumentException("GeoJSON cannot be null or empty", nameof(geojson));

        return _geoJsonReader.Read<NetTopologySuite.Geometries.Geometry>(geojson);
    }

    /// <summary>
    ///     Geometry 转 GeoJSON
    /// </summary>
    public static string Geometry2Geojson(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return _geoJsonWriter.Write(geom);
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
    public static bool Intersects(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null || b == null) return false;
        return a.Intersects(b);
    }

    /// <summary>
    ///     判断 a 是否包含 b
    /// </summary>
    public static bool Contains(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null || b == null) return false;
        return a.Contains(b);
    }

    /// <summary>
    ///     判断 a 是否在 b 内部
    /// </summary>
    public static bool Within(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null || b == null) return false;
        return a.Within(b);
    }

    /// <summary>
    ///     判断两个几何对象是否接触
    /// </summary>
    public static bool Touches(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null || b == null) return false;
        return a.Touches(b);
    }

    /// <summary>
    ///     判断两个几何对象是否交叉
    /// </summary>
    public static bool Crosses(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null || b == null) return false;
        return a.Crosses(b);
    }

    /// <summary>
    ///     判断两个几何对象是否重叠
    /// </summary>
    public static bool Overlaps(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null || b == null) return false;
        return a.Overlaps(b);
    }

    /// <summary>
    ///     判断两个几何对象是否不相交
    /// </summary>
    public static bool Disjoint(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null || b == null) return true;
        return a.Disjoint(b);
    }

    #endregion

    #region Spatial Analysis

    /// <summary>
    ///     缓冲区分析
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Buffer(NetTopologySuite.Geometries.Geometry geom,
        double distance)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Buffer(distance);
    }

    /// <summary>
    ///     交集
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Intersection(NetTopologySuite.Geometries.Geometry a,
        NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));

        return a.Intersection(b);
    }

    /// <summary>
    ///     并集
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Union(NetTopologySuite.Geometries.Geometry a,
        NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));

        return a.Union(b);
    }

    /// <summary>
    ///     多个几何对象合并
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Union(
        IEnumerable<NetTopologySuite.Geometries.Geometry> geometries)
    {
        if (geometries == null)
            throw new ArgumentNullException(nameof(geometries));

        List<NetTopologySuite.Geometries.Geometry> geomList = geometries.ToList();
        if (geomList.Count == 0)
            throw new ArgumentException("Geometry list cannot be empty", nameof(geometries));

        var factory = geomList[0].Factory;
        var collection = factory.CreateGeometryCollection(geomList.ToArray());
        return collection.Union();
    }

    /// <summary>
    ///     差集
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Difference(NetTopologySuite.Geometries.Geometry a,
        NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));

        return a.Difference(b);
    }

    /// <summary>
    ///     对称差集
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry SymDifference(NetTopologySuite.Geometries.Geometry a,
        NetTopologySuite.Geometries.Geometry b)
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
    public static double Area(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Area;
    }

    /// <summary>
    ///     计算长度
    /// </summary>
    public static double Length(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Length;
    }

    /// <summary>
    ///     获取质心
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Centroid(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Centroid;
    }

    /// <summary>
    ///     获取内部点
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry InteriorPoint(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.InteriorPoint;
    }

    /// <summary>
    ///     获取几何维度
    /// </summary>
    public static int Dimension(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return (int)geom.Dimension;
    }

    /// <summary>
    ///     获取点数量
    /// </summary>
    public static int NumPoints(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.NumPoints;
    }

    /// <summary>
    ///     获取几何类型
    /// </summary>
    public static OguGeometryType GetGeometryType(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.GeometryType.ToUpper() switch
        {
            "POINT" => OguGeometryType.POINT,
            "LINESTRING" => OguGeometryType.LINESTRING,
            "POLYGON" => OguGeometryType.POLYGON,
            "MULTIPOINT" => OguGeometryType.MULTIPOINT,
            "MULTILINESTRING" => OguGeometryType.MULTILINESTRING,
            "MULTIPOLYGON" => OguGeometryType.MULTIPOLYGON,
            "GEOMETRYCOLLECTION" => OguGeometryType.GEOMETRYCOLLECTION,
            _ => OguGeometryType.UNKNOWN
        };
    }

    /// <summary>
    ///     判断是否为空几何
    /// </summary>
    public static bool IsEmpty(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null) return true;
        return geom.IsEmpty;
    }

    #endregion

    #region Geometry Operations

    /// <summary>
    ///     获取边界
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Boundary(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Boundary;
    }

    /// <summary>
    ///     获取外包矩形
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Envelope(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.Envelope;
    }

    /// <summary>
    ///     凸包
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry ConvexHull(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return geom.ConvexHull();
    }

    /// <summary>
    ///     简化几何
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Simplify(NetTopologySuite.Geometries.Geometry geom,
        double tolerance)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return DouglasPeuckerSimplifier.Simplify(geom, tolerance);
    }

    /// <summary>
    ///     密化几何（增加顶点）
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry Densify(NetTopologySuite.Geometries.Geometry geom,
        double distanceTolerance)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        return Densifier.Densify(geom, distanceTolerance);
    }

    #endregion

    #region Topology Validation

    /// <summary>
    ///     验证几何有效性
    /// </summary>
    public static TopologyValidationResult IsValid(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        var result = new TopologyValidationResult { IsValid = geom.IsValid };

        if (!result.IsValid)
        {
            var validator = new IsValidOp(geom);
            var error = validator.ValidationError;

            if (error != null)
            {
                result.ErrorMessage = error.Message;
                result.ErrorLocation = error.Coordinate != null
                    ? $"POINT ({error.Coordinate.X} {error.Coordinate.Y})"
                    : null;
                result.ErrorType = MapTopologyErrorType(error.ErrorType);
            }
        }

        return result;
    }

    /// <summary>
    ///     判断是否为简单几何
    /// </summary>
    public static SimpleGeometryResult IsSimple(NetTopologySuite.Geometries.Geometry geom)
    {
        if (geom == null)
            throw new ArgumentNullException(nameof(geom));

        var result = new SimpleGeometryResult { IsSimple = geom.IsSimple };

        if (!result.IsSimple) result.Reason = "Geometry has self-intersections or other complexity";

        return result;
    }

    private static OguTopologyErrorType MapTopologyErrorType(TopologyValidationErrors errorType)
    {
        // Simplified mapping since NTS enum values may differ across versions
        var errorName = errorType.ToString();

        if (errorName.Contains("Hole") && errorName.Contains("Shell"))
            return OguTopologyErrorType.HOLE_OUTSIDE_SHELL;
        if (errorName.Contains("Nested") && errorName.Contains("Hole"))
            return OguTopologyErrorType.NESTED_HOLES;
        if (errorName.Contains("Disconnected"))
            return OguTopologyErrorType.DISCONNECTED_INTERIOR;
        if (errorName.Contains("SelfIntersection"))
            return OguTopologyErrorType.SELF_INTERSECTION;
        if (errorName.Contains("Ring") && errorName.Contains("SelfIntersection"))
            return OguTopologyErrorType.RING_SELF_INTERSECTION;
        if (errorName.Contains("Nested") && errorName.Contains("Shell"))
            return OguTopologyErrorType.NESTED_SHELLS;
        if (errorName.Contains("Duplicate") && errorName.Contains("Ring"))
            return OguTopologyErrorType.DUPLICATE_RINGS;
        if (errorName.Contains("TooFew") || errorName.Contains("Few"))
            return OguTopologyErrorType.TOO_FEW_POINTS;
        if (errorName.Contains("InvalidCoordinate"))
            return OguTopologyErrorType.INVALID_COORDINATE;
        if (errorName.Contains("Closed") || errorName.Contains("NotClosed"))
            return OguTopologyErrorType.RING_NOT_CLOSED;
        if (errorName.Contains("Repeated"))
            return OguTopologyErrorType.REPEATED_POINT;

        return OguTopologyErrorType.ERROR;
    }

    #endregion

    #region Geometry Equality

    /// <summary>
    ///     精确比较（坐标完全相同）
    /// </summary>
    public static bool EqualsExact(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.EqualsExact(b);
    }

    /// <summary>
    ///     精确比较（带容差）
    /// </summary>
    public static bool EqualsExactTolerance(NetTopologySuite.Geometries.Geometry a,
        NetTopologySuite.Geometries.Geometry b, double tolerance)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.EqualsExact(b, tolerance);
    }

    /// <summary>
    ///     拓扑相等
    /// </summary>
    public static bool EqualsTopo(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.EqualsTopologically(b);
    }

    #endregion

    #region Distance Calculations

    /// <summary>
    ///     计算两个几何对象之间的距离
    /// </summary>
    public static double Distance(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
    {
        if (a == null || b == null)
            throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));

        return a.Distance(b);
    }

    /// <summary>
    ///     判断两个几何对象之间的距离是否在指定范围内
    /// </summary>
    public static bool IsWithinDistance(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b,
        double maxDistance)
    {
        if (a == null || b == null) return false;
        return a.IsWithinDistance(b, maxDistance);
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
