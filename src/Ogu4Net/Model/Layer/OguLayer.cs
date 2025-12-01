using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Ogu4Net.Common;
using Ogu4Net.Enums;

namespace Ogu4Net.Model.Layer
{
    /// <summary>
    /// OGU图层类
    /// <para>
    /// 统一的GIS图层定义，提供图层名称、坐标系、几何类型、字段定义和要素集合等属性。
    /// 支持JSON序列化/反序列化，以及要素过滤功能。
    /// </para>
    /// </summary>
    public class OguLayer
    {
        /// <summary>
        /// 图层名称
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 图层别名
        /// </summary>
        public string? Alias { get; set; }

        /// <summary>
        /// 空间参考WKID（EPSG代码）
        /// </summary>
        public int? Wkid { get; set; }

        /// <summary>
        /// 空间类型
        /// </summary>
        public GeometryType? GeometryType { get; set; }

        /// <summary>
        /// 容差
        /// </summary>
        public double? Tolerance { get; set; }

        /// <summary>
        /// 字段定义集合
        /// </summary>
        public List<OguField>? Fields { get; set; }

        /// <summary>
        /// 要素集合
        /// </summary>
        public List<OguFeature>? Features { get; set; }

        /// <summary>
        /// 图层元数据
        /// </summary>
        public OguLayerMetadata? Metadata { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public OguLayer()
        {
        }

        /// <summary>
        /// 从JSON字符串解析为OguLayer
        /// </summary>
        /// <param name="json">JSON字符串</param>
        /// <returns>OguLayer对象</returns>
        public static OguLayer? FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore
            };
            return JsonConvert.DeserializeObject<OguLayer>(json, settings);
        }

        /// <summary>
        /// 转换为JSON字符串
        /// </summary>
        /// <returns>JSON字符串</returns>
        public string ToJson()
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Formatting = Formatting.None
            };
            return JsonConvert.SerializeObject(this, settings);
        }

        /// <summary>
        /// 转换为格式化的JSON字符串
        /// </summary>
        /// <returns>格式化的JSON字符串</returns>
        public string ToJsonFormatted()
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Formatting = Formatting.Indented
            };
            return JsonConvert.SerializeObject(this, settings);
        }

        /// <summary>
        /// 验证图层数据完整性
        /// </summary>
        /// <exception cref="InvalidOperationException">验证失败时抛出异常</exception>
        public void Validate()
        {
            if (GeometryType == null)
            {
                throw new InvalidOperationException("未获取到几何类型");
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidOperationException("未获取到图层名称");
            }

            if (Wkid == null)
            {
                throw new InvalidOperationException("未获取到坐标系");
            }

            if (Tolerance == null)
            {
                Tolerance = CrsUtil.GetTolerance(Wkid.Value);
            }
        }

        /// <summary>
        /// 应用要素过滤器
        /// </summary>
        /// <param name="filter">过滤器函数</param>
        /// <returns>过滤后的要素列表</returns>
        public List<OguFeature> Filter(OguFeatureFilter filter)
        {
            if (Features == null || Features.Count == 0)
                return new List<OguFeature>();

            return Features.Where(f => filter(f)).ToList();
        }

        /// <summary>
        /// 应用要素过滤器（使用Func委托）
        /// </summary>
        /// <param name="predicate">过滤条件</param>
        /// <returns>过滤后的要素列表</returns>
        public List<OguFeature> Filter(Func<OguFeature, bool> predicate)
        {
            if (Features == null || Features.Count == 0)
                return new List<OguFeature>();

            return Features.Where(predicate).ToList();
        }

        /// <summary>
        /// 获取要素数量
        /// </summary>
        /// <returns>要素数量</returns>
        public int GetFeatureCount()
        {
            return Features?.Count ?? 0;
        }

        /// <summary>
        /// 获取字段数量
        /// </summary>
        /// <returns>字段数量</returns>
        public int GetFieldCount()
        {
            return Fields?.Count ?? 0;
        }

        /// <summary>
        /// 根据字段名称获取字段定义
        /// </summary>
        /// <param name="fieldName">字段名称</param>
        /// <returns>字段定义，null表示不存在</returns>
        public OguField? GetField(string fieldName)
        {
            if (Fields == null || Fields.Count == 0)
                return null;

            return Fields.FirstOrDefault(f =>
                f.Name != null && f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 根据索引获取要素
        /// </summary>
        /// <param name="index">索引</param>
        /// <returns>要素，null表示索引越界</returns>
        public OguFeature? GetFeature(int index)
        {
            if (Features == null || index < 0 || index >= Features.Count)
                return null;

            return Features[index];
        }
    }
}
