using System.Collections.Generic;

namespace Athena.Mythic.Model.Response
{
    public class ResponseResult
    {
        public string task_id;
        public string user_output;
        public bool completed;
        public string status;
    }

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
