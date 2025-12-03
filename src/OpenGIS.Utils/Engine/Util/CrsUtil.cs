using System;
using System.Collections.Generic;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using OpenGIS.Utils.Configuration;

namespace OpenGIS.Utils.Engine.Util
{
    /// <summary>
    /// 坐标参考系统工具类
    /// </summary>
    public static class CrsUtil
    {
        /// <summary>
        /// 坐标转换（WKT）
        /// </summary>
        public static string Transform(string wkt, int sourceWkid, int targetWkid)
        {
            if (string.IsNullOrWhiteSpace(wkt))
                throw new ArgumentException("WKT cannot be null or empty", nameof(wkt));

            if (sourceWkid == targetWkid)
                return wkt;

            // TODO: Implement coordinate transformation using GDAL
            throw new NotImplementedException("CrsUtil.Transform is not yet implemented");
        }

        /// <summary>
        /// 坐标转换（Geometry）
        /// </summary>
        public static NtsGeometry Transform(NtsGeometry geometry, int sourceWkid, int targetWkid)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));

            if (sourceWkid == targetWkid)
                return geometry;

            // TODO: Implement coordinate transformation using GDAL
            throw new NotImplementedException("CrsUtil.Transform(Geometry) is not yet implemented");
        }

        /// <summary>
        /// 获取带号
        /// </summary>
        public static int GetDh(NtsGeometry geometry)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));

            var centroid = geometry.Centroid;
            return GetDh(centroid.X);
        }

        /// <summary>
        /// 根据经度获取带号（3度带）
        /// </summary>
        public static int GetDh(double longitude)
        {
            return (int)Math.Floor((longitude + 1.5) / 3.0);
        }

        /// <summary>
        /// 根据经度获取带号（6度带）
        /// </summary>
        public static int GetDh6(double longitude)
        {
            return (int)Math.Floor(longitude / 6.0) + 1;
        }

        /// <summary>
        /// 从投影坐标系 WKID 获取带号
        /// </summary>
        public static int GetDhFromWkid(int projectedWkid)
        {
            // CGCS2000 3度带: 4491-4554 (带号 24-45)
            if (projectedWkid >= 4491 && projectedWkid <= 4554)
            {
                return projectedWkid - 4467;
            }

            // CGCS2000 6度带: 4513-4533 (带号 13-23)
            if (projectedWkid >= 4513 && projectedWkid <= 4533)
            {
                return projectedWkid - 4500;
            }

            throw new ArgumentException($"Cannot determine zone number from WKID {projectedWkid}", nameof(projectedWkid));
        }

        /// <summary>
        /// 根据带号获取投影坐标系 WKID（3度带）
        /// </summary>
        public static int GetProjectedWkid(int zoneNumber)
        {
            // CGCS2000 3度带
            if (zoneNumber >= 24 && zoneNumber <= 45)
            {
                return 4467 + zoneNumber;
            }

            throw new ArgumentException($"Invalid zone number {zoneNumber} for 3-degree zone", nameof(zoneNumber));
        }

        /// <summary>
        /// 根据带号获取投影坐标系 WKID（6度带）
        /// </summary>
        public static int GetProjectedWkid6(int zoneNumber)
        {
            // CGCS2000 6度带
            if (zoneNumber >= 13 && zoneNumber <= 23)
            {
                return 4500 + zoneNumber;
            }

            throw new ArgumentException($"Invalid zone number {zoneNumber} for 6-degree zone", nameof(zoneNumber));
        }

        /// <summary>
        /// 获取容差
        /// </summary>
        public static double GetTolerance(int wkid)
        {
            // 地理坐标系使用较小容差
            if (wkid == 4326 || wkid == 4490)
                return 0.0000001;

            // 投影坐标系使用默认容差
            return LibrarySettings.DefaultTolerance;
        }

        /// <summary>
        /// 判断是否为投影坐标系
        /// </summary>
        public static bool IsProjectedCRS(int wkid)
        {
            // CGCS2000 3度带
            if (wkid >= 4491 && wkid <= 4554)
                return true;

            // CGCS2000 6度带
            if (wkid >= 4513 && wkid <= 4533)
                return true;

            // WGS84 UTM
            if (wkid >= 32601 && wkid <= 32660) // UTM North
                return true;
            if (wkid >= 32701 && wkid <= 32760) // UTM South
                return true;

            // 常见地理坐标系
            if (wkid == 4326 || wkid == 4490 || wkid == 4269)
                return false;

            // 默认假设投影坐标系
            return wkid >= 2000;
        }
    }
}
