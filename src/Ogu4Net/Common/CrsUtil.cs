using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using Ogu4Net.Geometry;
using Ogu4Net.Model.Layer;
using ProjNet;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Ogu4Net.Common
{
    /// <summary>
    /// 坐标参考系（CRS）工具类
    /// <para>
    /// 提供坐标参考系的获取、转换、判断和几何对象的坐标转换功能。
    /// 支持EPSG:4490-4554范围内的坐标系（中国2000国家大地坐标系及其投影）。
    /// 所有方法均为静态方法，无需实例化即可使用。
    /// </para>
    /// </summary>
    public static class CrsUtil
    {
        private static readonly Dictionary<int, CoordinateSystem> SupportedCrsList = new Dictionary<int, CoordinateSystem>();
        private static readonly CoordinateSystemFactory CsFactory = new CoordinateSystemFactory();
        private static readonly CoordinateTransformationFactory CtFactory = new CoordinateTransformationFactory();

        /// <summary>
        /// CGCS2000地理坐标系WKT
        /// </summary>
        private static readonly string Cgcs2000Wkt = @"GEOGCS[""GCS_China_Geodetic_Coordinate_System_2000"",DATUM[""D_China_2000"",SPHEROID[""CGCS2000"",6378137,298.257222101]],PRIMEM[""Greenwich"",0],UNIT[""Degree"",0.0174532925199433]]";

        /// <summary>
        /// 获取坐标系容差
        /// </summary>
        /// <param name="wkid">WKID</param>
        /// <returns>容差</returns>
        public static double GetTolerance(int wkid)
        {
            // 4490是CGCS2000地理坐标系，其他大多是投影坐标系
            if (wkid == 4490)
            {
                return 0.000000001;
            }
            return 0.0001;
        }

        /// <summary>
        /// 判断坐标系是否为投影坐标系
        /// </summary>
        /// <param name="wkid">WKID</param>
        /// <returns>是否为投影坐标系</returns>
        public static bool IsProjectedCrs(int wkid)
        {
            // 4490是地理坐标系，4491-4554是投影坐标系
            return wkid > 4490 && wkid <= 4554;
        }

        /// <summary>
        /// 根据WKT字符串获取几何所在带号
        /// </summary>
        /// <param name="wkt">WKT格式字符串</param>
        /// <returns>所在带号</returns>
        public static int GetZoneNumber(string wkt)
        {
            var geom = GeometryConverter.Wkt2Geometry(wkt);
            return GetZoneNumber(geom);
        }

        /// <summary>
        /// 获取几何所在带号
        /// </summary>
        /// <param name="geometry">几何对象</param>
        /// <returns>所在带号</returns>
        public static int GetZoneNumber(NetTopologySuite.Geometries.Geometry geometry)
        {
            var point = geometry.Centroid;
            int zoneNumber = 0;

            if (point.X < 180)
            {
                // 地理坐标，根据经度计算3度带号
                zoneNumber = (int)((point.X + 1.5) / 3);
            }
            else if (point.X / 10000000 > 3)
            {
                // 投影坐标，从坐标值中提取带号
                zoneNumber = (int)(point.X / 1000000);
            }

            return zoneNumber;
        }

        /// <summary>
        /// 根据投影坐标系WKID获取带号
        /// </summary>
        /// <param name="projectedWkid">投影坐标系WKID</param>
        /// <returns>所在带号</returns>
        public static int GetZoneNumberFromWkid(int projectedWkid)
        {
            return projectedWkid - 4488;
        }

        /// <summary>
        /// 获取几何WKID
        /// </summary>
        /// <param name="geometry">几何对象</param>
        /// <returns>WKID，如果是经纬度则返回4490，否则返回投影坐标系WKID</returns>
        public static int GetWkid(NetTopologySuite.Geometries.Geometry geometry)
        {
            var point = geometry.Centroid;
            if (point.X < 180)
            {
                return 4490;
            }
            return GetProjectedWkid(GetZoneNumber(geometry));
        }

        /// <summary>
        /// 根据带号获取投影坐标系WKID
        /// </summary>
        /// <param name="zoneNumber">带号</param>
        /// <returns>WKID</returns>
        public static int GetProjectedWkid(int zoneNumber)
        {
            return 4488 + zoneNumber;
        }

        /// <summary>
        /// 根据几何获取投影坐标系WKID
        /// </summary>
        /// <param name="geometry">几何对象</param>
        /// <returns>WKID</returns>
        public static int GetProjectedWkidFromGeometry(NetTopologySuite.Geometries.Geometry geometry)
        {
            return GetProjectedWkid(GetZoneNumber(geometry));
        }

        /// <summary>
        /// WKT格式字符串的坐标转换
        /// </summary>
        /// <param name="wkt">WKT格式字符串</param>
        /// <param name="sourceWkid">源坐标系WKID</param>
        /// <param name="targetWkid">目标坐标系WKID</param>
        /// <returns>转换后的WKT格式字符串，如果转换失败返回null</returns>
        public static string? Transform(string wkt, int sourceWkid, int targetWkid)
        {
            var geom = GeometryConverter.Wkt2Geometry(wkt);
            var transformed = Transform(geom, sourceWkid, targetWkid);
            return transformed?.AsText();
        }

        /// <summary>
        /// 几何对象转换坐标系
        /// </summary>
        /// <param name="geometry">几何对象</param>
        /// <param name="sourceWkid">源坐标系WKID</param>
        /// <param name="targetWkid">目标坐标系WKID</param>
        /// <returns>坐标转换后的几何对象</returns>
        public static NetTopologySuite.Geometries.Geometry? Transform(NetTopologySuite.Geometries.Geometry geometry, int sourceWkid, int targetWkid)
        {
            if (sourceWkid == targetWkid)
            {
                return geometry;
            }

            try
            {
                var sourceCrs = GetCoordinateSystem(sourceWkid);
                var targetCrs = GetCoordinateSystem(targetWkid);

                if (sourceCrs == null || targetCrs == null)
                {
                    return null;
                }

                var transformation = CtFactory.CreateFromCoordinateSystems(sourceCrs, targetCrs);
                var mathTransform = transformation.MathTransform;

                return TransformGeometry(geometry, mathTransform);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 转换坐标系
        /// </summary>
        /// <param name="oguLayer">OGU图层</param>
        /// <param name="targetWkid">目标坐标系WKID</param>
        /// <returns>转换后的OGU图层</returns>
        public static OguLayer Reproject(OguLayer oguLayer, int targetWkid)
        {
            oguLayer.Validate();

            if (oguLayer.Wkid == targetWkid)
            {
                return oguLayer;
            }

            var clonedLayer = CloneLayer(oguLayer);

            if (clonedLayer.Features != null)
            {
                foreach (var feature in clonedLayer.Features)
                {
                    if (!string.IsNullOrEmpty(feature.Geometry))
                    {
                        var geometry = GeometryConverter.Wkt2Geometry(feature.Geometry);
                        var transformed = Transform(geometry, oguLayer.Wkid!.Value, targetWkid);
                        if (transformed != null)
                        {
                            feature.Geometry = transformed.AsText();
                        }
                    }
                }
            }

            clonedLayer.Wkid = targetWkid;
            clonedLayer.Tolerance = GetTolerance(targetWkid);

            return clonedLayer;
        }

        /// <summary>
        /// 获取支持的坐标系列表
        /// </summary>
        /// <returns>支持的坐标系字典</returns>
        public static IReadOnlyDictionary<int, CoordinateSystem> GetSupportedCrsList()
        {
            // 延迟初始化常用坐标系
            if (SupportedCrsList.Count == 0)
            {
                InitializeSupportedCrs();
            }
            return SupportedCrsList;
        }

        // ==================== 私有方法 ====================

        private static void InitializeSupportedCrs()
        {
            try
            {
                // 添加CGCS2000地理坐标系
                var cgcs2000 = CsFactory.CreateFromWkt(Cgcs2000Wkt) as CoordinateSystem;
                if (cgcs2000 != null)
                {
                    SupportedCrsList[4490] = cgcs2000;
                }

                // 添加CGCS2000 3度带投影坐标系 (EPSG:4491-4554)
                for (int zone = 25; zone <= 45; zone++)
                {
                    int wkid = 4488 + zone;
                    var projectedCrs = CreateCgcs2000GaussKruger3DegreeZone(zone);
                    if (projectedCrs != null)
                    {
                        SupportedCrsList[wkid] = projectedCrs;
                    }
                }
            }
            catch
            {
                // 忽略初始化错误
            }
        }

        private static CoordinateSystem? GetCoordinateSystem(int wkid)
        {
            if (SupportedCrsList.Count == 0)
            {
                InitializeSupportedCrs();
            }

            if (SupportedCrsList.TryGetValue(wkid, out var crs))
            {
                return crs;
            }

            // 尝试动态创建
            if (wkid == 4490)
            {
                var cgcs2000 = CsFactory.CreateFromWkt(Cgcs2000Wkt) as CoordinateSystem;
                if (cgcs2000 != null)
                {
                    SupportedCrsList[wkid] = cgcs2000;
                    return cgcs2000;
                }
            }
            else if (wkid > 4490 && wkid <= 4554)
            {
                int zone = wkid - 4488;
                var projectedCrs = CreateCgcs2000GaussKruger3DegreeZone(zone);
                if (projectedCrs != null)
                {
                    SupportedCrsList[wkid] = projectedCrs;
                    return projectedCrs;
                }
            }

            return null;
        }

        private static CoordinateSystem? CreateCgcs2000GaussKruger3DegreeZone(int zone)
        {
            try
            {
                double centralMeridian = zone * 3;
                string wkt = $@"PROJCS[""CGCS2000 / 3-degree Gauss-Kruger zone {zone}"",GEOGCS[""GCS_China_Geodetic_Coordinate_System_2000"",DATUM[""D_China_2000"",SPHEROID[""CGCS2000"",6378137,298.257222101]],PRIMEM[""Greenwich"",0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Transverse_Mercator""],PARAMETER[""latitude_of_origin"",0],PARAMETER[""central_meridian"",{centralMeridian}],PARAMETER[""scale_factor"",1],PARAMETER[""false_easting"",{zone}500000],PARAMETER[""false_northing"",0],UNIT[""Meter"",1]]";
                return CsFactory.CreateFromWkt(wkt) as CoordinateSystem;
            }
            catch
            {
                return null;
            }
        }

        private static NetTopologySuite.Geometries.Geometry TransformGeometry(NetTopologySuite.Geometries.Geometry geometry, MathTransform mathTransform)
        {
            if (geometry is Point point)
            {
                var coord = TransformCoordinate(point.Coordinate, mathTransform);
                return new Point(coord);
            }
            else if (geometry is LineString lineString)
            {
                var coords = TransformCoordinates(lineString.Coordinates, mathTransform);
                return new LineString(coords);
            }
            else if (geometry is Polygon polygon)
            {
                var shell = new LinearRing(TransformCoordinates(polygon.ExteriorRing.Coordinates, mathTransform));
                var holes = new LinearRing[polygon.NumInteriorRings];
                for (int i = 0; i < polygon.NumInteriorRings; i++)
                {
                    holes[i] = new LinearRing(TransformCoordinates(polygon.GetInteriorRingN(i).Coordinates, mathTransform));
                }
                return new Polygon(shell, holes);
            }
            else if (geometry is MultiPoint multiPoint)
            {
                var points = new Point[multiPoint.NumGeometries];
                for (int i = 0; i < multiPoint.NumGeometries; i++)
                {
                    points[i] = (Point)TransformGeometry(multiPoint.GetGeometryN(i), mathTransform);
                }
                return new MultiPoint(points);
            }
            else if (geometry is MultiLineString multiLineString)
            {
                var lineStrings = new LineString[multiLineString.NumGeometries];
                for (int i = 0; i < multiLineString.NumGeometries; i++)
                {
                    lineStrings[i] = (LineString)TransformGeometry(multiLineString.GetGeometryN(i), mathTransform);
                }
                return new MultiLineString(lineStrings);
            }
            else if (geometry is MultiPolygon multiPolygon)
            {
                var polygons = new Polygon[multiPolygon.NumGeometries];
                for (int i = 0; i < multiPolygon.NumGeometries; i++)
                {
                    polygons[i] = (Polygon)TransformGeometry(multiPolygon.GetGeometryN(i), mathTransform);
                }
                return new MultiPolygon(polygons);
            }
            else if (geometry is GeometryCollection geometryCollection)
            {
                var geoms = new NetTopologySuite.Geometries.Geometry[geometryCollection.NumGeometries];
                for (int i = 0; i < geometryCollection.NumGeometries; i++)
                {
                    geoms[i] = TransformGeometry(geometryCollection.GetGeometryN(i), mathTransform);
                }
                return new GeometryCollection(geoms);
            }

            return geometry;
        }

        private static Coordinate TransformCoordinate(Coordinate coord, MathTransform mathTransform)
        {
            var xy = mathTransform.Transform(coord.X, coord.Y);
            return new Coordinate(xy.x, xy.y);
        }

        private static Coordinate[] TransformCoordinates(Coordinate[] coords, MathTransform mathTransform)
        {
            var result = new Coordinate[coords.Length];
            for (int i = 0; i < coords.Length; i++)
            {
                result[i] = TransformCoordinate(coords[i], mathTransform);
            }
            return result;
        }

        private static OguLayer CloneLayer(OguLayer source)
        {
            // 简单的深拷贝实现
            var json = source.ToJson();
            return OguLayer.FromJson(json) ?? new OguLayer();
        }
    }
}
