using Workflow.Contracts;
using Workflow.Models;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "ifconfig";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
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
            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = job.task.id,
            });
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
