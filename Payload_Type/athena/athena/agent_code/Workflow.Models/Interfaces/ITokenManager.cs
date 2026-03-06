using Workflow.Models;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Workflow.Contracts
{
    public interface ICredentialProvider
    {
        public TokenTaskResponse AddToken(SafeAccessTokenHandle hToken, CreateToken tokenOptions, string task_id);
        public bool Impersonate(int i);
        public string List(ServerJob job);
        public bool Revert();
        public int getIntegrity();
        public SafeAccessTokenHandle GetImpersonationContext(int id);
        public void RunTaskImpersonated(IModule plug, ServerJob job);
        public void HandleFilePluginImpersonated(IFileModule plug, ServerJob job, ServerTaskingResponse response);
        public void HandleInteractivePluginImpersonated(IInteractiveModule plug, ServerJob job, InteractMessage message);
    }
}
