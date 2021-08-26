using Athena.Mythic.Model;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.Loader;
using System.Reflection;
using System.Threading;
using System.IO;
using System.Security.Cryptography;

namespace Athena
{
    public class Globals
    {
        public static AssemblyLoadContext alc = new AssemblyLoadContext("Athena"); //Will need to randomize this name as it can be an IoC for Athena
        public static AssemblyLoadContext loadcontext = new AssemblyLoadContext("commands");
        public static bool encrypted = false;
        //public static CancellationTokenSource cancellationsource = new CancellationTokenSource();
        public static Dictionary<string, Assembly> loadedcommands = new Dictionary<string, Assembly>();
        public static Dictionary<string, MythicJob> jobs = new Dictionary<string, MythicJob>();
        public static Dictionary<string, MythicDownloadJob> downloadJobs = new Dictionary<string, MythicDownloadJob>();
        public static HttpClient client = new HttpClient();
        public static MythicClient mc;
        public static string executeAssemblyTask = "";
        public static Thread executeAseemblyThread;
        public static RSACryptoServiceProvider rsa;
        //public static ClientWebSocket ws;
    }
}
