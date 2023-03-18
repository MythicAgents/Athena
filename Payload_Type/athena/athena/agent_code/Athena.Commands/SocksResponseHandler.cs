using Athena.Models.Mythic.Response;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            List<SocksMessage> msgOut;
            msgOut = new List<SocksMessage>(messagesOut);
            messagesOut.Clear();
            //msgOut.Reverse();
            Debug.WriteLine($"[{DateTime.Now}] Returning: {msgOut.Count} messages");
            return msgOut;
        }
    }
}
