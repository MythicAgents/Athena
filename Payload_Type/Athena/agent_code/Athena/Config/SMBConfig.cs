using System.IO.Pipes;
using System.Threading;

namespace Athena.Config
{
    public class SmbServer
    {
        public NamedPipeServerStream pipe { get; set; }
        public string namedpipe { get; set; }
        public CancellationTokenSource cancellationTokenSource { get; set; }

        public SmbServer()
        {

        }

        public void Start(string name)
        {
  
        }

        public void Stop()
        {
        }
    }
}
