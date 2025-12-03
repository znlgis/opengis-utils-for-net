using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GIS 引擎工厂类
/// </summary>
public static class GisEngineFactory
{
    /// <summary>
    ///     根据引擎类型获取引擎实例
    /// </summary>
    public static GisEngine GetEngine(GisEngineType engineType)
    {
        return engineType switch
        {
            GisEngineType.GEOTOOLS => new GdalEngine(), // GeoTools now redirects to GDAL
            GisEngineType.GDAL => new GdalEngine(),
            _ => throw new EngineNotSupportedException($"Engine type {engineType} is not supported")
        };
    }

    /// <summary>
    ///     根据数据格式自动选择合适的引擎
    /// </summary>
    public static GisEngine GetEngine(DataFormatType format)
    {
        // All formats now use GDAL
        return new GdalEngine();
    }

    /// <summary>
    ///     尝试获取支持指定格式的引擎
    /// </summary>
    public static bool TryGetEngine(DataFormatType format, out GisEngine? engine)
    {
        try
        {
            engine = GetEngine(format);
            return true;
        }
        catch
        {
            engine = null;
            return false;
        }
    }
}
