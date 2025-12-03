using System;

namespace OpenGIS.Utils.Engine.Util;

/// <summary>
///     GeoTools 工具类（已废弃，请使用 GDAL）
/// </summary>
[Obsolete("GeoTools (NetTopologySuite) has been removed. Use GDAL/OGR instead.")]
public static class GeotoolsUtil
{
    /// <summary>
    ///     初始化 GeoTools 环境
    /// </summary>
    [Obsolete("GeoTools (NetTopologySuite) has been removed. Use GdalConfiguration instead.")]
    public static void Initialize()
    {
        // This method is obsolete, use GdalConfiguration.ConfigureGdal() instead
    }

    /// <summary>
    ///     获取版本信息
    /// </summary>
    [Obsolete("GeoTools (NetTopologySuite) has been removed. Use GdalConfiguration.GetGdalVersion() instead.")]
    public static string GetVersion()
    {
        return "N/A - NetTopologySuite has been removed";
    }
}
