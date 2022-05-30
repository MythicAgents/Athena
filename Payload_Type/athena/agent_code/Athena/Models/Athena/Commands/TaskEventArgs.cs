using Athena.Models.Mythic.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
