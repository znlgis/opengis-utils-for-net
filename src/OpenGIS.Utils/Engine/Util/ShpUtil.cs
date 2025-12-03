using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Utils;
using OpenGIS.Utils.Configuration;
using OSGeo.OGR;
using OgrDataSource = OSGeo.OGR.DataSource;

namespace OpenGIS.Utils.Engine.Util;

/// <summary>
///     Shapefile 工具类
/// </summary>
public static class ShpUtil
{
    /// <summary>
    ///     读取 Shapefile
    /// </summary>
    public static OguLayer ReadShapefile(string shpPath, Encoding? encoding = null)
    {
        if (!File.Exists(shpPath))
            throw new FileNotFoundException("Shapefile not found", shpPath);

        // 使用 GdalReader 读取
        var reader = new GdalReader();
        return reader.Read(shpPath, null, null, null,
            new Dictionary<string, object> { { "encoding", encoding ?? GetShapefileEncoding(shpPath) } });
    }

    /// <summary>
    ///     写入 Shapefile
    /// </summary>
    public static void WriteShapefile(OguLayer layer, string shpPath, Encoding? encoding = null)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));
        if (string.IsNullOrWhiteSpace(shpPath))
            throw new ArgumentException("Path cannot be null or empty", nameof(shpPath));

        // 使用 GdalWriter 写入
        var writer = new GdalWriter();
        writer.Write(layer, shpPath, null,
            new Dictionary<string, object> { { "encoding", encoding ?? Encoding.UTF8 } });
    }

    /// <summary>
    ///     获取 Shapefile 编码
    /// </summary>
    public static Encoding GetShapefileEncoding(string shpPath)
    {
        if (!File.Exists(shpPath))
            throw new FileNotFoundException("Shapefile not found", shpPath);

        var cpgPath = Path.ChangeExtension(shpPath, ".cpg");
        if (File.Exists(cpgPath))
        {
            var cpgContent = File.ReadAllText(cpgPath).Trim();

            // 尝试根据 CPG 文件内容确定编码
            if (cpgContent.Contains("UTF-8", StringComparison.OrdinalIgnoreCase))
                return Encoding.UTF8;
            if (cpgContent.Contains("GBK", StringComparison.OrdinalIgnoreCase))
                return Encoding.GetEncoding("GBK");
            if (cpgContent.Contains("GB2312", StringComparison.OrdinalIgnoreCase))
                return Encoding.GetEncoding("GB2312");
        }

        // 如果没有 CPG 文件，尝试从 DBF 文件检测
        var dbfPath = Path.ChangeExtension(shpPath, ".dbf");
        if (File.Exists(dbfPath)) return EncodingUtil.GetFileEncoding(dbfPath);

        return Encoding.UTF8;
    }

    /// <summary>
    ///     创建 CPG 文件
    /// </summary>
    public static void CreateCpgFile(string shpPath, Encoding encoding)
    {
        if (string.IsNullOrWhiteSpace(shpPath))
            throw new ArgumentException("Path cannot be null or empty", nameof(shpPath));
        if (encoding == null)
            throw new ArgumentNullException(nameof(encoding));

        var cpgPath = Path.ChangeExtension(shpPath, ".cpg");

        string encodingName = encoding.WebName.ToUpper();
        if (encodingName == "GB2312" || encodingName == "GBK")
            encodingName = "GBK";

        File.WriteAllText(cpgPath, encodingName);
    }

    /// <summary>
    ///     获取 Shapefile 边界
    /// </summary>
    public static OSGeo.OGR.Envelope? GetShapefileBounds(string shpPath)
    {
        if (!File.Exists(shpPath))
            throw new FileNotFoundException("Shapefile not found", shpPath);

        GdalConfiguration.ConfigureGdal();
        
        OgrDataSource? dataSource = null;
        try
        {
            dataSource = Ogr.Open(shpPath, 0);
            if (dataSource == null)
                return null;
                
            var layer = dataSource.GetLayerByIndex(0);
            if (layer == null)
                return null;
                
            var envelope = new OSGeo.OGR.Envelope();
            layer.GetExtent(envelope, 1);
            
            return envelope;
        }
        finally
        {
            dataSource?.Dispose();
        }
    }

    /// <summary>
    ///     修复 Shapefile
    /// </summary>
    public static void RepairShapefile(string shpPath)
    {
        if (!File.Exists(shpPath))
            throw new FileNotFoundException("Shapefile not found", shpPath);

        // 读取并重新写入 Shapefile 可以修复一些问题
        var layer = ReadShapefile(shpPath);
        var tempPath = Path.Combine(Path.GetDirectoryName(shpPath) ?? "",
            Path.GetFileNameWithoutExtension(shpPath) + "_temp.shp");

        try
        {
            WriteShapefile(layer, tempPath);

            // 删除原文件
            var extensions = new[] { ".shp", ".shx", ".dbf", ".prj", ".cpg" };
            foreach (var ext in extensions)
            {
                var file = Path.ChangeExtension(shpPath, ext);
                if (File.Exists(file))
                    File.Delete(file);
            }

            // 重命名临时文件
            foreach (var ext in extensions)
            {
                var tempFile = Path.ChangeExtension(tempPath, ext);
                var targetFile = Path.ChangeExtension(shpPath, ext);
                if (File.Exists(tempFile))
                    File.Move(tempFile, targetFile);
            }
        }
        finally
        {
            // 清理临时文件
            var extensions = new[] { ".shp", ".shx", ".dbf", ".prj", ".cpg" };
            foreach (var ext in extensions)
            {
                var tempFile = Path.ChangeExtension(tempPath, ext);
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
    }
}
