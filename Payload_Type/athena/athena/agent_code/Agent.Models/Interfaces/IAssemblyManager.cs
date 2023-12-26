namespace Agent.Interfaces
{
    public interface IAssemblyManager
    {
        public abstract bool LoadAssemblyAsync(string task_id, byte[] buf);
        public abstract bool LoadPluginAsync(string task_id, string pluginName, byte[] buf);
        public abstract bool TryGetPlugin<T>(string name, out T? plugin) where T : IPlugin;
    }
}
