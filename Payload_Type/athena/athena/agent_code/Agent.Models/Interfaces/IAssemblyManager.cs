namespace Agent.Interfaces
{
    public interface IAssemblyManager
    {
        public abstract bool LoadAssemblyAsync(string task_id, byte[] buf);
        public abstract bool LoadPluginAsync(string task_id, string pluginName, byte[] buf);
        public abstract bool TryGetPlugin(string name, out IPlugin? plugin);
        public abstract bool TryGetPlugin(string name, out IFilePlugin? plugin);
        public abstract bool TryGetPlugin(string name, out IProxyPlugin? plugin);
        public abstract bool TryGetPlugin(string name, out IForwarderPlugin? plugin);
    }
}
