using System;
using System.Collections.Generic;
using OpenGIS.Utils.Engine.IO;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GeoTools 读取器（已废弃，重定向到 GdalReader）
/// </summary>
[Obsolete("GeoToolsReader based on NetTopologySuite has been removed. Use GdalReader instead.")]
public class GeoToolsReader : ILayerReader
{
    private readonly GdalReader _gdalReader = new();

    /// <summary>
    ///     读取图层
    /// </summary>
    public OguLayer Read(string path, string? layerName = null, string? attributeFilter = null,
        string? spatialFilterWkt = null, Dictionary<string, object>? options = null)
    {
        return _gdalReader.Read(path, layerName, attributeFilter, spatialFilterWkt, options);
    }

    /// <summary>
    ///     获取图层名称列表
    /// </summary>
    public IList<string> GetLayerNames(string path)
    {
        return _gdalReader.GetLayerNames(path);
    }
}
