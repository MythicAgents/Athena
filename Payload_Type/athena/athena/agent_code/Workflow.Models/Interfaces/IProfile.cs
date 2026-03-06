using Workflow.Models;

namespace Workflow.Contracts
{
    //IChannel manages the communication between mythic and the agent
    //CheckIn() get's called first, initiates checkin process with Mythic and updates required data
    //StartBeacon() the main beacon, how the loop is handled is dependant on the profile
    //StopBeacon() stops the beacon
    public interface IChannel
    {
        public abstract Task StartBeacon();
        public abstract bool StopBeacon();
        public abstract Task<CheckinResponse> Checkin(Checkin checkin);
        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
    }
}
