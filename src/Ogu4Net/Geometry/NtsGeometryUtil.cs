using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Densify;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Operation.Valid;
using NetTopologySuite.Simplify;
using Ogu4Net.Enums;
using Ogu4Net.Model;

namespace Ogu4Net.Geometry
{
    /// <summary>
    /// NTS几何处理工具类
    /// <para>
    /// 提供基于NetTopologySuite的几何属性查询、空间关系判断、空间分析和几何处理功能。
    /// 所有方法均为静态方法，无需实例化即可使用。
    /// </para>
    /// </summary>
    public static class NtsGeometryUtil
    {
        private static readonly GeometryFactory GeometryFactory = new GeometryFactory();

        // ==================== 几何属性方法 ====================

        /// <summary>
        /// 判断几何是否为空
        /// </summary>
        public static bool IsEmpty(NetTopologySuite.Geometries.Geometry geom)
        {
            return geom.IsEmpty;
        }

        /// <summary>
        /// 获取几何长度
        /// </summary>
        public static double Length(NetTopologySuite.Geometries.Geometry geom)
        {
            return geom.Length;
        }

        /// <summary>
        /// 获取几何面积
        /// </summary>
        public static double Area(NetTopologySuite.Geometries.Geometry geom)
        {
            return geom.Area;
        }

        /// <summary>
        /// 获取几何中心点
        /// </summary>
        public static Point Centroid(NetTopologySuite.Geometries.Geometry geom)
        {
            return geom.Centroid;
        }

        /// <summary>
        /// 获取几何内部中心点
        /// </summary>
        public static Point InteriorPoint(NetTopologySuite.Geometries.Geometry geom)
        {
            return geom.InteriorPoint;
        }

        /// <summary>
        /// 获取几何维度，点为0，线为1，面为2
        /// </summary>
        public static int Dimension(NetTopologySuite.Geometries.Geometry geom)
        {
            return (int)geom.Dimension;
        }

        /// <summary>
        /// 获取节点个数
        /// </summary>
        public static int NumPoints(NetTopologySuite.Geometries.Geometry geom)
        {
            return geom.NumPoints;
        }

        /// <summary>
        /// 获取几何类型
        /// </summary>
        public static GeometryType? GetGeometryType(NetTopologySuite.Geometries.Geometry geom)
        {
            return GeometryTypeExtensions.FromTypeName(geom.GeometryType);
        }

        /// <summary>
        /// 获取几何边界
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry Boundary(NetTopologySuite.Geometries.Geometry geom)
        {
            return geom.Boundary;
        }

        /// <summary>
        /// 获取几何外包矩形
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry Envelope(NetTopologySuite.Geometries.Geometry geom)
        {
            return geom.Envelope;
        }

        // ==================== 空间关系判断方法 ====================

        /// <summary>
        /// 判断几何是否相交
        /// </summary>
        public static bool Intersects(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.Intersects(b);
        }

        /// <summary>
        /// 判断几何是否相离，一个公共点都没有
        /// </summary>
        public static bool Disjoint(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.Disjoint(b);
        }

        /// <summary>
        /// 判断几何是否接触，有公共点但是没有公共区域
        /// </summary>
        public static bool Touches(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.Touches(b);
        }

        /// <summary>
        /// 判断几何是否交叉，有公共区域但是不包含
        /// </summary>
        public static bool Crosses(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.Crosses(b);
        }

        /// <summary>
        /// 判断几何A是否包含几何B
        /// </summary>
        public static bool Contains(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.Contains(b);
        }

        /// <summary>
        /// 判断几何A是否在几何B内部
        /// </summary>
        public static bool Within(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.Within(b);
        }

        /// <summary>
        /// 判断几何是否重叠，有公共区域且包含
        /// </summary>
        public static bool Overlaps(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.Overlaps(b);
        }

        /// <summary>
        /// 判断几何是否符合给定关系
        /// </summary>
        /// <param name="a">几何A</param>
        /// <param name="b">几何B</param>
        /// <param name="pattern">给定关系模式，例如：T*T***FF*</param>
        public static bool RelatePattern(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b, string pattern)
        {
            return a.Relate(b, pattern);
        }

        /// <summary>
        /// 获取几何关系矩阵
        /// </summary>
        public static string Relate(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.Relate(b).ToString();
        }

        // ==================== 距离计算方法 ====================

        /// <summary>
        /// 计算几何之间的距离
        /// </summary>
        public static double Distance(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.Distance(b);
        }

        /// <summary>
        /// 判断几何间最短距离是否小于给定距离
        /// </summary>
        public static bool IsWithinDistance(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b, double distance)
        {
            return a.IsWithinDistance(b, distance);
        }

        // ==================== 拓扑验证方法 ====================

        /// <summary>
        /// 判断几何拓扑是否合法
        /// </summary>
        public static TopologyValidationResult IsValid(NetTopologySuite.Geometries.Geometry geom)
        {
            var isValidOp = new IsValidOp(geom);
            if (!isValidOp.IsValid)
            {
                var error = isValidOp.ValidationError;
                var errorType = TopologyValidationErrorTypeExtensions.FromErrorType((int)error.ErrorType);
                string msg = errorType?.GetDescription() ?? "未知拓扑错误";
                return new TopologyValidationResult(false, error.Coordinate, errorType, msg);
            }

            return new TopologyValidationResult(true);
        }

        /// <summary>
        /// 判断几何是否简单几何
        /// </summary>
        public static SimpleGeometryResult CheckIsSimple(NetTopologySuite.Geometries.Geometry geom)
        {
            var isSimpleOp = new NetTopologySuite.Operation.Valid.IsSimpleOp(geom);
            bool isSimple = isSimpleOp.IsSimple();
            if (!isSimple)
            {
                var nonSimpleLocation = isSimpleOp.NonSimpleLocation;
                var nonSimplePoints = nonSimpleLocation != null
                    ? new List<Coordinate> { nonSimpleLocation }
                    : null;
                return new SimpleGeometryResult(false, nonSimplePoints);
            }

            return new SimpleGeometryResult(true);
        }

        // ==================== 几何相等判断方法 ====================

        /// <summary>
        /// 判断几何是否对象结构相等，必须有相同的节点和相同的节点顺序
        /// </summary>
        public static bool EqualsExact(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.EqualsExact(b);
        }

        /// <summary>
        /// 按照给定容差判断几何是否对象结构相等
        /// </summary>
        public static bool EqualsExactTolerance(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b, double tolerance)
        {
            return a.EqualsExact(b, tolerance);
        }

        /// <summary>
        /// 判断几何是否对象结构相等，不判断节点顺序
        /// </summary>
        public static bool EqualsNorm(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.EqualsNormalized(b);
        }

        /// <summary>
        /// 判断几何是否拓扑相等
        /// </summary>
        public static bool EqualsTopo(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.EqualsTopologically(b);
        }

        // ==================== 空间分析方法 ====================

        /// <summary>
        /// 获取几何缓冲区
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry Buffer(NetTopologySuite.Geometries.Geometry geom, double distance)
        {
            return geom.Buffer(distance);
        }

        /// <summary>
        /// 获取几何凸包
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry ConvexHull(NetTopologySuite.Geometries.Geometry geom)
        {
            return geom.ConvexHull();
        }

        /// <summary>
        /// 获取几何交集
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry Intersection(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.Intersection(b);
        }

        /// <summary>
        /// 获取几何并集
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry Union(params NetTopologySuite.Geometries.Geometry[] geoms)
        {
            NetTopologySuite.Geometries.Geometry? result = null;
            foreach (var g in geoms)
            {
                if (result == null)
                    result = g;
                else
                    result = result.Union(g);
            }
            return result ?? GeometryFactory.CreateEmpty(NetTopologySuite.Geometries.Dimension.Point);
        }

        /// <summary>
        /// 获取A与B并集擦除B的部分
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry Difference(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.Difference(b);
        }

        /// <summary>
        /// 获取A与B并集减去A与B交集
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry SymDifference(NetTopologySuite.Geometries.Geometry a, NetTopologySuite.Geometries.Geometry b)
        {
            return a.SymmetricDifference(b);
        }

        // ==================== 几何处理方法 ====================

        /// <summary>
        /// 简化几何
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry Simplify(NetTopologySuite.Geometries.Geometry geom, double distanceTolerance)
        {
            return DouglasPeuckerSimplifier.Simplify(geom, distanceTolerance);
        }

        /// <summary>
        /// 增加几何节点密度
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry Densify(NetTopologySuite.Geometries.Geometry geom, double distanceTolerance)
        {
            return Densifier.Densify(geom, distanceTolerance);
        }

        /// <summary>
        /// 获取/创建给定几何体的有效版本
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry Validate(NetTopologySuite.Geometries.Geometry geom)
        {
            if (geom is Polygon polygon)
            {
                if (geom.IsValid)
                {
                    geom.Normalize();
                    return geom;
                }
                var polygonizer = new Polygonizer();
                AddPolygon(polygon, polygonizer);
                return ToPolygonGeometry(polygonizer.GetPolygons());
            }
            else if (geom is MultiPolygon multiPolygon)
            {
                if (geom.IsValid)
                {
                    geom.Normalize();
                    return geom;
                }
                var polygonizer = new Polygonizer();
                for (int n = geom.NumGeometries; n-- > 0;)
                {
                    AddPolygon((Polygon)geom.GetGeometryN(n), polygonizer);
                }
                return ToPolygonGeometry(polygonizer.GetPolygons());
            }
            return geom;
        }

        /// <summary>
        /// 多边形化几何
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry Polygonize(NetTopologySuite.Geometries.Geometry geom)
        {
            var lines = LineStringExtracter.GetLines(geom);
            var polygonizer = new Polygonizer();
            polygonizer.Add(lines.Cast<NetTopologySuite.Geometries.Geometry>().ToArray());
            var polys = polygonizer.GetPolygons();
            var polyArray = polys.Cast<Polygon>().ToArray();
            return geom.Factory.CreateGeometryCollection(polyArray);
        }

        /// <summary>
        /// 按照给定线切割多边形
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry SplitPolygon(NetTopologySuite.Geometries.Geometry polygon, LineString line)
        {
            var nodedLinework = polygon.Boundary.Union(line);
            var polys = Polygonize(nodedLinework);

            var output = new List<Polygon>();
            for (int i = 0; i < polys.NumGeometries; i++)
            {
                var candpoly = (Polygon)polys.GetGeometryN(i);
                if (polygon.Contains(candpoly.InteriorPoint))
                {
                    output.Add(candpoly);
                }
            }
            return polygon.Factory.CreateGeometryCollection(output.ToArray());
        }

        // ==================== 线和点操作方法 ====================

        /// <summary>
        /// 获取几何个数
        /// </summary>
        public static int NumGeometries(NetTopologySuite.Geometries.Geometry collection)
        {
            return collection.NumGeometries;
        }

        /// <summary>
        /// 按照索引获取几何
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry GetGeometryN(GeometryCollection collection, int index)
        {
            return collection.GetGeometryN(index);
        }

        /// <summary>
        /// 获取点的X坐标
        /// </summary>
        public static double GetX(Point point)
        {
            return point.X;
        }

        /// <summary>
        /// 获取点的Y坐标
        /// </summary>
        public static double GetY(Point point)
        {
            return point.Y;
        }

        /// <summary>
        /// 判断线是否闭合
        /// </summary>
        public static bool IsClosed(LineString line)
        {
            return line.IsClosed;
        }

        /// <summary>
        /// 按照索引获取线的节点
        /// </summary>
        public static Point PointN(LineString line, int index)
        {
            return line.GetPointN(index);
        }

        /// <summary>
        /// 获取线的起点
        /// </summary>
        public static Point StartPoint(LineString line)
        {
            return line.StartPoint;
        }

        /// <summary>
        /// 获取线的终点
        /// </summary>
        public static Point EndPoint(LineString line)
        {
            return line.EndPoint;
        }

        /// <summary>
        /// 判断线是否为环
        /// </summary>
        public static bool IsRing(LineString line)
        {
            return line.IsRing;
        }

        /// <summary>
        /// 获取面的外环
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry ExteriorRing(Polygon polygon)
        {
            return polygon.ExteriorRing;
        }

        /// <summary>
        /// 获取面的内环个数
        /// </summary>
        public static int NumInteriorRing(Polygon polygon)
        {
            return polygon.NumInteriorRings;
        }

        /// <summary>
        /// 按照索引获取面的内环
        /// </summary>
        public static NetTopologySuite.Geometries.Geometry InteriorRingN(Polygon polygon, int index)
        {
            return polygon.GetInteriorRingN(index);
        }

        // ==================== 私有辅助方法 ====================

        private static void AddPolygon(Polygon polygon, Polygonizer polygonizer)
        {
            AddLineString(polygon.ExteriorRing, polygonizer);
            for (int n = polygon.NumInteriorRings; n-- > 0;)
            {
                AddLineString(polygon.GetInteriorRingN(n), polygonizer);
            }
        }

        private static void AddLineString(NetTopologySuite.Geometries.Geometry lineString, Polygonizer polygonizer)
        {
            if (lineString is LinearRing ring)
            {
                lineString = ring.Factory.CreateLineString(ring.CoordinateSequence);
            }

            if (lineString is LineString ls)
            {
                var point = lineString.Factory.CreatePoint(ls.GetCoordinateN(0));
                var toAdd = lineString.Union(point);
                polygonizer.Add(toAdd);
            }
        }

        private static NetTopologySuite.Geometries.Geometry ToPolygonGeometry(IEnumerable<NetTopologySuite.Geometries.Geometry> polygons)
        {
            var polyList = polygons.ToList();
            switch (polyList.Count)
            {
                case 0:
                    return GeometryFactory.CreateEmpty(NetTopologySuite.Geometries.Dimension.Surface);
                case 1:
                    return polyList[0];
                default:
                    NetTopologySuite.Geometries.Geometry? result = null;
                    foreach (var poly in polyList)
                    {
                        if (result == null)
                            result = poly;
                        else
                            result = result.SymmetricDifference(poly);
                    }
                    return result ?? GeometryFactory.CreateEmpty(NetTopologySuite.Geometries.Dimension.Surface);
            }
        }
    }
}
