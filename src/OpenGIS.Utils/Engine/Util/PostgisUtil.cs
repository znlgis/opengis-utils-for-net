using System;
using System.Collections.Generic;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Engine.Util;

/// <summary>
///     PostGIS 工具类
/// </summary>
public static class PostgisUtil
{
    /// <summary>
    ///     读取 PostGIS 表
    /// </summary>
    public static OguLayer ReadPostGIS(string connectionString, string tableName, string? filter = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));

        // 确保 GDAL 已初始化
        GdalConfiguration.ConfigureGdal();

        // 使用 OGR PostgreSQL 驱动
        // 格式: PG:"host=localhost dbname=database user=user password=password"
        var reader = new GdalReader();
        return reader.Read(connectionString, tableName, filter);
    }

    /// <summary>
    ///     写入 PostGIS 表
    /// </summary>
    public static void WritePostGIS(OguLayer layer, string connectionString, string tableName)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));

        // 确保 GDAL 已初始化
        GdalConfiguration.ConfigureGdal();

        // 使用 OGR PostgreSQL 驱动
        var writer = new GdalWriter();
        var options = new Dictionary<string, object> { { "driver", "PostgreSQL" } };
        writer.Write(layer, connectionString, tableName, options);
    }

    /// <summary>
    ///     判断表是否存在
    /// </summary>
    public static bool TableExists(string connectionString, string tableName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));

        try
        {
            // 尝试获取图层名称来判断表是否存在
            var reader = new GdalReader();
            var layerNames = reader.GetLayerNames(connectionString);
            return layerNames.Contains(tableName);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     创建空间索引
    /// </summary>
    public static void CreateSpatialIndex(string connectionString, string tableName, string geomColumn = "geom")
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));

        // 空间索引创建需要直接执行 SQL
        // 这里提供基本实现，实际使用时需要 Npgsql 或通过 GDAL ExecuteSQL
        // 由于我们依赖 GDAL，可以使用 OGR 的 ExecuteSQL 功能
        // 但这需要打开数据源并执行 SQL，这里提供简单的占位实现

        // 注意：实际的空间索引创建应该通过 PostgreSQL 客户端库执行
        // CREATE INDEX idx_tablename_geom ON tablename USING GIST (geom);
        throw new NotSupportedException(
            "Creating spatial index requires direct database access. Use PostgreSQL client to execute: CREATE INDEX idx_" +
            tableName + "_" + geomColumn + " ON " + tableName + " USING GIST (" + geomColumn + ");");
    }
}
