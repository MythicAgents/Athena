using Athena.Models.Mythic.Tasks;
using System.Diagnostics;
using System.Text;

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
