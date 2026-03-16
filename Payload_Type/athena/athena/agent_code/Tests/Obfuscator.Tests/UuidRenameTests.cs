using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Config;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class UuidRenameTests
{
    private static ContractNames TestNames => new(
        Interfaces:
        [
            "IModule", "IInteractiveModule", "IFileModule",
            "IForwarderModule", "IProxyModule", "IBufferedProxyModule",
            "IChannel", "IService", "IComponentProvider",
            "IDataBroker", "IServiceConfig", "ISecurityProvider",
            "ILogger", "IRequestDispatcher", "IRuntimeExecutor",
            "ICredentialProvider", "IScriptEngine", "IServiceExtension",
        ],
        InterfaceMembers:
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
        ],
        Types:
        [
            "ServerJob", "InteractMessage", "ServerTaskingResponse",
            "DelegateMessage", "ServerDatagram",
            "PluginContext", "ITaskResponse", "Checkin",
            "CheckinResponse", "TaskingReceivedArgs",
            "DatagramSource", "SpawnOptions", "CreateToken",
            "TokenTaskResponse",
        ],
        Namespaces:
        [
            "Workflow.Contracts", "Workflow.Models",
        ],
        RecordParams:
        [
            "MessageManager", "Config", "Logger",
            "TokenManager", "Spawner", "ScriptEngine",
        ]);

    private static IEnumerable<string> AllNames =>
        TestNames.Interfaces
            .Concat(TestNames.InterfaceMembers)
            .Concat(TestNames.Types)
            .Concat(TestNames.RecordParams)
            .Concat(TestNames.Namespaces);

    [TestMethod]
    public void SameUuid_ProducesSameMapping()
    {
        var uuid = "550e8400-e29b-41d4-a716-446655440000";
        var map1 = UuidRenameMap.Derive(uuid, TestNames);
        var map2 = UuidRenameMap.Derive(uuid, TestNames);

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
            "550e8400-e29b-41d4-a716-446655440000", TestNames);
        var map2 = UuidRenameMap.Derive(
            "6ba7b810-9dad-11d1-80b4-00c04fd430c8", TestNames);

        var anyDifferent = AllNames.Any(
            n => map1.GetRenamed(n) != map2.GetRenamed(n));

        Assert.IsTrue(
            anyDifferent,
            "Different UUIDs produced identical mappings.");
    }

    [TestMethod]
    public void AllContractTypes_AreMapped()
    {
        var map = UuidRenameMap.Derive("test-uuid-1234", TestNames);

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
        var map = UuidRenameMap.Derive(
            "collision-test-uuid", TestNames);
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
        var map = UuidRenameMap.Derive(
            "underscore-test-uuid", TestNames);
        var allRenamed = map.GetAllRenamedValues();

        foreach (var renamed in allRenamed)
        {
            Assert.IsTrue(
                renamed.StartsWith('_'),
                $"Renamed value '{renamed}' does not start with '_'.");
        }
    }

    // --- UuidRenameTransform (syntax rewriter) tests ---

    private static string ApplyRenameTransform(
        string source, string uuid)
    {
        var map = UuidRenameMap.Derive(uuid, TestNames);
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

    [TestMethod]
    public void BclMemberAccess_IsNotRenamed()
    {
        var source = @"
using System;
using System.Diagnostics;
namespace Workflow.Models
{
    public static class DebugLog
    {
        public static void Log(string msg)
        {
            Debug.WriteLine(msg);
            Console.WriteLine(msg);
        }
    }
}";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsTrue(result.Contains("Debug.WriteLine("),
            "Debug.WriteLine should not be renamed.");
        Assert.IsTrue(result.Contains("Console.WriteLine("),
            "Console.WriteLine should not be renamed.");
    }

    [TestMethod]
    public void ContractMemberAccess_IsRenamed()
    {
        var source = @"
using Workflow.Contracts;
namespace Workflow
{
    public class Plugin
    {
        private IDataBroker broker;
        public void Run()
        {
            broker.Write(""hello"");
        }
    }
}";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsFalse(result.Contains("broker.Write("),
            "Write on contract-typed variable should be renamed.");
    }

    [TestMethod]
    public void JsonPropertyName_IsNotRenamed()
    {
        var source = @"
using System.Text.Json;
namespace Workflow.Models
{
    public class Util
    {
        public string Get(JsonProperty node)
        {
            return node.Name;
        }
    }
}";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsTrue(result.Contains("node.Name"),
            "JsonProperty.Name should not be renamed.");
    }

    [TestMethod]
    public void TypeNameInMemberAccess_IsRenamed()
    {
        var source = @"
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Workflow.Models
{
    public class TokenTaskResponse { }

    [JsonSerializable(typeof(TokenTaskResponse))]
    public partial class Ctx : JsonSerializerContext { }

    public class Util
    {
        public string Get()
        {
            return Ctx.Default.TokenTaskResponse.ToString();
        }
    }
}";
        var result = ApplyRenameTransform(source, "test-uuid-1");
        Assert.IsFalse(result.Contains("TokenTaskResponse"),
            "Contract type name in member access should be renamed.");
    }

    [TestMethod]
    public void IsAlwaysRename_TypeNames_ReturnsTrue()
    {
        var map = UuidRenameMap.Derive("test-uuid-1", TestNames);
        Assert.IsTrue(map.IsAlwaysRename("IModule"));
        Assert.IsTrue(map.IsAlwaysRename("ServerJob"));
        Assert.IsTrue(map.IsAlwaysRename("TokenTaskResponse"));
        Assert.IsTrue(map.IsAlwaysRename("Workflow.Contracts"));
        Assert.IsTrue(map.IsAlwaysRename("Workflow.Models"));
    }

    [TestMethod]
    public void IsAlwaysRename_MemberNames_ReturnsFalse()
    {
        var map = UuidRenameMap.Derive("test-uuid-1", TestNames);
        Assert.IsFalse(map.IsAlwaysRename("Name"));
        Assert.IsFalse(map.IsAlwaysRename("Write"));
        Assert.IsFalse(map.IsAlwaysRename("Execute"));
        Assert.IsFalse(map.IsAlwaysRename("Config"));
        Assert.IsFalse(map.IsAlwaysRename("Logger"));
    }

    [TestMethod]
    public void ContractScanner_ScansWorkflowModels()
    {
        var contractsDir = FindWorkflowModelsDir();
        if (contractsDir is null)
        {
            Assert.Inconclusive(
                "Workflow.Models directory not found.");
            return;
        }

        var names = ContractScanner.Scan(contractsDir);

        Assert.IsTrue(names.Interfaces.Count > 0,
            $"No interfaces found in {contractsDir}. "
            + $"CS files found: {Directory.EnumerateFiles(contractsDir, "*.cs", SearchOption.AllDirectories).Count()}");
        Assert.IsTrue(names.Interfaces.Contains("IModule"),
            $"Should find IModule interface. "
            + $"Found: {string.Join(", ", names.Interfaces)}");
        Assert.IsTrue(names.Interfaces.Contains("IDataBroker"),
            "Should find IDataBroker interface");
        Assert.IsTrue(names.InterfaceMembers.Contains("Execute"),
            $"Should find Execute member. "
            + $"Found: {string.Join(", ", names.InterfaceMembers.Take(20))}");
        Assert.IsTrue(names.InterfaceMembers.Contains("Write"),
            "Should find Write member");
        Assert.IsTrue(names.Types.Contains("ServerJob"),
            $"Should find ServerJob type. "
            + $"Found: {string.Join(", ", names.Types)}");
        Assert.IsTrue(
            names.Namespaces.Contains("Workflow.Contracts"),
            $"Should find Workflow.Contracts namespace. "
            + $"Found: {string.Join(", ", names.Namespaces)}");
        Assert.IsTrue(
            names.RecordParams.Contains("MessageManager"),
            $"Should find MessageManager record param. "
            + $"Found: {string.Join(", ", names.RecordParams)}");

        // Verify we're not picking up non-contract types
        Assert.IsFalse(
            names.Namespaces.Contains("Workflow.Utilities"),
            "Should not include Workflow.Utilities namespace");
    }

    private static string? FindWorkflowModelsDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "Workflow.Models");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
