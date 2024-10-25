using Agent.Models;

namespace Agent.Interfaces
{
    //IProfile manages the communication between mythic and the agent
    //CheckIn() get's called first, initiates checkin process with Mythic and updates required data
    //StartBeacon() the main beacon, how the loop is handled is dependant on the profile
    //StopBeacon() stops the beacon
    public interface IAgentMod
    {
        public abstract Task Go();
    }
}
