using System.Collections.Generic;

namespace Ogu4Net.Model.Layer
{
    /// <summary>
    /// OGU图层元数据类
    /// <para>
    /// 用于存储图层的扩展属性信息，如坐标系参数、数据来源、投影类型等。
    /// 主要用于国土TXT坐标文件的属性描述部分。
    /// </para>
    /// </summary>
    public class OguLayerMetadata
    {
        /// <summary>
        /// 格式版本号
        /// </summary>
        public string? FormatVersion { get; set; }

        /// <summary>
        /// 数据产生单位
        /// </summary>
        public string? DataSource { get; set; }

        /// <summary>
        /// 数据产生日期
        /// </summary>
        public string? DataDate { get; set; }

        /// <summary>
        /// 坐标系名称
        /// </summary>
        public string? CoordinateSystemName { get; set; }

        /// <summary>
        /// 几度分带（3度或6度分带）
        /// </summary>
        public string? ZoneDivision { get; set; }

        /// <summary>
        /// 投影类型
        /// </summary>
        public string? ProjectionType { get; set; }

        /// <summary>
        /// 计量单位
        /// </summary>
        public string? MeasureUnit { get; set; }

        /// <summary>
        /// 带号
        /// </summary>
        public string? ZoneNumber { get; set; }

        /// <summary>
        /// 精度
        /// </summary>
        public string? Precision { get; set; }

        /// <summary>
        /// 转换参数
        /// </summary>
        public string? TransformParams { get; set; }

        /// <summary>
        /// 扩展信息集合
        /// </summary>
        public List<ExtendedInfo>? ExtendedInfos { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public OguLayerMetadata()
        {
        }

        /// <summary>
        /// 完整构造函数
        /// </summary>
        public OguLayerMetadata(
            string? formatVersion,
            string? dataSource,
            string? dataDate,
            string? coordinateSystemName,
            string? zoneDivision,
            string? projectionType,
            string? measureUnit,
            string? zoneNumber,
            string? precision,
            string? transformParams,
            List<ExtendedInfo>? extendedInfos)
        {
            FormatVersion = formatVersion;
            DataSource = dataSource;
            DataDate = dataDate;
            CoordinateSystemName = coordinateSystemName;
            ZoneDivision = zoneDivision;
            ProjectionType = projectionType;
            MeasureUnit = measureUnit;
            ZoneNumber = zoneNumber;
            Precision = precision;
            TransformParams = transformParams;
            ExtendedInfos = extendedInfos;
        }

        /// <summary>
        /// 扩展信息类
        /// </summary>
        public class ExtendedInfo
        {
            /// <summary>
            /// 扩展信息名称
            /// </summary>
            public string? Name { get; set; }

            /// <summary>
            /// 扩展信息字段
            /// </summary>
            public Dictionary<string, string>? Properties { get; set; }

            /// <summary>
            /// 默认构造函数
            /// </summary>
            public ExtendedInfo()
            {
            }

            /// <summary>
            /// 构造函数
            /// </summary>
            public ExtendedInfo(string? name, Dictionary<string, string>? properties = null)
            {
                Name = name;
                Properties = properties;
            }
        }
    }
}
