using System.Collections.Generic;

namespace Athena.Models.Mythic.Response
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
