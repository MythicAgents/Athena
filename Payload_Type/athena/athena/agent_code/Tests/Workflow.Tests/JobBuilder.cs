using Workflow.Models;
using System.Text.Json;

namespace Workflow.Tests
{
    public class JobBuilder
    {
        private string _command;
        private object _parameters;
        private string _taskId = "test-1";

        public JobBuilder(string command)
        {
            _command = command;
        }

        public JobBuilder WithParameters(object parameters)
        {
            _parameters = parameters;
            return this;
        }

        public JobBuilder WithTaskId(string taskId)
        {
            _taskId = taskId;
            return this;
        }

        public ServerJob Build()
        {
            return new ServerJob()
            {
                task = new ServerTask()
                {
                    id = _taskId,
                    parameters = _parameters != null
                        ? JsonSerializer.Serialize(_parameters)
                        : "{}",
                    command = _command
                }
            };
        }
    }
}
