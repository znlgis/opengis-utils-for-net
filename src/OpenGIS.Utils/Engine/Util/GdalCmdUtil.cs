using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OSGeo.OGR;
using OpenGIS.Utils.Engine.Model;
using OpenGIS.Utils.Configuration;
using SysException = System.Exception;

namespace OpenGIS.Utils.Engine.Util
{
    /// <summary>
    /// GDAL 命令行工具类
    /// </summary>
    public static class GdalCmdUtil
    {
        /// <summary>
        /// 获取 GDB 数据结构
        /// </summary>
        public static GdbGroupModel GetGdbDataStructure(string gdbPath)
        {
            if (string.IsNullOrWhiteSpace(gdbPath))
                throw new ArgumentException("GDB path cannot be null or empty", nameof(gdbPath));

            // 确保 GDAL 已初始化
            GdalConfiguration.ConfigureGdal();

            var model = new GdbGroupModel
            {
                GdbPath = gdbPath
            };

            using var dataSource = Ogr.Open(gdbPath, 0);
            if (dataSource == null)
                throw new SysException($"Failed to open GDB: {gdbPath}");

            // 读取所有图层
            for (int i = 0; i < dataSource.GetLayerCount(); i++)
            {
                using var layer = dataSource.GetLayerByIndex(i);
                if (layer != null)
                {
                    model.LayerNames.Add(layer.GetName());
                }
            }

            return model;
        }

        /// <summary>
        /// 执行 ogrinfo 命令
        /// </summary>
        public static string ExecuteOgrInfo(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            // 确保 GDAL 已初始化
            GdalConfiguration.ConfigureGdal();

            var sb = new StringBuilder();

            using var dataSource = Ogr.Open(path, 0);
            if (dataSource == null)
            {
                return $"ERROR: Failed to open data source: {path}";
            }

            sb.AppendLine($"Data source: {path}");
            sb.AppendLine($"Layer count: {dataSource.GetLayerCount()}");
            sb.AppendLine();

            for (int i = 0; i < dataSource.GetLayerCount(); i++)
            {
                using var layer = dataSource.GetLayerByIndex(i);
                if (layer == null) continue;

                sb.AppendLine($"Layer #{i + 1}: {layer.GetName()}");
                sb.AppendLine($"  Geometry: {layer.GetGeomType()}");
                sb.AppendLine($"  Feature count: {layer.GetFeatureCount(1)}");
                
                var layerDefn = layer.GetLayerDefn();
                sb.AppendLine($"  Field count: {layerDefn.GetFieldCount()}");
                
                for (int j = 0; j < layerDefn.GetFieldCount(); j++)
                {
                    var fieldDefn = layerDefn.GetFieldDefn(j);
                    sb.AppendLine($"    Field #{j + 1}: {fieldDefn.GetName()} ({fieldDefn.GetFieldType()})");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// 执行 gdalinfo 命令
        /// </summary>
        public static string ExecuteGdalInfo(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            // 确保 GDAL 已初始化
            GdalConfiguration.ConfigureGdal();

            // gdalinfo 主要用于栅格数据
            // 对于矢量数据，使用 ogrinfo
            return ExecuteOgrInfo(path);
        }
    }
}
