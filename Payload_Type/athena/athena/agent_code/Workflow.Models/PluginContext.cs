using Workflow.Models;

namespace Workflow.Contracts
{
    public record PluginContext(
        IDataBroker MessageManager,
        IServiceConfig Config,
        ILogger Logger,
        ICredentialProvider TokenManager,
        IRuntimeExecutor Spawner,
        IScriptEngine ScriptEngine
    );
}
