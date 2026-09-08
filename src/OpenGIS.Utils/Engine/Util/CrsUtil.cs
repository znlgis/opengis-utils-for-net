using System;
using System.Collections.Generic;
using OpenGIS.Utils.Configuration;
using OSGeo.OSR;
using OgrGeometry = OSGeo.OGR.Geometry;
using SysException = System.Exception;

namespace OpenGIS.Utils.Engine.Util;

/// <summary>
///     坐标参考系统的基本元数据。
/// </summary>
public sealed class CrsInfo
{
    internal CrsInfo(int wkid, string name, string? authorityName, string? authorityCode,
        bool isGeographic, bool isProjected, bool isGeocentric)
    {
        Wkid = wkid;
        Name = name;
        AuthorityName = authorityName;
        AuthorityCode = authorityCode;
        IsGeographic = isGeographic;
        IsProjected = isProjected;
        IsGeocentric = isGeocentric;
    }

    public int Wkid { get; }
    public string Name { get; }
    public string? AuthorityName { get; }
    public string? AuthorityCode { get; }
    public bool IsGeographic { get; }
    public bool IsProjected { get; }
    public bool IsGeocentric { get; }
}

/// <summary>
///     坐标转换路径建议。
/// </summary>
public sealed class TransformRecommendation
{
    internal TransformRecommendation(bool requiresExplicitPath, string message, int[] intermediateWkids)
    {
        RequiresExplicitPath = requiresExplicitPath;
        Message = message;
        IntermediateWkids = intermediateWkids;
    }

    public bool RequiresExplicitPath { get; }
    public string Message { get; }
    public IReadOnlyList<int> IntermediateWkids { get; }
}

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
    /// <exception cref="ArgumentException">当 WKT 为空、无效或 source/target WKID 无效时抛出</exception>
    /// <exception cref="SysException">当坐标转换或转换结果导出失败时抛出</exception>
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

        if (sourceWkid == targetWkid && IsValidWkid(sourceWkid))
            return wkt;

        // 确保 GDAL 已初始化
        GdalConfiguration.ConfigureGdal();

        // 使用 OGR 进行坐标转换
        using var geometry = OgrGeometry.CreateFromWkt(wkt);
        if (geometry == null)
            throw new ArgumentException("Invalid WKT", nameof(wkt));

        using var sourceSrs = GetSpatialReference(sourceWkid, "sourceWkid");
        using var targetSrs = GetSpatialReference(targetWkid, "targetWkid");

        using var transform = new CoordinateTransformation(sourceSrs, targetSrs);

        if (geometry.Transform(transform) != 0)
            throw new SysException("Coordinate transformation failed");

        if (geometry.ExportToWkt(out string transformedWkt) != 0 || string.IsNullOrWhiteSpace(transformedWkt))
            throw new SysException("Failed to export transformed geometry to WKT");

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
    /// <exception cref="ArgumentException">当几何对象为空时抛出</exception>
    /// <exception cref="SysException">当坐标转换或转换结果无法创建时抛出</exception>
    /// <remarks>
    ///     当 source 和 target WKID 相同时返回输入对象本身；发生实际转换时返回新的 OGR 几何对象。
    ///     调用方负责释放返回的 OGR 几何对象，并在 source 与 target 相同时负责管理传入对象的生命周期。
    /// </remarks>
    public static OgrGeometry Transform(OgrGeometry geometry, int sourceWkid, int targetWkid)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        if (geometry.IsEmpty())
            throw new ArgumentException("Geometry cannot be empty", nameof(geometry));

        if (sourceWkid == targetWkid)
            return geometry;

        // 将 Geometry 转为 WKT，进行转换，再转回来
        if (geometry.ExportToWkt(out string wkt) != 0 || string.IsNullOrWhiteSpace(wkt))
            throw new SysException("Failed to export geometry to WKT");
        var transformedWkt = Transform(wkt, sourceWkid, targetWkid);

        var transformedGeometry = OgrGeometry.CreateFromWkt(transformedWkt);
        if (transformedGeometry == null)
            throw new SysException("Failed to create transformed geometry");

        return transformedGeometry;
    }

    /// <summary>
    ///     按指定中间坐标系执行坐标转换。
    /// </summary>
    public static string TransformThrough(string wkt, int sourceWkid, int intermediateWkid, int targetWkid)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            throw new ArgumentException("WKT cannot be null or empty", nameof(wkt));

        var intermediate = Transform(wkt, sourceWkid, intermediateWkid);
        return Transform(intermediate, intermediateWkid, targetWkid);
    }

    /// <summary>
    ///     按指定中间坐标系执行坐标转换。
    /// </summary>
    public static OgrGeometry TransformThrough(OgrGeometry geometry, int sourceWkid, int intermediateWkid,
        int targetWkid)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        if (geometry.IsEmpty())
            throw new ArgumentException("Geometry cannot be empty", nameof(geometry));

        if (sourceWkid == intermediateWkid && intermediateWkid == targetWkid)
            return geometry;

        if (geometry.ExportToWkt(out string wkt) != 0 || string.IsNullOrWhiteSpace(wkt))
            throw new SysException("Failed to export geometry to WKT");
        var transformedWkt = TransformThrough(wkt, sourceWkid, intermediateWkid, targetWkid);
        var transformedGeometry = OgrGeometry.CreateFromWkt(transformedWkt);
        if (transformedGeometry == null)
            throw new SysException("Failed to create transformed geometry");

        return transformedGeometry;
    }

    /// <summary>
    ///     判断坐标系是否为地理坐标系。
    /// </summary>
    public static bool IsGeographicCRS(int wkid)
    {
        using var spatialReference = GetSpatialReference(wkid);
        return spatialReference.IsGeographic() != 0;
    }

    /// <summary>
    ///     获取坐标系元数据。坐标系定义由 GDAL/PROJ 数据库提供。
    /// </summary>
    public static CrsInfo GetCrsInfo(int wkid)
    {
        using var spatialReference = GetSpatialReference(wkid);
        var authorityName = spatialReference.GetAuthorityName(null);
        var authorityCode = spatialReference.GetAuthorityCode(null);
        var name = spatialReference.GetName();

        return new CrsInfo(
            wkid,
            string.IsNullOrWhiteSpace(name) ? $"EPSG:{wkid}" : name,
            authorityName,
            authorityCode,
            spatialReference.IsGeographic() != 0,
            spatialReference.IsProjected() != 0,
            spatialReference.IsGeocentric() != 0);
    }

    /// <summary>
    ///     获取已知坐标转换风险的建议路径。
    /// </summary>
    public static TransformRecommendation GetTransformRecommendation(int sourceWkid, int targetWkid)
    {
        if (sourceWkid == 2326 && targetWkid == 4490)
        {
            return new TransformRecommendation(
                true,
                "HK1980 Grid to CGCS2000 should be transformed through EPSG:4326 to avoid the low-accuracy default pipeline.",
                new[] { 4326 });
        }

        return new TransformRecommendation(
            false,
            "No explicit intermediate coordinate system is currently recommended.",
            Array.Empty<int>());
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

        using var centroid = geometry.Centroid();
        if (centroid == null || centroid.IsEmpty())
            throw new ArgumentException("Failed to calculate centroid", nameof(geometry));

        double longitude = centroid.GetX(0);
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
        try
        {
            // 地理坐标系使用角度容差；投影坐标系使用线性容差。
            if (IsGeographicCRS(wkid))
                return 0.0000001;
        }
        catch (ArgumentException)
        {
            // 保留旧行为：无法识别的 WKID 使用默认容差。
        }

        // 投影坐标系使用默认容差
        return LibrarySettings.DefaultTolerance;
    }

    /// <summary>
    ///     判断是否为投影坐标系
    /// </summary>
    /// <param name="wkid">坐标系 WKID</param>
    /// <returns>如果是投影坐标系返回 true，否则返回 false</returns>
    /// <remarks>通过 GDAL/PROJ 的坐标系定义判断，不依赖特定国家或地区的 WKID 范围。</remarks>
    public static bool IsProjectedCRS(int wkid)
    {
        try
        {
            using var spatialReference = GetSpatialReference(wkid);
            return spatialReference.IsProjected() != 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static SpatialReference GetSpatialReference(int wkid, string parameterName = "wkid")
    {
        GdalConfiguration.ConfigureGdal();

        var spatialReference = new SpatialReference(null);
        try
        {
            var result = spatialReference.ImportFromEPSG(wkid);
            if (result != 0)
                throw new ArgumentException($"Invalid WKID: {wkid}", parameterName);
        }
        catch (SysException ex)
        {
            spatialReference.Dispose();
            throw new ArgumentException($"Invalid WKID: {wkid}", parameterName, ex);
        }

        return spatialReference;
    }

    private static bool IsValidWkid(int wkid)
    {
        using var spatialReference = GetSpatialReference(wkid);
        return true;
    }
}
