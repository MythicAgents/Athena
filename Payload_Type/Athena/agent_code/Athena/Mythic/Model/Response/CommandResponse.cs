using System.Collections.Generic;

namespace Athena.Mythic.Model.Response
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
