using System.Collections.Generic;
using System.Text.Json;

namespace OpenGIS.Utils.Engine.Model.Layer;

/// <summary>
///     统一的要素类
/// </summary>
public class OguFeature
{
    /// <summary>
    ///     构造函数
    /// </summary>
    public OguFeature()
    {
        Attributes = new Dictionary<string, OguFieldValue>();
    }

    /// <summary>
    ///     要素 ID
    /// </summary>
    public int Fid { get; set; }

    /// <summary>
    ///     几何信息（WKT 格式）
    /// </summary>
    public string? Wkt { get; set; }

    /// <summary>
    ///     属性值字典
    /// </summary>
    public Dictionary<string, OguFieldValue> Attributes { get; set; }

    /// <summary>
    ///     获取属性原始值
    /// </summary>
    /// <param name="fieldName">字段名称</param>
    /// <returns>字段值，如果字段不存在则返回 null</returns>
    public object? GetValue(string fieldName)
    {
        if (Attributes.TryGetValue(fieldName, out var fieldValue)) return fieldValue.Value;
        return null;
    }

    /// <summary>
    ///     设置属性值
    /// </summary>
    /// <param name="fieldName">字段名称</param>
    /// <param name="value">字段值</param>
    public void SetValue(string fieldName, object? value)
    {
        if (Attributes.ContainsKey(fieldName))
            Attributes[fieldName].Value = value;
        else
            Attributes[fieldName] = new OguFieldValue(value);
    }

    /// <summary>
    ///     获取字段值对象
    /// </summary>
    /// <param name="fieldName">字段名称</param>
    /// <returns>字段值对象，如果字段不存在则返回 null</returns>
    public OguFieldValue? GetAttribute(string fieldName)
    {
        if (Attributes.TryGetValue(fieldName, out var fieldValue)) return fieldValue;
        return null;
    }

    /// <summary>
    ///     判断是否存在属性
    /// </summary>
    /// <param name="fieldName">字段名称</param>
    /// <returns>如果属性存在返回 true，否则返回 false</returns>
    public bool HasAttribute(string fieldName)
    {
        return Attributes.ContainsKey(fieldName);
    }

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
    /// <returns>反序列化的要素对象，如果失败则返回 null</returns>
    public static OguFeature? FromJson(string json)
    {
        return JsonSerializer.Deserialize<OguFeature>(json);
    }

    /// <summary>
    ///     深拷贝
    /// </summary>
    /// <returns>要素的完整副本，包括所有属性值</returns>
    public OguFeature Clone()
    {
        var clone = new OguFeature { Fid = Fid, Wkt = Wkt };

        foreach (var kvp in Attributes) clone.Attributes[kvp.Key] = new OguFieldValue(kvp.Value.Value);

        return clone;
    }
}
