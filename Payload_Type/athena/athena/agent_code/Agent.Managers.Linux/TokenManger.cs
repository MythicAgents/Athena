using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Principal;
using Agent.Models;
using Agent.Managers;

using Agent.Interfaces;

namespace Agent.Managers
{
    public class TokenManager : ITokenManager
    {
        private ILogger logger { get; set; }
        public TokenManager(ILogger logger) {
            this.logger = logger;
        }
        public bool Impersonate(int i)
        {
            return true;
        }
        public string List(ServerJob job)
        {
            return String.Empty;
        }
        public bool Revert()
        {
            return true;
        }
        public int getIntegrity()
        {
            return Native.geteuid() == 0 ? 3 : 2;
        }

        public TokenTaskResponse AddToken(SafeAccessTokenHandle hToken, CreateToken tokenOptions, string task_id)
        {
            return new TokenTaskResponse()
            {
                user_output = "not supported in this configuration.",
                task_id = task_id,
                completed = true,
                status = "error",
            };
        }

        public SafeAccessTokenHandle GetImpersonationContext(int id)
        {
            throw new NotImplementedException();
        }

        public void RunTaskImpersonated(IPlugin plug, ServerJob job)
        {
            plug.Execute(job);
        }

        public void HandleFilePluginImpersonated(IFilePlugin plug, ServerJob job, ServerTaskingResponse response)
        {
            plug.HandleNextMessage(response);
        }

        public void HandleInteractivePluginImpersonated(IInteractivePlugin plug, ServerJob job, InteractMessage message)
        {
            plug.Interact(message);
        }
    }
}
