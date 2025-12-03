using System;
using System.Collections.Generic;
using System.IO;
using OSGeo.OGR;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.IO;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Engine.Util;
using OgrDataSource = OSGeo.OGR.DataSource;
using SysException = System.Exception;

namespace OpenGIS.Utils.Engine
{
    /// <summary>
    /// GDAL 写入器
    /// </summary>
    public class GdalWriter : ILayerWriter
    {
        static GdalWriter()
        {
            // 确保 GDAL 已初始化
            GdalConfiguration.ConfigureGdal();
        }

        /// <summary>
        /// 写入图层
        /// </summary>
        public void Write(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null)
        {
            if (layer == null)
                throw new ArgumentNullException(nameof(layer));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            // 推断驱动名称
            string driverName = InferDriverName(path, options);
            var driver = Ogr.GetDriverByName(driverName);
            
            if (driver == null)
                throw new SysException($"Driver '{driverName}' not available");

            // 确保目录存在
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 删除已存在的文件
            if (File.Exists(path) || Directory.Exists(path))
            {
                try
                {
                    driver.DeleteDataSource(path);
                }
                catch
                {
                    // 忽略删除错误
                }
            }

            OgrDataSource? dataSource = null;
            try
            {
                // 创建数据源
                dataSource = driver.CreateDataSource(path, new string[] { });
                
                if (dataSource == null)
                    throw new SysException($"Failed to create data source: {path}");

                // 创建图层
                var ogrGeomType = MapToOgrGeometryType(layer.GeometryType);
                var ogrLayer = dataSource.CreateLayer(
                    layerName ?? layer.Name ?? "layer", 
                    null, 
                    ogrGeomType, 
                    new string[] { });

                if (ogrLayer == null)
                    throw new SysException("Failed to create layer");

                // 创建字段
                foreach (var field in layer.Fields)
                {
                    var fieldDefn = CreateOgrFieldDefn(field);
                    ogrLayer.CreateField(fieldDefn, 1);
                    fieldDefn.Dispose();
                }

                // 写入要素
                foreach (var oguFeature in layer.Features)
                {
                    if (string.IsNullOrWhiteSpace(oguFeature.Wkt))
                        continue;

                    Feature? ogrFeature = null;
                    OSGeo.OGR.Geometry? geometry = null;

                    try
                    {
                        // 创建要素
                        ogrFeature = new Feature(ogrLayer.GetLayerDefn());

                        // 设置几何
                        geometry = OSGeo.OGR.Geometry.CreateFromWkt(oguFeature.Wkt);
                        if (geometry != null)
                        {
                            ogrFeature.SetGeometry(geometry);
                        }

                        // 设置属性
                        foreach (var field in layer.Fields)
                        {
                            var fieldIndex = ogrFeature.GetFieldIndex(field.Name);
                            if (fieldIndex >= 0)
                            {
                                var value = oguFeature.GetValue(field.Name);
                                SetFieldValue(ogrFeature, fieldIndex, value, field.DataType);
                            }
                        }

                        // 添加要素到图层
                        if (ogrLayer.CreateFeature(ogrFeature) != 0)
                        {
                            Console.WriteLine($"Warning: Failed to create feature {oguFeature.Fid}");
                        }
                    }
                    catch (SysException ex)
                    {
                        Console.WriteLine($"Warning: Error writing feature {oguFeature?.Fid}: {ex.Message}");
                    }
                    finally
                    {
                        geometry?.Dispose();
                        ogrFeature?.Dispose();
                    }
                }

                // 同步到磁盘
                dataSource.SyncToDisk();
            }
            finally
            {
                dataSource?.Dispose();
            }
        }

        /// <summary>
        /// 追加要素到已存在的图层
        /// </summary>
        public void Append(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null)
        {
            // TODO: Implement append functionality using GDAL
            throw new NotImplementedException("GdalWriter.Append is not yet implemented");
        }

        private string InferDriverName(string path, Dictionary<string, object>? options)
        {
            // 从选项中获取驱动名称
            if (options != null && options.TryGetValue("driver", out var driverObj))
            {
                return driverObj.ToString() ?? "ESRI Shapefile";
            }

            // 根据扩展名推断
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension switch
            {
                ".shp" => "ESRI Shapefile",
                ".gdb" => "FileGDB",
                ".gpkg" => "GPKG",
                ".kml" => "KML",
                ".dxf" => "DXF",
                ".geojson" or ".json" => "GeoJSON",
                _ => "ESRI Shapefile"
            };
        }

        private wkbGeometryType MapToOgrGeometryType(Enums.GeometryType geomType)
        {
            return geomType switch
            {
                Enums.GeometryType.POINT => wkbGeometryType.wkbPoint,
                Enums.GeometryType.LINESTRING => wkbGeometryType.wkbLineString,
                Enums.GeometryType.POLYGON => wkbGeometryType.wkbPolygon,
                Enums.GeometryType.MULTIPOINT => wkbGeometryType.wkbMultiPoint,
                Enums.GeometryType.MULTILINESTRING => wkbGeometryType.wkbMultiLineString,
                Enums.GeometryType.MULTIPOLYGON => wkbGeometryType.wkbMultiPolygon,
                Enums.GeometryType.GEOMETRYCOLLECTION => wkbGeometryType.wkbGeometryCollection,
                _ => wkbGeometryType.wkbUnknown
            };
        }

        private FieldDefn CreateOgrFieldDefn(OguField field)
        {
            var ogrType = MapToOgrFieldType(field.DataType);
            var fieldDefn = new FieldDefn(field.Name, ogrType);

            if (field.Length.HasValue && field.Length.Value > 0)
            {
                fieldDefn.SetWidth(field.Length.Value);
            }

            if (field.Precision.HasValue && field.Precision.Value > 0)
            {
                fieldDefn.SetPrecision(field.Precision.Value);
            }

            return fieldDefn;
        }

        private FieldType MapToOgrFieldType(FieldDataType dataType)
        {
            return dataType switch
            {
                FieldDataType.INTEGER => FieldType.OFTInteger,
                FieldDataType.LONG => FieldType.OFTInteger64,
                FieldDataType.DOUBLE or FieldDataType.FLOAT => FieldType.OFTReal,
                FieldDataType.STRING => FieldType.OFTString,
                FieldDataType.DATE => FieldType.OFTDate,
                FieldDataType.DATETIME => FieldType.OFTDateTime,
                FieldDataType.BINARY => FieldType.OFTBinary,
                _ => FieldType.OFTString
            };
        }

        private void SetFieldValue(Feature feature, int fieldIndex, object? value, FieldDataType dataType)
        {
            if (value == null)
            {
                feature.UnsetField(fieldIndex);
                return;
            }

            switch (dataType)
            {
                case FieldDataType.INTEGER:
                    feature.SetField(fieldIndex, Convert.ToInt32(value));
                    break;
                case FieldDataType.LONG:
                    feature.SetField(fieldIndex, Convert.ToInt64(value));
                    break;
                case FieldDataType.DOUBLE:
                case FieldDataType.FLOAT:
                    feature.SetField(fieldIndex, Convert.ToDouble(value));
                    break;
                case FieldDataType.DATE:
                case FieldDataType.DATETIME:
                    if (value is DateTime dt)
                    {
                        feature.SetField(fieldIndex, dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, 0);
                    }
                    break;
                default:
                    feature.SetField(fieldIndex, value.ToString());
                    break;
            }
        }
    }
}
