using System.Collections.Generic;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.IO;
using System;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GeoTools 引擎（已废弃，请使用 GdalEngine）
/// </summary>
[Obsolete("GeoTools engine based on NetTopologySuite has been removed. Use GdalEngine instead.")]
public class GeoToolsEngine : GisEngine
{
    private static readonly IList<DataFormatType> _supportedFormats = new List<DataFormatType>
    {
        DataFormatType.SHP, DataFormatType.GEOJSON, DataFormatType.POSTGIS, DataFormatType.TXT
    };

    /// <summary>
    ///     引擎类型
    /// </summary>
    public override GisEngineType EngineType => GisEngineType.GDAL; // Redirect to GDAL

    /// <summary>
    ///     支持的格式列表
    /// </summary>
    public override IList<DataFormatType> SupportedFormats => _supportedFormats;

    /// <summary>
    ///     创建读取器（重定向到 GdalReader）
    /// </summary>
    public override ILayerReader CreateReader()
    {
        return new GdalReader();
    }

    /// <summary>
    ///     创建写入器（重定向到 GdalWriter）
    /// </summary>
    public override ILayerWriter CreateWriter()
    {
        return new GdalWriter();
    }
}
