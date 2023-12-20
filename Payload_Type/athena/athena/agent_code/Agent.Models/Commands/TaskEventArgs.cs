namespace Agent.Models
{
    public class TaskingReceivedArgs : EventArgs
    {
        public GetTaskingResponse tasking_response { get; set; }
        public TaskingReceivedArgs(GetTaskingResponse response)
        {
            this.tasking_response = response;
        }
    }
}
