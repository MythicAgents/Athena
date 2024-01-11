using Agent.Interfaces;
using Agent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class ExecArgs
    {
        public int parent { get; set; }
        public string commandline { get; set; }
        public string spoofedcommandline { get; set; }
        public bool output { get; set; }
        public bool suspended { get; set; } = false;

        public SpawnOptions getSpawnOptions(string task_id)
        {
            return new SpawnOptions()
            {
                parent = this.parent,
                commandline = this.commandline,
                output = this.output,
                task_id = task_id,
                spoofedcommandline = this.spoofedcommandline,
                suspended = this.suspended,
            };
        }
    }
}
