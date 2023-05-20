using Athena.Models.Mythic.Response;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Athena.Commands
{
    public class SocksResponseHandler
    {
        //private static ConcurrentDictionary<int, SocksMessage> messagesOut = new ConcurrentDictionary<int, SocksMessage>();
        private static ConcurrentBag<SocksMessage> messagesOut = new ConcurrentBag<SocksMessage>();
        public static async Task AddSocksMessageAsync(SocksMessage sm)
        {
            messagesOut.Add(sm);
            //messagesOut.AddOrUpdate(sm.server_id, sm, (k, oldValue) => {
            //    SocksMessage msg = oldValue;
            //    if (sm.exit) //If server indicates it's time to exit, then we should exit
            //    {
            //        msg.exit = true;
            //    }

            //    msg.bdata = sm.bdata.Concat(oldValue.bdata).ToArray(); //Concat byte array together
            //    return msg;
            //});

        }
        public static async Task<List<SocksMessage>> GetSocksMessagesAsync()
        {
            if(messagesOut.IsEmpty)
            {
                return new List<SocksMessage>();
            }

            List<SocksMessage> messages = new List<SocksMessage>(messagesOut);
            messagesOut.Clear();
            foreach (var message in messages)
            {
                message.PrepareMessage();
            }
            return messages;
        }
    }
}
