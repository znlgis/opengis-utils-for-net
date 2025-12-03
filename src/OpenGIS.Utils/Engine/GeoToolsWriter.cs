using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.IO;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Engine.Util;
using AttributesTable = NetTopologySuite.Features.AttributesTable;
using SysException = System.Exception;

namespace OpenGIS.Utils.Engine
{
    /// <summary>
    /// GeoTools 写入器（基于 NetTopologySuite）
    /// </summary>
    public class GeoToolsWriter : ILayerWriter
    {
        /// <summary>
        /// 写入图层
        /// </summary>
        public void Write(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null)
        {
            if (layer == null)
                throw new ArgumentNullException(nameof(layer));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            var extension = Path.GetExtension(path).ToLowerInvariant();

            switch (extension)
            {
                case ".shp":
                    WriteShapefile(layer, path, options);
                    break;
                case ".geojson":
                case ".json":
                    WriteGeoJson(layer, path);
                    break;
                default:
                    throw new FormatException($"Unsupported file format: {extension}");
            }
        }

        /// <summary>
        /// 追加要素到已存在的图层
        /// </summary>
        public void Append(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null)
        {
            // TODO: Implement append functionality
            throw new NotImplementedException("GeoToolsWriter.Append is not yet implemented");
        }

        private void WriteShapefile(OguLayer layer, string path, Dictionary<string, object>? options)
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 确定编码
            Encoding encoding = Encoding.UTF8;
            if (options != null && options.TryGetValue("encoding", out var encodingObj))
            {
                if (encodingObj is Encoding enc)
                    encoding = enc;
                else if (encodingObj is string encodingName)
                    encoding = Encoding.GetEncoding(encodingName);
            }

            // 创建 DbaseFileHeader
            var header = new DbaseFileHeader(encoding);
            
            foreach (var field in layer.Fields)
            {
                var dbaseField = CreateDbaseField(field);
                header.AddColumn(dbaseField.Name, dbaseField.DbaseType, dbaseField.Length, dbaseField.DecimalCount);
            }

            header.NumRecords = layer.Features.Count;

            // 写入 Shapefile
            var geometryFactory = GeometryFactory.Default;
            var wktReader = new WKTReader();

            var writer = new ShapefileDataWriter(path, geometryFactory, encoding);
            writer.Header = header;

            var features = new List<IFeature>();
            foreach (var oguFeature in layer.Features)
            {
                if (string.IsNullOrWhiteSpace(oguFeature.Wkt))
                    continue;

                try
                {
                    var geometry = wktReader.Read(oguFeature.Wkt);
                    var attributesTable = new AttributesTable();

                    foreach (var field in layer.Fields)
                    {
                        var value = oguFeature.GetValue(field.Name);
                        attributesTable.Add(field.Name, value);
                    }

                    var feature = new Feature(geometry, attributesTable);
                    features.Add(feature);
                }
                catch (SysException ex)
                {
                    // 跳过无效的几何
                    Console.WriteLine($"Warning: Skipping invalid geometry for feature {oguFeature.Fid}: {ex.Message}");
                }
            }

            writer.Write(features);

            // 创建 .cpg 文件
            ShpUtil.CreateCpgFile(path, encoding);
        }

        private void WriteGeoJson(OguLayer layer, string path)
        {
            var geometryFactory = GeometryFactory.Default;
            var wktReader = new WKTReader();
            var features = new List<IFeature>();

            foreach (var oguFeature in layer.Features)
            {
                if (string.IsNullOrWhiteSpace(oguFeature.Wkt))
                    continue;

                try
                {
                    var geometry = wktReader.Read(oguFeature.Wkt);
                    var attributesTable = new AttributesTable();

                    foreach (var field in layer.Fields)
                    {
                        var value = oguFeature.GetValue(field.Name);
                        attributesTable.Add(field.Name, value);
                    }

                    var feature = new Feature(geometry, attributesTable);
                    features.Add(feature);
                }
                catch (SysException ex)
                {
                    Console.WriteLine($"Warning: Skipping invalid geometry for feature {oguFeature.Fid}: {ex.Message}");
                }
            }

            var featureCollection = new FeatureCollection();
            foreach (var feature in features)
            {
                featureCollection.Add(feature);
            }

            var geoJsonWriter = new GeoJsonWriter();
            var geoJson = geoJsonWriter.Write(featureCollection);

            // 确保目录存在
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, geoJson, Encoding.UTF8);
        }

        private (string Name, char DbaseType, int Length, int DecimalCount) CreateDbaseField(OguField field)
        {
            return field.DataType switch
            {
                FieldDataType.STRING => (field.Name, 'C', field.Length ?? 254, 0),
                FieldDataType.INTEGER => (field.Name, 'N', 10, 0),
                FieldDataType.LONG => (field.Name, 'N', 18, 0),
                FieldDataType.DOUBLE => (field.Name, 'N', 19, field.Scale ?? 8),
                FieldDataType.FLOAT => (field.Name, 'F', 19, field.Scale ?? 8),
                FieldDataType.BOOLEAN => (field.Name, 'L', 1, 0),
                FieldDataType.DATE => (field.Name, 'D', 8, 0),
                FieldDataType.DATETIME => (field.Name, 'D', 8, 0),
                _ => (field.Name, 'C', 254, 0)
            };
        }
    }
}
