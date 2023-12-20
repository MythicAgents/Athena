using Agent.Models;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Agent.Interfaces
{
    public interface ITokenManager
    {
        public TokenResponseResult AddToken(SafeAccessTokenHandle hToken, CreateToken tokenOptions, string task_id);
        //public Task<string> Make(ServerJob job);
        //public Task<string> Steal(ServerJob job);
        public bool Impersonate(int i);
        public string List(ServerJob job);
        public bool Revert();
        public int getIntegrity();
    }
}
