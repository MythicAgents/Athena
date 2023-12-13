using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Athena.Commands;
using Athena.Models.Responses;
using System.Linq;
using ps;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Ps : IPlugin
    {
        public string Name => "ps";

        public bool Interactive => false;

        public void Start(Dictionary<string, string> args)
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
                    if (OperatingSystem.IsWindows())
                    {
                        processes.AddRange(ProcessHelper.GetProcessesWithParent());
                    }
                    else
                    {
                        processes.AddRange(convertProcessToMythicProcess(Process.GetProcesses()));
                    }
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

        public void Interact(InteractiveMessage message)
        {
            throw new NotImplementedException();
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }
    }
}
