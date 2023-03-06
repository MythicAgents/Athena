using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Net.NetworkInformation;
using Athena.Plugins;

namespace Plugins
{
    public class TimeStomp : AthenaPlugin
    {
        public override string Name => "timestomp";
        public override void Execute(Dictionary<string, string> args)
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
            PluginHandler.Write(sb.ToString(), args["task-id"], true);
        }
    }
}