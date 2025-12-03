using System.Collections.Generic;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.IO;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GDAL 引擎（基于 MaxRev.Gdal.Universal 实现）
/// </summary>
public class GdalEngine : GisEngine
{
    private static readonly IReadOnlyList<DataFormatType> _supportedFormats = new List<DataFormatType>
    {
        DataFormatType.FILEGDB,
        DataFormatType.GEOPACKAGE,
        DataFormatType.KML,
        DataFormatType.DXF,
        DataFormatType.SHP,
        DataFormatType.GEOJSON
    };

    /// <summary>
    ///     引擎类型
    /// </summary>
    public override GisEngineType EngineType => GisEngineType.GDAL;

    /// <summary>
    ///     支持的格式列表
    /// </summary>
    public override IList<DataFormatType> SupportedFormats => (IList<DataFormatType>)_supportedFormats;

    /// <summary>
    ///     创建读取器
    /// </summary>
    public override ILayerReader CreateReader()
    {
        return new GdalReader();
    }

    /// <summary>
    ///     创建写入器
    /// </summary>
    public override ILayerWriter CreateWriter()
    {
        return new GdalWriter();
    }
}
