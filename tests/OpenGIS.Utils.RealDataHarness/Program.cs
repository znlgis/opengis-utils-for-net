using System.Text;
using OpenGIS.Utils.RealDataHarness;

namespace OpenGIS.Utils.RealDataHarness;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        // 数据目录只从参数或环境变量传入，harness 不内置任何数据路径
        var dataDir = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : Environment.GetEnvironmentVariable("OGU_REAL_DATA_DIR");
        var outputDir = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
            ? args[1]
            : Path.Combine(Path.GetTempPath(), "OguRealDataReport");

        if (string.IsNullOrWhiteSpace(dataDir))
        {
            Console.WriteLine("用法: OguRealDataHarness <数据目录> [报告输出目录]");
            Console.WriteLine("或设置环境变量 OGU_REAL_DATA_DIR 指向包含 Shapefile 的目录。");
            Console.WriteLine("将递归发现目录下所有成对的 .shp/.dbf 并执行六维度集成检查。");
            return 2;
        }

        var envError = RealDataChecks.ValidateEnvironment(dataDir);
        if (envError != null)
        {
            Console.WriteLine($"[环境校验失败] {envError}");
            return 2;
        }

        Console.WriteLine($"数据目录: {dataDir}");
        Console.WriteLine($"GDAL 版本: {OpenGIS.Utils.Configuration.GdalConfiguration.GetGdalVersion()}");
        Console.WriteLine("发现的图层:");
        foreach (var spec in RealDataChecks.DiscoverLayers(dataDir))
            Console.WriteLine($"  - {spec}");
        Console.WriteLine();

        var workDir = Path.Combine(Path.GetTempPath(), "OguRealData_" + Guid.NewGuid().ToString("N"));
        List<CheckResult> results;
        try
        {
            results = RealDataChecks.RunAll(dataDir, workDir);
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"[运行中断] {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 3;
        }

        foreach (var r in results)
            Console.WriteLine(r.ToString());

        Console.WriteLine();
        var pass = results.Count(r => r.Status == CheckStatus.Pass);
        var warn = results.Count(r => r.Status == CheckStatus.Warn);
        var fail = results.Count(r => r.Status == CheckStatus.Fail);
        var info = results.Count(r => r.Status == CheckStatus.Info);
        Console.WriteLine($"汇总: Pass {pass} | Warn {warn} | Fail {fail} | Info {info} | 共 {results.Count} 项");

        // 输出 md 与 json 报告
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "realdata-check-results.json"),
            System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }),
            new UTF8Encoding(false));

        var md = BuildMarkdown(results, dataDir, workDir, pass, warn, fail, info);
        File.WriteAllText(Path.Combine(outputDir, "realdata-check-report.md"), md, new UTF8Encoding(false));
        Console.WriteLine();
        Console.WriteLine($"报告已输出: {Path.Combine(outputDir, "realdata-check-report.md")}");

        try
        {
            Directory.Delete(workDir, true);
        }
        catch (System.Exception)
        {
            // 临时目录清理失败不影响结果
        }

        return fail == 0 ? 0 : 1;
    }

    private static string BuildMarkdown(List<CheckResult> results, string dataDir, string workDir,
        int pass, int warn, int fail, int info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# OpenGIS Utils for .NET 真实数据集成测试报告");
        sb.AppendLine();
        sb.AppendLine($"- 数据目录: `{dataDir}`");
        sb.AppendLine($"- GDAL 版本: {OpenGIS.Utils.Configuration.GdalConfiguration.GetGdalVersion()}");
        sb.AppendLine($"- 执行时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- 结果汇总: **Pass {pass} / Warn {warn} / Fail {fail} / Info {info}**（共 {results.Count} 项）");
        sb.AppendLine();
        sb.AppendLine("## 自动发现的数据图层");
        sb.AppendLine();
        sb.AppendLine("| 图层 | 源 shape 类型 | DBF 头记录数 |");
        sb.AppendLine("|---|---|---|");
        foreach (var spec in RealDataChecks.DiscoverLayers(dataDir))
            sb.AppendLine($"| {spec.LayerName} | {spec.SourceShapeType} | {spec.ExpectedCount} |");
        sb.AppendLine();
        sb.AppendLine("## 检查明细");
        sb.AppendLine();
        sb.AppendLine("| 维度 | 对象 | 检查项 | 结果 | 明细 |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var r in results)
        {
            var status = r.Status switch
            {
                CheckStatus.Pass => "✅ Pass",
                CheckStatus.Warn => "⚠️ Warn",
                CheckStatus.Fail => "❌ Fail",
                _ => "ℹ️ Info"
            };
            var details = r.Details.Replace("|", "\\|").ReplaceLineEndings(" ");
            sb.AppendLine($"| {r.Dimension} | {r.Target} | {r.Check} | {status} | {details} |");
        }

        return sb.ToString();
    }
}
