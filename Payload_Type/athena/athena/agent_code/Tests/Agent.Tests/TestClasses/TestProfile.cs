using Agent.Interfaces;
using Agent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestInterfaces
{
    public class TestProfile : IProfile
    {
        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
        public CheckinResponse? checkinResponse { get; set; }
        public GetTaskingResponse? getTaskingResponse { get; set; }
        public bool isRunning = false;
        private bool returnedTask = false;
        public CancellationTokenSource cts = new CancellationTokenSource();
        public ManualResetEvent taskingSent = new ManualResetEvent(false);
        public TestProfile() 
        {
        }
        public TestProfile(CheckinResponse? checkinResponse)
        {
            this.checkinResponse = checkinResponse;
        }

        public TestProfile(GetTaskingResponse? getTaskingResponse)
        {
            this.getTaskingResponse = getTaskingResponse;
        }
        public TestProfile(bool nullable)
        {
            this.checkinResponse = null;
            this.getTaskingResponse = null;
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            if (this.checkinResponse is null)
            {
                return new CheckinResponse()
                {
                    status = "success",
                    action = "checkin",
                    id = Guid.NewGuid().ToString(),
                    encryption_key = "",
                    decryption_key = "",
                    process_name = "",
                };
            }
            return this.checkinResponse;
        }

        public async Task StartBeacon()
        {
            isRunning = true;
            while (!cts.Token.IsCancellationRequested)
            {
                if (!returnedTask)
                {
                    TaskingReceivedArgs tra;
                    if (this.getTaskingResponse is null)
                    {

                        tra = new TaskingReceivedArgs(new GetTaskingResponse()
                        {
                            action = "get_tasking",
                            tasks = new List<ServerTask>()
                                {
                                    new ServerTask()
                                    {
                                        id = Guid.NewGuid().ToString(),
                                        command = "whoami",
                                        parameters = "",
                                    },
                                },
                            socks = new List<ServerDatagram>(),
                            rpfwd = new List<ServerDatagram>(),
                            delegates = new List<DelegateMessage>(),
                        });
                    }
                    else
                    {
                        tra = new TaskingReceivedArgs(this.getTaskingResponse);
                    }

                    SetTaskingReceived?.Invoke(this, tra);
                    returnedTask = true;
                    taskingSent.Set();
                    return;
                }
            }
            isRunning = false;
        }

        public bool StopBeacon()
        {
            cts.Cancel();
            return true;
        }
    }
}
