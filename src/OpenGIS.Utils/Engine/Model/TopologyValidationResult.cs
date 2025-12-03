using OpenGIS.Utils.Engine.Enums;

namespace OpenGIS.Utils.Engine.Model;

/// <summary>
///     拓扑验证结果
/// </summary>
public class TopologyValidationResult
{
    /// <summary>
    ///     是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    ///     错误类型
    /// </summary>
    public TopologyValidationErrorType? ErrorType { get; set; }

    /// <summary>
    ///     错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///     错误位置（WKT）
    /// </summary>
    public string? ErrorLocation { get; set; }
}
