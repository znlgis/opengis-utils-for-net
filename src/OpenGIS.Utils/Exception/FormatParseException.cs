namespace OpenGIS.Utils.Exception;

/// <summary>
///     格式解析异常
/// </summary>
public class FormatParseException : OguException
{
    public FormatParseException(string message) : base(message)
    {
    }

    public FormatParseException(string message, System.Exception innerException) : base(message, innerException)
    {
    }

    public FormatParseException(string message, int errorCode) : base(message, errorCode)
    {
    }
}
