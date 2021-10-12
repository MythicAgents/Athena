using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Athena
{
    public static class Plugin
    {
        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            string output = "";
            foreach (NetworkInterface netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                output += netInterface.Name + Environment.NewLine +Environment.NewLine;
                output += "\t      Description: " + netInterface.Description + Environment.NewLine + Environment.NewLine;
                IPInterfaceProperties ipProps = netInterface.GetIPProperties();
                int i = 0;

                foreach (UnicastIPAddressInformation unicastIPAddressInformation in netInterface.GetIPProperties().UnicastAddresses)
                {
                    if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (i == 0)
                        {
                            output += "\t      Subnet Mask: " + unicastIPAddressInformation.IPv4Mask + Environment.NewLine;
                        }
                        else
                        {
                            output += "\t\t\t   " + unicastIPAddressInformation.IPv4Mask + Environment.NewLine;
                        }
                        i++;
                    }
                }
                i = 0;
                output += Environment.NewLine;

                foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                {
                    if (i == 0)
                    {
                        output += "\t\tAddresses: " + addr.Address.ToString() + Environment.NewLine;
                    }
                    else
                    {
                        output += "\t\t\t   " + addr.Address.ToString() + Environment.NewLine;
                    }
                    i++;
                }
                i = 0;
                output += Environment.NewLine;
                if (ipProps.GatewayAddresses.Count == 0)
                {
                    output += "\t  Default Gateway:" + Environment.NewLine;
                }
                else
                {
                    foreach (var gateway in ipProps.GatewayAddresses)
                    {
                        if (i == 0)
                        {
                            output += "\t  Default Gateway: " + gateway.Address.ToString() + Environment.NewLine;
                        }
                        else
                        {
                            output += "\t\t\t " + gateway.Address.ToString() + Environment.NewLine;
                        }
                    }
                }
                output += Environment.NewLine + Environment.NewLine + Environment.NewLine;
            }
            return new PluginResponse()
            {
                success = true,
                output = output
            };
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
