using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ps;
using Agent.Interfaces;

using System.Xml.Schema;
using Agent.Models;
using Agent.Utilities;

namespace ps
{
    public class Ps : IPlugin
    {
        public string Name => "ps";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }

        public Ps(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                List<ServerProcessInfo> processes = new List<ServerProcessInfo>();
                //This can support remote computers, I just need to see if mythic supports it
                List<Process> procs = new List<Process>();

                if (args.ContainsKey("host"))
                {
                    processes.AddRange(convertProcessToServerProcess(Process.GetProcesses(args["host"])));
                }
                else if (args.ContainsKey("targetlist"))
                {
                    IEnumerable<string> hosts = GetTargetsFromFile(Convert.FromBase64String(args["targetlist"].ToString())).ToArray<string>();

                    foreach (var host in hosts)
                    {
                        processes.AddRange(convertProcessToServerProcess(Process.GetProcesses(host)));
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
                        processes.AddRange(convertProcessToServerProcess(Process.GetProcesses()));
                    }
                }

                await messageManager.AddResponse(new ProcessResponseResult
                {
                    task_id = job.task.id,
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x2C" } },
                    processes = processes
                });
            }
            catch (Exception e)
            {
                await messageManager.AddResponse(new ProcessResponseResult
                {
                    task_id = job.task.id,
                    completed = true,
                    user_output = e.ToString(),
                    processes = new List<ServerProcessInfo>()
                });
            }
            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }
        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = System.Text.Encoding.ASCII.GetString(b);

            return allData.Split(Environment.NewLine);
        }

        private List<ServerProcessInfo> convertProcessToServerProcess(Process[] procs)
        {
            List<ServerProcessInfo> processes = new List<ServerProcessInfo>();

            foreach (var proc in procs)
            {
                try
                {
                    processes.Add(new ServerProcessInfo()
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
                    processes.Add(new ServerProcessInfo()
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
