using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GIS 引擎工厂类
/// </summary>
public static class GisEngineFactory
{
    private static readonly GdalEngine _gdalEngineInstance = new();

    /// <summary>
    ///     根据引擎类型获取引擎实例
    /// </summary>
    /// <param name="engineType">引擎类型</param>
    /// <returns>GIS 引擎实例</returns>
    /// <exception cref="EngineNotSupportedException">当引擎类型不支持时抛出</exception>
    public static GisEngine GetEngine(GisEngineType engineType)
    {
        return engineType switch
        {
            GisEngineType.GEOTOOLS => _gdalEngineInstance, // GeoTools now redirects to GDAL
            GisEngineType.GDAL => _gdalEngineInstance,
            _ => throw new EngineNotSupportedException($"Engine type {engineType} is not supported")
        };
    }

    /// <summary>
    ///     根据数据格式自动选择合适的引擎
    /// </summary>
    /// <param name="format">数据格式类型</param>
    /// <returns>支持该格式的 GIS 引擎实例</returns>
    /// <remarks>当前所有格式都使用 GDAL 引擎</remarks>
    public static GisEngine GetEngine(DataFormatType format)
    {
        // All formats now use GDAL
        return _gdalEngineInstance;
    }

    /// <summary>
    ///     尝试获取支持指定格式的引擎
    /// </summary>
    /// <param name="format">数据格式类型</param>
    /// <param name="engine">输出的引擎实例，如果失败则为 null</param>
    /// <returns>如果成功获取引擎返回 true，否则返回 false</returns>
    public static bool TryGetEngine(DataFormatType format, out GisEngine? engine)
    {
        engine = _gdalEngineInstance;
        return true;
    }
}
