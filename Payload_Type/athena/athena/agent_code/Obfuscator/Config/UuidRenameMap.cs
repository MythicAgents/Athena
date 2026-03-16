using System.Security.Cryptography;
using System.Text;

namespace Obfuscator.Config;

public sealed class UuidRenameMap
{
    private static readonly string[] Interfaces =
    [
        "IModule", "IInteractiveModule", "IFileModule",
        "IForwarderModule", "IProxyModule", "IBufferedProxyModule",
        "IChannel", "IService", "IComponentProvider",
        "IDataBroker", "IServiceConfig", "ISecurityProvider",
        "ILogger", "IRequestDispatcher", "IRuntimeExecutor",
        "ICredentialProvider", "IScriptEngine", "IServiceExtension",
    ];

    private static readonly string[] InterfaceMembers =
    [
        "Name", "Execute", "Interact", "HandleNextMessage",
        "ForwardDelegate", "HandleDatagram", "FlushServerMessages",
        "StartBeacon", "StopBeacon", "SetTaskingReceived",
        "TryGetModule", "LoadModuleAsync", "LoadAssemblyAsync",
        "AddTaskResponse", "AddDelegateMessage",
        "AddInteractMessage", "AddDatagram", "Write", "WriteLine",
        "AddKeystroke", "AddJob", "GetJobs", "TryGetJob",
        "CompleteJob", "GetAgentResponseString",
        "HasResponses", "CaptureStdOut", "ReleaseStdOut",
        "StdIsBusy", "GetStdOut",
        "Spawn", "TryGetHandle",
        "AddToken", "Impersonate", "List", "Revert",
        "getIntegrity", "GetImpersonationContext",
        "RunTaskImpersonated", "HandleFilePluginImpersonated",
        "HandleInteractivePluginImpersonated",
        "LoadPyLib", "ExecuteScriptAsync", "ExecuteScript",
        "ClearPyLib",
    ];

    private static readonly string[] ContractTypes =
    [
        "ServerJob", "InteractMessage", "ServerTaskingResponse",
        "DelegateMessage", "ServerDatagram",
        "PluginContext", "ITaskResponse", "Checkin",
        "CheckinResponse", "TaskingReceivedArgs",
        "DatagramSource", "SpawnOptions", "CreateToken",
        "TokenTaskResponse",
    ];

    private static readonly string[] PluginContextParams =
    [
        "MessageManager", "Config", "Logger",
        "TokenManager", "Spawner", "ScriptEngine",
    ];

    private static readonly string[] Namespaces =
    [
        "Workflow.Contracts", "Workflow.Models",
    ];

    private static readonly string[] AllOriginalNames =
        Interfaces
            .Concat(InterfaceMembers)
            .Concat(ContractTypes)
            .Concat(PluginContextParams)
            .Concat(Namespaces)
            .ToArray();

    private static readonly HashSet<string> AlwaysRenameNames =
        new(Interfaces
            .Concat(ContractTypes)
            .Concat(Namespaces));

    private static readonly char[] AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private readonly Dictionary<string, string> _map;

    private UuidRenameMap(Dictionary<string, string> map)
    {
        _map = map;
    }

    public static UuidRenameMap Derive(string uuid)
    {
        var seed = ComputeSeed(uuid);
        var rng = new Random(seed);
        var used = new HashSet<string>();
        var map = new Dictionary<string, string>();

        foreach (var name in AllOriginalNames)
        {
            var renamed = GenerateUniqueName(rng, used);
            map[name] = renamed;
        }

        return new UuidRenameMap(map);
    }

    public string GetRenamed(string originalName)
    {
        return _map[originalName];
    }

    public List<string> GetAllRenamedValues()
    {
        return _map.Values.ToList();
    }

    public Dictionary<string, string> GetAllMappings()
    {
        return new Dictionary<string, string>(_map);
    }

    /// <summary>
    /// Returns true if the name is a type, interface, or namespace
    /// that should always be renamed regardless of context.
    /// Returns false for interface members and parameters that
    /// should only be renamed in contract contexts.
    /// </summary>
    public static bool IsAlwaysRename(string name)
    {
        return AlwaysRenameNames.Contains(name);
    }

    private static int ComputeSeed(string uuid)
    {
        var input = Encoding.UTF8.GetBytes(uuid + "athena-obfs");
        var hash = SHA256.HashData(input);
        return BitConverter.ToInt32(hash, 0);
    }

    private static string GenerateUniqueName(
        Random rng,
        HashSet<string> used)
    {
        var length = 2;
        while (true)
        {
            var candidate = GenerateCandidate(rng, length);
            if (used.Add(candidate))
            {
                return candidate;
            }
            length++;
        }
    }

    private static string GenerateCandidate(Random rng, int length)
    {
        var sb = new StringBuilder(length + 1);
        sb.Append('_');
        for (var i = 0; i < length; i++)
        {
            sb.Append(AlphaNumChars[rng.Next(AlphaNumChars.Length)]);
        }
        return sb.ToString();
    }
}
