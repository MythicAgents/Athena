using Athena.Models.Mythic.Tasks;
using System;

namespace Athena.Models.Athena.Commands
{
    public class TaskEventArgs : EventArgs
    {
        public MythicJob job { get; set; }

        public TaskEventArgs (MythicJob job)
        {
            this.job = job;
        }
    }
}
