namespace OpenGIS.Utils.Exception;

/// <summary>
///     图层验证异常
/// </summary>
public class LayerValidationException : OguException
{
    public LayerValidationException(string message) : base(message)
    {
    }

    public LayerValidationException(string message, System.Exception innerException) : base(message, innerException)
    {
    }

    public LayerValidationException(string message, int errorCode) : base(message, errorCode)
    {
    }
}
