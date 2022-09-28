using PluginBase;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(int DestIP, int SrcIP, byte[] pMacAddr, ref uint PhyAddrLen);

        private static uint macAddrLen = (uint)new byte[6].Length;
        private const string separator = "|";

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

        public void CheckStatus(IPAddressCollection ipList, int timeout, string task_id)
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
                        PluginHandler.Write(ThreadedARPRequest(ipString.ToString()), task_id, false);
                    });
                }).Wait();
            }
            catch (Exception e)
            {
                sb.AppendLine(e.ToString());
            }
            Thread.Sleep(timeout);
        }
        public override void Execute(Dictionary<string, object> args)
        {
            try
            {
                IPNetwork ipnetwork = IPNetwork.Parse((string)args["cidr"]);
                IPAddressCollection iac = ipnetwork.ListIPAddress();
                int timeout = (int)args["timeout"];

                CheckStatus(iac, timeout * 1000, (string)args["task-id"]);
                PluginHandler.Write("Finished Executing", (string)args["task-id"], true);


            }
            catch (Exception e)
            {
                PluginHandler.Write(e.ToString(), (string)args["task-id"], true, "error");
            }
        }
    }
}