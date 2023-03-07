using Athena.Models.Mythic.Tasks;
using Athena.Models.Athena.Commands;
using Athena.Utilities;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Athena.Plugins;
using Athena.Models.Mythic.Checkin;
using System.Text.Json;
using Athena.Models;
using System.Security.Principal;

namespace Athena.Commands
{
    public class TokenHandler
    {
        static Dictionary<int, SafeAccessTokenHandle> tokens = new Dictionary<int, SafeAccessTokenHandle>();
        /// <summary>
        /// Create a Token for impersonation
        /// </summary>
        /// <param name="job">The MythicJob containing the token information</param>
        public async Task<string> CreateToken(MythicJob job)
        {
            return new ResponseResult()
            {
                completed = true,
                user_output = "Not available in this configuration",
                task_id = job.task.id,
            }.ToJson();
        }
        /// <summary>
        /// Begin impersonation for the thread
        /// </summary>
        /// <param name="job">The token ID</param>
        public async Task<bool> ThreadImpersonate(int t)
        {
            return true;
        }
        /// <summary>
        /// End impersonation for the thread
        /// </summary>
        public async Task<bool> ThreadRevert()
        {
            return true;
        }
        /// <summary>
        /// List available tokens for impersonation
        /// </summary>
        /// <param name="job">The MythicJob containing the token information</param>
        public async Task<string> ListTokens(MythicJob job)
        {
            return new ResponseResult()
            {
                completed = true,
                user_output = "Not available in this configuration",
                task_id = job.task.id,
            }.ToJson();
        }

        public static int getIntegrity()
        {
            return PInvoke.geteuid() == 0 ? 3 : 2;
        }
    }
}
