using System;
using NetTopologySuite.Geometries;

namespace OpenGIS.Utils.Engine.Model.Layer;

/// <summary>
///     坐标类，支持 2D/3D 坐标及点号/圈号（国土 TXT 格式）
/// </summary>
public class OguCoordinate
{
    /// <summary>
    ///     X 坐标
    /// </summary>
    public double X { get; set; }

    /// <summary>
    ///     Y 坐标
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    ///     Z 坐标（可选）
    /// </summary>
    public double? Z { get; set; }

    /// <summary>
    ///     点号
    /// </summary>
    public string? PointNumber { get; set; }

    /// <summary>
    ///     圈号
    /// </summary>
    public string? RingNumber { get; set; }

    /// <summary>
    ///     备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    ///     转为 WKT POINT
    /// </summary>
    public string ToWkt()
    {
        if (Z.HasValue) return $"POINT Z ({X} {Y} {Z.Value})";
        return $"POINT ({X} {Y})";
    }

    /// <summary>
    ///     从 WKT 解析
    /// </summary>
    public static OguCoordinate FromWkt(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            throw new ArgumentException("WKT cannot be null or empty", nameof(wkt));

        // 简化的 WKT 解析
        var cleaned = wkt.Trim().ToUpper();
        if (!cleaned.StartsWith("POINT"))
            throw new ArgumentException("Only POINT geometries are supported", nameof(wkt));

        var start = cleaned.IndexOf('(');
        var end = cleaned.LastIndexOf(')');
        if (start < 0 || end < 0)
            throw new ArgumentException("Invalid WKT format", nameof(wkt));

        var coords = cleaned.Substring(start + 1, end - start - 1).Trim()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var result = new OguCoordinate();
        if (coords.Length >= 2)
        {
            result.X = double.Parse(coords[0]);
            result.Y = double.Parse(coords[1]);
        }

        if (coords.Length >= 3) result.Z = double.Parse(coords[2]);

        return result;
    }

    /// <summary>
    ///     转换为 NetTopologySuite Coordinate
    /// </summary>
    public Coordinate ToNetTopologySuiteCoordinate()
    {
        if (Z.HasValue) return new CoordinateZ(X, Y, Z.Value);
        return new Coordinate(X, Y);
    }

    /// <summary>
    ///     从 NetTopologySuite Coordinate 创建
    /// </summary>
    public static OguCoordinate FromNetTopologySuiteCoordinate(Coordinate coordinate)
    {
        var result = new OguCoordinate { X = coordinate.X, Y = coordinate.Y };

        if (!double.IsNaN(coordinate.Z)) result.Z = coordinate.Z;

        return result;
    }
}
