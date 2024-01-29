using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "arp";
        private IMessageManager messageManager { get; set; }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(int DestIP, int SrcIP, byte[] pMacAddr, ref uint PhyAddrLen);

        private static uint macAddrLen = (uint)new byte[6].Length;
        private const string separator = "|";

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                IPNetwork ipnetwork = IPNetwork.Parse(args["cidr"]);
                System.Net.IPAddressCollection iac = ipnetwork.ListIPAddress();
                int timeout = int.Parse(args["timeout"]);

                CheckStatus(iac, timeout * 1000, job.task.id);
                messageManager.Write("Finished Executing", job.task.id, true);
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private string MacAddresstoString(byte[] macAdrr)
        {
            string macString = BitConverter.ToString(macAdrr);
            return macString.ToUpper();
        }

        private string ThreadedARPRequest(string ipString)
        {
            IPAddress ipAddress;
            byte[] macAddr = new byte[6];

            try
            {
                ipAddress = IPAddress.Parse(ipString);
                SendARP((int)BitConverter.ToInt32(ipAddress.GetAddressBytes(), 0), 0, macAddr, ref macAddrLen);
                if (MacAddresstoString(macAddr) != "00-00-00-00-00-00")
                {
                    string macString = MacAddresstoString(macAddr);
                    return $"{ipString} - {macString} - Alive";
                }
            }
            catch (Exception e)
            {
                return $"{ipString} - Invalid";
            }
            return "";
        }

        private void CheckStatus(System.Net.IPAddressCollection ipList, int timeout, string task_id)
        {
            List<Tuple<string, string, string>> result = new List<Tuple<string, string, string>>();
            byte[] macAddr = new byte[6];
            StringBuilder sb = new StringBuilder();
            try
            {
                Task.Run(() =>
                {
                    Parallel.ForEach(ipList, ipString =>
                    {
                        messageManager.WriteLine(ThreadedARPRequest(ipString.ToString()), task_id, false);
                    });
                }).Wait();
            }
            catch (Exception e)
            {
                sb.AppendLine(e.ToString());
            }
            Thread.Sleep(timeout);
        }
    }
}
