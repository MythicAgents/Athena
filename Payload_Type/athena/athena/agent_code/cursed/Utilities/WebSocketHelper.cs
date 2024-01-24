using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    static class WebSocketHelper
    {
        public static async Task<bool> TrySendMessage(System.Net.WebSockets.ClientWebSocket webSocket, string message)
        {
            try
            {
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(message);
                await webSocket.SendAsync(new ArraySegment<byte>(buffer), System.Net.WebSockets.WebSocketMessageType.Text, true, System.Threading.CancellationToken.None);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<string> ReceiveMessage(System.Net.WebSockets.ClientWebSocket webSocket)
        {
            try
            {
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
            catch
            {
                return "";
            }
        }
    }
}
