using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestClasses
{
    internal class TestTokenManager : ITokenManager
    {
        public TokenTaskResponse AddToken(SafeAccessTokenHandle hToken, CreateToken tokenOptions, string task_id)
        {
            throw new NotImplementedException();
        }

        public SafeAccessTokenHandle GetImpersonationContext(int id)
        {
            throw new NotImplementedException();
        }

        public int getIntegrity()
        {
            return 1;
        }

        public void HandleFilePluginImpersonated(IFilePlugin plug, ServerJob job, ServerTaskingResponse response)
        {
            throw new NotImplementedException();
        }

        public void HandleInteractivePluginImpersonated(IInteractivePlugin plug, ServerJob job, InteractMessage message)
        {
            throw new NotImplementedException();
        }

        public bool Impersonate(int i)
        {
            throw new NotImplementedException();
        }

        public string List(ServerJob job)
        {
            throw new NotImplementedException();
        }

        public bool Revert()
        {
            throw new NotImplementedException();
        }

        public void RunTaskImpersonated(IPlugin plug, ServerJob job)
        {
            throw new NotImplementedException();
        }
    }
}
