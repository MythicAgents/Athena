﻿using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Athena.Commands;
using Athena.Models.Responses;

namespace Plugins
{
    public class IfConfig : AthenaPlugin
    {
        public override string Name => "ifconfig";
        public override void Execute(Dictionary<string, string> args)
        {
            StringBuilder sb = new StringBuilder();
            foreach (NetworkInterface netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                sb.Append(netInterface.Name + Environment.NewLine + Environment.NewLine);
                sb.Append("\tDescription: " + netInterface.Description + Environment.NewLine + Environment.NewLine);
                IPInterfaceProperties ipProps = netInterface.GetIPProperties();
                int i = 0;

                foreach (UnicastIPAddressInformation unicastIPAddressInformation in netInterface.GetIPProperties().UnicastAddresses)
                {
                    if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (i == 0)
                        {
                            sb.Append("\tSubnet Mask: " + unicastIPAddressInformation.IPv4Mask + Environment.NewLine);
                        }
                        else
                        {
                            sb.Append("\t\t\t" + unicastIPAddressInformation.IPv4Mask + Environment.NewLine);
                        }
                        i++;
                    }
                }
                i = 0;
                sb.Append(Environment.NewLine);

                foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                {
                    if (i == 0)
                    {
                        sb.Append("\t\tAddresses: " + addr.Address.ToString() + Environment.NewLine);
                    }
                    else
                    {
                        sb.Append("\t\t\t" + addr.Address.ToString() + Environment.NewLine);
                    }
                    i++;
                }
                i = 0;
                sb.AppendLine();
                if (ipProps.GatewayAddresses.Count == 0)
                {
                    sb.Append("\tDefault Gateway:" + Environment.NewLine);
                }
                else
                {
                    foreach (var gateway in ipProps.GatewayAddresses)
                    {
                        if (i == 0)
                        {
                            sb.Append("\tDefault Gateway: " + gateway.Address.ToString() + Environment.NewLine);
                        }
                        else
                        {
                            sb.Append("\t\t\t" + gateway.Address.ToString() + Environment.NewLine);
                        }
                    }
                }
                sb.Append(Environment.NewLine + Environment.NewLine + Environment.NewLine);
            }
            TaskResponseHandler.AddResponse(new ResponseResult
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = args["task-id"],
            });
        }
    }
}
