using System.Collections.Generic;

namespace OpenGIS.Utils.Engine.Model;

/// <summary>
///     GDB 组模型
/// </summary>
public class GdbGroupModel
{
    /// <summary>
    ///     构造函数
    /// </summary>
    public GdbGroupModel()
    {
        LayerNames = new List<string>();
    }

    /// <summary>
    ///     GDB 路径
    /// </summary>
    public string? GdbPath { get; set; }

    /// <summary>
    ///     图层名称列表
    /// </summary>
    public List<string> LayerNames { get; set; }
}
