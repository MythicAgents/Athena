namespace Nager.TcpClient
{
    /// <summary>
    /// TcpClient Config
    /// </summary>
    public class TcpClientConfig
    {
        /// <summary>
        /// Receive Buffer Size
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192;

        /// <summary>
        /// Receive Timeout
        /// </summary>
        public int ReceiveTimeout { get; set; } = 0;

        /// <summary>
        /// Send Buffer Size
        /// </summary>
        public int SendBufferSize { get; set; } = 8192;

        /// <summary>
        /// Send Timeout 
        /// </summary>
        public int SendTimeout { get; set; } = 0;

        /// <summary>
        /// No Delay 
        /// </summary>
        public bool NoDelay = false;
    }
}