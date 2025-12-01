using System.Collections.Generic;
using System.Linq;

namespace Ogu4Net.Model.Layer
{
    /// <summary>
    /// OGU要素类
    /// <para>
    /// 表示GIS图层中的一个地理要素，包含要素ID、几何信息（WKT格式）和属性值集合。
    /// 提供属性值的获取和设置方法。
    /// </para>
    /// </summary>
    public class OguFeature
    {
        /// <summary>
        /// 要素ID
        /// 注意：不同GIS系统对FID或OID的定义都不同，
        /// 此处只是如实返回，没有做兼容，
        /// 所以除非确定，最好不要把此字段当作OBJECTID用，推荐直接用属性过滤和查找
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// 要素图形WKT (Well-Known Text)
        /// </summary>
        public string? Geometry { get; set; }

        /// <summary>
        /// 要素属性值集合
        /// </summary>
        public List<OguFieldValue>? Attributes { get; set; }

        /// <summary>
        /// 坐标点集合（用于TXT格式等特殊场景）
        /// </summary>
        public List<OguCoordinate>? Coordinates { get; set; }

        /// <summary>
        /// 原始属性值列表（用于TXT格式等特殊场景）
        /// </summary>
        public List<string>? RawValues { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public OguFeature()
        {
        }

        /// <summary>
        /// 完整构造函数
        /// </summary>
        public OguFeature(string? id, string? geometry, List<OguFieldValue>? attributes, List<OguCoordinate>? coordinates = null, List<string>? rawValues = null)
        {
            Id = id;
            Geometry = geometry;
            Attributes = attributes;
            Coordinates = coordinates;
            RawValues = rawValues;
        }

        /// <summary>
        /// 获取指定名称的属性值对象
        /// </summary>
        /// <param name="fieldName">要获取的字段名称</param>
        /// <returns>属性值对象，null表示该字段不存在</returns>
        public OguFieldValue? GetAttribute(string fieldName)
        {
            if (Attributes == null || Attributes.Count == 0)
                return null;

            return Attributes.FirstOrDefault(attr =>
                attr.Field != null &&
                attr.Field.Name != null &&
                attr.Field.Name.Equals(fieldName, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取指定字段的值
        /// </summary>
        /// <param name="fieldName">要获取的字段名称</param>
        /// <returns>字段的值，null表示字段不存在或值为空</returns>
        public object? GetValue(string fieldName)
        {
            var attr = GetAttribute(fieldName);
            return attr?.Value;
        }

        /// <summary>
        /// 设置指定字段的值
        /// </summary>
        /// <param name="fieldName">字段名称</param>
        /// <param name="value">字段值</param>
        /// <returns>是否设置成功</returns>
        public bool SetValue(string fieldName, object? value)
        {
            var attr = GetAttribute(fieldName);
            if (attr != null)
            {
                attr.Value = value;
                return true;
            }
            return false;
        }
    }
}
