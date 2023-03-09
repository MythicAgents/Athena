using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Commands
{
    public class PluginHandler
    {
        private static StringWriter sw = new StringWriter();
        private static bool stdOutIsMonitored = false;
        private static string monitoring_task = "";
        private static TextWriter origStdOut;
        public static bool CaptureStdOut(string task_id)
        {
            if (stdOutIsMonitored)
            {
                return false;
            }
            try
            {
                monitoring_task = task_id;
                origStdOut = Console.Out;
                Console.SetOut(sw);
                stdOutIsMonitored = true;
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static void ReleaseStdOut()
        {
            stdOutIsMonitored = false;
            Console.SetOut(origStdOut);
        }
        public static bool StdIsBusy()
        {
            return stdOutIsMonitored;
        }
        public static string StdOwner()
        {
            return monitoring_task;
        }
        public async static Task<string> GetStdOut()
        {
            await sw.FlushAsync();
            string output = sw.GetStringBuilder().ToString();

            //Clear the writer
            sw.GetStringBuilder().Clear();
            return output;
        }
    }
}
