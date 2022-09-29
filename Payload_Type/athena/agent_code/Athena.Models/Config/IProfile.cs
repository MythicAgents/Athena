using Athena.Models.Mythic.Response;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Models.Config
{
    public interface IProfile
    {
        public PSKCrypto crypt { get; set; }
        public bool encrypted { get; set; }
        public string psk { get; set; }
        public abstract Task<string> Send(object obj);
    }
}
