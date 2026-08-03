using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenGIS.Utils.Configuration;

/// <summary>
///     库级日志配置。
/// </summary>
/// <remarks>
///     默认情况下不产生任何日志输出（使用 <see cref="NullLoggerFactory" />）。
///     调用方可在应用启动时设置 <see cref="LoggerFactory" />，以接收库内部的诊断日志
///     （例如读写要素、坐标解析失败等在过去被静默忽略的信息）。
/// </remarks>
/// <example>
///     <code>
/// // 在应用启动时配置日志（例如接入 Microsoft.Extensions.Logging）
/// OguLogging.LoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
/// </code>
/// </example>
public static class OguLogging
{
    private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    /// <summary>
    ///     用于创建库内部日志记录器的工厂。
    /// </summary>
    /// <remarks>设置为 <c>null</c> 时会回退到 <see cref="NullLoggerFactory" />（不输出日志）。</remarks>
    public static ILoggerFactory LoggerFactory
    {
        get => _loggerFactory;
        set => _loggerFactory = value ?? NullLoggerFactory.Instance;
    }

    /// <summary>
    ///     创建指定类别名称的日志记录器。
    /// </summary>
    /// <param name="categoryName">日志类别名称</param>
    /// <returns>日志记录器实例</returns>
    public static ILogger CreateLogger(string categoryName)
    {
        return _loggerFactory.CreateLogger(categoryName);
    }

    /// <summary>
    ///     创建以类型全名作为类别名称的日志记录器。
    /// </summary>
    /// <typeparam name="T">用于确定日志类别的类型</typeparam>
    /// <returns>日志记录器实例</returns>
    public static ILogger CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger(typeof(T).FullName ?? typeof(T).Name);
    }
}
