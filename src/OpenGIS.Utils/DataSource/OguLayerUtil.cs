using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenGIS.Utils.Engine;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.DataSource;

/// <summary>
///     统一图层工具类
/// </summary>
public static class OguLayerUtil
{
    /// <summary>
    ///     读取图层
    /// </summary>
    public static OguLayer ReadLayer(
        DataFormatType format,
        string path,
        string? layerName = null,
        string? attributeFilter = null,
        string? spatialFilterWkt = null,
        GisEngineType? engineType = null,
        Dictionary<string, object>? options = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        // 获取引擎
        var engine = engineType.HasValue
            ? GisEngineFactory.GetEngine(engineType.Value)
            : GisEngineFactory.GetEngine(format);

        // 创建读取器并读取图层
        var reader = engine.CreateReader();
        return reader.Read(path, layerName, attributeFilter, spatialFilterWkt, options);
    }

    /// <summary>
    ///     异步读取图层
    /// </summary>
    public static Task<OguLayer> ReadLayerAsync(
        DataFormatType format,
        string path,
        string? layerName = null,
        string? attributeFilter = null,
        string? spatialFilterWkt = null,
        GisEngineType? engineType = null,
        Dictionary<string, object>? options = null)
    {
        return Task.Run(() =>
            ReadLayer(format, path, layerName, attributeFilter, spatialFilterWkt, engineType, options));
    }

    /// <summary>
    ///     写入图层
    /// </summary>
    public static void WriteLayer(
        DataFormatType format,
        OguLayer layer,
        string path,
        string? layerName = null,
        GisEngineType? engineType = null,
        Dictionary<string, object>? options = null)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        // 获取引擎
        var engine = engineType.HasValue
            ? GisEngineFactory.GetEngine(engineType.Value)
            : GisEngineFactory.GetEngine(format);

        // 创建写入器并写入图层
        var writer = engine.CreateWriter();
        writer.Write(layer, path, layerName, options);
    }

    /// <summary>
    ///     异步写入图层
    /// </summary>
    public static Task WriteLayerAsync(
        DataFormatType format,
        OguLayer layer,
        string path,
        string? layerName = null,
        GisEngineType? engineType = null,
        Dictionary<string, object>? options = null)
    {
        return Task.Run(() => WriteLayer(format, layer, path, layerName, engineType, options));
    }

    /// <summary>
    ///     获取图层名称列表
    /// </summary>
    public static IList<string> GetLayerNames(DataFormatType format, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        // 获取引擎
        var engine = GisEngineFactory.GetEngine(format);

        // 创建读取器并获取图层名称
        var reader = engine.CreateReader();
        return reader.GetLayerNames(path);
    }

    /// <summary>
    ///     格式转换
    /// </summary>
    public static void ConvertFormat(
        string inputPath,
        DataFormatType inputFormat,
        string outputPath,
        DataFormatType outputFormat,
        GisEngineType? engineType = null,
        string? layerName = null)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
            throw new ArgumentException("Input path cannot be null or empty", nameof(inputPath));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

        // 读取输入图层
        var layer = ReadLayer(inputFormat, inputPath, layerName, engineType: engineType);

        // 写入输出格式
        WriteLayer(outputFormat, layer, outputPath, layerName, engineType);
    }

    /// <summary>
    ///     异步格式转换
    /// </summary>
    public static Task ConvertFormatAsync(
        string inputPath,
        DataFormatType inputFormat,
        string outputPath,
        DataFormatType outputFormat,
        GisEngineType? engineType = null,
        string? layerName = null)
    {
        return Task.Run(() => ConvertFormat(inputPath, inputFormat, outputPath, outputFormat, engineType, layerName));
    }
}
