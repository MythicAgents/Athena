using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "cursed";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            //Parse for chosen browser

            //Kill all 
            //Spawn Chrome/Edge/Brave process with debug port
            //Support PPID Soofing, cmdline Spoofing

        }
        //https://github.com/BishopFox/sliver/blob/master/client/overlord/overlord.go#L243C6-L243C32
        private void FindExtensionWithPermissions(Uri debugUrl)
        {
            var extensions = GetChromeExtensions(debugUrl);

            foreach(var extension in extensions)
            {
                var websocketUrl = GetChromeWebsocketUrl(debugUrl);
                //Perform websocket connection to extension
                //Send `{"id":1,"method":"Runtime.evaluate","params":{"expression":"chrome.runtime.getManifest()"}}`
                //Parse response for `permissions` key
                //cursedChromePermissions    = []string{overlord.AllURLs, overlord.WebRequest, overlord.WebRequestBlocking}
                //cursedChromePermissionsAlt = []string{ overlord.AllHTTP, overlord.AllHTTPS, overlord.WebRequest, overlord.WebRequestBlocking}
            }
        }

        //https://github.com/BishopFox/sliver/blob/master/client/overlord/overlord.go#L243C6-L243C32
        private List<object> GetChromeExtensions(Uri debugUrl)
        {
            //Perform GET to debug url

            //Parse HTML response json

            //Go through json looking for `Scheme` value of `chrome-extension`

            //Return an array of chrome-extension names and URLs

            return new List<object>();
        }

        private Uri GetChromeWebsocketUrl(Uri debugUrl)
        {
            return new Uri("");
        }
    }
}
