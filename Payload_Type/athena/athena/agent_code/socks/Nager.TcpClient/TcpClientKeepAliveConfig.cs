namespace Nager.TcpClient
{
    /// <summary>
    /// TcpClient KeepAlive Config
    /// </summary>
    public class TcpClientKeepAliveConfig
    {
        /// <summary>
        /// KeepAliveInterval
        /// </summary>
        public int KeepAliveInterval = 2;

        /// <summary>
        /// KeepAliveTime
        /// </summary>
        public int KeepAliveTime = 2;

        /// <summary>
        /// KeepAliveRetryCount
        /// </summary>
        public int KeepAliveRetryCount = 3;
    }
}