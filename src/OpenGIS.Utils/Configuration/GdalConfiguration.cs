using System;
using System.Collections.Generic;
using System.IO;
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
    /// <exception cref="InvalidOperationException">当 GDAL 配置失败时抛出</exception>
    /// <remarks>此方法是线程安全的，多次调用只会执行一次配置</remarks>
    public static void ConfigureGdal()
    {
        lock (_lock)
        {
            if (_isConfigured)
                return;

            try
            {
                // 清除可能冲突的 OSGeo4W 等系统级 GDAL 环境变量，
                // 确保 MaxRev.Gdal 使用其自带的 GDAL 运行时（使用 Process 级别清除）
                Environment.SetEnvironmentVariable("GDAL_DRIVER_PATH", null, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("GDAL_DATA", null, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("PROJ_LIB", null, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("OSGEO4W_ROOT", null, EnvironmentVariableTarget.Process);

                // 通过 GDAL config 显式覆盖驱动路径，防止 AllRegister 扫描不兼容的系统插件
                Gdal.SetConfigOption("GDAL_DRIVER_PATH", "");

                // MaxRev.Gdal.Universal 会自动配置路径
                GdalBase.ConfigureAll();

                // DXF 等驱动运行时需要 GDAL 数据文件（header.dxf 等），MaxRev 包将其部署在
                // runtimes/any/native/gdal-data，但不会自动设置 GDAL_DATA，这里显式指向
                ConfigureGdalData();

                // 注册所有驱动（GDAL AllRegister 已在 ConfigureAll 中调用，此处补充 OGR 注册）
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
    /// <remarks>注册 GDAL 和 OGR 的所有可用驱动程序</remarks>
    public static void RegisterAllDrivers()
    {
        Gdal.AllRegister();
        Ogr.RegisterAll();
    }

    /// <summary>
    ///     设置 GDAL 配置选项
    /// </summary>
    /// <remarks>设置 UTF-8 文件名、Shapefile 编码、GIS 坐标顺序等选项</remarks>
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
    /// <returns>GDAL 版本字符串</returns>
    public static string GetGdalVersion()
    {
        EnsureConfigured();
        return Gdal.VersionInfo("RELEASE_NAME");
    }

    /// <summary>
    ///     获取支持的驱动列表
    /// </summary>
    /// <returns>驱动名称列表</returns>
    public static IList<string> GetSupportedDrivers()
    {
        EnsureConfigured();
        var count = Ogr.GetDriverCount();
        var drivers = new List<string>(count);

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
    /// <param name="driverName">驱动名称</param>
    /// <returns>如果驱动可用返回 true，否则返回 false</returns>
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

    /// <summary>
    ///     定位并设置 GDAL_DATA（仅当当前未指向有效目录时）
    /// </summary>
    /// <remarks>
    ///     搜索 MaxRev.Gdal 部署的 runtimes/any/native/gdal-data 目录，
    ///     覆盖应用输出目录与 NuGet 包缓存两种布局（向上最多回溯 5 级）。
    /// </remarks>
    private static void ConfigureGdalData()
    {
        var existing = Environment.GetEnvironmentVariable("GDAL_DATA");
        if (!string.IsNullOrEmpty(existing) && Directory.Exists(existing))
            return;

        var anchors = new List<string>();
        try
        {
            var assemblyLocation = typeof(GdalBase).Assembly.Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
                anchors.Add(Path.GetDirectoryName(assemblyLocation)!);
        }
        catch (SysException)
        {
            // 程序集位置不可用时忽略
        }

        anchors.Add(AppContext.BaseDirectory);

        foreach (var anchor in anchors)
        {
            var dataDir = FindGdalDataDir(anchor);
            if (dataDir == null)
                continue;

            Environment.SetEnvironmentVariable("GDAL_DATA", dataDir, EnvironmentVariableTarget.Process);
            Gdal.SetConfigOption("GDAL_DATA", dataDir);
            return;
        }
    }

    private static string? FindGdalDataDir(string baseDirectory)
    {
        if (string.IsNullOrEmpty(baseDirectory))
            return null;

        var current = new DirectoryInfo(baseDirectory);
        for (var i = 0; i < 5 && current != null; i++, current = current.Parent!)
        {
            var candidate = Path.Combine(current.FullName, "runtimes", "any", "native", "gdal-data");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "header.dxf")))
                return candidate;
        }

        return null;
    }
}
