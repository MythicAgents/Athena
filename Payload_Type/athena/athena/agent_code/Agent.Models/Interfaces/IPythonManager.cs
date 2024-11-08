using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Models
{
    public interface IPythonManager
    {
        public abstract bool LoadPyLib(byte[] bytes);
        public abstract Task<string> ExecuteScriptAsync(string[] args, string script);
        public abstract string ExecuteScript(string script, string[] args);
        public abstract bool ClearPyLib();
    }
}
