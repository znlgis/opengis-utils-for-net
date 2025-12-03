using System;
using OpenGIS.Utils.Configuration;
using OSGeo.OSR;
using OgrGeometry = OSGeo.OGR.Geometry;
using SysException = System.Exception;

namespace OpenGIS.Utils.Engine.Util;

/// <summary>
///     坐标参考系统工具类
/// </summary>
public static class CrsUtil
{
    /// <summary>
    ///     坐标转换（WKT）
    /// </summary>
    /// <param name="wkt">WKT 格式的几何字符串</param>
    /// <param name="sourceWkid">源坐标系 WKID</param>
    /// <param name="targetWkid">目标坐标系 WKID</param>
    /// <returns>转换后的 WKT 字符串</returns>
    /// <exception cref="ArgumentException">当 WKT 为空或无效时抛出</exception>
    /// <exception cref="SysException">当坐标转换失败时抛出</exception>
    /// <example>
    ///     <code>
    /// // WGS84 (4326) 转 CGCS2000 (4490)
    /// var wkt = "POINT (116.404 39.915)";
    /// var transformed = CrsUtil.Transform(wkt, 4326, 4490);
    /// </code>
    /// </example>
    public static string Transform(string wkt, int sourceWkid, int targetWkid)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            throw new ArgumentException("WKT cannot be null or empty", nameof(wkt));

        if (sourceWkid == targetWkid)
            return wkt;

        // 确保 GDAL 已初始化
        GdalConfiguration.ConfigureGdal();

        // 使用 OGR 进行坐标转换
        using var geometry = OgrGeometry.CreateFromWkt(wkt);
        if (geometry == null)
            throw new ArgumentException("Invalid WKT", nameof(wkt));

        var sourceSrs = new SpatialReference(null);
        sourceSrs.ImportFromEPSG(sourceWkid);

        var targetSrs = new SpatialReference(null);
        targetSrs.ImportFromEPSG(targetWkid);

        var transform = new CoordinateTransformation(sourceSrs, targetSrs);

        if (geometry.Transform(transform) != 0)
            throw new SysException("Coordinate transformation failed");

        geometry.ExportToWkt(out string transformedWkt);

        sourceSrs.Dispose();
        targetSrs.Dispose();
        transform.Dispose();

        return transformedWkt;
    }

    /// <summary>
    ///     坐标转换（Geometry）
    /// </summary>
    /// <param name="geometry">几何对象</param>
    /// <param name="sourceWkid">源坐标系 WKID</param>
    /// <param name="targetWkid">目标坐标系 WKID</param>
    /// <returns>转换后的几何对象</returns>
    /// <exception cref="ArgumentNullException">当几何对象为 null 时抛出</exception>
    public static OgrGeometry Transform(OgrGeometry geometry, int sourceWkid, int targetWkid)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        if (sourceWkid == targetWkid)
            return geometry;

        // 将 Geometry 转为 WKT，进行转换，再转回来
        geometry.ExportToWkt(out string wkt);
        var transformedWkt = Transform(wkt, sourceWkid, targetWkid);

        return OgrGeometry.CreateFromWkt(transformedWkt);
    }

    /// <summary>
    ///     获取带号
    /// </summary>
    /// <param name="geometry">几何对象</param>
    /// <returns>3度带带号</returns>
    /// <exception cref="ArgumentNullException">当几何对象为 null 时抛出</exception>
    /// <exception cref="ArgumentException">当无法计算质心时抛出</exception>
    /// <remarks>根据几何对象的质心经度计算 3度带带号</remarks>
    public static int GetDh(OgrGeometry geometry)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        var centroid = geometry.Centroid();
        if (centroid == null || centroid.IsEmpty())
            throw new ArgumentException("Failed to calculate centroid", nameof(geometry));

        double longitude = centroid.GetX(0);
        centroid.Dispose();
        return GetDh(longitude);
    }

    /// <summary>
    ///     根据经度获取带号（3度带）
    /// </summary>
    /// <param name="longitude">经度值</param>
    /// <returns>3度带带号</returns>
    public static int GetDh(double longitude)
    {
        return (int)Math.Floor((longitude + 1.5) / 3.0);
    }

    /// <summary>
    ///     根据经度获取带号（6度带）
    /// </summary>
    /// <param name="longitude">经度值</param>
    /// <returns>6度带带号</returns>
    public static int GetDh6(double longitude)
    {
        return (int)Math.Floor(longitude / 6.0) + 1;
    }

    /// <summary>
    ///     从投影坐标系 WKID 获取带号
    /// </summary>
    /// <param name="projectedWkid">投影坐标系 WKID</param>
    /// <returns>带号</returns>
    /// <exception cref="ArgumentException">当无法从 WKID 确定带号时抛出</exception>
    /// <remarks>支持 CGCS2000 3度带和6度带</remarks>
    public static int GetDhFromWkid(int projectedWkid)
    {
        // CGCS2000 3度带: 4491-4554 (带号 24-45)
        if (projectedWkid >= 4491 && projectedWkid <= 4554) return projectedWkid - 4467;

        // CGCS2000 6度带: 4513-4533 (带号 13-23)
        if (projectedWkid >= 4513 && projectedWkid <= 4533) return projectedWkid - 4500;

        throw new ArgumentException($"Cannot determine zone number from WKID {projectedWkid}", nameof(projectedWkid));
    }

    /// <summary>
    ///     根据带号获取投影坐标系 WKID（3度带）
    /// </summary>
    /// <param name="zoneNumber">带号（24-45）</param>
    /// <returns>CGCS2000 3度带投影坐标系 WKID</returns>
    /// <exception cref="ArgumentException">当带号不在有效范围时抛出</exception>
    public static int GetProjectedWkid(int zoneNumber)
    {
        // CGCS2000 3度带
        if (zoneNumber >= 24 && zoneNumber <= 45) return 4467 + zoneNumber;

        throw new ArgumentException($"Invalid zone number {zoneNumber} for 3-degree zone", nameof(zoneNumber));
    }

    /// <summary>
    ///     根据带号获取投影坐标系 WKID（6度带）
    /// </summary>
    /// <param name="zoneNumber">带号（13-23）</param>
    /// <returns>CGCS2000 6度带投影坐标系 WKID</returns>
    /// <exception cref="ArgumentException">当带号不在有效范围时抛出</exception>
    public static int GetProjectedWkid6(int zoneNumber)
    {
        // CGCS2000 6度带
        if (zoneNumber >= 13 && zoneNumber <= 23) return 4500 + zoneNumber;

        throw new ArgumentException($"Invalid zone number {zoneNumber} for 6-degree zone", nameof(zoneNumber));
    }

    /// <summary>
    ///     获取容差
    /// </summary>
    /// <param name="wkid">坐标系 WKID</param>
    /// <returns>推荐的容差值</returns>
    /// <remarks>地理坐标系（如 WGS84、CGCS2000）使用较小容差，投影坐标系使用默认容差</remarks>
    public static double GetTolerance(int wkid)
    {
        // 地理坐标系使用较小容差
        if (wkid == 4326 || wkid == 4490)
            return 0.0000001;

        // 投影坐标系使用默认容差
        return LibrarySettings.DefaultTolerance;
    }

    /// <summary>
    ///     判断是否为投影坐标系
    /// </summary>
    /// <param name="wkid">坐标系 WKID</param>
    /// <returns>如果是投影坐标系返回 true，否则返回 false</returns>
    /// <remarks>支持识别 CGCS2000 3/6度带、WGS84 UTM、常见地理坐标系</remarks>
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
