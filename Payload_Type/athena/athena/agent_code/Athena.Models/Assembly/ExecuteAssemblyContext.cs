using System.Runtime.Loader;

namespace Athena.Models.Assembly
{
    public class ExecuteAssemblyContext : AssemblyLoadContext
    {
        public ExecuteAssemblyContext(string name) : base(name, isCollectible: true)
        {
        }
    }
}
