using Microsoft.Win32.SafeHandles;
using Agent.Models;
using Agent.Interfaces;

namespace Agent.Managers
{
    public class TokenManager : ITokenManager
    {
        private ILogger logger { get; set; }
        public TokenManager(ILogger logger)
        {
            this.logger = logger;
        }
        public bool Impersonate(int i) => true;
        public string List(ServerJob job) => string.Empty;
        public bool Revert() => true;
        public int getIntegrity() => Native.geteuid() == 0 ? 3 : 2;

        public TokenTaskResponse AddToken(SafeAccessTokenHandle hToken, CreateToken tokenOptions, string task_id) =>
            new TokenTaskResponse()
            {
                user_output = "not supported in this configuration.",
                task_id = task_id,
                completed = true,
                status = "error",
            };

        public SafeAccessTokenHandle GetImpersonationContext(int id) =>
            new SafeAccessTokenHandle(IntPtr.Zero);

        public void RunTaskImpersonated(IPlugin plug, ServerJob job) =>
            plug.Execute(job);

        public Task HandleFilePluginImpersonated(IFilePlugin plug, ServerJob job, ServerTaskingResponse response) =>
            plug.HandleNextMessage(response);

        public void HandleInteractivePluginImpersonated(IInteractivePlugin plug, ServerJob job, InteractMessage message) =>
            plug.Interact(message);
    }
}
