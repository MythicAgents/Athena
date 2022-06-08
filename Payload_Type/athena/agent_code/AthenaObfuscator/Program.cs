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
                //{ "Pinvoke", RandomString(30) },
                { "GetTaskingResponse", RandomString(30) },
				{ "GetTasking", RandomString(30) },
				{ "CheckinResponse", RandomString(30) },
				//{ "Checkin", RandomString(30) }, //Breaks
				{ "SocksMessage", RandomString(30) },
				{ "PSKCrypto", RandomString(30) },
				{ "Misc", RandomString(30) },
				{ "ExecuteAssemblyContext", RandomString(30) },
				{ "LoadAssembly", RandomString(30) },
				{ "ExecuteCommand", RandomString(30) },
				{ "LoadCommand", RandomString(30) },
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
				{ "RequestExit", RandomString(30) },
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
				{ "CheckIn", RandomString(30) },
				{ "GetArch", RandomString(30) },
				{ "downloadJob", RandomString(30) },
                { "uploadJob", RandomString(30) },
                { "dstportBytes", RandomString(30) },
                { "dstBytes", RandomString(30) },
                { "addressType", RandomString(30) },
                { "packetBytes", RandomString(30) },
                { "GetDestinationBytes", RandomString(30) },
                { "GetDestination", RandomString(30) },
                { "GetPort", RandomString(30) },
                //{ "server_id", RandomString(30) }, //Breaks Socks
                { "isRunning", RandomString(30) },
                { "process", RandomString(30) },
                { "sb", RandomString(30) },
                { "uploadParams", RandomString(30) },
                { "loadedAssembly", RandomString(30) },
                { "methodInfo", RandomString(30) },
                { "ShellJob", RandomString(30) },
                { "job", RandomString(30) },
                { "executeAssemblyContext", RandomString(30) },
                //{ "assembly", RandomString(30) }, //Breaks execute-assembly command
				{ "origStdOut", RandomString(30) },
                { "result", RandomString(30) },
                { "activeJobs", RandomString(30) },
                { "StartDownloadJob", RandomString(30) },
                { "StartInternalForwarder", RandomString(30) },
                { "LoadCommandAsync", RandomString(30) },
                { "LoadAssemblyAsync", RandomString(30) },
                { "ShellExec", RandomString(30) },
                { "UpdateSleepAndJitter", RandomString(30) },
                { "StartSocksProxy", RandomString(30) },
                { "StopSocksProxy", RandomString(30) },
                { "StopInternalForwarder", RandomString(30) },
                //{ "responses", RandomString(30) }, //Break GetTaskingResponse
                { "response", RandomString(30) },
                { "HasUploadJob", RandomString(30) },
                { "HasDownloadJob", RandomString(30) },
                { "CompleteUploadJob", RandomString(30) },
                { "CompleteDownloadJob", RandomString(30) },
                { "SetSleepAndJitterHandler", RandomString(30) },
                { "SetSleepAndJitter", RandomString(30) },
                { "StartForwarderHandler", RandomString(30) },
                { "StartForwarder", RandomString(30) },
                { "StopForwarderHandler", RandomString(30) },
                { "StopForwarder", RandomString(30) },
                { "StartSocksHandler", RandomString(30) },
                { "StartSocks", RandomString(30) },
                { "StopSocksHandler", RandomString(30) },
                { "StopSocks", RandomString(30) },
                { "ExitRequestedHandler", RandomString(30) },
                { "ExitRequested", RandomString(30) },
                { "TaskEventArgs", RandomString(30) },
                { "plaintext", RandomString(30) },
                { "encryptMemStream", RandomString(30) },
                { "encryptCryptoStream", RandomString(30) },
                { "encryptStreamWriter", RandomString(30) },
                { "scAes", RandomString(30) },
                { "hmac", RandomString(30) },
                //{ "final", RandomString(30) }, //Breaks Delegate Message
                { "input", RandomString(30) },
                { "uuidLength", RandomString(30) },
                { "uuidInput", RandomString(30) },
                //{ "IV", RandomString(30) }, //Breaks PSKCrypto
                { "ciphertext", RandomString(30) },
                { "sha256", RandomString(30) },
                { "decryptor", RandomString(30) },
                { "decryptMemStream", RandomString(30) },
                { "decryptCryptoStream", RandomString(30) },
                { "decryptStreamReader", RandomString(30) },
                { "decrypted", RandomString(30) },
                { "PSK", RandomString(30) },
                { "maxMissedCheckins", RandomString(30) },
                { "missedCheckins", RandomString(30) },
                { "mc", RandomString(30) },
                { "delegateTask", RandomString(30) },
                { "socksTask", RandomString(30) },
                { "responsesTask", RandomString(30) },
                //{ "tasks", RandomString(30) }, //Break GetTaskingResponse
                { "insideDoubleQuote", RandomString(30) },
                { "insideSingleQuote", RandomString(30) },
                { "nextPiece", RandomString(30) },
                { "controller", RandomString(30) },
                { "str", RandomString(30) },
                { "plainText", RandomString(30) },
                { "plainTextBytes", RandomString(30) },
                { "bytes", RandomString(30) },
                { "base64EncodedData", RandomString(30) },
                //{ "path", RandomString(30) },
                //{ "message", RandomString(30) },
                { "addrtype", RandomString(30) },
                { "bndaddr", RandomString(30) },
                { "bndport", RandomString(30) },
                { "ToByte", RandomString(30) },
                { "rsv", RandomString(30) },
                { "ParsePacket", RandomString(30) },
                { "port", RandomString(30) },
                { "HandleSocksEvent", RandomString(30) },
                { "exited", RandomString(30) },
                { "smOut", RandomString(30) },
                //{ "assemblyBytes", RandomString(30) }, //Breaks assembly loading
                { "target", RandomString(30) },
                { "clientPipe", RandomString(30) },
                { "partialMessages", RandomString(30) },
                { "_lock", RandomString(30) },
                { "ForwardDelegateMessage", RandomString(30) },
                { "AddMessageToQueue", RandomString(30) },
            };


            stringsToReplace.Add("using Athena.", "using " + stringsToReplace["namespace Athena"].Replace("namespace ","") + ".");


            Parallel.ForEach(Directory.GetFiles(args[0], "*.cs", SearchOption.AllDirectories), async file =>
             {
                 await RegexReplace(file, stringsToReplace);
                 //await RegularReplace(file, stringsToDestroy);
             });

        }

        static async Task RegexReplace(string file, Dictionary<string,string> stringsToReplace)
        {
			if (file.Contains("AssemblyInfo") || file.Contains("NETCoreApp"))
            {
				return;
            }

			string fileContent = File.ReadAllText(file);

            foreach(var s in stringsToReplace)
            {
				string pattern = @"\b" + s.Key + @"\b";
				fileContent = Regex.Replace(fileContent, pattern, s.Value);
                //fileContent = fileContent.Replace(s.Key, s.Value);
            }
            File.WriteAllText(file, fileContent);
        }

        static async Task RegularReplace(string file, Dictionary<string, string> stringsToReplace)
        {
            if (file.Contains("AssemblyInfo") || file.Contains("NETCoreApp"))
            {
                return;
            }

            string fileContent = File.ReadAllText(file);

            foreach (var s in stringsToReplace)
            {
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




