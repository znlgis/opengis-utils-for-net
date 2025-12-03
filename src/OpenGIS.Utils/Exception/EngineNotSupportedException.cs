namespace OpenGIS.Utils.Exception;

/// <summary>
///     引擎不支持异常
/// </summary>
public class EngineNotSupportedException : OguException
{
    public EngineNotSupportedException(string message) : base(message)
    {
    }

    public EngineNotSupportedException(string message, System.Exception innerException) : base(message, innerException)
    {
    }

    public EngineNotSupportedException(string message, int errorCode) : base(message, errorCode)
    {
    }
}
