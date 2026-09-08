using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Engine.Util;
using OpenGIS.Utils.Geometry;
using OSGeo.OGR;
using OgrGeometry = OSGeo.OGR.Geometry;
using OgrDataSource = OSGeo.OGR.DataSource;
using Exception = System.Exception;

namespace OpenGIS.Utils.RealDataHarness;

/// <summary>
///     通用真实数据集成检查：对指定目录下自动发现的所有 Shapefile 执行七维度检查。
///     期望值（要素数、几何类型）直接从 SHP/DBF 文件头解析，独立于被测的 GDAL 读取链路，
///     从而形成交叉校验；不内置任何具体数据集的名称、路径或坐标范围假设。
///     维度：读取链路 / 3D几何 / 坐标系 / 格式转换 / 几何处理 / 写出回读 / PostGIS往返。
///     第七维度需要显式传入可用的 PostGIS 连接串，未提供或不可达时整体记为跳过而非失败。
/// </summary>
public static partial class RealDataChecks
{
    /// <summary>
    ///     图层规格：全部由数据文件自身推导
    /// </summary>
    public sealed record LayerSpec(
        string ShpPath,
        string LayerName,
        int ExpectedCount,
        GeometryType ExpectedGeomType,
        string SourceShapeType,
        bool HasZ)
    {
        public override string ToString()
        {
            return $"{LayerName} ({SourceShapeType}, 期望 {ExpectedCount} 要素)";
        }
    }

    /// <summary>
    ///     ESRI shape 类型码 →（库几何类型、类型名、是否含 Z）
    /// </summary>
    private static readonly IReadOnlyDictionary<int, (GeometryType Geom, string Name, bool HasZ)> ShapeTypeMap =
        new Dictionary<int, (GeometryType, string, bool)>
        {
            [1] = (GeometryType.POINT, "Point", false),
            [3] = (GeometryType.LINESTRING, "Polyline", false),
            [5] = (GeometryType.POLYGON, "Polygon", false),
            [8] = (GeometryType.MULTIPOINT, "MultiPoint", false),
            [11] = (GeometryType.POINT, "PointZ", true),
            [13] = (GeometryType.LINESTRING, "PolylineZ", true),
            [15] = (GeometryType.POLYGON, "PolygonZ", true),
            [18] = (GeometryType.MULTIPOINT, "MultiPointZ", true),
            [21] = (GeometryType.POINT, "PointM", false),
            [23] = (GeometryType.LINESTRING, "PolylineM", false),
            [25] = (GeometryType.POLYGON, "PolygonM", false),
            [28] = (GeometryType.MULTIPOINT, "MultiPointM", false)
        };

    private const int Wgs84Wkid = 4326;

    private const int Cgcs2000Wkid = 4490;

    /// <summary>
    ///     数据目录与环境前提校验，返回错误消息；null 表示通过。
    ///     要求目录存在且至少包含一个带 .dbf 的 .shp 文件。
    /// </summary>
    public static string? ValidateEnvironment(string dataDir)
    {
        if (string.IsNullOrWhiteSpace(dataDir))
            return "未指定数据目录";
        if (!Directory.Exists(dataDir))
            return $"数据目录不存在: {dataDir}";
        if (DiscoverLayers(dataDir).Count == 0)
            return $"目录（含子目录）中未发现成对的 .shp/.dbf 文件: {dataDir}";
        return null;
    }

    /// <summary>
    ///     递归发现数据目录下所有成对的 .shp/.dbf 文件并推导期望规格
    /// </summary>
    public static IReadOnlyList<LayerSpec> DiscoverLayers(string dataDir)
    {
        var specs = new List<LayerSpec>();
        foreach (var shpPath in Directory
                     .EnumerateFiles(dataDir, "*.shp", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var dbfPath = Path.ChangeExtension(shpPath, ".dbf");
            if (!File.Exists(dbfPath))
                continue;
            specs.Add(BuildSpec(shpPath, dbfPath));
        }

        return specs;
    }

    /// <summary>
    ///     从文件头独立解析期望值（不经过 GDAL）：
    ///     SHP 偏移 32（LE int32）为 shape 类型码；DBF 偏移 4（LE int32）为记录数。
    /// </summary>
    private static LayerSpec BuildSpec(string shpPath, string dbfPath)
    {
        var shapeType = ReadInt32(shpPath, 32);
        var recordCount = ReadInt32(dbfPath, 4);

        var (geom, name, hasZ) = ShapeTypeMap.TryGetValue(shapeType, out var mapped)
            ? mapped
            : (GeometryType.UNKNOWN, $"Type{shapeType}", false);

        return new LayerSpec(shpPath, Path.GetFileNameWithoutExtension(shpPath), recordCount, geom, name, hasZ);
    }

    private static int ReadInt32(string path, int offset)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buf = new byte[offset + 4];
            var read = 0;
            while (read < buf.Length)
            {
                var n = fs.Read(buf, read, buf.Length - read);
                if (n <= 0) break;
                read += n;
            }

            return read >= buf.Length ? BitConverter.ToInt32(buf, offset) : -1;
        }
        catch (System.Exception)
        {
            return -1;
        }
    }

    /// <param name="dataDir">真实数据目录（递归发现 .shp/.dbf）</param>
    /// <param name="workDir">中间产物目录，由调用方负责清理</param>
    /// <param name="postgisConnection">
    ///     PostGIS 连接串（无引号形式，如 <c>PG:host=127.0.0.1 port=5432 dbname=postgres user=postgres password=postgres</c>）。
    ///     为 null 或不可达时跳过第七维度。
    /// </param>
    public static List<CheckResult> RunAll(string dataDir, string workDir, string? postgisConnection = null)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        GdalConfiguration.ConfigureGdal();

        Directory.CreateDirectory(workDir);
        var specs = DiscoverLayers(dataDir);
        var results = new List<CheckResult>();
        var loaded = new List<(LayerSpec Spec, OguLayer Layer)>();
        foreach (var spec in specs)
            loaded.Add((spec, OguLayerUtil.ReadLayer(DataFormatType.SHP, spec.ShpPath)));

        RunReadChecks(results, loaded);
        Run3DChecks(results, loaded);
        RunCrsChecks(results, loaded);
        RunConvertChecks(workDir, results, loaded);
        RunGeometryOpChecks(results, loaded);
        RunWriteBackChecks(workDir, results, loaded);
        RunPostgisChecks(results, loaded, postgisConnection);

        return results;
    }

    // ─────────────────────────── 维度1：读取链路 ───────────────────────────

    private static void RunReadChecks(List<CheckResult> results,
        IReadOnlyList<(LayerSpec Spec, OguLayer Layer)> loaded)
    {
        const string dim = "1-读取链路";
        foreach (var (spec, layer) in loaded)
        {
            var target = spec.LayerName;

            Add(results, dim, target, "要素数量",
                layer.GetFeatureCount() == spec.ExpectedCount ? CheckStatus.Pass : CheckStatus.Fail,
                $"实际 {layer.GetFeatureCount()}，DBF 头期望 {spec.ExpectedCount}");

            Add(results, dim, target, "几何类型映射",
                layer.GeometryType == spec.ExpectedGeomType ? CheckStatus.Pass : CheckStatus.Warn,
                $"实际 {layer.GeometryType}，SHP 头期望 {spec.ExpectedGeomType}（源 shape 类型 {spec.SourceShapeType}）");

            Add(results, dim, target, "坐标系识别(Wkid)",
                layer.Wkid != null ? CheckStatus.Pass : CheckStatus.Warn,
                layer.Wkid != null ? $"Wkid={layer.Wkid}" : "未能从 .prj 识别出 Wkid");

            var fieldCount = layer.Fields.Count;
            var longNames = layer.Fields.Where(f => f.Name.Length > 10).Select(f => f.Name).ToList();
            Add(results, dim, target, "字段读取", fieldCount > 0 ? CheckStatus.Pass : CheckStatus.Fail,
                $"共 {fieldCount} 个字段；超10字符字段名 {longNames.Count} 个" +
                (longNames.Count > 0 ? $"（如 {string.Join(",", longNames.Take(3))}，SHP 重写会被截断）" : ""));

            var wktNull = layer.Features.Count(f => string.IsNullOrWhiteSpace(f.Wkt));
            var parseFail = 0;
            foreach (var f in layer.Features)
                try
                {
                    if (!string.IsNullOrWhiteSpace(f.Wkt))
                        using (GeometryUtil.Wkt2Geometry(f.Wkt!))
                        {
                        }
                }
                catch (System.Exception)
                {
                    parseFail++;
                }

            Add(results, dim, target, "WKT解析",
                wktNull == 0 && parseFail == 0 ? CheckStatus.Pass : CheckStatus.Fail,
                $"空WKT {wktNull} 个，解析失败 {parseFail} 个");

            try
            {
                layer.Validate();
                Add(results, dim, target, "Layer.Validate()", CheckStatus.Pass, "通过");
            }
            catch (System.Exception ex)
            {
                Add(results, dim, target, "Layer.Validate()", CheckStatus.Fail, ex.Message);
            }

            var names = OguLayerUtil.GetLayerNames(DataFormatType.SHP, spec.ShpPath);
            Add(results, dim, target, "GetLayerNames",
                names.Contains(spec.LayerName) ? CheckStatus.Pass : CheckStatus.Warn,
                $"[{string.Join(",", names)}]");

            var bounds = ShpUtil.GetShapefileBounds(spec.ShpPath);
            var env = ComputeLayerEnvelope(layer);
            var maxDiff = Math.Max(Math.Max(
                Math.Abs(bounds.MinX - env.MinX), Math.Abs(bounds.MaxX - env.MaxX)),
                Math.Max(Math.Abs(bounds.MinY - env.MinY), Math.Abs(bounds.MaxY - env.MaxY)));
            Add(results, dim, target, "GetShapefileBounds",
                maxDiff < 1e-6 ? CheckStatus.Pass : CheckStatus.Fail,
                $"与逐要素包络最大偏差 {maxDiff:G4}；X[{bounds.MinX:F1},{bounds.MaxX:F1}] Y[{bounds.MinY:F1},{bounds.MaxY:F1}]");

            // 编码：.cpg 存在时与检测编码交叉比对
            var encoding = ShpUtil.GetShapefileEncoding(spec.ShpPath);
            var cpgPath = Path.ChangeExtension(spec.ShpPath, ".cpg");
            CheckStatus cpgStatus;
            string cpgDetail;
            if (File.Exists(cpgPath))
            {
                var cpg = File.ReadAllText(cpgPath).Trim();
                var match = string.Equals(cpg, encoding.WebName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(cpg.Replace("-", ""), encoding.WebName.Replace("-", ""),
                                StringComparison.OrdinalIgnoreCase);
                cpgStatus = match ? CheckStatus.Pass : CheckStatus.Warn;
                cpgDetail = $"检测为 {encoding.WebName}，.cpg={cpg}";
            }
            else
            {
                cpgStatus = CheckStatus.Info;
                cpgDetail = $"检测为 {encoding.WebName}，无 .cpg";
            }

            Add(results, dim, target, "编码检测", cpgStatus, cpgDetail);

            var nonAscii = layer.Features.SelectMany(f => f.Attributes.Values)
                .Count(v => v.Value?.ToString()?.Any(c => c > 127) == true);
            Add(results, dim, target, "非ASCII属性", CheckStatus.Info, $"含非ASCII属性值 {nonAscii} 处");
        }
    }

    private static (double MinX, double MaxX, double MinY, double MaxY) ComputeLayerEnvelope(OguLayer layer)
    {
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var f in layer.Features)
        {
            if (string.IsNullOrWhiteSpace(f.Wkt)) continue;
            using var g = GeometryUtil.Wkt2Geometry(f.Wkt);
            if (g == null) continue;
            var e = new Envelope();
            g.GetEnvelope(e);
            minX = Math.Min(minX, e.MinX); maxX = Math.Max(maxX, e.MaxX);
            minY = Math.Min(minY, e.MinY); maxY = Math.Max(maxY, e.MaxY);
        }

        return (minX, maxX, minY, maxY);
    }

    // ─────────────────────────── 维度2：3D几何 ───────────────────────────

    private static void Run3DChecks(List<CheckResult> results,
        IReadOnlyList<(LayerSpec Spec, OguLayer Layer)> loaded)
    {
        const string dim = "2-3D几何";
        foreach (var (spec, layer) in loaded)
        {
            var target = spec.LayerName;

            // Z 维保留判定：ExportToWkt 输出 WKT1 传统格式（POINT (x y z)，无 ISO "Z" 标记），
            // 因此以"每个坐标点均为三元组"为准
            var withZ = layer.Features.Count(f =>
            {
                if (string.IsNullOrWhiteSpace(f.Wkt)) return false;
                using var g = GeometryUtil.Wkt2Geometry(f.Wkt!);
                var n = GeometryUtil.NumPoints(g);
                return n > 0 && ZRegex().Matches(f.Wkt!).Count >= n;
            });
            var zStatus = !spec.HasZ
                ? withZ == 0 ? CheckStatus.Info : CheckStatus.Warn
                : withZ == layer.GetFeatureCount() ? CheckStatus.Pass : CheckStatus.Warn;
            Add(results, dim, target, "WKT保留Z维(坐标三元组)", zStatus,
                spec.HasZ
                    ? $"{withZ}/{layer.GetFeatureCount()} 个要素 WKT 坐标含 Z 分量（源类型 {spec.SourceShapeType}，WKT1 传统格式 POINT (x y z)）"
                    : $"源为 2D 类型 {spec.SourceShapeType}，读取到 {withZ} 个含 Z 要素（应为 0）");

            // Z 值范围（从 WKT 提取第三个坐标）
            var zVals = new List<double>();
            foreach (var f in layer.Features)
                foreach (Match m in ZRegex().Matches(f.Wkt ?? ""))
                    if (m.Groups[3].Success && double.TryParse(m.Groups[3].Value, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var z))
                        zVals.Add(z);
            Add(results, dim, target, "Z值读取",
                !spec.HasZ
                    ? CheckStatus.Info
                    : zVals.Count > 0 ? CheckStatus.Pass : CheckStatus.Warn,
                zVals.Count > 0
                    ? $"Z 范围 [{zVals.Min():F1}, {zVals.Max():F1}]（{zVals.Count} 个坐标点）"
                    : "未提取到 Z 值");

            if (spec.ExpectedGeomType == GeometryType.LINESTRING)
            {
                var lengths = new List<double>();
                var badLen = 0;
                foreach (var f in layer.Features)
                    try
                    {
                        var len = GeometryUtil.LengthWkt(f.Wkt!);
                        if (len > 0) lengths.Add(len); else badLen++;
                    }
                    catch (System.Exception)
                    {
                        badLen++;
                    }

                var totalKm = lengths.Sum() / 1000.0;
                Add(results, dim, target, "长度计算",
                    badLen == 0 && lengths.Count == layer.GetFeatureCount() ? CheckStatus.Pass : CheckStatus.Warn,
                    $"总长 {totalKm:F1} km，长度为0/失败 {badLen} 条");
            }
            else if (spec.ExpectedGeomType == GeometryType.POINT)
            {
                var pts = layer.Features.Count(f =>
                {
                    using var g = GeometryUtil.Wkt2Geometry(f.Wkt!);
                    return GeometryUtil.NumPoints(g) >= 1;
                });
                Add(results, dim, target, "点要素解析",
                    pts == layer.GetFeatureCount() ? CheckStatus.Pass : CheckStatus.Fail,
                    $"{pts}/{layer.GetFeatureCount()} 个点几何可用");
            }
        }

        // 抽样几何量算：取要素最多的线图层与点图层
        var primaryLine = loaded
            .Where(x => x.Spec.ExpectedGeomType == GeometryType.LINESTRING)
            .OrderByDescending(x => x.Layer.GetFeatureCount())
            .FirstOrDefault();
        if (primaryLine.Layer != null)
        {
            var tunnel = primaryLine.Layer;
            var longest = tunnel.Features
                .Select(f => (F: f, L: GeometryUtil.LengthWkt(f.Wkt!)))
                .OrderByDescending(x => x.L).First();
            using var g = GeometryUtil.Wkt2Geometry(longest.F.Wkt!);
            var np = GeometryUtil.NumPoints(g);
            using var centroid = GeometryUtil.Centroid(g);
            using var env = GeometryUtil.Envelope(g);
            Add(results, dim, primaryLine.Spec.LayerName, "质心/包络/顶点(抽样)", CheckStatus.Pass,
                $"最长线要素 {longest.L / 1000:F2} km、{np} 个顶点，质心与包络均可计算（包络类型 {GeometryUtil.GetGeometryType(env)}）");
        }
        else
        {
            Add(results, dim, "-", "线抽样量算", CheckStatus.Info, "数据目录中无线图层，跳过");
        }

        var primaryPoint = loaded
            .Where(x => x.Spec.ExpectedGeomType == GeometryType.POINT)
            .OrderByDescending(x => x.Layer.GetFeatureCount())
            .FirstOrDefault();
        if (primaryPoint.Layer != null && primaryPoint.Layer.GetFeatureCount() >= 3)
        {
            var wktList = primaryPoint.Layer.Features.Select(f => f.Wkt!).ToList();
            using var union = GeometryUtil.Wkt2Geometry(GeometryUtil.UnionWkt(wktList));
            var unionType = GeometryUtil.GetGeometryType(union);
            using var hull = GeometryUtil.ConvexHull(union);
            var area = GeometryUtil.Area(hull);
            Add(results, dim, primaryPoint.Spec.LayerName, "点云凸包(抽样)", area > 0 ? CheckStatus.Pass : CheckStatus.Fail,
                $"{wktList.Count} 点 Union 后凸包面积 {area / 1e6:F3}（Union 结果类型 {unionType}）");
        }
    }

    [GeneratedRegex(
        @"(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)\s+(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)\s+(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)")]
    private static partial Regex ZRegex();

    // ─────────────────────────── 维度3：坐标系 ───────────────────────────

    private static void RunCrsChecks(List<CheckResult> results,
        IReadOnlyList<(LayerSpec Spec, OguLayer Layer)> loaded)
    {
        const string dim = "3-坐标系";
        foreach (var (spec, layer) in loaded)
        {
            var target = spec.LayerName;

            if (layer.Wkid is not int wkid || layer.GetFeatureCount() == 0)
            {
                Add(results, dim, target, "坐标系转换", CheckStatus.Warn,
                    "图层无可用 Wkid 或无要素，跳过转换检查");
                continue;
            }

            if (wkid == Wgs84Wkid)
            {
                Add(results, dim, target, "坐标系转换", CheckStatus.Info,
                    "源坐标系即 WGS84(4326)，无需重投影检查");
                continue;
            }

            var wkt = layer.Features[0].Wkt!;
            try
            {
                var wgs = CrsUtil.Transform(wkt, wkid, Wgs84Wkid);
                var (lon, lat) = FirstCoordinate(wgs);
                var sane = lon is >= -180 and <= 180 && lat is >= -90 and <= 90 &&
                           Math.Abs(lon) > 1e-9 && Math.Abs(lat) > 1e-9;
                Add(results, dim, target, $"{wkid}→{Wgs84Wkid}", sane ? CheckStatus.Pass : CheckStatus.Fail,
                    $"首点经纬度 ({lon:F6}, {lat:F6})，{(sane ? "落在有效经纬度范围" : "超出有效经纬度范围")}");
            }
            catch (System.Exception ex)
            {
                Add(results, dim, target, $"{wkid}→{Wgs84Wkid}", CheckStatus.Fail, $"异常: {ex.Message}");
            }

            try
            {
                var wgs = CrsUtil.Transform(wkt, wkid, Wgs84Wkid);
                var back = CrsUtil.Transform(wgs, Wgs84Wkid, wkid);
                var (x1, y1) = FirstCoordinate(wkt);
                var (x2, y2) = FirstCoordinate(back);
                var d = Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
                // 往返容差按源坐标数量级自适应：相对偏差 < 1e-6 视为亚毫米级
                var tol = Math.Max(1e-3, Math.Max(Math.Abs(x1), Math.Abs(y1)) * 1e-6);
                Add(results, dim, target, $"{wkid}→{Wgs84Wkid}→{wkid}往返",
                    d < tol ? CheckStatus.Pass : d < tol * 1000 ? CheckStatus.Warn : CheckStatus.Fail,
                    $"往返偏差 {d:G4}（容差 {tol:G3}）");
            }
            catch (System.Exception ex)
            {
                Add(results, dim, target, $"{wkid}→{Wgs84Wkid}→{wkid}往返", CheckStatus.Fail, $"异常: {ex.Message}");
            }
        }

        // 批量转换性能（取要素最多的图层）
        var largest = loaded.OrderByDescending(x => x.Layer.GetFeatureCount()).FirstOrDefault();
        if (largest.Layer is { Wkid: int lw } && largest.Layer.GetFeatureCount() > 0 && lw != Wgs84Wkid)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var count = 0;
                foreach (var f in largest.Layer.Features)
                {
                    CrsUtil.Transform(f.Wkt!, lw, Wgs84Wkid);
                    count++;
                }

                sw.Stop();
                Add(results, dim, largest.Spec.LayerName, $"批量转换({count}要素)", CheckStatus.Pass,
                    $"耗时 {sw.ElapsedMilliseconds} ms（{lw}→{Wgs84Wkid}）");
            }
            catch (System.Exception ex)
            {
                Add(results, dim, largest.Spec.LayerName, "批量转换", CheckStatus.Fail, $"异常: {ex.Message}");
            }
        }
    }

    private static (double X, double Y) FirstCoordinate(string wkt)
    {
        var m = Regex.Match(wkt,
            @"(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)\s+(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)");
        if (!m.Success)
            throw new FormatException($"WKT 中未找到坐标: {wkt[..Math.Min(60, wkt.Length)]}");
        return (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    // ─────────────────────────── 维度4：格式转换 ───────────────────────────

    private static void RunConvertChecks(string workDir, List<CheckResult> results,
        IReadOnlyList<(LayerSpec Spec, OguLayer Layer)> loaded)
    {
        const string dim = "4-格式转换";

        OguLayer SourceOf(LayerSpec spec)
        {
            return loaded.First(x => x.Spec.LayerName == spec.LayerName).Layer;
        }

        void ConvertAndCompare(LayerSpec spec, DataFormatType outFormat, string outPath,
            string label, int maxAttrDiffAllowed = 0, string? readLayerName = null,
            bool expectWgs84Reprojection = false, int sourceWkid = Wgs84Wkid)
        {
            var target = spec.LayerName;
            try
            {
                OguLayerUtil.ConvertFormat(spec.ShpPath, DataFormatType.SHP, outPath, outFormat,
                    layerName: spec.LayerName);

                // readLayerName 为 null 时按图层名读取；为空串时读取首个图层（DXF 等固定层名驱动）
                var effectiveReadLayerName = readLayerName == null ? spec.LayerName
                    : readLayerName.Length == 0 ? null
                    : readLayerName;
                var back = OguLayerUtil.ReadLayer(outFormat, outPath, effectiveReadLayerName);

                if (expectWgs84Reprojection)
                {
                    // KML 规范强制 WGS84：驱动会自动重投影坐标，因此不做几何一致比较，
                    // 改为与 CrsUtil.Transform 直接计算的目标坐标比对（容差 1e-4 度）
                    var source = SourceOf(spec);
                    var okCount = 0;
                    for (var i = 0; i < back.Features.Count && i < source.Features.Count; i++)
                        try
                        {
                            var expected = CrsUtil.Transform(source.Features[i].Wkt!, sourceWkid, Wgs84Wkid);
                            var (elon, elat) = FirstCoordinate(expected);
                            var (lon, lat) = FirstCoordinate(back.Features[i].Wkt!);
                            if (Math.Abs(lon - elon) < 1e-4 && Math.Abs(lat - elat) < 1e-4) okCount++;
                        }
                        catch (System.Exception)
                        {
                            // 计入未通过
                        }

                    var status = back.GetFeatureCount() == spec.ExpectedCount &&
                                 okCount == spec.ExpectedCount ? CheckStatus.Pass : CheckStatus.Fail;
                    Add(results, dim, target, label, status,
                        $"要素 {back.GetFeatureCount()}/{spec.ExpectedCount}，WGS84 坐标比对 {okCount}/{spec.ExpectedCount}" +
                        "（KML 规范要求 WGS84，坐标已由驱动自动重投影）");
                }
                else
                {
                    var diffs = CompareLayers(SourceOf(spec), back, outFormat);
                    var hardFails = diffs.Count(d => d.Kind == DiffKind.FeatureCount || d.Kind == DiffKind.Geometry);
                    var attrDiffs = diffs.Count(d => d.Kind == DiffKind.AttributeValue);
                    var fieldDiffs = diffs.Count(d => d.Kind == DiffKind.Field);

                    var status = hardFails > 0 ? CheckStatus.Fail
                        : attrDiffs > maxAttrDiffAllowed || fieldDiffs > 0 ? CheckStatus.Warn
                        : CheckStatus.Pass;
                    var detail = $"要素 {back.GetFeatureCount()}/{spec.ExpectedCount}，几何不一致 {hardFails}，" +
                                 $"字段差异 {fieldDiffs}，属性差异 {attrDiffs}";
                    if (diffs.Count > 0)
                        detail += "；示例: " + string.Join("; ", diffs.Take(3).Select(d => d.ToString()));
                    Add(results, dim, target, label, status, detail);
                }

                var names = OguLayerUtil.GetLayerNames(outFormat, outPath);
                Add(results, dim, target, label + "-图层名",
                    names.Contains(spec.LayerName) ? CheckStatus.Pass : CheckStatus.Warn,
                    $"[{string.Join(",", names)}]");
            }
            catch (System.Exception ex)
            {
                Add(results, dim, target, label, CheckStatus.Fail,
                    $"异常: {ex.GetType().Name}: {RootMessage(ex)}");
            }
        }

        foreach (var (spec, layer) in loaded)
        {
            var dir = Path.Combine(workDir, spec.LayerName);
            Directory.CreateDirectory(dir);

            ConvertAndCompare(spec, DataFormatType.GEOJSON, Path.Combine(dir, spec.LayerName + ".geojson"),
                "→GeoJSON");

            // GeoJSON → SHP 完整往返
            try
            {
                var gj = Path.Combine(dir, spec.LayerName + ".geojson");
                var rt = Path.Combine(dir, spec.LayerName + "_rt.shp");
                OguLayerUtil.ConvertFormat(gj, DataFormatType.GEOJSON, rt, DataFormatType.SHP,
                    layerName: spec.LayerName);
                var back = OguLayerUtil.ReadLayer(DataFormatType.SHP, rt);
                var diffs = CompareLayers(layer, back, DataFormatType.SHP);
                var hard = diffs.Count(d => d.Kind == DiffKind.FeatureCount || d.Kind == DiffKind.Geometry);
                var attr = diffs.Count(d => d.Kind == DiffKind.AttributeValue);
                var field = diffs.Count(d => d.Kind == DiffKind.Field);
                var status = hard > 0 ? CheckStatus.Fail : attr + field > 0 ? CheckStatus.Warn : CheckStatus.Pass;
                Add(results, dim, spec.LayerName, "SHP→GeoJSON→SHP往返", status,
                    $"要素 {back.GetFeatureCount()}/{spec.ExpectedCount}，几何不一致 {hard}，字段差异 {field}，属性差异 {attr}" +
                    (diffs.Count > 0 ? "；示例: " + string.Join("; ", diffs.Take(3).Select(d => d.ToString())) : ""));
            }
            catch (System.Exception ex)
            {
                Add(results, dim, spec.LayerName, "SHP→GeoJSON→SHP往返", CheckStatus.Fail,
                    $"异常: {ex.GetType().Name}: {RootMessage(ex)}");
            }

            ConvertAndCompare(spec, DataFormatType.GEOPACKAGE, Path.Combine(dir, spec.LayerName + ".gpkg"),
                "→GeoPackage");
            // KML 规范强制 WGS84，驱动会自动重投影；属性并入 Name/description 由驱动决定
            ConvertAndCompare(spec, DataFormatType.KML, Path.Combine(dir, spec.LayerName + ".kml"), "→KML",
                maxAttrDiffAllowed: int.MaxValue, expectWgs84Reprojection: true,
                sourceWkid: layer.Wkid ?? Wgs84Wkid);
            // DXF 驱动使用固定 schema（仅 entities 图层，字段受限）
            ConvertAndCompare(spec, DataFormatType.DXF, Path.Combine(dir, spec.LayerName + ".dxf"), "→DXF",
                maxAttrDiffAllowed: int.MaxValue, readLayerName: "");
        }

        // FILEGDB / TXT：各取要素最少的图层执行（缩短耗时）
        var smallest = loaded.OrderBy(x => x.Layer.GetFeatureCount()).First();

        try
        {
            var gdb = Path.Combine(workDir, "filegdb_test.gdb");
            OguLayerUtil.WriteLayer(DataFormatType.FILEGDB, smallest.Layer, gdb, smallest.Spec.LayerName);
            var back = OguLayerUtil.ReadLayer(DataFormatType.FILEGDB, gdb, smallest.Spec.LayerName);
            Add(results, dim, smallest.Spec.LayerName, "→FileGDB(OpenFileGDB)",
                back.GetFeatureCount() == smallest.Spec.ExpectedCount ? CheckStatus.Pass : CheckStatus.Fail,
                $"要素 {back.GetFeatureCount()}/{smallest.Spec.ExpectedCount}，字段 {back.Fields.Count}/{smallest.Layer.Fields.Count}");
        }
        catch (System.Exception ex)
        {
            Add(results, dim, smallest.Spec.LayerName, "→FileGDB(OpenFileGDB)", CheckStatus.Fail,
                $"异常: {ex.GetType().Name}: {RootMessage(ex)}");
        }

        // TXT（国土坐标文件，GtTxtUtil）——仅点图层
        var smallestPoint = loaded
            .Where(x => x.Spec.ExpectedGeomType == GeometryType.POINT)
            .OrderBy(x => x.Layer.GetFeatureCount())
            .FirstOrDefault();
        if (smallestPoint.Layer != null)
        {
            try
            {
                var txt = Path.Combine(workDir, "gttxt_test.txt");
                GtTxtUtil.SaveTxt(smallestPoint.Layer, txt);
                var loadedTxt = GtTxtUtil.LoadTxt(txt);
                Add(results, dim, smallestPoint.Spec.LayerName, "→TXT(GtTxtUtil)往返",
                    loadedTxt.GetFeatureCount() == smallestPoint.Spec.ExpectedCount
                        ? CheckStatus.Pass
                        : CheckStatus.Fail,
                    $"SaveTxt→LoadTxt 读回 {loadedTxt.GetFeatureCount()}/{smallestPoint.Spec.ExpectedCount} 要素");
            }
            catch (System.Exception ex)
            {
                Add(results, dim, smallestPoint.Spec.LayerName, "→TXT(GtTxtUtil)往返", CheckStatus.Fail,
                    $"异常: {ex.GetType().Name}: {RootMessage(ex)}");
            }
        }
        else
        {
            Add(results, dim, "-", "→TXT(GtTxtUtil)往返", CheckStatus.Info, "数据目录中无点图层，跳过");
        }
    }

    // ─────────────────────────── 维度5：几何处理 ───────────────────────────

    private static void RunGeometryOpChecks(List<CheckResult> results,
        IReadOnlyList<(LayerSpec Spec, OguLayer Layer)> loaded)
    {
        const string dim = "5-几何处理";
        var primaryLine = loaded
            .Where(x => x.Spec.ExpectedGeomType == GeometryType.LINESTRING)
            .OrderByDescending(x => x.Layer.GetFeatureCount())
            .FirstOrDefault();
        var primaryPoint = loaded
            .Where(x => x.Spec.ExpectedGeomType == GeometryType.POINT)
            .OrderByDescending(x => x.Layer.GetFeatureCount())
            .FirstOrDefault();

        if (primaryLine.Layer != null)
        {
            var layer = primaryLine.Layer;
            var lineName = primaryLine.Spec.LayerName;
            var wkid = layer.Wkid;

            // Buffer：仅在投影坐标系（米制）下做面积理论值比对
            if (wkid is int w && CrsUtil.IsProjectedCRS(w))
            {
                try
                {
                    var first = layer.Features.First(f => !string.IsNullOrWhiteSpace(f.Wkt));
                    var len = GeometryUtil.LengthWkt(first.Wkt!);
                    var buffered = GeometryUtil.BufferWkt(first.Wkt!, 50.0);
                    var area = GeometryUtil.AreaWkt(buffered);
                    var expected = len * 100 + Math.PI * 2500;
                    var ratio = expected > 0 ? area / expected : 0;
                    var status = ratio is > 0.9 and < 1.1 ? CheckStatus.Pass
                        : ratio > 0 ? CheckStatus.Warn : CheckStatus.Fail;
                    Add(results, dim, lineName, "Buffer(50单位)", status,
                        $"缓冲面积 {area / 1e4:F2} 万单位²，理论≈{expected / 1e4:F2}（比值 {ratio:F3}）");
                }
                catch (System.Exception ex)
                {
                    Add(results, dim, lineName, "Buffer(50单位)", CheckStatus.Fail, $"异常: {RootMessage(ex)}");
                }
            }
            else
            {
                Add(results, dim, lineName, "Buffer(50单位)", CheckStatus.Info,
                    "非投影坐标系或无 Wkid，跳过面积理论值比对");
            }

            // Union（批量线合并）
            try
            {
                var wktList = layer.Features.Take(20).Select(f => f.Wkt!).ToList();
                var unionWkt = GeometryUtil.UnionWkt(wktList);
                using var u = GeometryUtil.Wkt2Geometry(unionWkt);
                var lenAfter = GeometryUtil.Length(u);
                var lenBefore = wktList.Sum(w2 => GeometryUtil.LengthWkt(w2));
                var ok = lenBefore <= 0 || Math.Abs(lenAfter - lenBefore) / lenBefore < 0.01;
                Add(results, dim, lineName, $"Union({wktList.Count}条线)", ok ? CheckStatus.Pass : CheckStatus.Warn,
                    $"合并后长度 {lenAfter / 1000:F1} km（合并前合计 {lenBefore / 1000:F1} km，类型 {GeometryUtil.GetGeometryType(u)}）");
            }
            catch (System.Exception ex)
            {
                Add(results, dim, lineName, "Union(线)", CheckStatus.Fail, $"异常: {RootMessage(ex)}");
            }

            // Simplify / Densify
            try
            {
                var longest = layer.Features.Select(f => (F: f, L: GeometryUtil.LengthWkt(f.Wkt!)))
                    .OrderByDescending(x => x.L).First();
                using var g = GeometryUtil.Wkt2Geometry(longest.F.Wkt!);
                var n0 = GeometryUtil.NumPoints(g);
                using var simplified = GeometryUtil.Simplify(g, 1.0);
                var n1 = GeometryUtil.NumPoints(simplified);
                var lenDelta = Math.Abs(GeometryUtil.Length(simplified) - longest.L) / longest.L;
                Add(results, dim, lineName, "Simplify(1单位)",
                    n1 <= n0 && lenDelta < 0.02 ? CheckStatus.Pass : CheckStatus.Warn,
                    $"顶点 {n0}→{n1}，长度变化 {lenDelta * 100:F2}%");

                using var densified = GeometryUtil.Densify(g, 5.0);
                var n2 = GeometryUtil.NumPoints(densified);
                Add(results, dim, lineName, "Densify(5单位)", n2 >= n0 ? CheckStatus.Pass : CheckStatus.Warn,
                    $"顶点 {n0}→{n2}");
            }
            catch (System.Exception ex)
            {
                Add(results, dim, lineName, "Simplify/Densify", CheckStatus.Fail, $"异常: {RootMessage(ex)}");
            }
        }
        else
        {
            Add(results, dim, "-", "线几何处理", CheckStatus.Info, "数据目录中无线图层，跳过");
        }

        // 空间关系：点层 × 线层
        if (primaryLine.Layer != null && primaryPoint.Layer != null)
        {
            var relationTarget = $"{primaryPoint.Spec.LayerName}×{primaryLine.Spec.LayerName}";
            try
            {
                var t0 = primaryLine.Layer.Features[0].Wkt!;
                var near = 0;
                var minD = double.MaxValue;
                using var gt = GeometryUtil.Wkt2Geometry(t0);
                foreach (var p in primaryPoint.Layer.Features)
                {
                    using var gp = GeometryUtil.Wkt2Geometry(p.Wkt!);
                    var d = GeometryUtil.Distance(gp, gt);
                    minD = Math.Min(minD, d);
                    if (GeometryUtil.IsWithinDistance(gp, gt, 500.0)) near++;
                }

                Add(results, dim, relationTarget, "Distance/IsWithinDistance",
                    minD >= 0 ? CheckStatus.Pass : CheckStatus.Fail,
                    $"{primaryPoint.Layer.GetFeatureCount()} 个点距首条线的最小距离 {minD:F1}，500 内 {near} 个");

                var bufferWkt = GeometryUtil.BufferWkt(t0, 200.0);
                var contained = 0;
                foreach (var p in primaryPoint.Layer.Features)
                    if (GeometryUtil.ContainsWkt(bufferWkt, p.Wkt!))
                        contained++;
                Add(results, dim, relationTarget, "Contains(缓冲区)",
                    contained >= near ? CheckStatus.Pass : CheckStatus.Warn,
                    $"线 200 缓冲区包含 {contained} 个点（500 IsWithinDistance 为 {near}，二者应方向一致）");

                if (primaryLine.Layer.GetFeatureCount() >= 2)
                {
                    var inter = GeometryUtil.IntersectionWkt(bufferWkt,
                        GeometryUtil.BufferWkt(primaryLine.Layer.Features[1].Wkt!, 200.0));
                    var interArea = GeometryUtil.AreaWkt(inter);
                    Add(results, dim, primaryLine.Spec.LayerName, "Intersection(缓冲区相交)",
                        interArea >= 0 ? CheckStatus.Pass : CheckStatus.Fail,
                        $"两条线 200 缓冲区交集面积 {interArea / 1e4:F2} 万单位²");
                }
            }
            catch (System.Exception ex)
            {
                Add(results, dim, relationTarget, "Distance/Contains/Intersection", CheckStatus.Fail,
                    $"异常: {RootMessage(ex)}");
            }
        }

        // 拓扑校验全量
        foreach (var (spec, layer) in loaded)
        {
            try
            {
                var invalid = new List<(int Fid, string Reason)>();
                var nonSimple = 0;
                foreach (var f in layer.Features)
                {
                    using var g = GeometryUtil.Wkt2Geometry(f.Wkt!);
                    var v = GeometryUtil.IsValid(g);
                    if (!v.IsValid) invalid.Add((f.Fid, v.ErrorMessage ?? v.ErrorType?.ToString() ?? "invalid"));
                    if (!GeometryUtil.IsSimple(g).IsSimple) nonSimple++;
                }

                Add(results, dim, spec.LayerName, "IsValid(全量)",
                    invalid.Count == 0 ? CheckStatus.Pass : CheckStatus.Warn,
                    invalid.Count == 0
                        ? "全部有效"
                        : $"{invalid.Count} 个无效，示例 Fid={invalid[0].Fid}: {invalid[0].Reason}");
                Add(results, dim, spec.LayerName, "IsSimple(全量)",
                    nonSimple == 0 ? CheckStatus.Pass : CheckStatus.Warn,
                    nonSimple == 0 ? "全部简单" : $"{nonSimple} 个非简单几何");
            }
            catch (System.Exception ex)
            {
                Add(results, dim, spec.LayerName, "IsValid/IsSimple", CheckStatus.Fail, $"异常: {RootMessage(ex)}");
            }
        }
    }

    // ─────────────────────────── 维度6：写出回读 ───────────────────────────

    private static void RunWriteBackChecks(string workDir, List<CheckResult> results,
        IReadOnlyList<(LayerSpec Spec, OguLayer Layer)> loaded)
    {
        const string dim = "6-写出回读";
        foreach (var (spec, layer) in loaded)
        {
            var dir = Path.Combine(workDir, "writeback", spec.LayerName);
            Directory.CreateDirectory(dir);

            // OguLayerUtil.WriteLayer 全量写出
            try
            {
                var outPath = Path.Combine(dir, spec.LayerName + "_wb.shp");
                OguLayerUtil.WriteLayer(DataFormatType.SHP, layer, outPath);
                var back = OguLayerUtil.ReadLayer(DataFormatType.SHP, outPath);
                var diffs = CompareLayers(layer, back, DataFormatType.SHP);
                var hard = diffs.Count(d => d.Kind == DiffKind.FeatureCount || d.Kind == DiffKind.Geometry);
                var attr = diffs.Count(d => d.Kind == DiffKind.AttributeValue);
                var field = diffs.Count(d => d.Kind == DiffKind.Field);
                var status = hard > 0 ? CheckStatus.Fail : attr + field > 0 ? CheckStatus.Warn : CheckStatus.Pass;
                Add(results, dim, spec.LayerName, "WriteLayer→SHP→读回", status,
                    $"要素 {back.GetFeatureCount()}/{spec.ExpectedCount}，几何 {hard}，字段 {field}，属性 {attr} 差异" +
                    (diffs.Count > 0 ? "；示例: " + string.Join("; ", diffs.Take(3).Select(d => d.ToString())) : ""));
            }
            catch (System.Exception ex)
            {
                Add(results, dim, spec.LayerName, "WriteLayer→SHP→读回", CheckStatus.Fail,
                    $"异常: {ex.GetType().Name}: {RootMessage(ex)}");
            }

            // ShpUtil.WriteShapefile 指定 UTF-8
            try
            {
                var outPath = Path.Combine(dir, spec.LayerName + "_enc.shp");
                ShpUtil.WriteShapefile(layer, outPath, Encoding.UTF8);
                var back = ShpUtil.ReadShapefile(outPath);
                var nonAsciiDiff = 0;
                var nonAsciiChecked = 0;
                for (var i = 0; i < layer.Features.Count && i < back.Features.Count; i++)
                {
                    var src = layer.Features[i];
                    var dst = back.Features[i];
                    foreach (var (k, v) in src.Attributes)
                        if (v.Value?.ToString()?.Any(c => c > 127) == true)
                        {
                            nonAsciiChecked++;
                            if (!dst.Attributes.TryGetValue(k, out var dv) ||
                                dv.Value?.ToString() != v.Value.ToString()) nonAsciiDiff++;
                        }
                }

                Add(results, dim, spec.LayerName, "ShpUtil(UTF-8)非ASCII属性回读",
                    nonAsciiDiff == 0 && back.GetFeatureCount() == spec.ExpectedCount
                        ? CheckStatus.Pass
                        : CheckStatus.Fail,
                    $"非ASCII值核对 {nonAsciiChecked - nonAsciiDiff}/{nonAsciiChecked} 一致，要素 {back.GetFeatureCount()}/{spec.ExpectedCount}");
            }
            catch (System.Exception ex)
            {
                Add(results, dim, spec.LayerName, "ShpUtil(UTF-8)非ASCII属性回读", CheckStatus.Fail,
                    $"异常: {ex.GetType().Name}: {RootMessage(ex)}");
            }
        }

        // RepairShapefile（复制要素最少的图层再修复）
        var smallest = loaded.OrderBy(x => x.Layer.GetFeatureCount()).First();
        try
        {
            var srcDir = Path.Combine(workDir, "repair_src");
            Directory.CreateDirectory(srcDir);
            foreach (var ext in new[] { ".shp", ".shx", ".dbf", ".prj", ".cpg" })
            {
                var src = Path.ChangeExtension(smallest.Spec.ShpPath, ext);
                if (File.Exists(src))
                    File.Copy(src, Path.Combine(srcDir, smallest.Spec.LayerName + ext), true);
            }

            ShpUtil.RepairShapefile(Path.Combine(srcDir, smallest.Spec.LayerName + ".shp"));
            var repaired = ShpUtil.ReadShapefile(Path.Combine(srcDir, smallest.Spec.LayerName + ".shp"));
            Add(results, dim, smallest.Spec.LayerName, "RepairShapefile",
                repaired.GetFeatureCount() == smallest.Spec.ExpectedCount ? CheckStatus.Pass : CheckStatus.Warn,
                $"修复后要素 {repaired.GetFeatureCount()}/{smallest.Spec.ExpectedCount}");
        }
        catch (System.Exception ex)
        {
            Add(results, dim, smallest.Spec.LayerName, "RepairShapefile", CheckStatus.Fail,
                $"异常: {ex.GetType().Name}: {RootMessage(ex)}");
        }
    }

    // ─────────────────────────── 维度7：PostGIS往返 ───────────────────────────

    /// <summary>
    ///     第七维度名称，供测试项目按维度过滤断言
    /// </summary>
    public const string PostgisDimension = "7-PostGIS往返";

    /// <summary>
    ///     测试表前缀。本维度建立的所有表都带此前缀，便于 finally 中统一清理，避免误伤业务表。
    /// </summary>
    private const string PgTablePrefix = "ogu_test_";

    /// <summary>
    ///     PostGIS 环境前提校验，返回错误消息；null 表示可用。
    ///     不可用时整个维度记为 Info（跳过）而非 Fail，保证无数据库环境下测试仍为绿。
    /// </summary>
    public static string? ValidatePostgis(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "未指定连接串";
        try
        {
            // 校验常发生在 RunAll 之前（harness 启动预告、xunit 门控断言），
            // 驱动未注册时 Ogr.Open 直接返回 null，会把可用连接误判为环境不可用
            GdalConfiguration.ConfigureGdal();
            using var ds = Ogr.Open(connectionString, 0);
            if (ds == null)
                return "GDAL/OGR 无法打开连接串（须为无引号形式 PG:host=... port=... dbname=... user=... password=...）";
            var version = ScalarSql(ds, "SELECT postgis_lib_version()");
            if (string.IsNullOrWhiteSpace(version))
                return "数据库可连接，但 PostGIS 扩展不可用（postgis_lib_version() 无返回值）";
            return null;
        }
        catch (System.Exception ex)
        {
            return $"连接异常: {RootMessage(ex)}";
        }
    }

    private static void RunPostgisChecks(List<CheckResult> results,
        IReadOnlyList<(LayerSpec Spec, OguLayer Layer)> loaded, string? connectionString)
    {
        const string dim = PostgisDimension;

        var problem = ValidatePostgis(connectionString);
        if (problem != null)
        {
            Add(results, dim, "-", "环境前提", CheckStatus.Info, $"跳过全部 PostGIS 检查：{problem}");
            return;
        }

        var conn = connectionString!;
        var createdTables = new List<string>();
        try
        {
            Add(results, dim, "环境", "服务端/客户端版本", CheckStatus.Info, DescribeServers(conn));

            foreach (var (spec, layer) in loaded)
            {
                var table = PgTablePrefix + spec.LayerName.ToLowerInvariant();
                createdTables.Add(table);
                RunPostgisTableChecks(results, conn, spec, layer, table);
            }

            RunPostgisEntryChecks(results, conn, loaded, createdTables);
            RunPostgisCrsChecks(results, conn, loaded, createdTables);
        }
        finally
        {
            CleanupTables(conn, createdTables);
        }
    }

    /// <summary>
    ///     单图层完整往返：写入 → 服务端元数据 → 读回 → 字段/几何/属性 → 索引 → 过滤 → 重复写入
    /// </summary>
    private static void RunPostgisTableChecks(List<CheckResult> results, string conn,
        LayerSpec spec, OguLayer layer, string table)
    {
        const string dim = PostgisDimension;
        var target = spec.LayerName;

        ExecSql(conn, $"DROP TABLE IF EXISTS \"{table}\" CASCADE");

        bool existsBefore;
        try
        {
            existsBefore = PostgisUtil.TableExists(conn, table);
        }
        catch (System.Exception ex)
        {
            Add(results, dim, target, "TableExists(写前)", CheckStatus.Fail, $"异常: {RootMessage(ex)}");
            return;
        }

        Add(results, dim, target, "TableExists(写前)", existsBefore ? CheckStatus.Fail : CheckStatus.Pass,
            existsBefore ? $"清理后表 {table} 仍存在，无法验证写入语义" : $"表 {table} 不存在，符合预期");
        if (existsBefore)
            return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            PostgisUtil.WritePostGIS(layer, conn, table);
        }
        catch (System.Exception ex)
        {
            sw.Stop();
            Add(results, dim, target, "WritePostGIS", CheckStatus.Fail,
                $"异常: {ex.GetType().Name}: {RootMessage(ex)}");
            return;
        }

        sw.Stop();
        Add(results, dim, target, "WritePostGIS", CheckStatus.Pass,
            $"{layer.GetFeatureCount()} 要素 / {layer.Fields.Count} 字段 → {table}，耗时 {sw.ElapsedMilliseconds} ms");

        using var ds = Ogr.Open(conn, 0);
        if (ds == null)
        {
            Add(results, dim, target, "服务端校验", CheckStatus.Fail, "写入后无法以只读模式打开数据源");
            return;
        }

        CheckTablePresence(results, dim, target, ds, conn, table);
        var geomColumn = CheckServerGeometryMetadata(results, dim, target, ds, spec, layer, table);

        OguLayer? back = null;
        try
        {
            back = PostgisUtil.ReadPostGIS(conn, table);
            Add(results, dim, target, "ReadPostGIS", CheckStatus.Pass,
                $"读回 {back.GetFeatureCount()} 要素 / {back.Fields.Count} 字段，Wkid={back.Wkid?.ToString() ?? "null"}");
        }
        catch (System.Exception ex)
        {
            Add(results, dim, target, "ReadPostGIS", CheckStatus.Fail, $"异常: {ex.GetType().Name}: {RootMessage(ex)}");
            return;
        }

        CheckFields(results, dim, target, layer, back);
        CheckAttributes(results, dim, target, layer, back);
        CheckGeometryRoundTrip(results, dim, target, spec, layer, back);
        CheckSpatialIndex(results, dim, target, ds, conn, table, geomColumn);
        CheckAttributeFilter(results, dim, target, ds, conn, table, back);
        CheckRewriteBehavior(results, dim, target, ds, conn, layer, table);
    }

    private static void CheckTablePresence(List<CheckResult> results, string dim, string target,
        OgrDataSource ds, string conn, string table)
    {
        var existsAfter = PostgisUtil.TableExists(conn, table);
        var pgTables = ScalarSql(ds,
            $"SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename='{table}'");
        Add(results, dim, target, "TableExists(写后)",
            existsAfter && pgTables == "1" ? CheckStatus.Pass : CheckStatus.Fail,
            $"PostgisUtil.TableExists={existsAfter}，pg_tables 命中={pgTables}");

        var rowCount = ScalarSql(ds, $"SELECT count(*) FROM \"{table}\"");
        Add(results, dim, target, "服务端行数", ParseInt(rowCount) >= 0 ? CheckStatus.Pass : CheckStatus.Fail,
            $"count(*)={rowCount}");
    }

    /// <summary>
    ///     校验 geometry_columns 元数据（SRID/类型/维度）与 Z 值范围，返回实际几何列名（失败时为 null）
    /// </summary>
    private static string? CheckServerGeometryMetadata(List<CheckResult> results, string dim, string target,
        OgrDataSource ds, LayerSpec spec, OguLayer layer, string table)
    {
        var row = QueryRow(ds,
            "SELECT f_geometry_column, srid, type, coord_dimension FROM geometry_columns " +
            $"WHERE f_table_schema='public' AND f_table_name='{table}'");
        if (row == null || row.Length < 4)
        {
            Add(results, dim, target, "geometry_columns 元数据", CheckStatus.Fail,
                "geometry_columns 中查不到该表的登记记录");
            return null;
        }

        var geomColumn = row[0];
        var srid = ParseInt(row[1]);
        var geomType = row[2] ?? "";
        var coordDimension = ParseInt(row[3]);

        Add(results, dim, target, "SRID 登记",
            layer.Wkid.HasValue && srid == layer.Wkid.Value ? CheckStatus.Pass : CheckStatus.Fail,
            $"geometry_columns.srid={srid}，源图层 Wkid={layer.Wkid?.ToString() ?? "null"}");

        var expectedBase = spec.ExpectedGeomType.ToString();
        var typeOk = geomType.StartsWith(expectedBase, StringComparison.OrdinalIgnoreCase);
        Add(results, dim, target, "几何类型登记", typeOk ? CheckStatus.Pass : CheckStatus.Warn,
            $"geometry_columns.type={geomType}，源 shape 类型 {spec.SourceShapeType}（库内 {expectedBase}）");

        var expectedDim = spec.HasZ ? 3 : 2;
        Add(results, dim, target, "坐标维度登记", coordDimension == expectedDim ? CheckStatus.Pass : CheckStatus.Fail,
            $"coord_dimension={coordDimension}，源 HasZ={spec.HasZ}（期望 {expectedDim}）");

        if (!string.IsNullOrEmpty(geomColumn))
        {
            var ndims = ScalarSql(ds,
                $"SELECT max(ST_NDims(\"{geomColumn}\")) FROM \"{table}\" WHERE \"{geomColumn}\" IS NOT NULL");
            Add(results, dim, target, "ST_NDims 实测", ParseInt(ndims) == expectedDim ? CheckStatus.Pass : CheckStatus.Fail,
                $"服务端 ST_NDims={ndims}，期望 {expectedDim}");

            if (spec.HasZ)
            {
                var zRow = QueryRow(ds,
                    $"SELECT min(ST_ZMin(\"{geomColumn}\")), max(ST_ZMax(\"{geomColumn}\")) " +
                    $"FROM \"{table}\" WHERE \"{geomColumn}\" IS NOT NULL");
                var clientZ = ClientZRange(layer);
                var serverMin = ParseDouble(zRow?[0]);
                var serverMax = ParseDouble(zRow?[1]);
                var zOk = clientZ != null && zRow != null &&
                          Math.Abs(serverMin - clientZ.Value.Min) < 1e-3 &&
                          Math.Abs(serverMax - clientZ.Value.Max) < 1e-3;
                Add(results, dim, target, "Z 值范围一致", zOk ? CheckStatus.Pass : CheckStatus.Fail,
                    zRow == null || clientZ == null
                        ? $"客户端 Z 范围 {(clientZ == null ? "无" : $"[{clientZ.Value.Min:F3}, {clientZ.Value.Max:F3}]")}，服务端 {(zRow == null ? "查询失败" : $"[{serverMin:F3}, {serverMax:F3}]")}"
                        : $"客户端 [{clientZ.Value.Min:F3}, {clientZ.Value.Max:F3}]，服务端 [{serverMin:F3}, {serverMax:F3}]");
            }
        }

        return geomColumn;
    }

    private static void CheckFields(List<CheckResult> results, string dim, string target,
        OguLayer source, OguLayer back)
    {
        var srcNames = source.Fields.Select(f => f.Name).ToList();
        var dstNames = back.Fields.Select(f => f.Name).ToList();
        var missing = srcNames.Where(n => !dstNames.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
        var extra = dstNames.Where(n => !srcNames.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
        var caseChanged = srcNames
            .Select(n => dstNames.FirstOrDefault(d => string.Equals(d, n, StringComparison.OrdinalIgnoreCase)))
            .Where(d => d != null && !string.Equals(d, srcNames.First(n =>
                string.Equals(n, d, StringComparison.OrdinalIgnoreCase)), StringComparison.Ordinal))
            .ToList();

        Add(results, dim, target, "字段集合",
            missing.Count == 0 && extra.Count == 0 ? CheckStatus.Pass : CheckStatus.Fail,
            $"源 {srcNames.Count} 字段 → PG {dstNames.Count} 字段" +
            (missing.Count > 0 ? $"；丢失 {string.Join(",", missing.Take(6))}" : "") +
            (extra.Count > 0 ? $"；新增 {string.Join(",", extra.Take(6))}" : ""));

        var typeDiffs = srcNames
            .Select(n => (Source: source.GetField(n), Target: back.GetField(n) ??
                                                            back.GetField(dstNames.FirstOrDefault(d =>
                                                                string.Equals(d, n, StringComparison.OrdinalIgnoreCase)) ?? "")))
            .Where(x => x.Source != null && x.Target != null && x.Source.DataType != x.Target.DataType)
            .Select(x => $"{x.Source!.Name}:{x.Source.DataType}→{x.Target!.DataType}")
            .ToList();
        Add(results, dim, target, "字段类型",
            typeDiffs.Count == 0 ? CheckStatus.Pass : CheckStatus.Warn,
            typeDiffs.Count == 0
                ? "全部字段类型一致"
                : $"{typeDiffs.Count} 个字段类型发生变化：{string.Join("，", typeDiffs.Take(6))}");

        if (caseChanged.Count > 0)
            Add(results, dim, target, "字段名大小写", CheckStatus.Info,
                $"{caseChanged.Count} 个字段名大小写被 PG 规范化，如 {string.Join(",", caseChanged.Take(4))}");
    }

    private static void CheckAttributes(List<CheckResult> results, string dim, string target,
        OguLayer source, OguLayer back)
    {
        var srcNames = source.Fields.Select(f => f.Name).ToList();
        var nullCountDiffs = new List<string>();
        var signatureDiffs = new List<string>();

        foreach (var name in srcNames)
        {
            var dstName = back.Fields
                .Select(f => f.Name)
                .FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (dstName == null)
                continue;

            var srcValues = source.Features
                .Select(f => f.Attributes.TryGetValue(name, out var v) ? v.Value : null).ToList();
            var dstValues = back.Features
                .Select(f => f.Attributes.TryGetValue(dstName, out var v) ? v.Value : null).ToList();

            var srcNonNull = srcValues.Count(v => v != null);
            var dstNonNull = dstValues.Count(v => v != null);
            if (srcNonNull != dstNonNull)
                nullCountDiffs.Add($"{name}:{srcNonNull}→{dstNonNull}");

            if (ColumnSignature(srcValues) != ColumnSignature(dstValues))
            {
                var srcSorted = srcValues.Select(NormalizeValue).OrderBy(s => s, StringComparer.Ordinal).ToList();
                var dstSorted = dstValues.Select(NormalizeValue).OrderBy(s => s, StringComparer.Ordinal).ToList();
                var sample = srcSorted.Zip(dstSorted, (a, b) => (A: a, B: b))
                    .FirstOrDefault(p => p.A != p.B);
                signatureDiffs.Add(sample.A != null
                    ? $"{name}:[{Truncate(sample.A, 24)}]→[{Truncate(sample.B ?? "", 24)}]"
                    : name);
            }
        }

        Add(results, dim, target, "属性非空计数",
            nullCountDiffs.Count == 0 ? CheckStatus.Pass : CheckStatus.Fail,
            nullCountDiffs.Count == 0
                ? $"全部 {srcNames.Count} 个字段的非空值数量一致"
                : $"{nullCountDiffs.Count} 个字段非空计数变化：{string.Join("，", nullCountDiffs.Take(6))}");

        Add(results, dim, target, "属性值集合(顺序无关)",
            signatureDiffs.Count == 0 ? CheckStatus.Pass : CheckStatus.Warn,
            signatureDiffs.Count == 0
                ? "全部字段值的多重集合完全一致"
                : $"{signatureDiffs.Count} 个字段值不一致（可能为跨驱动格式化差异）：{string.Join("，", signatureDiffs.Take(6))}");
    }

    private static void CheckGeometryRoundTrip(List<CheckResult> results, string dim, string target,
        LayerSpec spec, OguLayer source, OguLayer back)
    {
        var aligned = CompareLayers(source, back, DataFormatType.POSTGIS)
            .Where(d => d.Kind == DiffKind.Geometry)
            .Select(d => d.Detail)
            .ToList();
        Add(results, dim, target, "几何往返(顺序对齐)",
            aligned.Count == 0 ? CheckStatus.Pass : CheckStatus.Warn,
            aligned.Count == 0
                ? "按索引逐要素 EqualsExactTolerance(1mm) 全部一致"
                : $"{aligned.Count} 处不一致（可能为读回顺序不同，见顺序无关量算）：{string.Join("；", aligned.Take(2))}");

        var srcStats = ComputeGeomStats(source);
        var dstStats = ComputeGeomStats(back);
        var diffs = new List<string>();
        if (srcStats.Vertices != dstStats.Vertices) diffs.Add($"顶点 {srcStats.Vertices}→{dstStats.Vertices}");
        if (Math.Abs(srcStats.LengthSum - dstStats.LengthSum) >
            Math.Max(1e-6, srcStats.LengthSum * 1e-9)) diffs.Add($"总长 {srcStats.LengthSum:F6}→{dstStats.LengthSum:F6}");
        if (!AlmostEqual(srcStats.MinX, dstStats.MinX) || !AlmostEqual(srcStats.MinY, dstStats.MinY) ||
            !AlmostEqual(srcStats.MaxX, dstStats.MaxX) || !AlmostEqual(srcStats.MaxY, dstStats.MaxY))
            diffs.Add($"包络 [{srcStats.MinX:F3},{srcStats.MinY:F3},{srcStats.MaxX:F3},{srcStats.MaxY:F3}]" +
                      $"→[{dstStats.MinX:F3},{dstStats.MinY:F3},{dstStats.MaxX:F3},{dstStats.MaxY:F3}]");
        if (spec.HasZ && (srcStats.ZCount != dstStats.ZCount ||
                          !AlmostEqual(srcStats.ZMin, dstStats.ZMin) || !AlmostEqual(srcStats.ZMax, dstStats.ZMax)))
            diffs.Add($"Z {srcStats.ZCount}点[{srcStats.ZMin:F3},{srcStats.ZMax:F3}]" +
                      $"→{dstStats.ZCount}点[{dstStats.ZMin:F3},{dstStats.ZMax:F3}]");

        Add(results, dim, target, "几何往返(顺序无关量算)",
            diffs.Count == 0 ? CheckStatus.Pass : CheckStatus.Fail,
            diffs.Count == 0
                ? $"顶点 {srcStats.Vertices}、总长 {srcStats.LengthSum / 1000:F3} km、Z 点 {srcStats.ZCount} 个，全部量算指标一致"
                : string.Join("；", diffs));

        // FID 与读回顺序保真：显式保留源 FID（含 PostgreSQL 下的 0）后，读回 FID 应逐一对应源 FID。
        // 若不一致，通常意味着写入时 FID 与序列自动分配值发生了链式 UNIQUE 冲突（旧缺陷特征），
        // 或数据库读回顺序与写入顺序不同（无 ORDER BY 保证）。
        var fidDiffs = new List<string>();
        var fidCount = Math.Min(source.GetFeatureCount(), back.GetFeatureCount());
        for (var i = 0; i < fidCount; i++)
        {
            var sf = source.Features[i].Fid;
            var bf = back.Features[i].Fid;
            if (sf != bf)
                fidDiffs.Add($"#{i}:{sf}→{bf}");
        }

        Add(results, dim, target, "FID 往返保真",
            fidDiffs.Count == 0 ? CheckStatus.Pass : CheckStatus.Warn,
            fidDiffs.Count == 0
                ? $"全部 {fidCount} 个要素 FID 与读回顺序逐一保真"
                : $"{fidDiffs.Count} 处 FID/顺序变化：{string.Join("；", fidDiffs.Take(3))}");
    }

    private static void CheckSpatialIndex(List<CheckResult> results, string dim, string target,
        OgrDataSource ds, string conn, string table, string? geomColumn)
    {
        if (string.IsNullOrEmpty(geomColumn))
        {
            Add(results, dim, target, "CreateSpatialIndex", CheckStatus.Info, "几何列名未知，跳过");
            return;
        }

        var defaultError = TryCall(() => PostgisUtil.CreateSpatialIndex(conn, table));
        var detectError = TryCall(() => PostgisUtil.CreateSpatialIndex(conn, table, null));
        var indexCount = ScalarSql(ds,
            $"SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND tablename='{table}'");

        Add(results, dim, target, "CreateSpatialIndex(默认列名)",
            defaultError == null && ParseInt(indexCount) >= 1 ? CheckStatus.Pass : CheckStatus.Fail,
            defaultError == null
                ? $"pg_indexes 命中 {indexCount} 条，默认列名与 GDAL 实际几何列 \"{geomColumn}\" 一致"
                : $"默认参数调用失败：{Truncate(defaultError, 140)}");

        Add(results, dim, target, "CreateSpatialIndex(null 自动探测)",
            detectError == null && ParseInt(indexCount) >= 1 ? CheckStatus.Pass : CheckStatus.Fail,
            detectError == null
                ? $"传 null 时探测到实际几何列 \"{geomColumn}\"，pg_indexes 命中 {indexCount} 条"
                : $"自动探测失败：{Truncate(detectError, 140)}");

        var secondError = TryCall(() => PostgisUtil.CreateSpatialIndex(conn, table, geomColumn));
        Add(results, dim, target, "CreateSpatialIndex(重复调用幂等)",
            secondError == null ? CheckStatus.Pass : CheckStatus.Fail,
            secondError == null
                ? "重复调用未抛异常"
                : $"重复调用抛异常（非幂等）：{Truncate(secondError, 120)}");
    }

    private static void CheckAttributeFilter(List<CheckResult> results, string dim, string target,
        OgrDataSource ds, string conn, string table, OguLayer back)
    {
        var column = back.Fields.Select(f => f.Name).FirstOrDefault();
        if (column == null)
        {
            Add(results, dim, target, "属性过滤下推", CheckStatus.Info, "读回图层无字段，跳过");
            return;
        }

        var expectedNotNull = ParseInt(ScalarSql(ds, $"SELECT count(*) FROM \"{table}\" WHERE \"{column}\" IS NOT NULL"));
        var notNullFilter = TryCall(() => PostgisUtil.ReadPostGIS(conn, table, $"\"{column}\" IS NOT NULL"),
            out var notNullLayer);
        var impossibleFilter = TryCall(
            () => PostgisUtil.ReadPostGIS(conn, table, $"\"{column}\" IS NULL AND \"{column}\" IS NOT NULL"),
            out var emptyLayer);

        var ok = notNullFilter == null && impossibleFilter == null &&
                 notNullLayer?.GetFeatureCount() == expectedNotNull &&
                 emptyLayer?.GetFeatureCount() == 0;
        Add(results, dim, target, "属性过滤下推", ok ? CheckStatus.Pass : CheckStatus.Fail,
            ok
                ? $"\"{column}\" IS NOT NULL 读回 {notNullLayer!.GetFeatureCount()} 行（服务端 count={expectedNotNull}），恒假条件读回 0 行"
                : $"过滤结果异常：非空过滤 {notNullLayer?.GetFeatureCount().ToString() ?? "异常"}（服务端 {expectedNotNull}），" +
                  $"恒假过滤 {emptyLayer?.GetFeatureCount().ToString() ?? "异常"}（应为 0）；" +
                  $"错误 {notNullFilter ?? impossibleFilter ?? "无"}");
    }

    private static void CheckRewriteBehavior(List<CheckResult> results, string dim, string target,
        OgrDataSource ds, string conn, OguLayer layer, string table)
    {
        var before = ParseInt(ScalarSql(ds, $"SELECT count(*) FROM \"{table}\""));
        var error = TryCall(() => PostgisUtil.WritePostGIS(layer, conn, table));
        var after = ParseInt(ScalarSql(ds, $"SELECT count(*) FROM \"{table}\""));

        var detail = error != null
            ? $"二次写入按默认策略拒绝覆盖：{Truncate(error, 140)}（表内行数仍为 {after}，见 overwrite=true 检查）"
            : after == before
                ? $"二次写入成功且行数不变（{after}），表现为覆盖/幂等"
                : $"二次写入成功后行数 {before}→{after}，表现为追加";
        Add(results, dim, target, "重复写入同名表", CheckStatus.Info, detail);

        var overwriteError = TryCall(() => OguLayerUtil.WriteLayer(DataFormatType.POSTGIS, layer, conn, table,
            null, new Dictionary<string, object> { { "overwrite", true } }));
        var afterOverwrite = ParseInt(ScalarSql(ds, $"SELECT count(*) FROM \"{table}\""));
        Add(results, dim, target, "overwrite=true 覆盖写入",
            overwriteError == null && afterOverwrite == before ? CheckStatus.Pass : CheckStatus.Fail,
            overwriteError != null
                ? $"异常: {Truncate(overwriteError, 140)}"
                : $"覆盖写入成功，行数 {before}→{afterOverwrite}");
    }

    /// <summary>
    ///     公共入口面检查：OguLayerUtil 的 POSTGIS 路径、不可达连接降级、文档中的连接串格式
    /// </summary>
    private static void RunPostgisEntryChecks(List<CheckResult> results, string conn,
        IReadOnlyList<(LayerSpec Spec, OguLayer Layer)> loaded, List<string> createdTables)
    {
        const string dim = PostgisDimension;
        var smallest = loaded.OrderBy(x => x.Layer.GetFeatureCount()).First();
        var table = PgTablePrefix + smallest.Spec.LayerName.ToLowerInvariant();

        // OguLayerUtil 读取 POSTGIS
        var readError = TryCall(() => OguLayerUtil.ReadLayer(DataFormatType.POSTGIS, conn, table), out var viaUtil);
        Add(results, dim, smallest.Spec.LayerName, "OguLayerUtil.ReadLayer(POSTGIS)",
            readError == null && viaUtil?.GetFeatureCount() == smallest.Spec.ExpectedCount
                ? CheckStatus.Pass
                : CheckStatus.Fail,
            readError == null
                ? $"读回 {viaUtil!.GetFeatureCount()} 要素，与 PostgisUtil.ReadPostGIS 等价"
                : $"异常: {Truncate(readError, 160)}");

        // OguLayerUtil 图层名枚举
        var namesError = TryCall(() => OguLayerUtil.GetLayerNames(DataFormatType.POSTGIS, conn), out var names);
        Add(results, dim, "-", "OguLayerUtil.GetLayerNames(POSTGIS)",
            namesError == null && names != null && names.Contains(table) ? CheckStatus.Pass : CheckStatus.Warn,
            namesError == null
                ? $"返回 {names!.Count} 个图层名，含 {table}"
                : $"异常: {Truncate(namesError, 160)}");

        // OguLayerUtil 写入 POSTGIS：连接串无扩展名，驱动必须按 PG: 前缀识别
        var writeTable = PgTablePrefix + "entry_write";
        createdTables.Add(writeTable);
        var cwdBefore = Directory.GetFiles(Environment.CurrentDirectory);
        var writeError = TryCall(() =>
            OguLayerUtil.WriteLayer(DataFormatType.POSTGIS, smallest.Layer, conn, writeTable));
        var junk = Directory.GetFiles(Environment.CurrentDirectory).Except(cwdBefore).ToList();
        foreach (var file in junk)
            TryDelete(file);

        Add(results, dim, smallest.Spec.LayerName, "OguLayerUtil.WriteLayer(POSTGIS)",
            writeError == null && junk.Count == 0 ? CheckStatus.Pass : CheckStatus.Fail,
            writeError != null
                ? $"该入口无法写入 PostGIS：{Truncate(writeError, 160)}（DataFormatType.POSTGIS 未被路由到 PostgreSQL 驱动）"
                : junk.Count > 0
                    ? $"未按连接串写入数据库，反而在当前目录生成文件：{string.Join(",", junk.Select(Path.GetFileName).Take(3))}"
                    : "写入成功");

        // 不可达连接必须与"表不存在"区分开
        var unreachable = "PG:host=127.0.0.1 port=1 dbname=postgres user=postgres password=postgres";
        var degradeError = TryCall(() => PostgisUtil.TableExists(unreachable, table), out var degraded);
        Add(results, dim, "-", "TableExists(不可达连接)",
            degradeError != null ? CheckStatus.Pass : CheckStatus.Fail,
            degradeError != null
                ? $"抛异常而非静默 false，调用方可区分连接失败：{Truncate(degradeError, 120)}"
                : $"返回 {degraded}（静默降级，调用方无法区分\"表不存在\"与\"连不上\"）");

        // PostgisUtil 注释曾推荐带引号连接串，该形式在 GDAL 下不可用
        var quoted = "PG:\"" + conn.Substring("PG:".Length) + "\"";
        var quotedError = TryCall(() => PostgisUtil.TableExists(quoted, table), out var quotedResult);
        Add(results, dim, "-", "带引号连接串(PG:\"...\")",
            quotedError != null ? CheckStatus.Info : CheckStatus.Fail,
            quotedError != null
                ? $"带引号形式不可用（文档已改为无引号写法）：{Truncate(quotedError, 100)}"
                : $"带引号形式意外可用：返回 {quotedResult}");
    }

    /// <summary>
    ///     客户端 GDAL/PROJ 与服务端 PostGIS/PROJ 的转换管道对比，以及 4490 表往返
    /// </summary>
    private static void RunPostgisCrsChecks(List<CheckResult> results, string conn,
        IReadOnlyList<(LayerSpec Spec, OguLayer Layer)> loaded, List<string> createdTables)
    {
        const string dim = PostgisDimension;

        var probe = loaded
            .Where(x => x.Layer.Wkid is int w && w != Wgs84Wkid && w != Cgcs2000Wkid && x.Layer.GetFeatureCount() > 0)
            .OrderByDescending(x => x.Layer.GetFeatureCount())
            .FirstOrDefault();
        if (probe.Layer == null)
        {
            Add(results, dim, "-", "转换管道对比", CheckStatus.Info, "数据目录中无投影坐标系图层，跳过");
            return;
        }

        var srcWkid = probe.Layer.Wkid!.Value;
        var target = probe.Spec.LayerName;
        var wkt = probe.Layer.Features.First(f => !string.IsNullOrWhiteSpace(f.Wkt)).Wkt!;
        var escaped = wkt.Replace("'", "''");

        double? c1x = null, c1y = null, c2x = null, c2y = null;
        string? clientError = null;
        try
        {
            (c1x, c1y) = FirstCoordinate(CrsUtil.Transform(wkt, srcWkid, Cgcs2000Wkid));
            (c2x, c2y) = FirstCoordinate(CrsUtil.TransformThrough(wkt, srcWkid, Wgs84Wkid, Cgcs2000Wkid));
        }
        catch (System.Exception ex)
        {
            clientError = RootMessage(ex);
        }

        using var ds = Ogr.Open(conn, 0);
        if (ds == null || clientError != null)
        {
            Add(results, dim, target, "转换管道对比", CheckStatus.Fail,
                $"无法完成对比：{(clientError != null ? $"客户端异常 {Truncate(clientError, 120)}" : "无法打开数据源")}");
            return;
        }

        // 别名必须避开 st_astext：OGR 的 PostgreSQL 驱动会把名为 st_astext 的结果列当作 WKT 几何列，
        // 导致字段数为 0、ScalarSql 取不到值。
        var s1 = ScalarSql(ds,
            $"SELECT ST_AsText(ST_Transform(ST_GeomFromText('{escaped}', {srcWkid}), {Cgcs2000Wkid})) AS direct_wkt");
        var s2 = ScalarSql(ds,
            $"SELECT ST_AsText(ST_Transform(ST_Transform(ST_GeomFromText('{escaped}', {srcWkid}), {Wgs84Wkid}), {Cgcs2000Wkid})) AS through_wkt");

        if (string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2))
        {
            OSGeo.GDAL.Gdal.ErrorReset();
            Add(results, dim, target, $"服务端 ST_Transform({srcWkid}→{Cgcs2000Wkid})", CheckStatus.Warn,
                $"服务端未返回结果：直连={s1 ?? "null"}，经4326={s2 ?? "null"}，GDAL 错误={Truncate(OSGeo.GDAL.Gdal.GetLastErrorMsg(), 160)}");
            return;
        }

        var (s1x, s1y) = FirstCoordinate(s1);
        var (s2x, s2y) = FirstCoordinate(s2);

        var direct = MetersBetweenDegrees(c1x!.Value, c1y!.Value, s1x, s1y);
        var through = MetersBetweenDegrees(c2x!.Value, c2y!.Value, s2x, s2y);
        Add(results, dim, target, $"客户端 vs 服务端（{srcWkid}→{Cgcs2000Wkid} 直连）",
            direct < 1 ? CheckStatus.Pass : direct < 100 ? CheckStatus.Warn : CheckStatus.Fail,
            $"同一管道下客户端 ({c1x:F7},{c1y:F7}) 与服务端 ({s1x:F7},{s1y:F7}) 相差 {direct:F3} m（客户端 PROJ 与服务端 PROJ 版本差异）");
        Add(results, dim, target, $"客户端 vs 服务端（{srcWkid}→{Wgs84Wkid}→{Cgcs2000Wkid}）",
            through < 1 ? CheckStatus.Pass : through < 100 ? CheckStatus.Warn : CheckStatus.Fail,
            $"同一管道下客户端 ({c2x:F7},{c2y:F7}) 与服务端 ({s2x:F7},{s2y:F7}) 相差 {through:F3} m");

        var clientGap = MetersBetweenDegrees(c1x.Value, c1y.Value, c2x.Value, c2y.Value);
        var serverGap = MetersBetweenDegrees(s1x, s1y, s2x, s2y);
        Add(results, dim, target, "直连 vs 经4326中转（管道系统偏差）",
            clientGap < 1 && serverGap < 1 ? CheckStatus.Pass : CheckStatus.Warn,
            $"客户端相差 {clientGap:F3} m，服务端相差 {serverGap:F3} m；" +
            (clientGap >= 1 || serverGap >= 1
                ? $"说明 {srcWkid}→{Cgcs2000Wkid} 必须显式经 {Wgs84Wkid} 中转（CrsUtil.TransformThrough）"
                : "两条管道结果一致，无需强制中转"));

        var recommendation = CrsUtil.GetTransformRecommendation(srcWkid, Cgcs2000Wkid);
        Add(results, dim, target, "转换建议与实测一致",
            recommendation.RequiresExplicitPath == (clientGap >= 1 || serverGap >= 1) ? CheckStatus.Pass : CheckStatus.Warn,
            $"GetTransformRecommendation({srcWkid},{Cgcs2000Wkid}).RequiresExplicitPath={recommendation.RequiresExplicitPath}，" +
            $"实测管道偏差 {Math.Max(clientGap, serverGap):F3} m；中转建议 {string.Join("→", recommendation.IntermediateWkids)}");

        // 4490 表往返：客户端 TransformThrough 结果写入后，SRID 与坐标是否保持
        var crsTable = PgTablePrefix + "crs" + Cgcs2000Wkid;
        createdTables.Add(crsTable);
        ExecSql(conn, $"DROP TABLE IF EXISTS \"{crsTable}\" CASCADE");
        var converted = BuildConvertedPointLayer(probe.Layer, srcWkid, crsTable);
        var writeError = TryCall(() => PostgisUtil.WritePostGIS(converted, conn, crsTable));
        if (writeError != null)
        {
            Add(results, dim, target, $"{Cgcs2000Wkid} 表往返", CheckStatus.Fail, $"写入异常: {Truncate(writeError, 160)}");
            return;
        }

        var readError = TryCall(() => PostgisUtil.ReadPostGIS(conn, crsTable), out var crsBack);
        if (readError != null || crsBack == null)
        {
            Add(results, dim, target, $"{Cgcs2000Wkid} 表往返", CheckStatus.Fail, $"读回异常: {Truncate(readError!, 160)}");
            return;
        }

        var sridBack = ParseInt(ScalarSql(ds,
            $"SELECT srid FROM geometry_columns WHERE f_table_schema='public' AND f_table_name='{crsTable}'"));
        var maxShift = 0.0;
        for (var i = 0; i < Math.Min(converted.GetFeatureCount(), crsBack.GetFeatureCount()); i++)
        {
            var (ax, ay) = FirstCoordinate(converted.Features[i].Wkt!);
            var (bx, by) = FirstCoordinate(crsBack.Features[i].Wkt!);
            maxShift = Math.Max(maxShift, Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by)));
        }

        Add(results, dim, target, $"{Cgcs2000Wkid} 表往返",
            crsBack.Wkid == Cgcs2000Wkid && sridBack == Cgcs2000Wkid && maxShift < 1e-9
                ? CheckStatus.Pass
                : CheckStatus.Fail,
            $"写入 Wkid={Cgcs2000Wkid}，服务端 srid={sridBack}，读回 Wkid={crsBack.Wkid?.ToString() ?? "null"}，" +
            $"要素 {crsBack.GetFeatureCount()}/{converted.GetFeatureCount()}，最大坐标漂移 {maxShift:G3}°");
    }

    /// <summary>
    ///     取源图层前若干点要素，按 source → 4326 → 4490 显式中转后构造一个 CGCS2000 点图层
    /// </summary>
    private static OguLayer BuildConvertedPointLayer(OguLayer source, int srcWkid, string name)
    {
        var result = new OguLayer
        {
            Name = name,
            Wkid = Cgcs2000Wkid,
            GeometryType = GeometryType.POINT,
            Fields = { new OguField { Name = "src_wkid", DataType = FieldDataType.INTEGER } }
        };

        var fid = 1;
        foreach (var feature in source.Features.Where(f => !string.IsNullOrWhiteSpace(f.Wkt)).Take(5))
        {
            var centroid = GeometryUtil.CentroidWkt(feature.Wkt!);
            result.Features.Add(new OguFeature
            {
                Fid = fid++,
                Wkt = CrsUtil.TransformThrough(centroid, srcWkid, Wgs84Wkid, Cgcs2000Wkid),
                Attributes = { ["src_wkid"] = new OguFieldValue(srcWkid) }
            });
        }

        return result;
    }

    // ─────────────────────────── PostGIS SQL 辅助 ───────────────────────────

    private static string DescribeServers(string conn)
    {
        using var ds = Ogr.Open(conn, 0)!;
        var pgVersion = ScalarSql(ds, "SELECT current_setting('server_version')");
        var full = ScalarSql(ds, "SELECT postgis_full_version()") ?? "";
        return $"PostgreSQL {pgVersion}；客户端 GDAL {OSGeo.GDAL.Gdal.VersionInfo("RELEASE_NAME")}；服务端 {Truncate(full, 200)}";
    }

    private static void CleanupTables(string conn, IEnumerable<string> tables)
    {
        foreach (var table in tables.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                ExecSql(conn, $"DROP TABLE IF EXISTS \"{table}\" CASCADE");
            }
            catch (System.Exception)
            {
                // 清理失败不应掩盖检查结果：表名均带 ogu_test_ 前缀，可手工清理
            }
        }
    }

    private static (bool Ok, string? Error) ExecSql(string conn, string sql)
    {
        var ds = Ogr.Open(conn, 1);
        if (ds == null)
            return (false, "无法以更新模式打开数据源");
        try
        {
            OSGeo.GDAL.Gdal.ErrorReset();
            var result = ds.ExecuteSQL(sql, null, null);
            if (result != null)
                ds.ReleaseResultSet(result);
            var type = OSGeo.GDAL.Gdal.GetLastErrorType();
            if (type < 3)
                return (true, null);
            var message = OSGeo.GDAL.Gdal.GetLastErrorMsg();
            return (false, string.IsNullOrWhiteSpace(message) ? $"GDAL 错误类型 {type}" : message);
        }
        catch (System.Exception ex)
        {
            return (false, RootMessage(ex));
        }
        finally
        {
            ds.Dispose();
        }
    }

    private static string? ScalarSql(string conn, string sql)
    {
        using var ds = Ogr.Open(conn, 0);
        return ds == null ? null : ScalarSql(ds, sql);
    }

    private static string? ScalarSql(OgrDataSource ds, string sql)
    {
        var row = QueryRow(ds, sql);
        return row == null || row.Length == 0 ? null : row[0];
    }

    private static string?[]? QueryRow(OgrDataSource ds, string sql)
    {
        var result = ds.ExecuteSQL(sql, null, null);
        if (result == null)
            return null;
        try
        {
            var feature = result.GetNextFeature();
            if (feature == null)
                return null;
            try
            {
                var count = feature.GetDefnRef().GetFieldCount();
                if (count == 0)
                    return GeometryFallback(feature);
                var values = new string?[count];
                for (var i = 0; i < count; i++)
                    values[i] = feature.IsFieldSet(i) ? feature.GetFieldAsString(i) : null;
                return values;
            }
            finally
            {
                feature.Dispose();
            }
        }
        finally
        {
            ds.ReleaseResultSet(result);
        }
    }

    /// <summary>
    /// OGR 的 PostgreSQL 驱动会把名为 st_astext 的结果列解析成 WKT 几何列而不是属性字段，
    /// 字段数为 0 时只能从几何引用取回文本。
    /// </summary>
    private static string?[]? GeometryFallback(OSGeo.OGR.Feature feature)
    {
        var geometry = feature.GetGeometryRef();
        if (geometry == null)
            return Array.Empty<string?>();
        return geometry.ExportToWkt(out var wkt) == 0 && !string.IsNullOrWhiteSpace(wkt)
            ? [wkt]
            : Array.Empty<string?>();
    }

    private static string? TryCall(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (System.Exception ex)
        {
            return $"{ex.GetType().Name}: {RootMessage(ex)}";
        }
    }

    private static string? TryCall<T>(Func<T> action, out T? value)
    {
        try
        {
            value = action();
            return null;
        }
        catch (System.Exception ex)
        {
            value = default;
            return $"{ex.GetType().Name}: {RootMessage(ex)}";
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (System.Exception)
        {
            // 删除失败仅影响临时产物清理，不影响检查结论
        }
    }

    // ─────────────────────────── 比较与工具 ───────────────────────────

    private enum DiffKind
    {
        FeatureCount,
        Geometry,
        Field,
        AttributeValue
    }

    private sealed record LayerDiff(DiffKind Kind, string Detail)
    {
        public override string ToString()
        {
            return $"{Kind}: {Detail}";
        }
    }

    /// <summary>
    ///     逐要素比较两个图层（按索引对齐）。几何用 EqualsExactTolerance(1mm) 加
    ///     NumPoints/Length 兜底（容忍仅 Z 维差异与类型提升）。
    /// </summary>
    private static List<LayerDiff> CompareLayers(OguLayer source, OguLayer target, DataFormatType via)
    {
        var diffs = new List<LayerDiff>();
        if (source.GetFeatureCount() != target.GetFeatureCount())
            diffs.Add(new LayerDiff(DiffKind.FeatureCount,
                $"{source.GetFeatureCount()} → {target.GetFeatureCount()}"));

        var srcFields = source.Fields.Select(f => f.Name).ToHashSet();
        var dstFields = target.Fields.Select(f => f.Name).ToHashSet();
        var missing = srcFields.Except(dstFields).ToList();
        if (missing.Count > 0)
            diffs.Add(new LayerDiff(DiffKind.Field, $"缺字段 {string.Join(",", missing.Take(5))}"));
        var extra = dstFields.Except(srcFields).ToList();
        if (extra.Count > 0)
            diffs.Add(new LayerDiff(DiffKind.Field, $"多字段 {string.Join(",", extra.Take(5))}"));

        var n = Math.Min(source.GetFeatureCount(), target.GetFeatureCount());
        var geomDiff = 0;
        var attrDiff = 0;
        var geomSample = "";
        var attrSample = "";
        for (var i = 0; i < n; i++)
        {
            var a = source.Features[i];
            var b = target.Features[i];
            var g = CompareWkt(a.Wkt, b.Wkt);
            if (g != null)
            {
                geomDiff++;
                if (geomSample == "") geomSample = $"#{i} {g}";
            }

            foreach (var field in srcFields.Intersect(dstFields))
            {
                var va = a.Attributes.TryGetValue(field, out var x) ? x.Value : null;
                var vb = b.Attributes.TryGetValue(field, out var y) ? y.Value : null;
                if (!ValueEquals(va, vb))
                {
                    attrDiff++;
                    if (attrSample == "" &&
                        (va?.ToString()?.Any(c => c > 127) == true || vb?.ToString()?.Any(c => c > 127) == true))
                        attrSample = $"#{i}.{field}: [{va}]→[{vb}]";
                }
            }
        }

        if (geomDiff > 0) diffs.Add(new LayerDiff(DiffKind.Geometry, $"{geomDiff} 个不一致，如 {geomSample}"));
        if (attrDiff > 0) diffs.Add(new LayerDiff(DiffKind.AttributeValue, $"{attrDiff} 处不一致，如 {attrSample}"));
        return diffs;
    }

    private static string? CompareWkt(string? wa, string? wb)
    {
        if (string.IsNullOrEmpty(wa) && string.IsNullOrEmpty(wb)) return null;
        if (string.IsNullOrEmpty(wa) || string.IsNullOrEmpty(wb)) return "一侧为空";
        try
        {
            using var ga = GeometryUtil.Wkt2Geometry(wa);
            using var gb = GeometryUtil.Wkt2Geometry(wb);
            if (ga == null || gb == null) return "WKT解析失败";
            if (GeometryUtil.EqualsExactTolerance(ga, gb, 0.001)) return null;
            // Z 维丢失/类型提升（如 POINT→POINT Z 差异）时用 2D 指标兜底
            if (GeometryUtil.NumPoints(ga) == GeometryUtil.NumPoints(gb))
            {
                var la = GeometryUtil.Length(ga);
                var lb = GeometryUtil.Length(gb);
                if (Math.Abs(la - lb) <= Math.Max(0.001, la * 1e-6)) return null;
            }

            return "几何不一致";
        }
        catch (System.Exception ex)
        {
            return $"比较异常: {ex.Message}";
        }
    }

    private static bool ValueEquals(object? va, object? vb)
    {
        if (va == null && vb == null) return true;
        if (va == null || vb == null) return false;
        var sa = va.ToString()!;
        var sb = vb.ToString()!;
        if (string.Equals(sa, sb, StringComparison.Ordinal)) return true;
        if (double.TryParse(sa, NumberStyles.Float, CultureInfo.InvariantCulture, out var da) &&
            double.TryParse(sb, NumberStyles.Float, CultureInfo.InvariantCulture, out var db))
            return Math.Abs(da - db) <= Math.Max(1e-6, Math.Abs(da) * 1e-9);
        return false;
    }

    /// <summary>
    ///     顺序无关的几何量算指标：顶点数、总长、包络、Z 值范围。
    ///     直接从 WKT 文本提取，避免读回顺序不同造成的伪差异。
    /// </summary>
    private sealed record GeomStats(
        int Vertices,
        double LengthSum,
        double MinX,
        double MinY,
        double MaxX,
        double MaxY,
        int ZCount,
        double ZMin,
        double ZMax);

    private static GeomStats ComputeGeomStats(OguLayer layer)
    {
        var vertices = 0;
        var lengthSum = 0.0;
        var zCount = 0;
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        double zMin = double.MaxValue, zMax = double.MinValue;

        foreach (var feature in layer.Features)
        {
            var wkt = feature.Wkt;
            if (string.IsNullOrWhiteSpace(wkt))
                continue;

            foreach (Match m in CoordPairRegex().Matches(wkt))
            {
                var x = ParseCoordinate(m.Groups[1].Value);
                var y = ParseCoordinate(m.Groups[2].Value);
                vertices++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            foreach (Match m in ZRegex().Matches(wkt))
            {
                var z = ParseCoordinate(m.Groups[3].Value);
                zCount++;
                if (z < zMin) zMin = z;
                if (z > zMax) zMax = z;
            }

            try
            {
                lengthSum += GeometryUtil.LengthWkt(wkt);
            }
            catch (System.Exception)
            {
                // 长度量算失败不阻断统计：顶点与包络已足以判定几何是否一致
            }
        }

        return new GeomStats(vertices, lengthSum, minX, minY, maxX, maxY, zCount,
            zCount > 0 ? zMin : 0, zCount > 0 ? zMax : 0);
    }

    [GeneratedRegex(@"(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)\s+(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)")]
    private static partial Regex CoordPairRegex();

    private static (double Min, double Max)? ClientZRange(OguLayer layer)
    {
        var stats = ComputeGeomStats(layer);
        return stats.ZCount > 0 ? (stats.ZMin, stats.ZMax) : null;
    }

    /// <summary>
    ///     经纬度差值的近似地面距离（米），用于量化不同转换管道之间的系统偏差
    /// </summary>
    private static double MetersBetweenDegrees(double lon1, double lat1, double lon2, double lat2)
    {
        var meanLatRadians = (lat1 + lat2) / 2 * Math.PI / 180;
        var dx = (lon2 - lon1) * 111320 * Math.Cos(meanLatRadians);
        var dy = (lat2 - lat1) * 110540;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool AlmostEqual(double a, double b)
    {
        return Math.Abs(a - b) <= Math.Max(1e-6, Math.Abs(a) * 1e-9);
    }

    /// <summary>
    ///     列值多重集合指纹：排序后哈希，与读回顺序无关
    /// </summary>
    private static string ColumnSignature(IEnumerable<object?> values)
    {
        var joined = string.Join('\u0001',
            values.Select(NormalizeValue).OrderBy(s => s, StringComparer.Ordinal));
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(joined)));
    }

    private static string NormalizeValue(object? value)
    {
        if (value == null)
            return "\0null";
        var text = value.ToString()!;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number.ToString("R", CultureInfo.InvariantCulture)
            : text;
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..max] + "…";
    }

    private static int ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;
    }

    private static double ParseDouble(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : double.NaN;
    }

    private static double ParseCoordinate(string value)
    {
        return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static string RootMessage(System.Exception ex)
    {
        var msg = ex.Message?.Trim();
        while (string.IsNullOrEmpty(msg) && ex.InnerException != null)
        {
            ex = ex.InnerException;
            msg = ex.Message?.Trim();
        }

        return (msg ?? ex.GetType().Name).ReplaceLineEndings(" ");
    }

    internal static void Add(List<CheckResult> results, string dim, string target, string check, CheckStatus status,
        string details)
    {
        results.Add(new CheckResult(dim, target, check, status, details));
    }
}
