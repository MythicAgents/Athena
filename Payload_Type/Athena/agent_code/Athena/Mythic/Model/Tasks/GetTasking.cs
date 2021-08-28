namespace Athena.Mythic.Model
{
    public class GetTasking
    {
        public string action;
        public int tasking_size;
        // Only necessary if we're passing on data from other agents (maybe a future release)
        //public Dictionary<string, string> delegates;
    }
}
