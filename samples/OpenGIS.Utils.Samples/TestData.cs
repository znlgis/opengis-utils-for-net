using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     合成测试数据生成器。
///     所有坐标、名称均为虚构(基点 108.9E/34.3N 只是一个合成的数值起点,
///     地名一律使用"示例"占位),不包含任何真实人员、权属或测绘成果数据。
///     随机数固定种子,保证每次运行输出一致,便于对照学习。
/// </summary>
public static class TestData
{
    /// <summary>合成数据参考起点(虚构),便于所有图层落在同一小范围内。</summary>
    public const double BaseLon = 108.900;

    public const double BaseLat = 34.300;

    /// <summary>演示统一使用 CGCS2000 经纬度坐标系。</summary>
    public const int BaseWkid = 4490;

    /// <summary>
    ///     面图层:虚构地块,Fid 从 100 起(非零,用于演示 FID 保留)。
    ///     字段名全部 ASCII(DBF 兼容),字段值中文(GBK 场景)。
    /// </summary>
    public static OguLayer BuildParcels(int count = 6)
    {
        var layer = new OguLayer
        {
            Name = "demo_parcels",
            Wkid = BaseWkid,
            GeometryType = GeometryType.POLYGON
        };
        AddParcelFields(layer);

        var rnd = new Random(20260909);
        for (var i = 0; i < count; i++)
        {
            var lon = BaseLon + i * 0.004 + rnd.NextDouble() * 0.001;
            var lat = BaseLat + (i / 2) * 0.004;
            var feature = new OguFeature
            {
                Fid = 100 + i,
                Wkt = Rect(lon, lat, 0.002, 0.0015)
            };
            FillParcelFeature(feature, 100 + i, i, rnd);
            layer.AddFeature(feature);
        }

        return layer;
    }

    /// <summary>
    ///     与 <see cref="BuildParcels" /> 字段结构完全一致的增补图层(Fid 从 200 起),
    ///     用于演示向已有数据源 Append 追加要素。
    /// </summary>
    public static OguLayer BuildParcelAppends(int count = 3)
    {
        var layer = BuildParcels(count); // 先复用同一字段结构与生成逻辑
        layer.Name = "demo_parcels_extra";

        var rnd = new Random(20260910);
        for (var i = 0; i < count; i++)
        {
            var feature = layer.Features[i];
            feature.Fid = 200 + i;
            feature.Wkt = Rect(BaseLon + 0.10 + i * 0.004, BaseLat + 0.03, 0.002, 0.0015);
            layer.Features[i].SetValue("ID", 200 + i);
            layer.Features[i].SetValue("NAME", $"示例增补地块-{i + 1:D2}");
            layer.Features[i].SetValue("GREEN_RATIO", Math.Round(rnd.NextDouble() * 0.5, 2));
            layer.Features[i].SetValue("CHECKED", i % 2 == 1);
        }

        return layer;
    }

    /// <summary>线图层:虚构道路,用于几何分析与多图层输出演示。</summary>
    public static OguLayer BuildRoads()
    {
        var layer = new OguLayer
        {
            Name = "demo_roads",
            Wkid = BaseWkid,
            GeometryType = GeometryType.LINESTRING
        };
        layer.AddField(new OguField { Name = "ID", DataType = FieldDataType.INTEGER });
        layer.AddField(new OguField { Name = "ROAD_NAME", DataType = FieldDataType.STRING, Length = 32 });

        var roads = new[]
        {
            ("示例一路", $"LINESTRING ({BaseLon} {BaseLat}, {BaseLon + 0.01} {BaseLat + 0.008}, {BaseLon + 0.02} {BaseLat + 0.008})"),
            ("示例二路", $"LINESTRING ({BaseLon + 0.005} {BaseLat - 0.005}, {BaseLon + 0.005} {BaseLat + 0.02})"),
            ("示例三路", $"LINESTRING ({BaseLon - 0.002} {BaseLat + 0.012}, {BaseLon + 0.018} {BaseLat + 0.002})")
        };
        for (var i = 0; i < roads.Length; i++)
        {
            var feature = new OguFeature { Fid = i + 1, Wkt = roads[i].Item2 };
            feature.SetValue("ID", i + 1);
            feature.SetValue("ROAD_NAME", roads[i].Item1);
            layer.AddFeature(feature);
        }

        return layer;
    }

    /// <summary>
    ///     测绘坐标点图层:字段沿用 GtTxtUtil 的中文约定(点号/圈号/备注),
    ///     坐标为虚构的高斯投影数值(带内假东 50 万公里起)。
    /// </summary>
    public static OguLayer BuildSurveyPoints(int count = 5)
    {
        var layer = new OguLayer
        {
            Name = "demo_survey_points",
            Wkid = 4524, // CGCS2000 / 3-degree Gauss-Kruger zone 36(EPSG 权威名核实过的真实码)
            GeometryType = GeometryType.POINT
        };
        layer.AddField(new OguField { Name = "点号", DataType = FieldDataType.STRING, Length = 16 });
        layer.AddField(new OguField { Name = "圈号", DataType = FieldDataType.STRING, Length = 16 });
        layer.AddField(new OguField { Name = "备注", DataType = FieldDataType.STRING, Length = 32 });

        for (var i = 0; i < count; i++)
        {
            var feature = new OguFeature
            {
                Fid = i + 1,
                Wkt = $"POINT (500100.000 {3798000.000 + i * 100.000} {450.5 + i})"
            };
            feature.SetValue("点号", $"J{i + 1}");
            feature.SetValue("圈号", i < count / 2 ? "1" : "2");
            feature.SetValue("备注", "示例控制点");
            layer.AddFeature(feature);
        }

        return layer;
    }

    /// <summary>生成矩形面 WKT(经度 lon、纬度 lat 起,宽 w 高 h,单位:度)。</summary>
    public static string Rect(double lon, double lat, double w, double h)
    {
        return $"POLYGON (({lon:0.######} {lat:0.######}, {lon + w:0.######} {lat:0.######}, " +
               $"{lon + w:0.######} {lat + h:0.######}, {lon:0.######} {lat + h:0.######}, " +
               $"{lon:0.######} {lat:0.######}))";
    }

    private static void AddParcelFields(OguLayer layer)
    {
        layer.AddField(new OguField { Name = "ID", DataType = FieldDataType.INTEGER });
        layer.AddField(new OguField { Name = "NAME", DataType = FieldDataType.STRING, Length = 32 });
        layer.AddField(new OguField { Name = "GREEN_RATIO", DataType = FieldDataType.DOUBLE });
        layer.AddField(new OguField { Name = "CHECKED", DataType = FieldDataType.BOOLEAN });
    }

    private static void FillParcelFeature(OguFeature feature, int id, int index, Random rnd)
    {
        feature.SetValue("ID", id);
        feature.SetValue("NAME", $"示例地块-{index + 1:D2}");
        feature.SetValue("GREEN_RATIO", Math.Round(rnd.NextDouble() * 0.5, 2));
        feature.SetValue("CHECKED", index % 2 == 0);
    }
}
