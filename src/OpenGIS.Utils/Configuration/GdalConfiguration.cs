using System;
using System.Collections.Generic;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;
using SysException = System.Exception;

namespace OpenGIS.Utils.Configuration;

/// <summary>
///     GDAL 配置类
/// </summary>
public static class GdalConfiguration
{
    private static bool _isConfigured;
    private static readonly object _lock = new();

    /// <summary>
    ///     静态构造函数，自动初始化
    /// </summary>
    static GdalConfiguration()
    {
        ConfigureGdal();
    }

    /// <summary>
    ///     配置 GDAL
    /// </summary>
    public static void ConfigureGdal()
    {
        lock (_lock)
        {
            if (_isConfigured)
                return;

            try
            {
                // MaxRev.Gdal.Universal 会自动配置路径
                GdalBase.ConfigureAll();

                // 注册所有驱动
                RegisterAllDrivers();

                // 设置配置选项
                SetConfigOptions();

                _isConfigured = true;
            }
            catch (SysException ex)
            {
                throw new InvalidOperationException("Failed to configure GDAL", ex);
            }
        }
    }

    /// <summary>
    ///     注册所有驱动
    /// </summary>
    public static void RegisterAllDrivers()
    {
        Gdal.AllRegister();
        Ogr.RegisterAll();
    }

    /// <summary>
    ///     设置 GDAL 配置选项
    /// </summary>
    public static void SetConfigOptions()
    {
        // 设置文件名为 UTF-8
        Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "YES");

        // 设置 Shapefile 编码为 UTF-8
        Gdal.SetConfigOption("SHAPE_ENCODING", "UTF-8");

        // 强制使用传统 GIS 坐标顺序
        Gdal.SetConfigOption("OGR_CT_FORCE_TRADITIONAL_GIS_ORDER", "YES");

        // 关闭调试信息
        Gdal.SetConfigOption("CPL_DEBUG", "OFF");
    }

    /// <summary>
    ///     获取 GDAL 版本
    /// </summary>
    public static string GetGdalVersion()
    {
        EnsureConfigured();
        return Gdal.VersionInfo("RELEASE_NAME");
    }

    /// <summary>
    ///     获取支持的驱动列表
    /// </summary>
    public static IList<string> GetSupportedDrivers()
    {
        EnsureConfigured();
        var drivers = new List<string>();

        var count = Ogr.GetDriverCount();
        for (int i = 0; i < count; i++)
        {
            var driver = Ogr.GetDriver(i);
            if (driver != null) drivers.Add(driver.GetName());
        }

        return drivers;
    }

    /// <summary>
    ///     检查驱动是否可用
    /// </summary>
    public static bool IsDriverAvailable(string driverName)
    {
        EnsureConfigured();
        var driver = Ogr.GetDriverByName(driverName);
        return driver != null;
    }

    /// <summary>
    ///     确保 GDAL 已配置
    /// </summary>
    private static void EnsureConfigured()
    {
        if (!_isConfigured) ConfigureGdal();
    }
}
