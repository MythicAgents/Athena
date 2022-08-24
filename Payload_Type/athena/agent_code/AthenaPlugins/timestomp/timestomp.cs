using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Net.NetworkInformation;
using PluginBase;

namespace Plugin
{
    public static class timestomp
    {


        public static void Execute(Dictionary<string, object> args)
        {
            StringBuilder sb = new StringBuilder();

            string sourceFile = (string)args["source"];
            string destFile = (string)args["dest"];

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
            PluginHandler.WriteOutput(sb.ToString(), (string)args["task-id"], true);
            return;
        }
    }
}