using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Utils;

namespace OpenGIS.Utils.DataSource;

/// <summary>
///     国土 TXT 坐标文件工具类
/// </summary>
public static class GtTxtUtil
{
    private static readonly Regex CoordinateLineRegex = new(
        @"^\s*(\S+)\s+(\S+)\s+([\d.]+)\s+([\d.]+)\s*([\d.]*)\s*(.*?)$",
        RegexOptions.Compiled);

    /// <summary>
    ///     加载 TXT 文件
    /// </summary>
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

            // 解析坐标行
            var coordinate = ParseTxtLine(line);
            if (coordinate != null)
            {
                var feature = new OguFeature { Fid = fid++, Wkt = coordinate.ToWkt() };

                feature.SetValue("点号", coordinate.PointNumber ?? string.Empty);
                feature.SetValue("圈号", coordinate.RingNumber ?? string.Empty);
                feature.SetValue("X", coordinate.X);
                feature.SetValue("Y", coordinate.Y);
                feature.SetValue("Z", coordinate.Z ?? 0.0);
                feature.SetValue("备注", coordinate.Remark ?? string.Empty);

                layer.AddFeature(feature);
            }
        }

        layer.Metadata = metadata;
        return layer;
    }

    /// <summary>
    ///     保存 TXT 文件
    /// </summary>
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
                    coordinate = OguCoordinate.FromWkt(feature.Wkt);
                }
                catch
                {
                    // 如果解析失败，跳过该要素
                    continue;
                }

            coordinate.PointNumber = feature.GetValue("点号")?.ToString();
            coordinate.RingNumber = feature.GetValue("圈号")?.ToString();
            coordinate.Remark = feature.GetValue("备注")?.ToString();

            lines.Add(FormatTxtLine(coordinate, zoneNumber ?? 0));
        }

        File.WriteAllLines(txtPath, lines, encoding);
    }

    /// <summary>
    ///     解析 TXT 坐标行
    /// </summary>
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
                X = double.Parse(match.Groups[3].Value),
                Y = double.Parse(match.Groups[4].Value)
            };

            if (!string.IsNullOrWhiteSpace(match.Groups[5].Value)) coordinate.Z = double.Parse(match.Groups[5].Value);

            if (!string.IsNullOrWhiteSpace(match.Groups[6].Value)) coordinate.Remark = match.Groups[6].Value.Trim();

            return coordinate;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     格式化 TXT 坐标行
    /// </summary>
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
