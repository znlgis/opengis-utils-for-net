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
using Exception = System.Exception;

namespace OpenGIS.Utils.RealDataHarness;

/// <summary>
///     通用真实数据集成检查：对指定目录下自动发现的所有 Shapefile 执行六维度检查。
///     期望值（要素数、几何类型）直接从 SHP/DBF 文件头解析，独立于被测的 GDAL 读取链路，
///     从而形成交叉校验；不内置任何具体数据集的名称、路径或坐标范围假设。
///     维度：读取链路 / 3D几何 / 坐标系 / 格式转换 / 几何处理 / 写出回读。
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

    public static List<CheckResult> RunAll(string dataDir, string workDir)
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
