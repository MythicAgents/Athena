using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Config;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class UuidRenameTests
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
        "AddTaskResponse", "AddDelegateMessage", "AddInteractMessage",
        "AddDatagram", "Write", "WriteLine",
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

    private static IEnumerable<string> AllNames =>
        Interfaces
            .Concat(InterfaceMembers)
            .Concat(ContractTypes)
            .Concat(PluginContextParams)
            .Concat(Namespaces);

    [TestMethod]
    public void SameUuid_ProducesSameMapping()
    {
        var uuid = "550e8400-e29b-41d4-a716-446655440000";
        var map1 = UuidRenameMap.Derive(uuid);
        var map2 = UuidRenameMap.Derive(uuid);

        foreach (var name in AllNames)
        {
            Assert.AreEqual(
                map1.GetRenamed(name),
                map2.GetRenamed(name),
                $"Mapping for '{name}' was not deterministic.");
        }
    }

    [TestMethod]
    public void DifferentUuid_ProducesDifferentMapping()
    {
        var map1 = UuidRenameMap.Derive(
            "550e8400-e29b-41d4-a716-446655440000");
        var map2 = UuidRenameMap.Derive(
            "6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        var anyDifferent = AllNames.Any(
            n => map1.GetRenamed(n) != map2.GetRenamed(n));

        Assert.IsTrue(
            anyDifferent,
            "Different UUIDs produced identical mappings.");
    }

    [TestMethod]
    public void AllContractTypes_AreMapped()
    {
        var map = UuidRenameMap.Derive("test-uuid-1234");

        foreach (var name in AllNames)
        {
            var renamed = map.GetRenamed(name);
            Assert.IsNotNull(
                renamed,
                $"'{name}' has no mapping.");
            Assert.AreNotEqual(
                string.Empty,
                renamed,
                $"'{name}' mapped to empty string.");
            Assert.AreNotEqual(
                name,
                renamed,
                $"'{name}' mapped to itself.");
        }
    }

    [TestMethod]
    public void GeneratedNames_DoNotCollide()
    {
        var map = UuidRenameMap.Derive("collision-test-uuid");
        var allRenamed = map.GetAllRenamedValues();

        var unique = new HashSet<string>(allRenamed);
        Assert.AreEqual(
            allRenamed.Count,
            unique.Count,
            "Duplicate renamed values detected: " +
            string.Join(", ", allRenamed
                .GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)));
    }

    [TestMethod]
    public void GeneratedNames_StartWithUnderscore()
    {
        var map = UuidRenameMap.Derive("underscore-test-uuid");
        var allRenamed = map.GetAllRenamedValues();

        foreach (var renamed in allRenamed)
        {
            Assert.IsTrue(
                renamed.StartsWith('_'),
                $"Renamed value '{renamed}' does not start with '_'.");
        }
    }

    // --- UuidRenameTransform (syntax rewriter) tests ---

    private static string ApplyRenameTransform(string source, string uuid)
    {
        var map = UuidRenameMap.Derive(uuid);
        var transform = new UuidRenameTransform(map);
        var tree = CSharpSyntaxTree.ParseText(source);
        var rewritten = transform.Rewrite(tree);
        return rewritten.GetRoot().ToFullString();
    }

    [TestMethod]
    public void NamespaceDeclaration_IsRenamed()
    {
        var source = "namespace Workflow.Contracts { public interface IModule { } }";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsFalse(result.Contains("Workflow.Contracts"));
        Assert.IsFalse(result.Contains("IModule"));
    }

    [TestMethod]
    public void UsingDirective_IsRenamed()
    {
        var source = "using Workflow.Contracts;";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsFalse(result.Contains("Workflow.Contracts"));
    }

    [TestMethod]
    public void InterfaceMember_IsRenamed()
    {
        var source = @"
namespace Workflow.Contracts
{
    public interface IModule
    {
        string Name { get; }
        System.Threading.Tasks.Task Execute(object job);
    }
}";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsFalse(result.Contains("IModule"));
        Assert.IsFalse(result.Contains("Name"));
        Assert.IsFalse(result.Contains("Execute"));
    }

    [TestMethod]
    public void NonContractType_IsNotRenamed()
    {
        var source = "public class MyCustomPlugin { public void DoStuff() { } }";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsTrue(result.Contains("MyCustomPlugin"));
        Assert.IsTrue(result.Contains("DoStuff"));
    }

    [TestMethod]
    public void WorkflowModels_NamespaceIsRenamed()
    {
        var source =
            "using Workflow.Models; namespace Workflow.Models { public class ServerJob { } }";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsFalse(result.Contains("Workflow.Models"));
        Assert.IsFalse(result.Contains("ServerJob"));
    }

    [TestMethod]
    public void PluginContextRecord_IsRenamed()
    {
        var source = @"
namespace Workflow.Contracts
{
    public record PluginContext(object MessageManager, object Config, object Logger);
}";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsFalse(result.Contains("PluginContext"));
        Assert.IsFalse(result.Contains("MessageManager"));
        Assert.IsFalse(result.Contains("Config"));
        Assert.IsFalse(result.Contains("Logger"));
    }

    [TestMethod]
    public void Constructor_IsRenamed_WhenClassIsRenamed()
    {
        var source = @"
namespace Workflow.Models
{
    public class ServerJob
    {
        public ServerJob() { }
        public ServerJob(string id) { }
    }
}";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsFalse(result.Contains("ServerJob"),
            "Constructor should be renamed along with its class.");
    }

    [TestMethod]
    public void OverrideMethod_IsNotRenamed()
    {
        var source = @"
using System.Text;
namespace Workflow.Models
{
    public class ConsoleWriter : System.IO.TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(string value) { }
        public override void WriteLine(string value) { }
    }
}";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsTrue(result.Contains("override void Write("),
            "Override method Write should not be renamed.");
        Assert.IsTrue(result.Contains("override void WriteLine("),
            "Override method WriteLine should not be renamed.");
    }
}
