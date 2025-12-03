using System.Collections.Generic;

namespace OpenGIS.Utils.Exception;

/// <summary>
///     OGU 基础异常类
/// </summary>
public class OguException : System.Exception
{
    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    public OguException(string message) : base(message)
    {
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public OguException(string message, System.Exception innerException) : base(message, innerException)
    {
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="errorCode">错误代码</param>
    public OguException(string message, int errorCode) : base(message)
    {
        ErrorCode = errorCode;
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    ///     错误代码
    /// </summary>
    public int ErrorCode { get; set; }

    /// <summary>
    ///     上下文信息
    /// </summary>
    public Dictionary<string, object> Context { get; set; }
}
