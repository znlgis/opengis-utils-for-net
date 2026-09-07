using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;
using OpenGIS.Utils.Utils;

namespace OpenGIS.Utils.DataSource;

/// <summary>
///     国土 TXT 坐标文件工具类
/// </summary>
public static class GtTxtUtil
{
    private static readonly ILogger Logger = OguLogging.CreateLogger("OpenGIS.Utils.DataSource.GtTxtUtil");

    private static readonly Regex CoordinateLineRegex = new(
        @"^\s*(\S+)\s+(\S+)\s+([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s+([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)?\s*(.*?)$",
        RegexOptions.Compiled);

    /// <summary>
    ///     加载 TXT 文件
    /// </summary>
    /// <param name="txtPath">TXT 文件路径</param>
    /// <param name="encoding">字符编码，如果为 null 则自动检测</param>
    /// <returns>图层对象</returns>
    /// <exception cref="FileNotFoundException">当文件不存在时抛出</exception>
    /// <exception cref="FormatParseException">当非元数据行无法解析为坐标时抛出</exception>
    public static OguLayer LoadTxt(string txtPath, Encoding? encoding = null)
    {
        if (!File.Exists(txtPath))
            throw new FileNotFoundException("TXT file not found", txtPath);

        encoding = encoding ?? EncodingUtil.GetFileEncoding(txtPath);

        var layer = new OguLayer
        {
            Name = Path.GetFileNameWithoutExtension(txtPath), GeometryType = GeometryType.POINT
        };

        // 添加标准字段
        layer.AddField(new OguField { Name = "点号", DataType = FieldDataType.STRING, Length = 50 });
        layer.AddField(new OguField { Name = "圈号", DataType = FieldDataType.STRING, Length = 50 });
        layer.AddField(new OguField { Name = "X", DataType = FieldDataType.DOUBLE });
        layer.AddField(new OguField { Name = "Y", DataType = FieldDataType.DOUBLE });
        layer.AddField(new OguField { Name = "Z", DataType = FieldDataType.DOUBLE });
        layer.AddField(new OguField { Name = "备注", DataType = FieldDataType.STRING, Length = 200 });

        var lines = File.ReadAllLines(txtPath, encoding);
        var metadata = new OguLayerMetadata();
        int fid = 1;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // 解析元数据行
            if (line.Contains("数据来源") || line.Contains("坐标系") || line.Contains("分带") ||
                line.Contains("投影") || line.Contains("单位"))
            {
                ParseMetadataLine(line, metadata);
                continue;
            }

            // 跳过 SaveTxt 输出的列头行（点号/圈号/X/Y/Z/备注）
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("点号", StringComparison.Ordinal) &&
                trimmedLine.Contains("X") && trimmedLine.Contains("Y"))
                continue;

            // 解析坐标行
            var coordinate = TryParseTxtLine(line, out var parsedCoordinate)
                ? parsedCoordinate
                : null;
            if (coordinate == null)
                throw new FormatParseException($"Invalid TXT coordinate line: {line}");

            var feature = new OguFeature { Fid = fid++, Wkt = coordinate.ToWkt() };

            feature.SetValue("点号", coordinate.PointNumber ?? string.Empty);
            feature.SetValue("圈号", coordinate.RingNumber ?? string.Empty);
            feature.SetValue("X", coordinate.X);
            feature.SetValue("Y", coordinate.Y);
            feature.SetValue("Z", coordinate.Z ?? 0.0);
            feature.SetValue("备注", coordinate.Remark ?? string.Empty);

            layer.AddFeature(feature);
        }

        layer.Metadata = metadata;
        return layer;
    }

    /// <summary>
    ///     保存 TXT 文件
    /// </summary>
    /// <param name="layer">图层对象</param>
    /// <param name="txtPath">输出 TXT 文件路径</param>
    /// <param name="metadata">元数据，如果为 null 则使用图层元数据</param>
    /// <param name="encoding">字符编码，如果为 null 则使用 UTF-8</param>
    /// <param name="zoneNumber">带号，用于格式化坐标，默认为 0</param>
    /// <exception cref="ArgumentNullException">当图层为 null 时抛出</exception>
    /// <exception cref="ArgumentException">当路径为空时抛出</exception>
    /// <exception cref="FormatParseException">当要素的 WKT 无法解析时抛出</exception>
    public static void SaveTxt(
        OguLayer layer,
        string txtPath,
        OguLayerMetadata? metadata = null,
        Encoding? encoding = null,
        int? zoneNumber = null)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));
        if (string.IsNullOrWhiteSpace(txtPath))
            throw new ArgumentException("Path cannot be null or empty", nameof(txtPath));
        if (layer.Features == null)
            throw new ArgumentException("Layer features collection cannot be null", nameof(layer));

        encoding = encoding ?? Encoding.UTF8;
        metadata = metadata ?? layer.Metadata ?? new OguLayerMetadata();

        var lines = new List<string>();

        // 写入元数据
        if (!string.IsNullOrWhiteSpace(metadata.DataSource))
            lines.Add($"数据来源: {metadata.DataSource}");
        if (!string.IsNullOrWhiteSpace(metadata.CoordinateSystemName))
            lines.Add($"坐标系: {metadata.CoordinateSystemName}");
        if (!string.IsNullOrWhiteSpace(metadata.ZoneDivision))
            lines.Add($"分带: {metadata.ZoneDivision}");
        if (!string.IsNullOrWhiteSpace(metadata.ProjectionType))
            lines.Add($"投影类型: {metadata.ProjectionType}");
        if (!string.IsNullOrWhiteSpace(metadata.MeasureUnit))
            lines.Add($"单位: {metadata.MeasureUnit}");

        if (lines.Count > 0)
            lines.Add(string.Empty);

        // 写入坐标数据
        lines.Add("点号\t圈号\tX\tY\tZ\t备注");

        foreach (var feature in layer.Features)
        {
            var coordinate = new OguCoordinate();

            // 从 WKT 解析坐标
            if (!string.IsNullOrWhiteSpace(feature.Wkt))
                try
                {
                    coordinate = OguCoordinate.FromWkt(feature.Wkt!);
                }
                catch (System.Exception ex)
                {
                    throw new FormatParseException($"Invalid feature WKT (Fid={feature.Fid})", ex);
                }

            coordinate.PointNumber = feature.GetValue("点号")?.ToString();
            coordinate.RingNumber = feature.GetValue("圈号")?.ToString();
            coordinate.Remark = feature.GetValue("备注")?.ToString();

            // 国土 TXT 格式要求点号/圈号非空（否则 LoadTxt 无法解析）；
            // 缺失时用要素 Fid / 默认圈号补齐，保证 SaveTxt→LoadTxt 可往返
            if (string.IsNullOrWhiteSpace(coordinate.PointNumber))
                coordinate.PointNumber = feature.Fid.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(coordinate.RingNumber))
                coordinate.RingNumber = "1";

            lines.Add(FormatTxtLine(coordinate, zoneNumber ?? 0));
        }

        File.WriteAllLines(txtPath, lines, encoding);
    }

    /// <summary>
    ///     解析 TXT 坐标行
    /// </summary>
    /// <param name="line">坐标行文本</param>
    /// <returns>解析出的坐标对象，如果无法解析则返回 null</returns>
    public static OguCoordinate? ParseTxtLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var match = CoordinateLineRegex.Match(line);
        if (!match.Success)
            return null;

        try
        {
            var coordinate = new OguCoordinate
            {
                PointNumber = match.Groups[1].Value,
                RingNumber = match.Groups[2].Value,
                X = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                Y = double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture)
            };

            if (!string.IsNullOrWhiteSpace(match.Groups[5].Value)) coordinate.Z = double.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(match.Groups[6].Value)) coordinate.Remark = match.Groups[6].Value.Trim();

            return coordinate;
        }
        catch (System.Exception ex)
        {
            Logger.LogDebug(ex, "解析 TXT 坐标行失败: {Line}", line);
            return null;
        }
    }

    /// <summary>
    ///     尝试解析 TXT 坐标行。
    /// </summary>
    /// <param name="line">坐标行文本</param>
    /// <param name="coordinate">解析出的坐标对象，解析失败时为 null</param>
    /// <returns>解析成功返回 true，否则返回 false</returns>
    /// <remarks>此方法提供不抛异常的尝试式 API；旧的 <see cref="ParseTxtLine"/> nullable API 保持兼容。</remarks>
    public static bool TryParseTxtLine(string line, out OguCoordinate? coordinate)
    {
        coordinate = ParseTxtLine(line);
        return coordinate != null;
    }

    /// <summary>
    ///     格式化 TXT 坐标行
    /// </summary>
    /// <param name="coordinate">坐标对象</param>
    /// <param name="zoneNumber">带号</param>
    /// <returns>格式化后的坐标行文本</returns>
    /// <exception cref="ArgumentNullException">当坐标为 null 时抛出</exception>
    public static string FormatTxtLine(OguCoordinate coordinate, int zoneNumber)
    {
        if (coordinate == null)
            throw new ArgumentNullException(nameof(coordinate));

        var pointNumber = coordinate.PointNumber ?? string.Empty;
        var ringNumber = coordinate.RingNumber ?? string.Empty;
        var x = NumUtil.GetPlainString(coordinate.X);
        var y = NumUtil.GetPlainString(coordinate.Y);
        var z = coordinate.Z.HasValue ? NumUtil.GetPlainString(coordinate.Z.Value) : string.Empty;
        var remark = coordinate.Remark ?? string.Empty;

        // Use string interpolation for better performance than string concatenation
        return $"{pointNumber}\t{ringNumber}\t{x}\t{y}\t{z}\t{remark}";
    }

    private static void ParseMetadataLine(string line, OguLayerMetadata metadata)
    {
        if (line.Contains("数据来源"))
        {
            var parts = line.Split(new[] { ':', '：' }, 2);
            if (parts.Length == 2)
                metadata.DataSource = parts[1].Trim();
        }
        else if (line.Contains("坐标系"))
        {
            var parts = line.Split(new[] { ':', '：' }, 2);
            if (parts.Length == 2)
                metadata.CoordinateSystemName = parts[1].Trim();
        }
        else if (line.Contains("分带"))
        {
            var parts = line.Split(new[] { ':', '：' }, 2);
            if (parts.Length == 2)
                metadata.ZoneDivision = parts[1].Trim();
        }
        else if (line.Contains("投影"))
        {
            var parts = line.Split(new[] { ':', '：' }, 2);
            if (parts.Length == 2)
                metadata.ProjectionType = parts[1].Trim();
        }
        else if (line.Contains("单位"))
        {
            var parts = line.Split(new[] { ':', '：' }, 2);
            if (parts.Length == 2)
                metadata.MeasureUnit = parts[1].Trim();
        }
    }
}
