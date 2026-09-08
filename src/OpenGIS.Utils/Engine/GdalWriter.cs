using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.IO;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;
using OSGeo.OGR;
using OSGeo.OSR;
using OgrDataSource = OSGeo.OGR.DataSource;
using SysException = System.Exception;

namespace OpenGIS.Utils.Engine;

/// <summary>
///     GDAL 写入器
/// </summary>
public class GdalWriter : ILayerWriter
{
    private static readonly ILogger Logger = OguLogging.CreateLogger<GdalWriter>();

    static GdalWriter()
    {
        // 确保 GDAL 已初始化
        GdalConfiguration.ConfigureGdal();
    }

    /// <summary>
    ///     写入图层
    /// </summary>
    /// <param name="layer">图层对象</param>
    /// <param name="path">输出路径</param>
    /// <param name="layerName">图层名称，如果为 null 则使用图层的 Name 属性</param>
    /// <param name="options">附加选项字典，可以包含驱动名称、编码等</param>
    /// <exception cref="ArgumentNullException">当图层为 null 时抛出</exception>
    /// <exception cref="ArgumentException">当路径为空时抛出</exception>
    /// <exception cref="DataSourceException">当驱动不可用或创建数据源失败时抛出</exception>
    /// <remarks>
    ///     如果目标数据源已存在，写入前会尝试删除并重新创建。<paramref name="options"/> 支持
    ///     <c>driver</c> 和 <c>encoding</c> 选项。空几何、无法解析的几何、字段转换失败及要素写入失败会汇总后抛出
    ///     <see cref="DataSourceException"/>。
    /// </remarks>
    public void Write(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        ValidateCollections(layer);

        // 推断驱动名称
        string driverName = InferDriverName(path, options);
        var driver = Ogr.GetDriverByName(driverName);

        if (driver == null)
            throw new DataSourceException($"Driver '{driverName}' not available");

        // 确保目录存在
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        // 删除已存在的文件
        if (File.Exists(path) || Directory.Exists(path))
        {
            try
            {
                if (driver.DeleteDataSource(path) != 0)
                    throw new DataSourceException($"Failed to delete existing data source: {path}");
            }
            catch (SysException ex)
            {
                if (ex is DataSourceException)
                    throw;

                throw new DataSourceException($"Failed to delete existing data source: {path}", ex);
            }
        }

        OgrDataSource? dataSource = null;
        try
        {
            // 创建数据源
            dataSource = driver.CreateDataSource(path, Array.Empty<string>());

            if (dataSource == null)
                throw new DataSourceException($"Failed to create data source: {path}");

            // 创建图层
            var ogrGeomType = ResolveOgrGeometryType(layer);
            var layerOptions = BuildLayerOptions(options, driverName);
            using var spatialReference = CreateSpatialReference(layer.Wkid);
            var ogrLayer = dataSource.CreateLayer(
                layerName ?? layer.Name ?? "layer",
                spatialReference,
                ogrGeomType,
                layerOptions);

            if (ogrLayer == null)
                throw new DataSourceException("Failed to create layer");

            // 创建字段
            foreach (var field in layer.Fields)
            {
                using var fieldDefn = CreateOgrFieldDefn(field);
                try
                {
                    if (ogrLayer.CreateField(fieldDefn, 1) != 0)
                    {
                        if (IsFixedSchemaDriver(driverName))
                        {
                            // DXF 等驱动使用固定 schema，不支持任意字段创建：跳过并告警
                            Logger.LogWarning("驱动 {Driver} 不支持字段 '{Field}'，已跳过", driverName, field.Name);
                            continue;
                        }

                        throw new DataSourceException($"Failed to create field '{field.Name}'");
                    }
                }
                catch (SysException ex)
                {
                    if (ex is DataSourceException)
                        throw;

                    if (IsFixedSchemaDriver(driverName))
                    {
                        Logger.LogWarning(ex, "驱动 {Driver} 不支持字段 '{Field}'，已跳过", driverName, field.Name);
                        continue;
                    }

                    throw new DataSourceException($"Failed to create field '{field.Name}'", ex);
                }
            }

            // 预计算字段索引映射，避免在要素循环内重复调用 GetFieldIndex
            var fieldIndexMap = new Dictionary<string, int>(layer.Fields.Count);
            foreach (var field in layer.Fields)
            {
                var index = ogrLayer.GetLayerDefn().GetFieldIndex(field.Name);
                if (index >= 0) fieldIndexMap[field.Name] = index;
            }

            // 写入要素
            int failedCount = 0;
            // PostgreSQL 的序列主键列允许显式写入 0：若把源 Fid=0 当作"未设置"交给序列自动分配，
            // 序列会先消耗一个值（分到 1），随后所有显式 FID 与已分配的序列值链式相撞 UNIQUE 约束，
            // 触发逐要素"失败→SetFID(-1)重试"，且驱动的事务恢复会重排服务端行序
            //（要素数据不丢失，但读回顺序与 FID 均与源不一致）。GPKG 等驱动的 FID 0 语义
            // 仍是"未设置"，维持旧行为。
            var preserveZeroFid = IsPostgresqlDriver(driverName);
            foreach (var oguFeature in layer.Features)
            {
                if (string.IsNullOrWhiteSpace(oguFeature.Wkt))
                {
                    failedCount++;
                    Logger.LogWarning("跳过空几何要素 (Fid={Fid})", oguFeature.Fid);
                    continue;
                }

                Feature? ogrFeature = null;
                OSGeo.OGR.Geometry? geometry = null;

                try
                {
                    // 创建要素
                    ogrFeature = new Feature(ogrLayer.GetLayerDefn());

                    if ((oguFeature.Fid != 0 || preserveZeroFid) && ogrFeature.SetFID(oguFeature.Fid) != 0)
                        throw new SysException($"设置要素 FID 失败 (Fid={oguFeature.Fid})");

                    // 设置几何
                    geometry = OSGeo.OGR.Geometry.CreateFromWkt(oguFeature.Wkt);
                    if (geometry == null)
                        throw new SysException($"无法解析要素几何 (Fid={oguFeature.Fid})");

                    if (ogrFeature.SetGeometry(geometry) != 0)
                        throw new SysException($"设置要素几何失败 (Fid={oguFeature.Fid})");

                    // 设置属性
                    foreach (var field in layer.Fields)
                    {
                        if (fieldIndexMap.TryGetValue(field.Name, out var fieldIndex))
                        {
                            var value = oguFeature.GetValue(field.Name);
                            SetFieldValue(ogrFeature, fieldIndex, value, field.DataType, field.Name);
                        }
                    }

                    // 添加要素到图层（MaxRev 绑定在插入失败时抛异常而非返回非零）
                    var created = TryCreateFeature(ogrLayer, ogrFeature);
                    if (!created && (oguFeature.Fid != 0 || preserveZeroFid))
                    {
                        // FID 冲突回退：源 Fid 为 0 的要素由驱动自动分配 FID（通常从 1 开始），
                        // 可能与显式指定的源 FID 撞 UNIQUE 约束（GPKG、OpenFileGDB 等）。
                        // 此时放弃保留源 FID，改为自动分配重试一次。
                        // PostgreSQL 下 Fid=0 也是显式保留值，冲突时同样允许回退重试。
                        ogrFeature.SetFID(-1);
                        created = TryCreateFeature(ogrLayer, ogrFeature);
                        if (created)
                            Logger.LogWarning("源 FID 冲突，已改用自动分配 FID (源Fid={Fid})", oguFeature.Fid);
                    }

                    if (!created)
                    {
                        failedCount++;
                        Logger.LogWarning("创建要素失败 (Fid={Fid})", oguFeature.Fid);
                    }
                }
                catch (SysException ex)
                {
                    failedCount++;
                    Logger.LogWarning(ex, "写入要素时出错 (Fid={Fid})", oguFeature.Fid);
                }
                finally
                {
                    geometry?.Dispose();
                    ogrFeature?.Dispose();
                }
            }

            // 同步到磁盘
            dataSource.SyncToDisk();

            if (failedCount > 0)
                throw new DataSourceException($"写入图层时 {failedCount} 个要素失败: {path}");
        }
        finally
        {
            dataSource?.Dispose();
        }
    }

    private static string[] BuildLayerOptions(Dictionary<string, object>? options, string driverName)
    {
        var layerOptions = new List<string>();

        if (options != null && options.TryGetValue("encoding", out var encodingObj) && encodingObj != null)
        {
            var encoding = encodingObj as Encoding ?? Encoding.GetEncoding(encodingObj.ToString() ?? "UTF-8");
            layerOptions.Add($"ENCODING={encoding.WebName}");
        }

        if (driverName is "GeoJSON" or "GeoJSONSeq")
        {
            layerOptions.Add("COORDINATE_PRECISION=15");
        }

        if (options != null && options.TryGetValue("overwrite", out var overwriteObj) && IsTrue(overwriteObj))
            // 数据库驱动不会像文件驱动那样先删除已有目标，重复写入必须显式声明覆盖
            layerOptions.Add("OVERWRITE=YES");

        return layerOptions.ToArray();
    }

    private static bool IsTrue(object? value)
    {
        if (value is bool flag)
            return flag;

        var text = value?.ToString();
        return text is "YES" or "yes" or "TRUE" or "true" or "1";
    }

    /// <summary>
    ///     追加要素到已存在的图层
    /// </summary>
    /// <param name="layer">图层对象</param>
    /// <param name="path">输出路径</param>
    /// <param name="layerName">图层名称</param>
    /// <param name="options">附加选项</param>
    /// <exception cref="ArgumentNullException">当图层为 null 时抛出</exception>
    /// <exception cref="ArgumentException">当路径为空时抛出</exception>
    /// <exception cref="DataSourceException">当数据源或图层无法打开时抛出</exception>
    /// <remarks>
    ///     追加操作不会删除已有数据源。输入字段必须能映射到目标图层；要素的非零 FID 会尝试保留
    ///     （PostgreSQL 驱动下 0 也会显式保留，避免与序列自动分配值链式冲突）。
    ///     空几何、字段转换失败及要素写入失败会汇总后抛出 <see cref="DataSourceException"/>。
    /// </remarks>
    public void Append(OguLayer layer, string path, string? layerName = null,
        Dictionary<string, object>? options = null)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        ValidateCollections(layer);

        using var dataSource = Ogr.Open(path, 1);
        if (dataSource == null)
            throw new DataSourceException($"Failed to open data source for appending: {path}");

        var targetLayerName = layerName ?? layer.Name;
        var ogrLayer = string.IsNullOrWhiteSpace(targetLayerName)
            ? dataSource.GetLayerByIndex(0)
            : dataSource.GetLayerByName(targetLayerName);
        if (ogrLayer == null)
            throw new DataSourceException($"Layer '{targetLayerName ?? ""}' not found");

        var layerDefinition = ogrLayer.GetLayerDefn();
        var fieldIndexMap = new Dictionary<string, int>(layer.Fields.Count);
        foreach (var field in layer.Fields)
        {
            var index = layerDefinition.GetFieldIndex(field.Name);
            if (index < 0)
                throw new DataSourceException($"Field '{field.Name}' not found in target layer");
            fieldIndexMap[field.Name] = index;
        }

        var failedCount = 0;
        // 与 Write 相同的 PostgreSQL Fid=0 显式保留策略：避免序列值与显式 FID 链式相撞 UNIQUE
        var appendPreserveZeroFid = IsPostgresqlDriver(dataSource.GetDriver().GetName());
        foreach (var oguFeature in layer.Features)
        {
            if (string.IsNullOrWhiteSpace(oguFeature.Wkt))
            {
                failedCount++;
                Logger.LogWarning("跳过空几何要素 (Fid={Fid})", oguFeature.Fid);
                continue;
            }

            Feature? ogrFeature = null;
            OSGeo.OGR.Geometry? geometry = null;
            try
            {
                ogrFeature = new Feature(layerDefinition);
                if ((oguFeature.Fid != 0 || appendPreserveZeroFid) && ogrFeature.SetFID(oguFeature.Fid) != 0)
                    throw new SysException($"设置要素 FID 失败 (Fid={oguFeature.Fid})");

                geometry = OSGeo.OGR.Geometry.CreateFromWkt(oguFeature.Wkt);
                if (geometry == null)
                    throw new SysException($"无法解析要素几何 (Fid={oguFeature.Fid})");
                if (ogrFeature.SetGeometry(geometry) != 0)
                    throw new SysException($"设置要素几何失败 (Fid={oguFeature.Fid})");

                foreach (var field in layer.Fields)
                {
                    var value = oguFeature.GetValue(field.Name);
                    SetFieldValue(ogrFeature, fieldIndexMap[field.Name], value, field.DataType, field.Name);
                }

                var appended = TryCreateFeature(ogrLayer, ogrFeature);
                if (!appended && (oguFeature.Fid != 0 || appendPreserveZeroFid))
                {
                    // FID 冲突时放弃保留源 FID，改为自动分配重试一次（与 Write 行为一致）
                    ogrFeature.SetFID(-1);
                    appended = TryCreateFeature(ogrLayer, ogrFeature);
                    if (appended)
                        Logger.LogWarning("源 FID 冲突，已改用自动分配 FID (源Fid={Fid})", oguFeature.Fid);
                }

                if (!appended)
                {
                    failedCount++;
                    Logger.LogWarning("追加要素失败 (Fid={Fid})", oguFeature.Fid);
                }
            }
            catch (SysException ex)
            {
                failedCount++;
                Logger.LogWarning(ex, "追加要素时出错 (Fid={Fid})", oguFeature.Fid);
            }
            finally
            {
                geometry?.Dispose();
                ogrFeature?.Dispose();
            }
        }

        dataSource.SyncToDisk();
        if (failedCount > 0)
            throw new DataSourceException($"追加到图层时 {failedCount} 个要素失败: {path}");
    }

    private static void ValidateCollections(OguLayer layer)
    {
        if (layer.Fields == null)
            throw new ArgumentException("Layer fields collection cannot be null", nameof(layer));

        if (layer.Features == null)
            throw new ArgumentException("Layer features collection cannot be null", nameof(layer));
    }

    /// <summary>
    ///     判断驱动是否使用固定 schema（不支持任意字段创建）
    /// </summary>
    /// <remarks>
    ///     DXF 驱动的写入端使用固定的实体属性 schema，CreateField 会失败；
    ///     这类驱动写入时跳过无法创建的字段而非整体失败。
    /// </remarks>
    private static bool IsFixedSchemaDriver(string driverName)
    {
        return driverName == "DXF";
    }

    private static bool IsPostgresqlDriver(string? driverName)
        => string.Equals(driverName, "PostgreSQL", StringComparison.OrdinalIgnoreCase);

    private string InferDriverName(string path, Dictionary<string, object>? options)
    {
        // 从选项中获取驱动名称
        if (options != null && options.TryGetValue("driver", out var driverObj))
            return driverObj.ToString() ?? "ESRI Shapefile";

        // 数据库连接串没有文件扩展名，必须先按前缀识别，否则会落到默认的 Shapefile 驱动
        if (path.StartsWith("PG:", StringComparison.OrdinalIgnoreCase))
            return "PostgreSQL";

        // 根据扩展名推断
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".shp" => "ESRI Shapefile",
            // 优先使用 OpenFileGDB（GDAL 3.6+ 支持创建），避免依赖 ESRI FileGDB SDK 的闭源驱动
            ".gdb" => "OpenFileGDB",
            ".gpkg" => "GPKG",
            ".kml" => "KML",
            ".dxf" => "DXF",
            ".geojson" or ".json" => "GeoJSON",
            _ => "ESRI Shapefile"
        };
    }

    private FieldDefn CreateOgrFieldDefn(OguField field)
    {
        var ogrType = OgrTypeMapper.MapToOgrFieldType(field.DataType);
        var fieldDefn = new FieldDefn(field.Name, ogrType);

        if (field.Length.HasValue && field.Length.Value > 0) fieldDefn.SetWidth(field.Length.Value);

        if (field.Precision.HasValue && field.Precision.Value > 0) fieldDefn.SetPrecision(field.Precision.Value);

        return fieldDefn;
    }

    private static SpatialReference? CreateSpatialReference(int? wkid)
    {
        if (!wkid.HasValue)
            return null;

        var spatialReference = new SpatialReference(null);
        if (spatialReference.ImportFromEPSG(wkid.Value) != 0)
        {
            spatialReference.Dispose();
            throw new SysException($"Failed to import spatial reference EPSG:{wkid.Value}");
        }

        return spatialReference;
    }

    /// <summary>
    ///     尝试创建要素。MaxRev 绑定在插入失败时抛异常而非返回非零，这里统一为布尔结果。
    /// </summary>
    private static bool TryCreateFeature(Layer layer, Feature feature)
    {
        try
        {
            return layer.CreateFeature(feature) == 0;
        }
        catch (SysException)
        {
            return false;
        }
    }

    /// <summary>
    ///     解析创建图层用的 OGR 几何类型
    /// </summary>
    /// <remarks>
    ///     <see cref="GeometryType"/> 枚举未建模 Z 维度，而读取 Shapefile（PointZ/PolylineZ）时
    ///     要素 WKT 会保留 Z 坐标；此时将图层类型升级为要素实际的维度类型，避免 GPKG 等驱动
    ///     出现"声明 2D 但包含 Z 几何"的不一致。
    /// </remarks>
    private static wkbGeometryType ResolveOgrGeometryType(OguLayer layer)
    {
        var mapped = OgrTypeMapper.MapToOgrGeometryType(layer.GeometryType);
        if (mapped == wkbGeometryType.wkbUnknown)
            return mapped;

        foreach (var feature in layer.Features)
        {
            if (string.IsNullOrWhiteSpace(feature.Wkt))
                continue;

            wkbGeometryType? actualType = null;
            try
            {
                using var geometry = OSGeo.OGR.Geometry.CreateFromWkt(feature.Wkt);
                if (geometry != null)
                    actualType = geometry.GetGeometryType();
            }
            catch (SysException)
            {
                // 无法解析的几何由要素写入阶段统一报错
                continue;
            }

            if (actualType is not { } actual)
                continue;

            // 仅当实际类型与映射类型基型一致（仅差 Z/M 维度位）时采用实际类型
            if ((int)actual != (int)mapped &&
                OgrTypeMapper.FlattenWkbType((int)actual) == mapped)
                return actual;

            break;
        }

        return mapped;
    }

    private void SetFieldValue(Feature feature, int fieldIndex, object? value, FieldDataType dataType,
        string fieldName)
    {
        if (value == null)
        {
            // 优先使用 OGR null 语义（GDAL 3.3+）：GeoJSON 的 WRITE_NULL_FIELDS 等选项
            // 依赖 null 而非 unset；旧绑定不支持时回退到 UnsetField
            try
            {
                feature.SetFieldNull(fieldIndex);
            }
            catch (SysException)
            {
                feature.UnsetField(fieldIndex);
            }

            return;
        }

        try
        {
            switch (dataType)
            {
                case FieldDataType.INTEGER:
                    feature.SetField(fieldIndex, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDataType.LONG:
                    feature.SetField(fieldIndex, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDataType.DOUBLE:
                case FieldDataType.FLOAT:
                    feature.SetField(fieldIndex, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDataType.DATE:
                case FieldDataType.DATETIME:
                    if (value is not DateTime dt)
                        throw new FormatException($"Value for date field '{fieldName}' is not a DateTime");

                    var seconds = dt.Second + (float)dt.Millisecond / 1000;
                    feature.SetField(fieldIndex, dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, seconds, 0);
                    break;
                default:
                    feature.SetField(fieldIndex, value.ToString());
                    break;
            }

            if (!feature.IsFieldSet(fieldIndex))
                throw new SysException($"Failed to set field '{fieldName}'");
        }
        catch (SysException ex)
        {
            if (ex is DataSourceException)
                throw;

            throw new DataSourceException($"Failed to set field '{fieldName}'", ex);
        }
    }
}
