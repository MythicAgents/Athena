using Athena.Models.Commands;
using Athena.Models.Mythic.Checkin;
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
        public string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public PSKCrypto crypt { get; set; }
        public bool encrypted { get; set; }
        public string psk { get; set; }
        public abstract Task StartBeacon();
        public abstract bool StopBeacon();
        public abstract Task<CheckinResponse> Checkin(Checkin checkin);
        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
    }
    public interface IProfile2
    {
        public PSKCrypto crypt { get; set; }
        public string uuid { get; set; }
        public bool encrypted { get; set; }
        public string psk { get; set; }
        public abstract Task<string> Beacon(string msg);
    }
}
