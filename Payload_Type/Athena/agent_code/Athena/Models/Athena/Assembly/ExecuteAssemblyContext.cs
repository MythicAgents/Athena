using System.Runtime.Loader;

namespace Athena.Commands.Model
{
    public class ExecuteAssemblyContext : AssemblyLoadContext
    {
        public ExecuteAssemblyContext() : base(isCollectible: true)
        {
        }
    }
}
