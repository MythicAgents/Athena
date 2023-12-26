using Agent.Models;
using Agent.Utilities;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ssh
{
    public class SshHelper : SshClient
    {
        string task_id { get; set; }
        public SshHelper(ConnectionInfo ci, string task_id) : base(ci)
        {
            this.task_id = task_id;
        }
    }
}
