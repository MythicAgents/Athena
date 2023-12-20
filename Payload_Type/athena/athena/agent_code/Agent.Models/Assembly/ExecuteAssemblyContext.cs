using System.Runtime.Loader;

namespace Agent.Models
{
    public class ExecuteAssemblyContext : AssemblyLoadContext
    {
        public ExecuteAssemblyContext(string name) : base(name, isCollectible: true)
        {
        }
    }
}
