namespace OpenGIS.Utils.Exception
{
    /// <summary>
    /// 数据源异常
    /// </summary>
    public class DataSourceException : OguException
    {
        public DataSourceException(string message) : base(message)
        {
        }

        public DataSourceException(string message, System.Exception innerException) : base(message, innerException)
        {
        }

        public DataSourceException(string message, int errorCode) : base(message, errorCode)
        {
        }
    }
}
