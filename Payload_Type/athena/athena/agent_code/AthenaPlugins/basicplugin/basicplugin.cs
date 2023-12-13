using Athena.Commands;
using Athena.Commands.Models;
using Athena.Models.Comms.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
namespace Plugins
{
    public class BasicPlugin : IPlugin
    {
        public string Name => "myplugin";

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
                TaskResponseHandler.Write("Hello World", args["task-id"], true, "error");
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
