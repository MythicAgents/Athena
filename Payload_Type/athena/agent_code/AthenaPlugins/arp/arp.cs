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

        public static string CheckStatus(IPAddressCollection ipList, int timeout)
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
                        sb.AppendLine(ThreadedARPRequest(ipString.ToString()));
                    });
                }).Wait();
            }
            catch (Exception e)
            {
                sb.AppendLine(e.ToString());
            }
            System.Threading.Thread.Sleep(timeout);
            return sb.ToString();
        }

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                IPNetwork ipnetwork = IPNetwork.Parse((string)args["cidr"]);
                IPAddressCollection iac = ipnetwork.ListIPAddress();
                int timeout = 0;
                ResponseResult rr = new ResponseResult();
                rr.task_id = (string)args["task-id"];
                if (int.TryParse((string)args["timeout"], out timeout))
                {
                    rr.user_output = CheckStatus(iac, timeout*1000);
                    rr.completed = "true";
                }
                else
                {
                    rr.user_output = "Invalid timeout specified";
                    rr.completed = "true";
                    rr.status = "errored";
                }
                return rr;

                
            }
            catch (Exception e)
            {
                //oh no an error
                return new ResponseResult
                {
                    completed = "true",
                    user_output = e.Message,
                    task_id = (string)args["task-id"],
                    status = "error"
                };
            }
        }



        //private static void FormatOutput(string message, System.ConsoleColor color)
        //{
        //    Console.ForegroundColor = color;
        //    Console.WriteLine(message);
        //    Console.ResetColor();
        //}
    }
}