using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Utilities;
using System.Linq;
using ps;

namespace Plugins
{
    public class Ps : AthenaPlugin
    {
        public override string Name => "ps";
        public override void Execute(Dictionary<string, string> args)
        {
            try
            {
                List<MythicProcessInfo> processes = new List<MythicProcessInfo>();
                //This can support remote computers, I just need to see if mythic supports it
                List<Process> procs = new List<Process>();

                if (args.ContainsKey("host"))
                {
                    processes.AddRange(convertProcessToMythicProcess(Process.GetProcesses(args["host"])));
                }
                else if (args.ContainsKey("targetlist"))
                {
                    IEnumerable<string> hosts = GetTargetsFromFile(Convert.FromBase64String(args["targetlist"].ToString())).ToArray<string>();

                    foreach (var host in hosts)
                    {
                        processes.AddRange(convertProcessToMythicProcess(Process.GetProcesses(host)));
                    }
                }
                else
                {
                    processes.AddRange(ProcessHelper.GetProcessesWithParent());
                }

                TaskResponseHandler.AddResponse(new ProcessResponseResult
                {
                    task_id = args["task-id"],
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x2C" } },
                    processes = processes
                });
            }
            catch (Exception e)
            {
                TaskResponseHandler.AddResponse(new ProcessResponseResult
                {
                    task_id = args["task-id"],
                    completed = true,
                    user_output = e.ToString(),
                    processes = new List<MythicProcessInfo>()
                });
            }
        }
        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = System.Text.Encoding.ASCII.GetString(b);

            return allData.Split(Environment.NewLine);
        }

        private List<MythicProcessInfo> convertProcessToMythicProcess(Process[] procs)
        {
            List<MythicProcessInfo> processes = new List<MythicProcessInfo>();
            foreach (var proc in procs)
            {
                try
                {
                    processes.Add(new MythicProcessInfo()
                    {
                        process_id = proc.Id,
                        name = proc.ProcessName,
                        description = proc.MainWindowTitle,
                        bin_path = proc.MainModule.FileName,
                        start_time = new DateTimeOffset(proc.StartTime).ToUnixTimeMilliseconds(),
                    });
                }
                catch
                {
                    processes.Add(new MythicProcessInfo()
                    {
                        process_id = proc.Id,
                        name = proc.ProcessName,
                        description = proc.MainWindowTitle,
                    });
                }
            }

            return processes;
        }
    }
}
