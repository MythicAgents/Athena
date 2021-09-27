using Athena.Models.Mythic.Response;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class SmbClient
    {
        public NamedPipeClientStream pipe { get; set; }
        public string namedpipe { get; set; }
        public bool connected { get; set; }
        public CancellationTokenSource cancellationTokenSource { get; set; }
        public List<DelegateMessage> messageIn { get; set; } // Mythic -> AthenaServer -> AthenaClient 
        public List<DelegateMessage> messageOut { get; set; } //AthenaClient -> AthenaServer -> Mythic

        public SmbClient()
        {
            this.namedpipe = "pipe_name";
            this.cancellationTokenSource = new CancellationTokenSource();
            this.messageIn = new List<DelegateMessage>();
            this.messageOut = new List<DelegateMessage>();
        }
        public List<DelegateMessage> GetMessages()
        {
            List<DelegateMessage> msgs = this.messageOut;
            this.messageOut.Clear();
            return msgs;
        }
        
        //Link to the Athena SMB Agent
        public bool Link(string host, string pipename)
        {
            if(this.connected)
            {
                return false;
            }

            try
            {
                this.pipe = new NamedPipeClientStream
                    (host, pipename, PipeDirection.InOut, PipeOptions.Asynchronous);

                //Should I add a timeout for this?
                this.pipe.Connect();
                Task.Run(() =>
                {
                    MessagesLoop();
                });

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
        
        //Main messages loop, to read messages, and add them to the out queue.
        private void MessagesLoop()
        {
            try
            {
                // Read user input and send that to the client process.
                using (BinaryWriter _bw = new BinaryWriter(this.pipe))
                using (BinaryReader _br = new BinaryReader(this.pipe))
                {
                    while (this.connected)
                    {
                        string rec = Encoding.ASCII.GetString(ReceiveFromNamedPipe());
                        messageOut.Add(JsonConvert.DeserializeObject<DelegateMessage>(rec));
                    }
                }
            }
            // Catch the IOException that is raised if the pipe is broken
            // or disconnected.
            catch (IOException e)
            {
                this.connected = false;
                //It may be worth adding some "link" functioanlity
            }
            catch
            {
                this.connected = false;
                //Generic catches
            }
        }
        
        //Read from pipe and return as byte array
        private byte[] ReceiveFromNamedPipe()
        {
            byte[] buffer = new byte[1024];
            using (var ms = new MemoryStream())
            {
                do
                {
                    var readBytes = this.pipe.Read(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, readBytes);
                }
                while (!this.pipe.IsMessageComplete);

                return ms.ToArray();
            }
        }
        
        //Since this is a Server -> SMB Athena message, we don't really need to receive a response from Athena. Message Reads will be handled by our main loop.
        public void Send(DelegateMessage msg)
        {

            if (!connected)
            {
                return;
            }

            try
            {
                // Read user input and send that to the client process.
                using (BinaryWriter _bw = new BinaryWriter(this.pipe))
                using (BinaryReader _br = new BinaryReader(this.pipe))
                {
                    var buf = Encoding.ASCII.GetBytes(msg);
                    _bw.Write((uint)buf.Length);
                    _bw.Write(buf);
                }
            }
            catch
            {
                this.connected = false;
                return;
            }
        }
        
        //Unlink from the named pipe
        public void Unlink()
        {
            this.pipe.Dispose();
            this.cancellationTokenSource.Cancel();
        }
    }
}
