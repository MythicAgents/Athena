using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Athena
{
    public static class Plugin
    {
        public static string Execute(Dictionary<string, object> args)
        {
            string output = "";
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .OrderByDescending(c => c.Speed);
            //.FirstOrDefault(c => c.NetworkInterfaceType != NetworkInterfaceType.Loopback && c.OperationalStatus == OperationalStatus.Up);

            foreach (var iface in interfaces)
            {
                output += iface.Name + Environment.NewLine;
                var props = iface.GetIPProperties();
                output += "Default Gateway . . . . . . . . . : " + props.GatewayAddresses;
                foreach(var ip in props.UnicastAddresses)
                {
                    Console.WriteLine(ip.Address.Address);
                }
                //var firstIpV4Address = props.UnicastAddresses
                //    .Where(c => c.Address.AddressFamily == AddressFamily.InterNetwork)
                //    .Select(c => c.Address);

            }

            return "Hello from Execute!";
        }
    }
}
