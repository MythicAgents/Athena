using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Athena
{

    class Program
    {
        static void Main(string[] args)
        {
            AsyncMain(args).GetAwaiter().GetResult();

        }

        static async Task AsyncMain(string[] args)
        {
            Dictionary<string,string> stringsToReplace = new Dictionary<string, string>()
			{
				{ "PInvoke", RandomString(30) },
				{ "GetTaskingResponse", RandomString(30) },
				{ "GetTasking", RandomString(30) },
				{ "CheckinResponse", RandomString(30) },
				{ "Checkin", RandomString(30) },
				{ "SocksMessage", RandomString(30) },
				{ "PSKCrypto", RandomString(30) },
				{ "Misc", RandomString(30) },
				{ "ExecuteAssemblyContext", RandomString(30) },
				{ "LoadAssembly", RandomString(30) },
				{ "ExecuteCommand", RandomString(30) },
				{ "LoadCommand", RandomString(30) },
				{ "ShellJob", RandomString(30) },
				{ "ConnectionOptions", RandomString(30) },
				{ "ConnectResponse", RandomString(30) },
				{ "DelegateMessage", RandomString(30) },
				{ "GetMessages", RandomString(30) },
				{ "ExecuteAssembly", RandomString(30) },
				{ "AssemblyHandler", RandomString(30) },
				{ "CommandHandler", RandomString(30) },
				{ "commandHandler", RandomString(30) },
				{ "DownloadHandler", RandomString(30) },
				{ "ShellHandler", RandomString(30) },
				{ "SocksHandler", RandomString(30) },
				{ "UploadHandler", RandomString(30) },
				{ "UploadJob", RandomString(30) },
				{ "DownloadJob", RandomString(30) },
				{ "GetUUIDString", RandomString(30) },
				{ "GetUUIDBytes", RandomString(30) },
				{ "UpdateUUID", RandomString(30) },
				{ "GetServerMessage", RandomString(30) },
				{ "AddByteArray", RandomString(30) },
				{ "StartUploadJob", RandomString(30) },
				{ "ContainsJob", RandomString(30) },
				{ "GetUploadJob", RandomString(30) },
				{ "UploadNextChunk", RandomString(30) },
				{ "DownloadNextChunk", RandomString(30) },
				{ "GetTotalChunks", RandomString(30) },
				{ "ReturnMessage", RandomString(30) },
				{ "HandleNewConnection", RandomString(30) },
				{ "HandleMessage", RandomString(30) },
				{ "RemoveConnection", RandomString(30) },
				{ "AddConnection", RandomString(30) },
				{ "GetOutput", RandomString(30) },
				{ "SetSleep", RandomString(30) },
				{ "HandleUploadPiece", RandomString(30) },
				{ "HandleDownloadPiece", RandomString(30) },
				{ "AddResponse", RandomString(30) },
				{ "GetJobs", RandomString(30) },
				{ "StartJob", RandomString(30) },
				{ "StopJob", RandomString(30) },
				{ "GetAssemblyOutput", RandomString(30) },
				{ "ClearAssemblyLoadContext", RandomString(30) },
				{ "RunLoadedCommand", RandomString(30) },
				{ "CommandIsLoaded", RandomString(30) },
				{ "callbackHost", RandomString(30) },
				{ "callbackInterval", RandomString(30) },
				{ "callbackJitter", RandomString(30) },
				{ "callbackPort", RandomString(30) },
				{ "encryptedExchangeCheck", RandomString(30) },
				{ "hostHeader", RandomString(30) },
				{ "queueIn", RandomString(30) },
				{ "messageOut", RandomString(30) },
				{ "messagesOut", RandomString(30) },
				{ "commandContext", RandomString(30) },
				{ "loadedCommands", RandomString(30) },
				{ "assemblyIsRunning", RandomString(30) },
				{ "assemblyTaskId", RandomString(30) },
				{ "assemblyHandler", RandomString(30) },
				{ "downloadHandler", RandomString(30) },
				{ "shellHandler", RandomString(30) },
				{ "uploadHandler", RandomString(30) },
				{ "responseResults", RandomString(30) },
				{ "GetResponses", RandomString(30) },
				{ "CheckAndRunPlugin", RandomString(30) },
				{ "athcmd", RandomString(30) },
				{ "downloadJobs", RandomString(30) },
				{ "HasRunningJobs", RandomString(30) },
				{ "connections", RandomString(30) },
				{ "uploadJobs", RandomString(30) },
				{ "endpoint", RandomString(30) },
				{ "psk", RandomString(30) },
				{ "userAgent", RandomString(30) },
				{ "encrypted", RandomString(30) },
				{ "arguments", RandomString(30) },
				{ "AthenaSocksConnection", RandomString(30) },
				{ "HasMessages", RandomString(30) },
				{ "HandleGetTaskingResponse", RandomString(30) },
				{ "HandleSocks", RandomString(30) },
				{ "HandleDelegates", RandomString(30) },
				{ "GetTasks", RandomString(30) },
				{ "MythicConfig", RandomString(30) },
				{ "MythicClient", RandomString(30) },
				{ "HandleMythicResponses", RandomString(30) },
				{ "currentConfig", RandomString(30) },
				{ "GetSleep", RandomString(30) },
				{ "delegateMessages", RandomString(30) },
				{ "socksMessage", RandomString(30) },
				{ "socksHandler", RandomString(30) },
				{ "Athena.Utilities", RandomString(30) },
				{ "namespace Athena", "namespace " + RandomString(30) },
				{ "Models.Athena.Commands", RandomString(30)},
				{ "Models.Mythic.Tasks" ,RandomString(30)},
				{ "Models.Mythic.Response", RandomString(30) },
				{ "Commands.Model", RandomString(30) },
				{ "Models.Athena.Assembly", RandomString(30) },
				{ "Models.Athena.Socks", RandomString(30) },
				{ "Config", RandomString(30) },
				{ "responseString", RandomString(30) },
				{ "SplitCommandLine", RandomString(30) },
				{ "SplitIt", RandomString(30) },
				{ "TrimMatchingQuotes", RandomString(30) },
				{ "Base64Encode", RandomString(30) },
				{ "Base64Decode", RandomString(30) },
				{ "Base64DecodeToByteArray", RandomString(30) },
				{ "AppendAllBytes", RandomString(30) },
				{ "WriteDebug", RandomString(30) },
				{ "WriteError", RandomString(30) },
				{ "getIntegrity", RandomString(30) },
				{ "Split2", RandomString(30) },
				{ "SplitByLength", RandomString(30) },
				{ "MythicTask", RandomString(30) },
				{ "MythicJob", RandomString(30) },
				{ "MythicResponseResult", RandomString(30) },
				{ "killDate", RandomString(30) },
				{ "forwarder", RandomString(30) },
				{ "Forwarder", RandomString(30) },
				{ "executeAssemblyWriter", RandomString(30) },
				{ "CheckIn()", RandomString(30) + "()" },
				{ "GetArch()", RandomString(30) + "()" },
				{ "downloadJob", RandomString(30) },
				{ "uploadJob", RandomString(30) },
				{ "dstportBytes", RandomString(30) },
				{ "dstBytes", RandomString(30) },
				{ "addressType", RandomString(30) },
				{ "packetBytes", RandomString(30) },
				{ "GetDestinationBytes", RandomString(30) },
				{ "GetDestination", RandomString(30) },
				{ "GetPort", RandomString(30) },
				{ "server_id", RandomString(30) },
				{ "isRunning", RandomString(30) },
				{ "process", RandomString(30) },
				{ "sb", RandomString(30) },
				{ "uploadParams", RandomString(30) },
			};

			stringsToReplace.Add("using Athena.", "using " + stringsToReplace["namespace Athena"].Replace("namespace ","") + ".");


            Parallel.ForEach(Directory.GetFiles(args[0], "*.cs", SearchOption.AllDirectories), async file =>
             {
                 await Replace(file, stringsToReplace);
             });

        }

        static async Task Replace(string file, Dictionary<string,string> stringsToReplace)
        {
			if (file.Contains("AssemblyInfo"))
            {
				return;
            }

			string fileContent = File.ReadAllText(file);

            foreach(var s in stringsToReplace)
            {
                string rand = RandomString(15);
                fileContent = fileContent.Replace(s.Key, s.Value);
            }
            File.WriteAllText(file, fileContent);
        }

        public static string RandomString(int length)
        {
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}




