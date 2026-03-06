using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Principal;
using Workflow.Models;
using Workflow.Providers;

using Workflow.Contracts;

namespace Workflow.Providers
{
    public class CredentialProvider : ICredentialProvider
    {
        private ILogger logger { get; set; }
        public CredentialProvider(ILogger logger) {
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
            int integrity = Native.geteuid() == 0 ? 3 : 2;
            DebugLog.Log($"getIntegrity result={integrity}");
            return integrity;
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

        public void RunTaskImpersonated(IModule plug, ServerJob job)
        {
            plug.Execute(job);
        }

        public void HandleFilePluginImpersonated(IFileModule plug, ServerJob job, ServerTaskingResponse response)
        {
            plug.HandleNextMessage(response);
        }

        public void HandleInteractivePluginImpersonated(IInteractiveModule plug, ServerJob job, InteractMessage message)
        {
            plug.Interact(message);
        }
    }
}
