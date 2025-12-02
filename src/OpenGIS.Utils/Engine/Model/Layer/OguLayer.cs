using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Engine.Model.Layer
{
    /// <summary>
    /// 统一的 GIS 图层定义类
    /// </summary>
    public class OguLayer
    {
        /// <summary>
        /// 图层名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 坐标系 WKID
        /// </summary>
        public int? Wkid { get; set; }

        /// <summary>
        /// 几何类型
        /// </summary>
        public GeometryType GeometryType { get; set; }

        /// <summary>
        /// 字段定义列表
        /// </summary>
        public IList<OguField> Fields { get; set; }

        /// <summary>
        /// 要素集合
        /// </summary>
        public IList<OguFeature> Features { get; set; }

        /// <summary>
        /// 图层元数据
        /// </summary>
        public OguLayerMetadata? Metadata { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public OguLayer()
        {
            Fields = new List<OguField>();
            Features = new List<OguFeature>();
        }

        /// <summary>
        /// 验证图层数据完整性
        /// </summary>
        /// <exception cref="LayerValidationException">数据验证失败时抛出</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new LayerValidationException("Layer name cannot be null or empty");
            }

            if (Fields == null || Fields.Count == 0)
            {
                throw new LayerValidationException("Layer must have at least one field");
            }

            // 验证字段名称唯一性
            var fieldNames = Fields.Select(f => f.Name).ToList();
            if (fieldNames.Distinct().Count() != fieldNames.Count)
            {
                throw new LayerValidationException("Field names must be unique");
            }

            // 验证要素属性与字段定义一致
            foreach (var feature in Features)
            {
                foreach (var fieldName in feature.Attributes.Keys)
                {
                    if (!Fields.Any(f => f.Name == fieldName))
                    {
                        throw new LayerValidationException($"Feature contains attribute '{fieldName}' that is not defined in Fields");
                    }
                }
            }
        }

        /// <summary>
        /// 过滤要素
        /// </summary>
        public IList<OguFeature> Filter(Func<OguFeature, bool> filter)
        {
            return Features.Where(filter).ToList();
        }

        /// <summary>
        /// 获取要素数量
        /// </summary>
        public int GetFeatureCount()
        {
            return Features?.Count ?? 0;
        }

        /// <summary>
        /// 转换为 JSON 字符串
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// 从 JSON 创建
        /// </summary>
        public static OguLayer? FromJson(string json)
        {
            return JsonSerializer.Deserialize<OguLayer>(json);
        }

        /// <summary>
        /// 深拷贝
        /// </summary>
        public OguLayer Clone()
        {
            var clone = new OguLayer
            {
                Name = Name,
                Wkid = Wkid,
                GeometryType = GeometryType
            };

            foreach (var field in Fields)
            {
                clone.Fields.Add(field.Clone());
            }

            foreach (var feature in Features)
            {
                clone.Features.Add(feature.Clone());
            }

            if (Metadata != null)
            {
                clone.Metadata = new OguLayerMetadata
                {
                    DataSource = Metadata.DataSource,
                    CoordinateSystemName = Metadata.CoordinateSystemName,
                    ZoneDivision = Metadata.ZoneDivision,
                    ProjectionType = Metadata.ProjectionType,
                    MeasureUnit = Metadata.MeasureUnit,
                    CreateTime = Metadata.CreateTime,
                    ModifyTime = Metadata.ModifyTime
                };

                foreach (var kvp in Metadata.ExtendedProperties)
                {
                    clone.Metadata.ExtendedProperties[kvp.Key] = kvp.Value;
                }
            }

            return clone;
        }

        /// <summary>
        /// 根据字段名获取字段定义
        /// </summary>
        public OguField? GetField(string fieldName)
        {
            return Fields.FirstOrDefault(f => f.Name == fieldName);
        }

        /// <summary>
        /// 添加字段
        /// </summary>
        public void AddField(OguField field)
        {
            if (Fields.Any(f => f.Name == field.Name))
            {
                throw new LayerValidationException($"Field '{field.Name}' already exists");
            }
            Fields.Add(field);
        }

        /// <summary>
        /// 添加要素
        /// </summary>
        public void AddFeature(OguFeature feature)
        {
            Features.Add(feature);
        }

        /// <summary>
        /// 移除要素
        /// </summary>
        public bool RemoveFeature(int fid)
        {
            var feature = Features.FirstOrDefault(f => f.Fid == fid);
            if (feature != null)
            {
                Features.Remove(feature);
                return true;
            }
            return false;
        }
    }
}
