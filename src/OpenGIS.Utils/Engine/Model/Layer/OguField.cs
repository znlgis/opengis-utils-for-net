using System.Text.Json;
using OpenGIS.Utils.Engine.Enums;

namespace OpenGIS.Utils.Engine.Model.Layer;

/// <summary>
///     统一的字段定义类
/// </summary>
public class OguField
{
    /// <summary>
    ///     字段名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     字段别名
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>
    ///     数据类型
    /// </summary>
    public FieldDataType DataType { get; set; }

    /// <summary>
    ///     字段长度（字符串类型）
    /// </summary>
    public int? Length { get; set; }

    /// <summary>
    ///     精度（数字类型）
    /// </summary>
    public int? Precision { get; set; }

    /// <summary>
    ///     小数位数（数字类型）
    /// </summary>
    public int? Scale { get; set; }

    /// <summary>
    ///     是否可为空
    /// </summary>
    public bool IsNullable { get; set; } = true;

    /// <summary>
    ///     默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    ///     转换为 JSON
    /// </summary>
    /// <returns>JSON 字符串</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    /// <summary>
    ///     从 JSON 创建
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>反序列化的字段定义，如果失败则返回 null</returns>
    public static OguField? FromJson(string json)
    {
        return JsonSerializer.Deserialize<OguField>(json);
    }

    /// <summary>
    ///     深拷贝
    /// </summary>
    /// <returns>字段定义的完整副本</returns>
    public OguField Clone()
    {
        return new OguField
        {
            Name = Name,
            Alias = Alias,
            DataType = DataType,
            Length = Length,
            Precision = Precision,
            Scale = Scale,
            IsNullable = IsNullable,
            DefaultValue = DefaultValue
        };
    }
}
