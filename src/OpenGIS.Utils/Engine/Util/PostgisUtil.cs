using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;
using OSGeo.OGR;

namespace OpenGIS.Utils.Engine.Util;

/// <summary>
///     PostGIS 工具类
/// </summary>
public static class PostgisUtil
{
    private static readonly ILogger Logger = OguLogging.CreateLogger("OpenGIS.Utils.Engine.Util.PostgisUtil");

    /// <summary>
    ///     读取 PostGIS 表
    /// </summary>
    /// <param name="connectionString">PostgreSQL 连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <param name="filter">属性过滤条件，可为 null</param>
    /// <returns>图层对象</returns>
    /// <exception cref="ArgumentException">当连接字符串或表名为空时抛出</exception>
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
    /// <param name="layer">图层对象</param>
    /// <param name="connectionString">PostgreSQL 连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <exception cref="ArgumentNullException">当图层为 null 时抛出</exception>
    /// <exception cref="ArgumentException">当连接字符串或表名为空时抛出</exception>
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
    /// <param name="connectionString">PostgreSQL 连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <returns>如果表存在返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentException">当连接字符串或表名为空时抛出</exception>
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
        catch (System.Exception ex)
        {
            Logger.LogWarning(ex, "检查表是否存在时出错，返回 false (table={TableName})", tableName);
            return false;
        }
    }

    /// <summary>
    ///     创建空间索引
    /// </summary>
    /// <param name="connectionString">PostgreSQL 连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <param name="geomColumn">几何列名，默认为 "geom"</param>
    /// <exception cref="ArgumentException">当连接字符串或表名为空时抛出</exception>
    /// <exception cref="DataSourceException">当 PostgreSQL 数据源或空间索引创建失败时抛出</exception>
    /// <remarks>
    ///     方法通过 GDAL/OGR 的 PostgreSQL 驱动以读写模式打开数据源，并执行 GIST 索引 DDL。
    ///     表名和几何列名仅允许字母、数字和下划线；当前实现不支持带引号、模式限定或其他复杂标识符。
    ///     运行环境必须提供可用的 GDAL PostgreSQL 驱动、数据库连接和创建索引所需权限。
    /// </remarks>
    public static void CreateSpatialIndex(string connectionString, string tableName, string geomColumn = "geom")
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
        ValidateIdentifier(tableName, nameof(tableName));
        ValidateIdentifier(geomColumn, nameof(geomColumn));

        GdalConfiguration.ConfigureGdal();
        using var dataSource = Ogr.Open(connectionString, 1);
        if (dataSource == null)
            throw new DataSourceException("Failed to open PostgreSQL data source");

        var indexName = $"idx_{tableName}_{geomColumn}";
        var sql = $"CREATE INDEX \"{indexName}\" ON \"{tableName}\" USING GIST (\"{geomColumn}\")";
        try
        {
            using var result = dataSource.ExecuteSQL(sql, null, null);
        }
        catch (System.Exception ex)
        {
            if (ex is DataSourceException)
                throw;
            throw new DataSourceException($"Failed to create spatial index for table '{tableName}'", ex);
        }
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
            throw new ArgumentException("Identifier may contain only letters, digits, and underscores", parameterName);
    }
}
