using PluginBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena
{
    public static class Plugin
    {
        public static ProcessResponseResult Execute(Dictionary<string, object> args)
        {
            List<MythicProcessInfo> processes = new List<MythicProcessInfo>();

            Process[] procs = Process.GetProcesses();
            Parallel.ForEach(procs, proc =>
            {
                processes.Add(new MythicProcessInfo()
                {
                    process_id = proc.Id,
                    name = proc.ProcessName,
                    description = proc.MainWindowTitle,
                    bin_path = proc.MainModule.FileName,
                    start_time = proc.StartTime.ToString(),
                });
            });

            return new ProcessResponseResult
            {
                task_id = (string)args["task-id"],
                completed = "true",
                user_output = "Done.",
                processes = processes
            };
        }



        public static ResponseResult ExecuteOld(Dictionary<string, object> args)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                Process[] procs;
                //if (args.ContainsKey("computer"))
                //{
                //    output += "Getting processes for: " + (string)args["computer"] + Environment.NewLine;
                //    try
                //    {
                //        procs = Process.GetProcesses((string)args["computer"]);
                //    }
                //    catch (Exception e)
                //    {
                //        output += "An error occured while enumerating remote processes: " + e.Message;
                //        return new PluginResponse()
                //        {
                //            success = false,
                //            output = output
                //        };
                //    }

                //}
                //else
                //{
                procs = Process.GetProcesses().OrderBy(p => p.Id).ToArray();
                //}
                sb.Append("[");
                foreach (var proc in procs)
                {
                    //There doesn't seem to be any way to get process owner when using plain .NET
                    sb.Append($"{{\"process_id\":\"{proc.Id}\",\"name\":\"{proc.ProcessName}\",\"title\":\"{proc.MainWindowTitle.Replace(@"\", @"\\")}\"}},");
                }

                sb.Remove(sb.Length - 1, 1);
                sb.Append("]");
                return new ResponseResult
                {
                    completed = "true",
                    user_output = sb.ToString().ToString(),
                    task_id = (string)args["task-id"],
                };
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = e.Message,
                    task_id = (string)args["task-id"],
                    status = "error"
                };
            }
        }
    }
}
