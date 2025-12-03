namespace OpenGIS.Utils.Engine.Enums;

/// <summary>
///     拓扑验证错误类型枚举
/// </summary>
public enum TopologyValidationErrorType
{
    /// <summary>
    ///     一般错误
    /// </summary>
    ERROR,

    /// <summary>
    ///     重复点
    /// </summary>
    REPEATED_POINT,

    /// <summary>
    ///     孔在壳外
    /// </summary>
    HOLE_OUTSIDE_SHELL,

    /// <summary>
    ///     嵌套孔
    /// </summary>
    NESTED_HOLES,

    /// <summary>
    ///     内部不连通
    /// </summary>
    DISCONNECTED_INTERIOR,

    /// <summary>
    ///     自相交
    /// </summary>
    SELF_INTERSECTION,

    /// <summary>
    ///     环自相交
    /// </summary>
    RING_SELF_INTERSECTION,

    /// <summary>
    ///     嵌套壳
    /// </summary>
    NESTED_SHELLS,

    /// <summary>
    ///     重复环
    /// </summary>
    DUPLICATE_RINGS,

    /// <summary>
    ///     点太少
    /// </summary>
    TOO_FEW_POINTS,

    /// <summary>
    ///     无效坐标
    /// </summary>
    INVALID_COORDINATE,

    /// <summary>
    ///     环未闭合
    /// </summary>
    RING_NOT_CLOSED
}
