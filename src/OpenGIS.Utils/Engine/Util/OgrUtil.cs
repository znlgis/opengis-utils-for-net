using System;
using System.Collections.Generic;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Configuration;

namespace OpenGIS.Utils.Engine.Util
{
    /// <summary>
    /// OGR 工具类（基于 GDAL/OGR）
    /// </summary>
    public static class OgrUtil
    {
        /// <summary>
        /// 读取 OGR 数据源
        /// </summary>
        public static OguLayer ReadOgrDataSource(string path, string? layerName = null, string? filter = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            // 确保 GDAL 已初始化
            GdalConfiguration.ConfigureGdal();

            // 使用 GdalReader 读取
            var reader = new GdalReader();
            return reader.Read(path, layerName, filter, null, null);
        }

        /// <summary>
        /// 写入 OGR 数据源
        /// </summary>
        public static void WriteOgrDataSource(OguLayer layer, string path, string? layerName = null, string? driverName = null)
        {
            if (layer == null)
                throw new ArgumentNullException(nameof(layer));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            // 确保 GDAL 已初始化
            GdalConfiguration.ConfigureGdal();

            // 使用 GdalWriter 写入
            var writer = new GdalWriter();
            var options = driverName != null 
                ? new Dictionary<string, object> { { "driver", driverName } }
                : null;
            writer.Write(layer, path, layerName, options);
        }

        /// <summary>
        /// 获取图层名称列表
        /// </summary>
        public static IList<string> GetLayerNames(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            // 确保 GDAL 已初始化
            GdalConfiguration.ConfigureGdal();

            // 使用 GdalReader 获取图层名称
            var reader = new GdalReader();
            return reader.GetLayerNames(path);
        }

        /// <summary>
        /// 根据格式获取驱动名称
        /// </summary>
        public static string GetDriverName(DataFormatType format)
        {
            return format switch
            {
                DataFormatType.SHP => "ESRI Shapefile",
                DataFormatType.GEOJSON => "GeoJSON",
                DataFormatType.FILEGDB => "FileGDB",
                DataFormatType.GEOPACKAGE => "GPKG",
                DataFormatType.KML => "KML",
                DataFormatType.DXF => "DXF",
                _ => throw new ArgumentException($"No OGR driver for format {format}", nameof(format))
            };
        }
    }
}
