using SysException = System.Exception;

namespace OpenGIS.Utils.Samples;

/// <summary>
///     所有示例实现此接口:由 <see cref="Program" /> 的运行器统一调度,
///     单个示例抛异常不影响其余示例继续执行。
/// </summary>
public interface ISample
{
    /// <summary>
    ///     标题,格式为 "序号-名称"。运行器按序号前缀或名称子串过滤,
    ///     例如 <c>dotnet run -- 03</c> 或 <c>dotnet run -- fileio</c>。
    /// </summary>
    string Title { get; }

    /// <summary>
    ///     执行示例。控制台输出即教学内容;生成的文件一律写入 <see cref="SampleOutput" />。
    /// </summary>
    void Run();
}

/// <summary>
///     示例主动抛出此异常表示"当前环境不满足运行条件"(如未配置 PostGIS 连接串),
///     运行器会标记为 SKIP 而不是 FAIL。
/// </summary>
public sealed class SampleSkipException : SysException
{
    public SampleSkipException(string reason) : base(reason)
    {
    }
}

/// <summary>
///     示例输出目录管理:固定为项目下 <c>samples-output\</c>(已加入 .gitignore),
///     程序启动时清空重建,学习者可以放心反复运行。
/// </summary>
public static class SampleOutput
{
    public static string Root { get; private set; } = string.Empty;

    /// <summary>
    ///     清空并重建输出目录,由 Program 在启动时调用一次。
    /// </summary>
    public static void Init()
    {
        var projectDir = FindProjectDir();
        Root = Path.Combine(projectDir, "samples-output");
        if (Directory.Exists(Root))
            Directory.Delete(Root, true);
        Directory.CreateDirectory(Root);
    }

    /// <summary>
    ///     返回输出目录下的文件路径,并自动创建其父目录。
    /// </summary>
    public static string File(params string[] parts)
    {
        var fullPath = Path.Combine(new[] { Root }.Concat(parts).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return fullPath;
    }

    /// <summary>
    ///     返回输出目录下的子目录路径(不存在则创建)。
    /// </summary>
    public static string Dir(params string[] parts)
    {
        var fullPath = Path.Combine(new[] { Root }.Concat(parts).ToArray());
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    private static string FindProjectDir()
    {
        // 从 bin/<配置>/<框架> 向上找到工程目录,保证 dotnet run 与直接运行 dll 时输出位置一致
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && dir.Name != "OpenGIS.Utils.Samples")
            dir = dir.Parent;
        return dir?.FullName ?? Environment.CurrentDirectory;
    }
}

/// <summary>
///     控制台输出小助手:统一示例内的打印格式,保持输出可读。
/// </summary>
public static class Out
{
    public static void Step(string text)
    {
        Console.WriteLine($"  [>] {text}");
    }

    public static void Result(string label, object? value)
    {
        Console.WriteLine($"      {label}: {value}");
    }

    public static void Note(string text)
    {
        Console.WriteLine($"      · {text}");
    }

    /// <summary>
    ///     把多行文本(典型为异常消息)压成单行并截断,便于控制台展示。
    /// </summary>
    public static string Head(string text, int max = 110)
    {
        text = text.ReplaceLineEndings(" ").Trim();
        return text.Length <= max ? text : text[..max] + " …";
    }
}
