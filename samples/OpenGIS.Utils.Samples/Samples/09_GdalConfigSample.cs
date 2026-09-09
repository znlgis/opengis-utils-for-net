using Microsoft.Extensions.Logging;
using OpenGIS.Utils.Configuration;
using OpenGIS.Utils.DataSource;
using OpenGIS.Utils.Engine.Enums;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     09. 运行环境与 GDAL 配置:初始化与驱动能力自检、FileGDB 演示、
///     以及把库内部日志接入标准 Microsoft.Extensions.Logging 管道的钩子 OguLogging。
/// </summary>
public sealed class GdalConfigSample : ISample
{
    public string Title => "09-GDAL初始化/驱动自检/日志钩子";

    public void Run()
    {
        Out.Step("1. GdalConfiguration:版本与驱动能力自检(部署到新机器先跑这个)");
        GdalConfiguration.ConfigureGdal(); // 幂等;各工具类首次使用时也会惰性调用
        Out.Result("GDAL 版本", GdalConfiguration.GetGdalVersion());
        var drivers = GdalConfiguration.GetSupportedDrivers();
        Out.Result("注册驱动总数", drivers.Count);

        string[] keyDrivers = { "ESRI Shapefile", "GeoJSON", "GPKG", "KML", "DXF", "FileGDB", "OpenFileGDB", "PostgreSQL" };
        foreach (var d in keyDrivers)
            Out.Result($"IsDriverAvailable({d})", GdalConfiguration.IsDriverAvailable(d) ? "可用" : "不可用");
        Out.Note("驱动可用性决定可写格式:FileGDB 不可用时可用 OpenFileGDB/GPKG 载体替代(见 03/04 示例)");

        EsriFileGdbDemo();
        LoggingHookDemo();
    }

    private static void EsriFileGdbDemo()
    {
        Out.Step("2. Esri FileGDB 载体(gdb),仅当驱动可用");
        if (!GdalConfiguration.IsDriverAvailable("FileGDB"))
        {
            Out.Note("当前运行环境无 FileGDB 驱动,跳过(功能矩阵其余格式见 03 示例)");
            return;
        }

        var gdbPath = SampleOutput.Dir("09_gdal", "demo.gdb");
        OguLayerUtil.WriteLayer(DataFormatType.FILEGDB, TestData.BuildParcels(), gdbPath, "demo_parcels");
        Out.Result("gdb 内图层", string.Join(", ", OguLayerUtil.GetLayerNames(DataFormatType.FILEGDB, gdbPath)));
        var back = OguLayerUtil.ReadLayer(DataFormatType.FILEGDB, gdbPath, layerName: "demo_parcels");
        Out.Result("读回要素数", back.GetFeatureCount());
        Out.Note("FileGDB 目录容器同受 WriteLayer 先删后建语义约束(见 03 §4):连续两次 WriteLayer 会让后者清掉前者;往已有 gdb 追加图层请用 Append(见 04)");
    }

    private static void LoggingHookDemo()
    {
        Out.Step("3. OguLogging:库内部日志接入宿主日志框架(示例工程挂接 Console)");
        // 只有一行胶水:把 Microsoft.Extensions.Logging 工厂交给库,
        // 换 Serilog/NLog/应用框架时同样接法,库自身不绑定任何具体日志实现。
        OguLogging.LoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = OguLogging.CreateLogger("OpenGIS.Utils.Samples");
        logger.LogInformation("演示日志:库内部组件经 OguLogging.CreateLogger 取到宿主 Logger 输出");
        OguLogging.LoggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance; // 复位,后续示例不再输出
        Out.Note("未挂接时库日志静默(NullLoggerFactory),不影响功能");
    }
}
