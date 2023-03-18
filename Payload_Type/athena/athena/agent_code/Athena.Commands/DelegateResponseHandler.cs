using Athena.Models.Mythic.Response;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Commands
{
    public class DelegateResponseHandler
    {
        private static ConcurrentBag<DelegateMessage> messageOut = new ConcurrentBag<DelegateMessage>();

        public static async Task AddDelegateMessageAsync(DelegateMessage dm)
        {
            messageOut.Add(dm);
        }
        public static async Task<List<DelegateMessage>> GetDelegateMessagesAsync()
        {
            List<DelegateMessage> messagesOut = new List<DelegateMessage>(messageOut);
            messageOut.Clear();
            return messagesOut;
        }
    }
}
