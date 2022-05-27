using Athena.Models.Mythic.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Models.Athena.Commands
{
    public class ShellJob : MythicJob
    {
        public StringBuilder sb { get; set; }
        public bool isRunning { get; set; }
        public Process process { get; set; }


        public ShellJob(MythicJob job)
        {
            this.task = job.task;

        }
    }
}
