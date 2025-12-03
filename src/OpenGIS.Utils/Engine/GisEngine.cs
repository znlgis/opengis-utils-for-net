using System.Collections.Generic;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.IO;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GIS 引擎抽象基类
/// </summary>
public abstract class GisEngine
{
    /// <summary>
    ///     引擎类型
    /// </summary>
    public abstract GisEngineType EngineType { get; }

    /// <summary>
    ///     支持的格式列表
    /// </summary>
    public abstract IList<DataFormatType> SupportedFormats { get; }

    /// <summary>
    ///     创建读取器
    /// </summary>
    /// <returns>图层读取器实例</returns>
    public abstract ILayerReader CreateReader();

    /// <summary>
    ///     创建写入器
    /// </summary>
    /// <returns>图层写入器实例</returns>
    public abstract ILayerWriter CreateWriter();

    /// <summary>
    ///     检查是否支持指定格式
    /// </summary>
    /// <param name="format">数据格式类型</param>
    /// <returns>如果支持该格式返回 true，否则返回 false</returns>
    public virtual bool SupportsFormat(DataFormatType format)
    {
        return SupportedFormats.Contains(format);
    }
}
