using Athena.Models.Commands;
using Athena.Models.Mythic.Checkin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Models.Config
{
    public interface IConfig
    {
        public static string uuid { get; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public IProfile profile { get; set; }
    }
    //public interface IConfig2
    //{
    //    public static string uuid { get; }
    //    public int sleep { get; set; }
    //    public int jitter { get; set; }
    //    public IProfile2 profile { get; set; }
    //    public abstract Task StartBeacon();
    //    public abstract Task<bool> StopBeacon();
    //    public abstract Task<CheckinResponse> Checkin(Checkin checkin);
    //    public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
    //}
}
