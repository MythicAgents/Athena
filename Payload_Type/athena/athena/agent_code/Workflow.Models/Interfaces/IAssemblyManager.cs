namespace Workflow.Contracts
{
    public interface IComponentProvider
    {
        public abstract bool LoadAssemblyAsync(string task_id, byte[] buf);
        public abstract bool LoadModuleAsync(string task_id, string moduleName, byte[] buf);
        public abstract bool TryGetModule<T>(string name, out T? plugin) where T : IModule;
    }
}
