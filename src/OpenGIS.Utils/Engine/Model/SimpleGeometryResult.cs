namespace OpenGIS.Utils.Engine.Model;

/// <summary>
///     简单几何结果
/// </summary>
public class SimpleGeometryResult
{
    /// <summary>
    ///     是否简单
    /// </summary>
    public bool IsSimple { get; set; }

    /// <summary>
    ///     原因
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    ///     非简单位置（WKT）
    /// </summary>
    public string? NonSimpleLocation { get; set; }
}
