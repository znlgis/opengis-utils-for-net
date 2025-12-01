using Ogu4Net.Enums;

namespace Ogu4Net.Model.Layer
{
    /// <summary>
    /// OGU字段定义类
    /// <para>
    /// 表示图层中的一个字段的元数据信息，包含字段名称、别名、数据类型等属性。
    /// </para>
    /// </summary>
    public class OguField
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 字段别名
        /// </summary>
        public string? Alias { get; set; }

        /// <summary>
        /// 字段描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 字段数据类型
        /// </summary>
        public FieldDataType? DataType { get; set; }

        /// <summary>
        /// 字段长度（用于字符串类型）
        /// </summary>
        public int? Length { get; set; }

        /// <summary>
        /// 是否可为空
        /// </summary>
        public bool? Nullable { get; set; }

        /// <summary>
        /// 默认值
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public OguField()
        {
        }

        /// <summary>
        /// 简化构造函数
        /// </summary>
        /// <param name="name">字段名称</param>
        /// <param name="alias">字段别名</param>
        /// <param name="dataType">字段数据类型</param>
        public OguField(string name, string? alias, FieldDataType dataType)
        {
            Name = name;
            Alias = alias;
            DataType = dataType;
        }

        /// <summary>
        /// 简化构造函数（含描述）
        /// </summary>
        /// <param name="name">字段名称</param>
        /// <param name="alias">字段别名</param>
        /// <param name="description">字段描述</param>
        /// <param name="dataType">字段数据类型</param>
        public OguField(string name, string? alias, string? description, FieldDataType dataType)
        {
            Name = name;
            Alias = alias;
            Description = description;
            DataType = dataType;
        }

        /// <summary>
        /// 完整构造函数
        /// </summary>
        public OguField(string? name, string? alias, string? description, FieldDataType? dataType, int? length, bool? nullable, object? defaultValue)
        {
            Name = name;
            Alias = alias;
            Description = description;
            DataType = dataType;
            Length = length;
            Nullable = nullable;
            DefaultValue = defaultValue;
        }
    }
}
