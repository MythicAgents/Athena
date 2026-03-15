using Workflow.Contracts;
using Workflow.Models;

namespace Workflow
{
    public class InjectArgs
    {
        public int pid { get; set; } = 0;
        public string commandline { get; set; } = "";
        public string spoofedcommandline { get; set; } = "";
        public string asm { get; set; } = "";
        public bool output { get; set; } = false;

        public SpawnOptions GetSpawnOptions(string task_id)
        {
            return new SpawnOptions()
            {
                task_id = task_id,
                commandline = commandline,
                spoofedcommandline = spoofedcommandline,
                output = output,
                suspended = true
            };
        }

        public bool Validate(out string message)
        {
            if (pid <= 0 && string.IsNullOrEmpty(commandline))
            {
                message = "A pid or command line needs to be specified.";
                return false;
            }

            if (string.IsNullOrEmpty(asm))
            {
                message = "No buffer provided";
                return false;
            }
            message = string.Empty;
            return true;
        }
    }
}
