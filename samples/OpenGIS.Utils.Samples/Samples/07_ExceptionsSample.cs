using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine;
using OpenGIS.Utils.Engine.Enums;
using OpenGIS.Utils.Engine.Model.Layer;
using OpenGIS.Utils.Exception;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     07. 类型化异常体系:所有业务异常继承 OguException,调用方按类型分流处理,
///     消息自带定位信息(路径/行号/要素 Fid)。本示例演示每种异常的触发条件与正确姿势。
/// </summary>
public sealed class ExceptionsSample : ISample
{
    public string Title => "07-异常体系(OguException 家族按类型分流)";

    public void Run()
    {
        Hierarchy();
        DataSourceErrors();
        ParseErrors();
        ValidationErrors();
        EngineErrors();
    }

    private static void Hierarchy()
    {
        Out.Step("1. 继承结构(均为 System.Exception 派生,可被通用日志中间件捕获)");
        var family = new[] { typeof(DataSourceException), typeof(FormatParseException), typeof(LayerValidationException), typeof(EngineNotSupportedException), typeof(TopologyException) };
        foreach (var type in family)
            Out.Result(type.Name, $"is OguException = {typeof(OguException).IsAssignableFrom(type)}");
        Out.Note("TopologyException 为几何拓扑类操作预留;当前 IsValid 系列返回结果对象而非抛出,见 02 示例");
    }

    private static void DataSourceErrors()
    {
        Out.Step("2. DataSourceException:数据源级失败,消息含具体路径/层名");
        try
        {
            OguLayerUtil.ReadLayer(DataFormatType.SHP, SampleOutput.File("07_err", "not_exists.shp"));
        }
        catch (DataSourceException ex)
        {
            Out.Result("文件不存在", Out.Head(ex.Message));
        }

        var gpkg = SampleOutput.File("07_err", "err.gpkg");
        OguLayerUtil.WriteLayer(DataFormatType.GEOPACKAGE, TestData.BuildParcels(2), gpkg, "demo_parcels");
        try
        {
            OguLayerUtil.ReadLayer(DataFormatType.GEOPACKAGE, gpkg, layerName: "no_such_layer");
        }
        catch (DataSourceException ex)
        {
            Out.Result("图层不存在", Out.Head(ex.Message));
        }

        try
        {
            new GdalWriter().Append(TestData.BuildParcelAppends(),
                SampleOutput.File("07_err", "not_exists.gpkg"), "demo_parcels");
        }
        catch (DataSourceException ex)
        {
            Out.Result("Append 目标不存在", Out.Head(ex.Message));
        }
    }

    private static void ParseErrors()
    {
        Out.Step("3. FormatParseException:内容格式错误,消息含行号/坏内容");
        var badTxt = SampleOutput.File("07_err", "bad.txt");
        File.WriteAllText(badTxt, "J1 1 500100 3798000\n乱码行 not-a-coordinate\n");
        try
        {
            GtTxtUtil.LoadTxt(badTxt);
        }
        catch (FormatParseException ex)
        {
            Out.Result("坏 TXT 行", Out.Head(ex.Message));
        }

        // 非法属性过滤器同属 FormatParseException:GPKG 驱动把 SQL 编译推迟到取要素时,库在 GetNextFeature 处统一包装
        var filterGpkg = SampleOutput.File("07_err", "filter.gpkg");
        OguLayerUtil.WriteLayer(DataFormatType.GEOPACKAGE, TestData.BuildParcels(2), filterGpkg, "demo_parcels");
        try
        {
            OguLayerUtil.ReadLayer(DataFormatType.GEOPACKAGE, filterGpkg, "demo_parcels",
                attributeFilter: "这不是合法的SQL");
        }
        catch (FormatParseException ex)
        {
            Out.Result("GPKG 非法属性过滤", Out.Head(ex.Message));
        }

        // 参数级错误走标准 ArgumentException,与业务异常分层,避免过度包装
        try
        {
            OguLayerUtil.ReadLayer(DataFormatType.SHP, "");
        }
        catch (ArgumentException ex)
        {
            Out.Result("空路径(ArgumentException)", Out.Head(ex.Message));
        }
    }

    private static void ValidationErrors()
    {
        Out.Step("4. LayerValidationException:写出前 Validate() 拦下结构性错误");
        var unnamed = new OguLayer();
        unnamed.AddField(new OguField { Name = "ID", DataType = FieldDataType.INTEGER });
        try
        {
            unnamed.Validate();
        }
        catch (LayerValidationException ex)
        {
            Out.Result("缺少图层名", Out.Head(ex.Message));
        }

        var badAttr = new OguLayer { Name = "demo" };
        badAttr.AddField(new OguField { Name = "ID", DataType = FieldDataType.INTEGER });
        var f = new OguFeature { Fid = 1, Wkt = "POINT (0 0)" };
        f.SetValue("TYPO_FIELD", 1); // 属性不在字段集中
        badAttr.AddFeature(f);
        try
        {
            badAttr.Validate();
        }
        catch (LayerValidationException ex)
        {
            Out.Result("属性拼写错误", Out.Head(ex.Message));
        }
        Out.Note("养成写盘前 Validate() 的习惯,能在 IO 之前暴露脏结构");
    }

    private static void EngineErrors()
    {
        Out.Step("5. EngineNotSupportedException:引擎/格式路由失败");
        try
        {
            GisEngineFactory.GetEngine((GisEngineType)int.MaxValue);
        }
        catch (EngineNotSupportedException ex)
        {
            Out.Result("未知引擎", Out.Head(ex.Message));
        }
        var ok = GisEngineFactory.TryGetEngine(DataFormatType.GEOPACKAGE, out var engine);
        Out.Result("TryGetEngine(GEOPACKAGE)", $"{ok} → {engine?.GetType().Name}");
        Out.Note("优先用 TryGetEngine 探测,再决定降级策略,而不是用异常控制流程");

        Out.Step("6. 统一兜底:catch (OguException) 即可拦截全部业务异常并记录");
        try
        {
            OguLayerUtil.ReadLayer(DataFormatType.SHP, SampleOutput.File("07_err", "again_missing.shp"));
        }
        catch (OguException ex)
        {
            Out.Result("按基类捕获", $"{ex.GetType().Name}: {Out.Head(ex.Message)}");
            Out.Result("仍可下钻", ex is DataSourceException);
        }
    }
}
