using System.Collections.Generic;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.IO;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GeoTools 引擎（基于 NetTopologySuite 实现）
/// </summary>
public class GeoToolsEngine : GisEngine
{
    private static readonly IList<DataFormatType> _supportedFormats = new List<DataFormatType>
    {
        DataFormatType.SHP, DataFormatType.GEOJSON, DataFormatType.POSTGIS, DataFormatType.TXT
    };

    /// <summary>
    ///     引擎类型
    /// </summary>
    public override GisEngineType EngineType => GisEngineType.GEOTOOLS;

    /// <summary>
    ///     支持的格式列表
    /// </summary>
    public override IList<DataFormatType> SupportedFormats => _supportedFormats;

    /// <summary>
    ///     创建读取器
    /// </summary>
    public override ILayerReader CreateReader()
    {
        return new GeoToolsReader();
    }

    /// <summary>
    ///     创建写入器
    /// </summary>
    public override ILayerWriter CreateWriter()
    {
        return new GeoToolsWriter();
    }
}
