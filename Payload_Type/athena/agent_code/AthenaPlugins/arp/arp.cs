using PluginBase;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Plugin
{
    public static class arp
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(int DestIP, int SrcIP, byte[] pMacAddr, ref uint PhyAddrLen);

        private static uint macAddrLen = (uint)new byte[6].Length;
        private const string separator = "|";
        private static List<string> macList = new List<string>();

        private static string MacAddresstoString(byte[] macAdrr)
        {
            string macString = BitConverter.ToString(macAdrr);
            return macString.ToUpper();
        }

        private static string ThreadedARPRequest(string ipString)
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

        public static void CheckStatus(IPAddressCollection ipList, int timeout, string task_id)
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
                        PluginHandler.WriteOutput(ThreadedARPRequest(ipString.ToString()), task_id, false);
                    });
                }).Wait();
            }
            catch (Exception e)
            {
                sb.AppendLine(e.ToString());
            }
            Thread.Sleep(timeout);
        }

        public static void Execute(Dictionary<string, object> args)
        {
            try
            {
                IPNetwork ipnetwork = IPNetwork.Parse((string)args["cidr"]);
                IPAddressCollection iac = ipnetwork.ListIPAddress();
                int timeout = (int)args["timeout"];

                CheckStatus(iac, timeout * 1000, (string)args["task-id"]);
                PluginHandler.WriteOutput("Finished Executing", (string)args["task-id"], true);


            }
            catch (Exception e)
            {
                PluginHandler.WriteOutput(e.ToString(), (string)args["task-id"], true, "error");
            }
        }
    }
}