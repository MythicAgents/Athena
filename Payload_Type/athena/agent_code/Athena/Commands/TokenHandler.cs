#if DEBUG
    #define WINBUILD
#endif

#if WINBUILD
using Athena.Models.Mythic.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using System.Collections;
using Athena.Models.Athena.Commands;
using Newtonsoft.Json;

namespace Athena.Commands
{
    public class TokenHandler
    {
        static Dictionary<string, SafeAccessTokenHandle> tokens = new Dictionary<string, SafeAccessTokenHandle>();
        public async Task<object> HandleTokenJob(MythicJob job)
        {
            
        }
        
        public async Task<object> CreateToken(MythicJob job)
        {
            CreateToken tokenOptions = JsonConvert.DeserializeObject<CreateToken>(job.task.parameters);

            
        }
        
        public async Task<object> Impersonate(MythicJob job)
        {

        }
        
        public async Task<object> Revert()
        {
            
        }
    }
}
#endif
