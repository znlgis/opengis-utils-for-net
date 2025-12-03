using System;
using System.Collections.Generic;
using OpenGIS.Utils.Engine.IO;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GeoTools 写入器（已废弃，重定向到 GdalWriter）
/// </summary>
[Obsolete("GeoToolsWriter based on NetTopologySuite has been removed. Use GdalWriter instead.")]
public class GeoToolsWriter : ILayerWriter
{
    private readonly GdalWriter _gdalWriter = new();

    /// <summary>
    ///     写入图层
    /// </summary>
    public void Write(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null)
    {
        _gdalWriter.Write(layer, path, layerName, options);
    }

    /// <summary>
    ///     追加要素到已存在的图层
    /// </summary>
    public void Append(OguLayer layer, string path, string? layerName = null,
        Dictionary<string, object>? options = null)
    {
        _gdalWriter.Append(layer, path, layerName, options);
    }
}
