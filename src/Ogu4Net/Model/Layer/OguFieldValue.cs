using System;
using System.Globalization;

namespace Ogu4Net.Model.Layer
{
    /// <summary>
    /// OGU字段值类
    /// <para>
    /// 表示要素的一个属性值，包含字段定义和具体值。
    /// 提供多种类型的值获取方法（字符串、整数、浮点数等）。
    /// </para>
    /// </summary>
    public class OguFieldValue
    {
        /// <summary>
        /// 字段定义
        /// </summary>
        public OguField? Field { get; set; }

        /// <summary>
        /// 字段值
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public OguFieldValue()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="field">字段定义</param>
        /// <param name="value">字段值</param>
        public OguFieldValue(OguField? field, object? value)
        {
            Field = field;
            Value = value;
        }

        /// <summary>
        /// 获取字段名称
        /// </summary>
        /// <returns>字段名称</returns>
        public string? GetFieldName()
        {
            return Field?.Name;
        }

        /// <summary>
        /// 获取字符串值
        /// </summary>
        /// <returns>字符串值</returns>
        public string? GetStringValue()
        {
            return Value?.ToString();
        }

        /// <summary>
        /// 获取整数值
        /// </summary>
        /// <returns>整数值，解析失败时返回null</returns>
        public int? GetIntValue()
        {
            if (Value == null)
                return null;

            if (Value is int intValue)
                return intValue;

            if (int.TryParse(Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out int result))
                return result;

            return null;
        }

        /// <summary>
        /// 获取长整型值
        /// </summary>
        /// <returns>长整型值，解析失败时返回null</returns>
        public long? GetLongValue()
        {
            if (Value == null)
                return null;

            if (Value is long longValue)
                return longValue;

            if (long.TryParse(Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out long result))
                return result;

            return null;
        }

        /// <summary>
        /// 获取双精度浮点值
        /// </summary>
        /// <returns>双精度浮点值，解析失败时返回null</returns>
        public double? GetDoubleValue()
        {
            if (Value == null)
                return null;

            if (Value is double doubleValue)
                return doubleValue;

            if (double.TryParse(Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                return result;

            return null;
        }

        /// <summary>
        /// 获取布尔值
        /// </summary>
        /// <returns>布尔值，解析失败时返回null</returns>
        public bool? GetBoolValue()
        {
            if (Value == null)
                return null;

            if (Value is bool boolValue)
                return boolValue;

            if (bool.TryParse(Value.ToString(), out bool result))
                return result;

            return null;
        }

        /// <summary>
        /// 获取日期时间值
        /// </summary>
        /// <returns>日期时间值，解析失败时返回null</returns>
        public DateTime? GetDateTimeValue()
        {
            if (Value == null)
                return null;

            if (Value is DateTime dateTimeValue)
                return dateTimeValue;

            if (DateTime.TryParse(Value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                return result;

            return null;
        }
    }
}
