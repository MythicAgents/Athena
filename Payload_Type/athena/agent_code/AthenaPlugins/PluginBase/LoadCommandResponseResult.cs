using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginBase
{
    public class LoadCommandResponseResult : ResponseResult
    {
        public List<CommandsResponse> commands;
    }
    public class CommandsResponse
    {
        public string action;
        public string cmd;
    }
}
