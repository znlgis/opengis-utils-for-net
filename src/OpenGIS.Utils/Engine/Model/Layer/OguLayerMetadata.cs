using System;
using System.Collections.Generic;

namespace OpenGIS.Utils.Engine.Model.Layer
{
    /// <summary>
    /// 图层元数据类
    /// </summary>
    public class OguLayerMetadata
    {
        /// <summary>
        /// 数据来源
        /// </summary>
        public string? DataSource { get; set; }

        /// <summary>
        /// 坐标系名称
        /// </summary>
        public string? CoordinateSystemName { get; set; }

        /// <summary>
        /// 分带
        /// </summary>
        public string? ZoneDivision { get; set; }

        /// <summary>
        /// 投影类型
        /// </summary>
        public string? ProjectionType { get; set; }

        /// <summary>
        /// 测量单位
        /// </summary>
        public string? MeasureUnit { get; set; }

        /// <summary>
        /// 扩展属性
        /// </summary>
        public Dictionary<string, object> ExtendedProperties { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime? CreateTime { get; set; }

        /// <summary>
        /// 修改时间
        /// </summary>
        public DateTime? ModifyTime { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public OguLayerMetadata()
        {
            ExtendedProperties = new Dictionary<string, object>();
        }
    }
}
