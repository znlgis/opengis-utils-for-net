using OpenGIS.Utils.Engine.Model;
using OpenGIS.Utils.Geometry;
using OgrGeometry = OSGeo.OGR.Geometry;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     02. 几何分析 GeometryUtil:WKT/GeoJSON 互转、缓冲、叠加、空间关系、测量、
///     拓扑校验、简化加密、距离判断。所有方法同时提供"Geometry 对象版"和"WKT 直入直出版",
///     后者适合不想管理 OGR 对象生命周期的场景。
/// </summary>
public sealed class GeometrySample : ISample
{
    // 虚构坐标:以合成基点为中心的小范围经纬度(单位:度)
    private const double Lon = TestData.BaseLon;
    private const double Lat = TestData.BaseLat;

    private static readonly string PolyA = TestData.Rect(Lon, Lat, 0.004, 0.004);
    private static readonly string PolyB = TestData.Rect(Lon + 0.002, Lat + 0.002, 0.004, 0.004); // 与 A 重叠
    private static readonly string PolyC = TestData.Rect(Lon + 0.004, Lat, 0.004, 0.004); // 与 A 共边(相切)
    private static readonly string PolyFar = TestData.Rect(Lon + 0.5, Lat + 0.5, 0.004, 0.004); // 完全分离
    private static readonly string LineCross = $"LINESTRING ({Lon - 0.001} {Lat - 0.001}, {Lon + 0.005} {Lat + 0.005})";

    public string Title => "02-几何分析(GeometryUtil 全套操作)";

    public void Run()
    {
        FormatConversion();
        Measurement();
        OverlayAndBuffer();
        SpatialRelations();
        SimplifyDensifyHull();
        TopologyValidation();
        EqualityAndDistance();
        WktDirectPipeline();
    }

    private static void FormatConversion()
    {
        Out.Step("1. 格式互转:WKT ↔ Geometry ↔ GeoJSON");
        using var point = GeometryUtil.Wkt2Geometry($"POINT ({Lon} {Lat})");
        Out.Result("Geometry2Geojson", GeometryUtil.Geometry2Geojson(point));
        Out.Result("Wkt2Geojson(线)", GeometryUtil.Wkt2Geojson(LineCross));
        // GeoJSON 字符串 → 几何:内部经 GDAL GeoJSON 驱动解析,支持裸几何 / Feature / FeatureCollection
        Out.Result("Geojson2Wkt(往返)", GeometryUtil.Geojson2Wkt(GeometryUtil.Geometry2Geojson(point)));
        using var fromGeojson = GeometryUtil.Geojson2Geometry("{\"type\":\"Point\",\"coordinates\":[116.4,39.9]}");
        Out.Result("Geojson2Geometry(裸几何)", GeometryUtil.Geometry2Wkt(fromGeojson));
        Out.Result("GetGeometryType", GeometryUtil.GetGeometryType(point));
        using var empty = GeometryUtil.Wkt2Geometry("POINT EMPTY");
        Out.Result("IsEmpty(POINT EMPTY)", GeometryUtil.IsEmpty(empty));
    }

    private static void Measurement()
    {
        Out.Step("2. 测量:面积/长度/质心/内点/外包盒/维度/点数(经纬度下单位为平方度/度)");
        using var poly = GeometryUtil.Wkt2Geometry(PolyA);
        Out.Result("Area", GeometryUtil.Area(poly));
        Out.Result("Length(周长)", GeometryUtil.Length(poly));
        using var centroid = GeometryUtil.Centroid(poly);
        Out.Result("Centroid", GeometryUtil.Geometry2Wkt(centroid));
        using var interior = GeometryUtil.InteriorPoint(poly);
        Out.Result("InteriorPoint", GeometryUtil.Geometry2Wkt(interior));
        using var envelope = GeometryUtil.Envelope(poly);
        Out.Result("Envelope", GeometryUtil.Geometry2Wkt(envelope));
        Out.Result("Dimension(面=2)", GeometryUtil.Dimension(poly));
        Out.Result("NumPoints", GeometryUtil.NumPoints(poly));
    }

    private static void OverlayAndBuffer()
    {
        Out.Step("3. 叠置分析与缓冲");
        using var a = GeometryUtil.Wkt2Geometry(PolyA);
        using var b = GeometryUtil.Wkt2Geometry(PolyB);
        using var buffered = GeometryUtil.Buffer(a, 0.002);
        Out.Result("Buffer 面积(原 " + Math.Round(GeometryUtil.Area(a), 6) + ")", Math.Round(GeometryUtil.Area(buffered), 6));
        using var intersection = GeometryUtil.Intersection(a, b);
        Out.Result("A∩B 面积", Math.Round(GeometryUtil.Area(intersection), 6));
        using var union = GeometryUtil.Union(a, b);
        Out.Result("A∪B 面积", Math.Round(GeometryUtil.Area(union), 6));
        using var difference = GeometryUtil.Difference(a, b);
        Out.Result("A-B 面积", Math.Round(GeometryUtil.Area(difference), 6));
        using var sym = GeometryUtil.SymDifference(a, b);
        Out.Result("A△B 面积", Math.Round(GeometryUtil.Area(sym), 6));

        using var c = GeometryUtil.Wkt2Geometry(PolyC);
        using var merged = GeometryUtil.Union(new[] { a, b, c });
        Out.Result("三矩形多元联合面积", Math.Round(GeometryUtil.Area(merged), 6));
    }

    private static void SpatialRelations()
    {
        Out.Step("4. OGC 空间关系(DE-9IM)");
        using var a = GeometryUtil.Wkt2Geometry(PolyA);
        using var b = GeometryUtil.Wkt2Geometry(PolyB);
        using var c = GeometryUtil.Wkt2Geometry(PolyC);
        using var far = GeometryUtil.Wkt2Geometry(PolyFar);
        using var line = GeometryUtil.Wkt2Geometry(LineCross);

        Out.Result("A 与 重叠矩形B", $"Intersects={GeometryUtil.Intersects(a, b)} Overlaps={GeometryUtil.Overlaps(a, b)} Contains={GeometryUtil.Contains(a, b)}");
        Out.Result("A 与 相切矩形C", $"Intersects={GeometryUtil.Intersects(a, c)} Touches={GeometryUtil.Touches(a, c)} Overlaps={GeometryUtil.Overlaps(a, c)}");
        using (var lineCenter = line.Centroid())
            Out.Result("线质心(在 A 内) Within A", GeometryUtil.Within(lineCenter, a));
        Out.Result("线L 与 A(穿入穿出)", $"Crosses={GeometryUtil.Crosses(line, a)} Intersects={GeometryUtil.Intersects(line, a)}");
        Out.Result("A 与 远矩形", $"Disjoint={GeometryUtil.Disjoint(a, far)}");
    }

    private static void SimplifyDensifyHull()
    {
        Out.Step("5. 简化 / 加密 / 凸包");
        // 构造锯齿折线:放大缩小后点数明显变化
        var pts = new List<string>();
        for (var i = 0; i <= 40; i++)
            pts.Add($"{Lon + i * 0.0005:0.######} {Lat + (i % 2 == 0 ? 0.001 : 0.0011):0.######}");
        using var zigzag = GeometryUtil.Wkt2Geometry($"LINESTRING ({string.Join(", ", pts)})");
        Out.Result("原始点数", GeometryUtil.NumPoints(zigzag));
        using var simplified = GeometryUtil.Simplify(zigzag, 0.0005);
        Out.Result("Simplify(tol=0.0005) 点数", GeometryUtil.NumPoints(simplified));
        using var densified = GeometryUtil.Densify(zigzag, 0.002);
        Out.Result("Densify(dist=0.002) 点数", GeometryUtil.NumPoints(densified));

        using var scattered = GeometryUtil.Wkt2Geometry(
            $"MULTIPOINT (({Lon} {Lat}), ({Lon + 0.01} {Lat}), ({Lon + 0.005} {Lat + 0.01}), ({Lon + 0.002} {Lat - 0.002}))");
        using var hull = GeometryUtil.ConvexHull(scattered);
        Out.Result("ConvexHull", GeometryUtil.Geometry2Wkt(hull));
    }

    private static void TopologyValidation()
    {
        Out.Step("6. 拓扑校验:IsValid(结构化结果,含错误类型与位置) / IsSimple");
        using var good = GeometryUtil.Wkt2Geometry(PolyA);
        var goodResult = GeometryUtil.IsValid(good);
        Out.Result("正常矩形 IsValid", goodResult.IsValid);

        using var bowtie = GeometryUtil.Wkt2Geometry("POLYGON ((0 0, 1 1, 0 1, 1 0, 0 0))"); // 自相交"蝴蝶结"
        var badResult = GeometryUtil.IsValid(bowtie);
        Out.Result("蝴蝶结 IsValid", badResult.IsValid);
        Out.Result("错误类型", badResult.ErrorType);
        Out.Result("错误消息", badResult.ErrorMessage);
        Out.Result("错误位置", badResult.ErrorLocation);

        using var selfCrossLine = GeometryUtil.Wkt2Geometry("LINESTRING (0 0, 1 1, 1 0, 0 1)");
        var simpleResult = GeometryUtil.IsSimple(selfCrossLine);
        Out.Result("自交折线 IsSimple", $"{simpleResult.IsSimple} ({simpleResult.Reason})");
        Out.Note("经纬度数据判断拓扑前应先用 CrsUtil.GetTolerance 选择合适的容差,见 06 坐标系示例");
    }

    private static void EqualityAndDistance()
    {
        Out.Step("7. 相等判断与距离");
        using var a = GeometryUtil.Wkt2Geometry(PolyA);
        using var aCopy = GeometryUtil.Wkt2Geometry(PolyA);
        using var b = GeometryUtil.Wkt2Geometry(PolyB);
        Out.Result("EqualsExact(同 WKT)", GeometryUtil.EqualsExact(a, aCopy));
        Out.Result("EqualsExact(不同几何)", GeometryUtil.EqualsExact(a, b));
        Out.Result("EqualsTopo(同 WKT)", GeometryUtil.EqualsTopo(a, aCopy));
        using (var nudged = GeometryUtil.Wkt2Geometry(TestData.Rect(Lon + 1e-9, Lat, 0.004, 0.004)))
            Out.Result("EqualsExactTolerance(a, a 微扰)", GeometryUtil.EqualsExactTolerance(a, nudged, 1e-6));
        Out.Result("EqualsExactTolerance(a, b)", GeometryUtil.EqualsExactTolerance(a, b, 1e-6));

        using var far = GeometryUtil.Wkt2Geometry($"POINT ({Lon + 0.01} {Lat})");
        using var center = GeometryUtil.Centroid(a);
        Out.Result("质心到远点距离(度)", Math.Round(GeometryUtil.Distance(center, far), 6));
        Out.Result("IsWithinDistance(0.02)", GeometryUtil.IsWithinDistance(center, far, 0.02));
        Out.Result("IsWithinDistance(0.001)", GeometryUtil.IsWithinDistance(center, far, 0.001));
    }

    private static void WktDirectPipeline()
    {
        Out.Step("8. WKT 直入直出版(无需管理 Geometry 生命周期)");
        Out.Result("IntersectsWkt", GeometryUtil.IntersectsWkt(PolyA, PolyB));
        Out.Result("ContainsWkt(含中心点)", GeometryUtil.ContainsWkt(PolyA, $"POINT ({Lon + 0.001} {Lat + 0.001})"));
        Out.Result("AreaWkt", Math.Round(GeometryUtil.AreaWkt(PolyA), 6));
        Out.Result("LengthWkt", Math.Round(GeometryUtil.LengthWkt(PolyA), 6));
        Out.Result("CentroidWkt", GeometryUtil.CentroidWkt(PolyA));
        Out.Result("BufferWkt(0.001) 类型", GeometryUtil.GetGeometryType(GeometryUtil.Wkt2Geometry(GeometryUtil.BufferWkt(PolyA, 0.001))));
        Out.Result("IntersectionWkt 面积", Math.Round(GeometryUtil.AreaWkt(GeometryUtil.IntersectionWkt(PolyA, PolyB)), 6));
        Out.Result("UnionWkt(3 个 WKT) 面积",
            Math.Round(GeometryUtil.AreaWkt(GeometryUtil.UnionWkt(new[] { PolyA, PolyB, PolyC })), 6));
        Out.Result("SimplifyWkt 字符数(原 " + PolyA.Length + ")", GeometryUtil.SimplifyWkt(PolyA, 0.001).Length);
    }
}
