using Agent.Models;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Agent.Interfaces
{
    public interface ITokenManager
    {
        public TokenTaskResponse AddToken(SafeAccessTokenHandle hToken, CreateToken tokenOptions, string task_id);
        public bool Impersonate(int i);
        public string List(ServerJob job);
        public bool Revert();
        public int getIntegrity();
        public SafeAccessTokenHandle GetImpersonationContext(int id);
        public void RunTaskImpersonated(IPlugin plug, ServerJob job);
        public void HandleFilePluginImpersonated(IFilePlugin plug, ServerJob job, ServerTaskingResponse response);
        public void HandleInteractivePluginImpersonated(IInteractivePlugin plug, ServerJob job, InteractMessage message);
    }
}
