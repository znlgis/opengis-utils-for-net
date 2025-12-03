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
    public object? GetValue(string fieldName)
    {
        if (Attributes.TryGetValue(fieldName, out var fieldValue)) return fieldValue.Value;
        return null;
    }

    /// <summary>
    ///     设置属性值
    /// </summary>
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
    public OguFieldValue? GetAttribute(string fieldName)
    {
        if (Attributes.TryGetValue(fieldName, out var fieldValue)) return fieldValue;
        return null;
    }

    /// <summary>
    ///     判断是否存在属性
    /// </summary>
    public bool HasAttribute(string fieldName)
    {
        return Attributes.ContainsKey(fieldName);
    }

    /// <summary>
    ///     转换为 JSON
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    /// <summary>
    ///     从 JSON 创建
    /// </summary>
    public static OguFeature? FromJson(string json)
    {
        return JsonSerializer.Deserialize<OguFeature>(json);
    }

    /// <summary>
    ///     深拷贝
    /// </summary>
    public OguFeature Clone()
    {
        var clone = new OguFeature { Fid = Fid, Wkt = Wkt };

        foreach (var kvp in Attributes) clone.Attributes[kvp.Key] = new OguFieldValue(kvp.Value.Value);

        return clone;
    }
}
