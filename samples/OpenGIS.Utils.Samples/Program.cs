using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenGIS.Utils.Samples;

// 示例数据大量使用 GBK(测绘行业最常见编码),需注册代码页编码提供程序
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
// WKT 规范要求小数点为 '.',字符串插值默认跟随系统区域;
// 固定不变文化,保证任何 locale 下拼出的 WKT/坐标文本都合法
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // 输出被重定向到文件时设置控制台编码会失败,不影响示例运行
}

SampleOutput.Init();

// 全部示例按编号顺序注册;新增示例只需在这里追加一行
ISample[] allSamples =
{
    new LayerModelSample(),
    new GeometrySample(),
    new FileIoSample(),
    new AppendAndFidSample(),
    new TxtSample(),
    new CrsSample(),
    new ExceptionsSample(),
    new UtilitySample(),
    new GdalConfigSample(),
    new PostgisSample()
};

// 过滤:dotnet run -- 03        按序号前缀
//       dotnet run -- fileio    按标题子串(不区分大小写)
ISample[] selected = args.Length == 0
    ? allSamples
    : allSamples
        .Where(s => args.Any(a => s.Title.Contains(a, StringComparison.OrdinalIgnoreCase)))
        .ToArray();

Console.WriteLine("OpenGIS.Utils 功能示例(全部数据为合成虚构数据,与任何真实调查成果无关)");
Console.WriteLine($"输出目录: {SampleOutput.Root}");
if (args.Length > 0)
    Console.WriteLine($"过滤条件: {string.Join(", ", args)} → 命中 {selected.Length}/{allSamples.Length} 个示例");

var pass = 0;
var skip = 0;
var failed = new List<string>();

foreach (var sample in selected)
{
    Console.WriteLine();
    Console.WriteLine($"========== {sample.Title} ==========");
    var sw = Stopwatch.StartNew();
    try
    {
        sample.Run();
        Console.WriteLine($"[PASS] {sample.Title}({sw.ElapsedMilliseconds} ms)");
        pass++;
    }
    catch (SampleSkipException ex)
    {
        Console.WriteLine($"[SKIP] {sample.Title}: {ex.Message}");
        skip++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] {sample.Title}: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine(ex);
        failed.Add(sample.Title);
    }
}

Console.WriteLine();
Console.WriteLine($"===== 汇总: PASS {pass} / SKIP {skip} / FAIL {failed.Count} =====");
if (failed.Count > 0)
    foreach (var t in failed)
        Console.WriteLine($"  失败: {t}");

return failed.Count == 0 ? 0 : 1;
