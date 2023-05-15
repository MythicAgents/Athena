using Athena.Models.Mythic.Response;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Athena.Commands
{
    public class SocksResponseHandler
    {
        private static ConcurrentDictionary<int, SocksMessage> messagesOut = new ConcurrentDictionary<int, SocksMessage>();

        public static async Task AddSocksMessageAsync(SocksMessage sm)
        {
            messagesOut.AddOrUpdate(sm.server_id, sm, (k, oldValue) => {
                SocksMessage msg = oldValue;
                
                if (sm.exit) //If server indicates it's time to exit, then we should exit
                {
                    msg.exit = true;
                }

                msg.bdata = oldValue.bdata.Concat(sm.bdata).ToArray(); //Concat byte array together
                return msg;
            });

        }
        public static async Task<List<SocksMessage>> GetSocksMessagesAsync()
        {
            if(messagesOut.IsEmpty)
            {
                return new List<SocksMessage>();
            }

            List<SocksMessage> messages = new List<SocksMessage>();
            foreach (var key in messagesOut.Keys)
            {
                SocksMessage sm;
                if (messagesOut.TryRemove(key, out sm))
                {
                    sm.PrepareMessage();
                    messages.Add(sm);
                }
            }

            Debug.WriteLine($"[{DateTime.Now}] Returning: {messages.Count} messages");
            return messages;
        }
    }
}
