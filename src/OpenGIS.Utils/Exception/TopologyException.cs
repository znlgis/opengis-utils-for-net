namespace OpenGIS.Utils.Exception
{
    /// <summary>
    /// 拓扑异常
    /// </summary>
    public class TopologyException : OguException
    {
        public TopologyException(string message) : base(message)
        {
        }

        public TopologyException(string message, System.Exception innerException) : base(message, innerException)
        {
        }

        public TopologyException(string message, int errorCode) : base(message, errorCode)
        {
        }
    }
}
