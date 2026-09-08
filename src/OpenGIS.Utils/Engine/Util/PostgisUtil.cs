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
    ///     GDAL PostgreSQL 驱动建表时使用的默认几何列名
    /// </summary>
    private const string DefaultGeometryColumn = "wkb_geometry";

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
        // 格式: PG:host=localhost port=5432 dbname=database user=user password=password
        // 注意: 整个连接串不能加引号，GDAL 会把引号当作参数名的一部分而连接失败
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
    /// <returns>数据源中存在该表时返回 true，数据源可正常打开但不含该表时返回 false</returns>
    /// <exception cref="ArgumentException">当连接字符串或表名为空时抛出</exception>
    /// <exception cref="DataSourceException">当数据库无法连接或连接字符串不被 GDAL 接受时抛出</exception>
    /// <remarks>
    ///     连接失败与"表不存在"是两种不同结果：前者意味着检查没有完成，必须让调用方感知，
    ///     静默返回 false 会让上层误判为可以安全建表。
    /// </remarks>
    public static bool TableExists(string connectionString, string tableName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));

        GdalConfiguration.ConfigureGdal();

        OSGeo.OGR.DataSource? dataSource;
        try
        {
            dataSource = Ogr.Open(connectionString, 0);
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning(ex, "打开 PostgreSQL 数据源失败 (table={TableName})", tableName);
            throw new DataSourceException("Failed to open PostgreSQL data source", ex);
        }

        if (dataSource == null)
            throw new DataSourceException("Failed to open PostgreSQL data source");

        using (dataSource)
        {
            var layerCount = dataSource.GetLayerCount();
            for (var i = 0; i < layerCount; i++)
            {
                using var layer = dataSource.GetLayerByIndex(i);
                if (layer != null && layer.GetName() == tableName)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     创建空间索引
    /// </summary>
    /// <param name="connectionString">PostgreSQL 连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <param name="geomColumn">
    ///     几何列名，默认为 GDAL PostgreSQL 驱动建表时使用的 "wkb_geometry"；
    ///     传 null 或空白时自动从 geometry_columns 探测实际列名
    /// </param>
    /// <exception cref="ArgumentException">当连接字符串或表名为空、或标识符含非法字符时抛出</exception>
    /// <exception cref="DataSourceException">当 PostgreSQL 数据源打开失败或空间索引创建失败时抛出</exception>
    /// <remarks>
    ///     方法通过 GDAL/OGR 的 PostgreSQL 驱动以读写模式打开数据源，并执行 CREATE INDEX IF NOT EXISTS 的 GIST 索引 DDL，
    ///     因此对同一张表重复调用是幂等的。
    ///     表名和几何列名仅允许字母、数字和下划线；当前实现不支持带引号、模式限定或其他复杂标识符。
    ///     运行环境必须提供可用的 GDAL PostgreSQL 驱动、数据库连接和创建索引所需权限。
    /// </remarks>
    public static void CreateSpatialIndex(string connectionString, string tableName,
        string? geomColumn = DefaultGeometryColumn)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
        ValidateIdentifier(tableName, nameof(tableName));
        var requestedColumn = string.IsNullOrWhiteSpace(geomColumn) ? null : geomColumn?.Trim();
        if (requestedColumn != null)
            ValidateIdentifier(requestedColumn, nameof(geomColumn));

        GdalConfiguration.ConfigureGdal();
        using var dataSource = Ogr.Open(connectionString, 1);
        if (dataSource == null)
            throw new DataSourceException("Failed to open PostgreSQL data source");

        var column = requestedColumn ?? DetectGeometryColumn(dataSource, tableName);
        var indexName = $"idx_{tableName}_{column}";
        var sql = $"CREATE INDEX IF NOT EXISTS \"{indexName}\" ON \"{tableName}\" USING GIST (\"{column}\")";
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

    private static string DetectGeometryColumn(OSGeo.OGR.DataSource dataSource, string tableName)
    {
        // 表可能由其他工具创建，几何列名未必是驱动默认的 wkb_geometry
        using var result = dataSource.ExecuteSQL(
            $"SELECT f_geometry_column FROM geometry_columns WHERE lower(f_table_name) = lower('{tableName}') LIMIT 1",
            null, null);
        using var feature = result?.GetNextFeature();
        var detected = feature?.GetFieldAsString(0) ?? string.Empty;
        if (detected.Length > 0 && detected.All(c => char.IsLetterOrDigit(c) || c == '_'))
            return detected;

        return DefaultGeometryColumn;
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
            throw new ArgumentException("Identifier may contain only letters, digits, and underscores", parameterName);
    }
}
