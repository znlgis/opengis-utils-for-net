namespace OpenGIS.Utils.Engine.Enums;

/// <summary>
///     几何类型枚举
/// </summary>
public enum GeometryType
{
    /// <summary>
    ///     点
    /// </summary>
    POINT,

    /// <summary>
    ///     线
    /// </summary>
    LINESTRING,

    /// <summary>
    ///     面
    /// </summary>
    POLYGON,

    /// <summary>
    ///     多点
    /// </summary>
    MULTIPOINT,

    /// <summary>
    ///     多线
    /// </summary>
    MULTILINESTRING,

    /// <summary>
    ///     多面
    /// </summary>
    MULTIPOLYGON,

    /// <summary>
    ///     几何集合
    /// </summary>
    GEOMETRYCOLLECTION,

    /// <summary>
    ///     未知类型
    /// </summary>
    UNKNOWN
}
