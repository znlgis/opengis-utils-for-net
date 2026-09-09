using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     01. 统一图层模型:OguLayer / OguField / OguFeature / OguFieldValue / OguLayerMetadata。
///     这是全库所有读写、分析 API 的公共数据结构——无论数据来自 SHP、GeoJSON、TXT 还是 PostGIS,
///     最终都表现为下面的形态,学会操作它就学会了本库的"通用语"。
/// </summary>
public sealed class LayerModelSample : ISample
{
    public string Title => "01-图层模型(OguLayer/字段/要素/属性值/元数据)";

    public void Run()
    {
        Out.Step("1. 手工构建图层:混合字段类型(整型/字符串/浮点/布尔/日期)");
        var layer = new OguLayer
        {
            Name = "demo_layer",
            Wkid = TestData.BaseWkid,
            GeometryType = GeometryType.POINT
        };
        layer.AddField(new OguField { Name = "ID", DataType = FieldDataType.INTEGER });
        layer.AddField(new OguField { Name = "LABEL", DataType = FieldDataType.STRING, Length = 20 });
        layer.AddField(new OguField { Name = "SCORE", DataType = FieldDataType.DOUBLE });
        layer.AddField(new OguField { Name = "ENABLED", DataType = FieldDataType.BOOLEAN });
        layer.AddField(new OguField { Name = "RECORD_DATE", DataType = FieldDataType.DATE });
        layer.AddField(new OguField { Name = "NOTE_TEXT", DataType = FieldDataType.STRING, Length = 16 });

        var scores = new[] { 61.5, 72.0, 85.5, 91.0, 79.5 };
        for (var i = 0; i < scores.Length; i++)
        {
            var feature = new OguFeature
            {
                Fid = i + 1,
                Wkt = $"POINT ({TestData.BaseLon + i * 0.001} {TestData.BaseLat})"
            };
            feature.SetValue("ID", i + 1);
            feature.SetValue("LABEL", $"示例点-{i + 1:D2}");
            feature.SetValue("SCORE", scores[i]);
            feature.SetValue("ENABLED", i % 2 == 0);
            feature.SetValue("RECORD_DATE", new DateTime(2026, 9, 1 + i));
            layer.AddFeature(feature);
        }

        Out.Result("字段数", layer.Fields.Count);
        Out.Result("要素数", layer.GetFeatureCount());

        Out.Step("2. Validate():写出前的结构自检(名称非空、至少一个字段、字段名唯一、属性键须属于字段集)");
        layer.Validate();
        Out.Note("自检通过。违反规则时抛 LayerValidationException,见 07 异常体系示例");

        Out.Step("3. 属性取值:GetValue 拿原始值 vs GetAttribute(...).GetXxxValue() 类型化安全转换");
        var f0 = layer.Features[0];
        Out.Result("GetValue(\"ID\")", f0.GetValue("ID"));
        Out.Result("GetAttribute(\"SCORE\")?.GetDoubleValue()", f0.GetAttribute("SCORE")?.GetDoubleValue());
        Out.Result("GetAttribute(\"RECORD_DATE\")?.GetDateTimeValue()", f0.GetAttribute("RECORD_DATE")?.GetDateTimeValue());
        Out.Result("HasAttribute(\"LABEL\")", f0.HasAttribute("LABEL"));
        Out.Note("字段不存在时 GetAttribute 返回 null,不会抛异常,适合防御式读取");

        Out.Step("4. Filter:按谓词筛选要素");
        var highScore = layer.Filter(f => f.GetValue("SCORE") is double d && d >= 80);
        Out.Result("SCORE>=80 的要素", string.Join(", ", highScore.Select(f => f.GetValue("LABEL"))));

        Out.Step("5. ToJson / FromJson:内存序列化往返(跨进程传递、调试快照常用)");
        var json = layer.ToJson();
        var restored = OguLayer.FromJson(json);
        Out.Result("JSON 长度", json.Length);
        Out.Result("往返后要素数", restored?.GetFeatureCount());
        Out.Result("往返后首个 LABEL", restored?.Features[0].GetValue("LABEL"));

        Out.Step("6. Clone 深拷贝 + RemoveFeature:改副本不影响原件");
        var copy = layer.Clone();
        var removed = copy.RemoveFeature(copy.Features[0].Fid);
        Out.Result("副本删除首个要素", removed);
        Out.Result("副本要素数 / 原件要素数", $"{copy.GetFeatureCount()} / {layer.GetFeatureCount()}");

        Out.Step("7. 图层元数据 OguLayerMetadata(TXT/GPKG 等格式可持久化)");
        layer.Metadata = new OguLayerMetadata
        {
            DataSource = "OpenGIS.Utils.Samples 合成数据",
            CoordinateSystemName = "CGCS2000",
            ZoneDivision = "不分带(经纬度)",
            ProjectionType = "地理坐标系",
            MeasureUnit = "度",
            CreateTime = new DateTime(2026, 9, 9)
        };
        layer.Metadata.ExtendedProperties["survey_year"] = 2026;
        Out.Result("元数据", $"{layer.Metadata.CoordinateSystemName} / {layer.Metadata.MeasureUnit} / 扩展项 {layer.Metadata.ExtendedProperties.Count} 个");

        // 提醒:属性值类型化转换失败时返回 null(如把字符串 "abc" 转 int),避免运行时崩溃。
        // NOTE_TEXT 已在第 1 步声明为字段——SetValue 写入未声明字段会破坏 Validate 契约
        layer.Features[0].SetValue("NOTE_TEXT", "abc");
        Out.Result("非法类型转换(\"abc\".GetIntValue)",
            layer.Features[0].GetAttribute("NOTE_TEXT")?.GetIntValue()?.ToString() ?? "null");
    }
}
