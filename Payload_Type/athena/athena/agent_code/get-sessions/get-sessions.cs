using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Agent.Interfaces;
using Agent.Utilities;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "get-sessions";

        enum NERR
        {
            /// <summary>
            /// Operation was a success.
            /// </summary>
            NERR_Success = 0,
            /// <summary>
            /// More data available to read. dderror getting all data.
            /// </summary>
            ERROR_MORE_DATA = 234,
            /// <summary>
            /// Network browsers not available.
            /// </summary>
            ERROR_NO_BROWSER_SERVERS_FOUND = 6118,
            /// <summary>
            /// LEVEL specified is not valid for this call.
            /// </summary>
            ERROR_INVALID_LEVEL = 124,
            /// <summary>
            /// Security context does not have permission to make this call.
            /// </summary>
            ERROR_ACCESS_DENIED = 5,
            /// <summary>
            /// Parameter was incorrect.
            /// </summary>
            ERROR_INVALID_PARAMETER = 87,
            /// <summary>
            /// Out of memory.
            /// </summary>
            ERROR_NOT_ENOUGH_MEMORY = 8,
            /// <summary>
            /// Unable to contact resource. Connection timed out.
            /// </summary>
            ERROR_NETWORK_BUSY = 54,
            /// <summary>
            /// Network Path not found.
            /// </summary>
            ERROR_BAD_NETPATH = 53,
            /// <summary>
            /// No available network connection to make call.
            /// </summary>
            ERROR_NO_NETWORK = 1222,
            /// <summary>
            /// Pointer is not valid.
            /// </summary>
            ERROR_INVALID_HANDLE_STATE = 1609,
            /// <summary>
            /// Extended Error.
            /// </summary>
            ERROR_EXTENDED_ERROR = 1208,
            /// <summary>
            /// Base.
            /// </summary>
            NERR_BASE = 2100,
            /// <summary>
            /// Unknown Directory.
            /// </summary>
            NERR_UnknownDevDir = (NERR_BASE + 16),
            /// <summary>
            /// Duplicate Share already exists on server.
            /// </summary>
            NERR_DuplicateShare = (NERR_BASE + 18),
            /// <summary>
            /// Memory allocation was to small.
            /// </summary>
            NERR_BufTooSmall = (NERR_BASE + 23)
        }
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private readonly SessionEnumerator sessionEnumerator;

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
            sessionEnumerator = new SessionEnumerator(new NetSessionNative());
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                string[] targets;

                if (args.ContainsKey("targetlist"))
                {
                    if (args["targetlist"].ToString() != "")
                    {
                        targets = GetTargetsFromFile(Convert.FromBase64String(args["targetlist"].ToString())).ToArray<string>();
                    }
                    else
                    {
                        messageManager.Write("A file was provided but contained no data", job.task.id, true, "error");
                        return;
                    }
                }
                else
                {
                    targets = args["hosts"].ToString().Split(',');
                }

                if (targets.Count() < 1)
                {
                    messageManager.Write("No targets provided.", job.task.id, true, "error");
                    return;
                }

                foreach (var server in targets)
                {
                    StringBuilder sb = new StringBuilder();

                    try
                    {
                        IReadOnlyList<SessionRecord> results = sessionEnumerator.Enumerate(server);

                        int sess = 0;
                        sb.AppendLine("Sessions for: " + server);
                        foreach (var result in results)
                        {
                            sb.AppendLine($"SessionID: {sess}");
                            sb.AppendLine("---------------------------------------");
                            sb.AppendLine($"Username: {result.UserName}");
                            sb.AppendLine($"From: {result.ClientName}");
                            sb.AppendLine($"Time Active: {result.ActiveSeconds}");
                            sb.AppendLine($"Time Idle: {result.IdleSeconds}");
                            sb.AppendLine("---------------------------------------\r\n");
                            sb.AppendLine();
                            sess++;
                        }

                        //Add output as we update
                        messageManager.Write(sb.ToString(), job.task.id, false);
                    }
                    catch (Exception e)
                    {
                        messageManager.Write(e.ToString(), job.task.id, true, "error");
                    }
                    Thread.Sleep(10000);
                }

            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }

            messageManager.Write("Execution Finished.", job.task.id, true);
        }
        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = Misc.GetEncoding(b).GetString(b);

            return allData.Split(Environment.NewLine);
        }
    }
}
