using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGIS.Utils.Engine.Model.Layer;

/// <summary>
///     字段值容器，提供类型安全的转换方法
/// </summary>
public class OguFieldValue
{
    /// <summary>
    ///     构造函数
    /// </summary>
    public OguFieldValue()
    {
    }

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="value">原始值</param>
    public OguFieldValue(object? value)
    {
        Value = value;
    }

    /// <summary>
    ///     原始值
    /// </summary>
    public object? Value { get; set; }

    internal static object? CloneValue(object? value)
    {
        if (value is null || value is string || value.GetType().IsValueType)
            return value;

        if (value is byte[] bytes)
            return bytes.Clone();

        if (value is Array array)
        {
            var clone = (Array)array.Clone();
            for (var index = 0; index < array.Length; index++)
                clone.SetValue(CloneValue(array.GetValue(index)), index);
            return clone;
        }

        if (value is ICloneable cloneable)
            return cloneable.Clone();

        if (value is IDictionary dictionary)
        {
            var clone = new Dictionary<object, object?>();
            foreach (DictionaryEntry entry in dictionary)
                clone[entry.Key] = CloneValue(entry.Value);
            return clone;
        }

        if (value is IList list)
        {
            var clone = new List<object?>();
            foreach (var item in list)
                clone.Add(CloneValue(item));
            return clone;
        }

        return value;
    }

    /// <summary>
    ///     是否为空
    /// </summary>
    public bool IsNull => Value == null;

    /// <summary>
    ///     转为字符串
    /// </summary>
    /// <returns>字符串值，如果为null则返回null</returns>
    public string? GetStringValue()
    {
        return Value?.ToString();
    }

    /// <summary>
    ///     转为整数
    /// </summary>
    /// <returns>整数值，如果无法转换则返回null</returns>
    public int? GetIntValue()
    {
        if (IsNull) return null;
        if (Value is int i) return i;
        if (int.TryParse(Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            return result;
        return null;
    }

    /// <summary>
    ///     转为长整数
    /// </summary>
    /// <returns>长整数值，如果无法转换则返回null</returns>
    public long? GetLongValue()
    {
        if (IsNull) return null;
        if (Value is long l) return l;
        if (long.TryParse(Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            return result;
        return null;
    }

    /// <summary>
    ///     转为双精度
    /// </summary>
    /// <returns>双精度浮点值，如果无法转换则返回null</returns>
    public double? GetDoubleValue()
    {
        if (IsNull) return null;
        if (Value is double d) return d;
        if (double.TryParse(Value?.ToString(), NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out double result))
            return result;
        return null;
    }

    /// <summary>
    ///     转为单精度
    /// </summary>
    /// <returns>单精度浮点值，如果无法转换则返回null</returns>
    public float? GetFloatValue()
    {
        if (IsNull) return null;
        if (Value is float f) return f;
        if (float.TryParse(Value?.ToString(), NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out float result))
            return result;
        return null;
    }

    /// <summary>
    ///     转为布尔值
    /// </summary>
    /// <returns>布尔值，如果无法转换则返回null</returns>
    public bool? GetBoolValue()
    {
        if (IsNull) return null;
        if (Value is bool b) return b;
        if (bool.TryParse(Value?.ToString(), out bool result))
            return result;
        return null;
    }

    /// <summary>
    ///     转为日期时间
    /// </summary>
    /// <returns>日期时间值，如果无法转换则返回null</returns>
    public DateTime? GetDateTimeValue()
    {
        if (IsNull) return null;
        if (Value is DateTime dt) return dt;
        if (DateTime.TryParse(Value?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None,
                out DateTime result))
            return result;
        return null;
    }

    /// <summary>
    ///     转为 Decimal
    /// </summary>
    /// <returns>Decimal值，如果无法转换则返回null</returns>
    public decimal? GetDecimalValue()
    {
        if (IsNull) return null;
        if (Value is decimal dec) return dec;
        if (decimal.TryParse(Value?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture,
                out decimal result))
            return result;
        return null;
    }

    /// <summary>
    ///     重写 ToString
    /// </summary>
    /// <returns>字符串表示形式</returns>
    public override string ToString()
    {
        return Value?.ToString() ?? string.Empty;
    }
}
