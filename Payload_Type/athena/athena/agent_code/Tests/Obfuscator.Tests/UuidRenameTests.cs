using Microsoft.CodeAnalysis;
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
    public void Derive_MalformedUuid_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            UuidRenameMap.Derive("not-a-uuid", TestNames));
    }

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
    public void SameUuid_ProducesSameMappingRegardlessOfScannerOrder()
    {
        var reversed = new ContractNames(
            TestNames.Interfaces.AsEnumerable().Reverse().ToList(),
            TestNames.InterfaceMembers.AsEnumerable().Reverse().ToList(),
            TestNames.Types.AsEnumerable().Reverse().ToList(),
            TestNames.Namespaces.AsEnumerable().Reverse().ToList(),
            TestNames.RecordParams.AsEnumerable().Reverse().ToList());
        var map1 = UuidRenameMap.Derive("b7bc82e9-d402-5bf2-b819-08c221706f75", TestNames);
        var map2 = UuidRenameMap.Derive("b7bc82e9-d402-5bf2-b819-08c221706f75", reversed);

        foreach (var name in AllNames)
            Assert.AreEqual(map1.GetRenamed(name), map2.GetRenamed(name), name);
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
        var map = UuidRenameMap.Derive("875432b4-d790-54b1-acda-16a6c29271db", TestNames);

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
            "1ea95820-7f59-5892-a22d-70a79afbf2f3", TestNames);
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
            "31df6f45-a2f1-5d50-8849-5cfb9806b87a", TestNames);
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
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
        Assert.IsFalse(result.Contains("Workflow.Contracts"));
        Assert.IsFalse(result.Contains("IModule"));
    }

    [TestMethod]
    public void UsingDirective_IsRenamed()
    {
        var source = "using Workflow.Contracts;";
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
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
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
        Assert.IsFalse(result.Contains("IModule"));
        Assert.IsFalse(result.Contains("Name"));
        Assert.IsFalse(result.Contains("Execute"));
    }

    [TestMethod]
    public void NonContractType_IsNotRenamed()
    {
        var source = "public class MyCustomPlugin { public void DoStuff() { } }";
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
        Assert.IsTrue(result.Contains("MyCustomPlugin"));
        Assert.IsTrue(result.Contains("DoStuff"));
    }

    [TestMethod]
    public void WorkflowModels_NamespaceIsRenamed()
    {
        var source =
            "using Workflow.Models; namespace Workflow.Models { public class ServerJob { } }";
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
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
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
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
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
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
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
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
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
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
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
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
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
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
        var result = ApplyRenameTransform(source, "81fb0dc3-25c9-5cda-9d13-057789471931");
        Assert.IsFalse(result.Contains("TokenTaskResponse"),
            "Contract type name in member access should be renamed.");
    }

    [TestMethod]
    public void IsAlwaysRename_TypeNames_ReturnsTrue()
    {
        var map = UuidRenameMap.Derive("81fb0dc3-25c9-5cda-9d13-057789471931", TestNames);
        Assert.IsTrue(map.IsAlwaysRename("IModule"));
        Assert.IsTrue(map.IsAlwaysRename("ServerJob"));
        Assert.IsTrue(map.IsAlwaysRename("TokenTaskResponse"));
        Assert.IsTrue(map.IsAlwaysRename("Workflow.Contracts"));
        Assert.IsTrue(map.IsAlwaysRename("Workflow.Models"));
    }

    [TestMethod]
    public void IsAlwaysRename_MemberNames_ReturnsFalse()
    {
        var map = UuidRenameMap.Derive("81fb0dc3-25c9-5cda-9d13-057789471931", TestNames);
        Assert.IsFalse(map.IsAlwaysRename("Name"));
        Assert.IsFalse(map.IsAlwaysRename("Write"));
        Assert.IsFalse(map.IsAlwaysRename("Execute"));
        Assert.IsFalse(map.IsAlwaysRename("Config"));
        Assert.IsFalse(map.IsAlwaysRename("Logger"));
    }

    [TestMethod]
    public void ContractScanner_ScansAgentPluginContractsAndDtoMembers()
    {
        var contractsDir = FindAgentModelsDir();
        Assert.IsNotNull(contractsDir, "Agent.Models directory not found.");

        var names = ContractScanner.Scan(contractsDir);

        CollectionAssert.Contains(names.Interfaces, "IPlugin");
        CollectionAssert.Contains(names.Interfaces, "IInteractivePlugin");
        CollectionAssert.Contains(names.Interfaces, "IProxyPlugin");
        CollectionAssert.DoesNotContain(names.Interfaces, "ILogger");
        CollectionAssert.Contains(names.InterfaceMembers, "Execute");
        CollectionAssert.Contains(names.InterfaceMembers, "Name");
        CollectionAssert.Contains(names.InterfaceMembers, "Interact");
        CollectionAssert.Contains(names.Types, "ServerJob");
        CollectionAssert.Contains(names.Types, "ServerTask");
        CollectionAssert.Contains(names.Types, "InteractMessage");
        CollectionAssert.Contains(names.InterfaceMembers, "started");
        CollectionAssert.Contains(names.InterfaceMembers, "task");
        CollectionAssert.Contains(names.InterfaceMembers, "parameters");
        CollectionAssert.Contains(names.InterfaceMembers, "id");
        CollectionAssert.Contains(names.InterfaceMembers, "command");
        CollectionAssert.Contains(names.InterfaceMembers, "message_type");
        CollectionAssert.Contains(names.Namespaces, "Agent.Models");
        CollectionAssert.Contains(names.Namespaces, "Agent.Interfaces");
    }

    [TestMethod]
    public void ContractMethods_RenameOnlyExactImplementationsAndReferences()
    {
        var map = UuidRenameMap.Derive("7e12cc15-1fb9-56e7-83d7-9b814fb29d0e", TestNames);
        var renamed = map.GetRenamed("Execute");
        const string source = """
            namespace Workflow.Contracts
            {
                public interface IModule
                {
                    void Execute();
                }

                public class ImplicitModule : IModule
                {
                    public virtual void Execute() { }
                    public void Call()
                    {
                        Execute();
                        this.Execute();
                    }
                }

                public class ExplicitModule : IModule
                {
                    void IModule.Execute() { }
                    public void Call(IModule module) => module.Execute();
                }

                public class OverrideBase : IModule
                {
                    public virtual void Execute() { }
                }

                public class OverrideModule : OverrideBase
                {
                    public override void Execute() => base.Execute();
                }

                public class HiddenModule : OverrideBase
                {
                    private new void Execute() { }
                    private new void Execute<T>(T value) { }
                    public void Call()
                    {
                        Execute();
                        this.Execute();
                        this.Execute<int>(1);
                        ((IModule)this).Execute();
                    }
                }
            }
            """;

        var result = ApplyRenameTransform(source, "7e12cc15-1fb9-56e7-83d7-9b814fb29d0e");

        StringAssert.Contains(result, $"void {renamed}();");
        StringAssert.Contains(result, $"public virtual void {renamed}()");
        StringAssert.Contains(result, $"void {map.GetRenamed("IModule")}.{renamed}()");
        StringAssert.Contains(result, $"public override void {renamed}()");
        StringAssert.Contains(result, $"base.{renamed}()");
        StringAssert.Contains(result, "private new void Execute()");
        StringAssert.Contains(result, "Execute();\n");
        StringAssert.Contains(result, "this.Execute();");
        StringAssert.Contains(result, "private new void Execute<T>");
        StringAssert.Contains(result, "this.Execute<int>(1);");
        StringAssert.Contains(result,
            $"(({map.GetRenamed("IModule")})this).{renamed}()");
        AssertCompiles(result);
    }

    [TestMethod]
    public void ContractProperties_RenameOnlyExactImplementationsAndReferences()
    {
        var map = UuidRenameMap.Derive("0f08677d-fa5d-5923-8f0c-fe52c6dbb601", TestNames);
        var renamed = map.GetRenamed("Name");
        var renamedInterface = map.GetRenamed("IModule");
        const string source = """
            namespace Workflow.Contracts
            {
                public interface IModule
                {
                    string Name { get; }
                }

                public class ImplicitModule : IModule
                {
                    public virtual string Name => "implicit";
                    public string Read() => Name + this.Name;
                }

                public class ExplicitModule : IModule
                {
                    string IModule.Name => "explicit";
                    public string Read(IModule module) => module.Name;
                }

                public class OverrideBase : IModule
                {
                    public virtual string Name => "base";
                }

                public class OverrideModule : OverrideBase
                {
                    public override string Name => base.Name;
                }

                public class HiddenModule : OverrideBase
                {
                    private new string Name => "hidden";
                    public string Read()
                        => Name + this.Name + ((IModule)this).Name;
                }
            }
            """;

        var result = ApplyRenameTransform(source, "0f08677d-fa5d-5923-8f0c-fe52c6dbb601");

        StringAssert.Contains(result, $"string {renamed}");
        StringAssert.Contains(result, $"public virtual string {renamed}");
        StringAssert.Contains(result, $"string {renamedInterface}.{renamed}");
        StringAssert.Contains(result, $"module.{renamed}");
        StringAssert.Contains(result, $"public override string {renamed}");
        StringAssert.Contains(result, $"base.{renamed}");
        StringAssert.Contains(result, "private new string Name");
        StringAssert.Contains(result, "=> Name + this.Name");
        StringAssert.Contains(result,
            $"(({renamedInterface})this).{renamed}");
        AssertCompiles(result);
    }

    [TestMethod]
    public void ContractPropertyNameCollision_DoesNotRenameBoundLocal()
    {
        var names = TestNames with
        {
            Interfaces = [.. TestNames.Interfaces, "IPlugin"],
        };
        var map = UuidRenameMap.Derive(
            "31aa1945-39ff-5a7a-8f75-18559232a7f5", names);
        const string source = """
            namespace Workflow.Contracts
            {
                public interface IPlugin
                {
                    int Name { get; }
                }

                public class Plugin : IPlugin
                {
                    public int Name
                    {
                        get
                        {
                            int Name = 1;
                            return Name;
                        }
                    }

                    public static int Run() => new Plugin().Name;
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var result = new UuidRenameTransform(map)
            .Rewrite(tree).GetRoot().ToFullString();

        StringAssert.Contains(result, $"int {map.GetRenamed("Name")}");
        StringAssert.Contains(result, "int Name = 1;");
        StringAssert.Contains(result, "return Name;");
        Assert.AreEqual(1, CompileAndInvokeStatic(result, "Plugin", "Run"));
    }

    [TestMethod]
    public void ContractMethodParameter_RenamesDeclarationAndReferenceConsistently()
    {
        var names = TestNames with
        {
            Interfaces = [.. TestNames.Interfaces, "IPlugin"],
        };
        var map = UuidRenameMap.Derive(
            "856c1bbc-4d5b-5747-9ee1-4ca05a788c52", names);
        var renamedName = map.GetRenamed("Name");
        const string source = """
            namespace Workflow.Contracts
            {
                public interface IPlugin
                {
                    int Apply(int Name);
                }

                public class Plugin : IPlugin
                {
                    public int Apply(int Name) => Name;
                    public static int Run() => new Plugin().Apply(7);
                }
            }
            """;

        var result = new UuidRenameTransform(map)
            .Rewrite(CSharpSyntaxTree.ParseText(source))
            .GetRoot().ToFullString();

        StringAssert.Contains(result, $"int Apply(int {renamedName}) => {renamedName};");
        Assert.AreEqual(7, CompileAndInvokeStatic(result, "Plugin", "Run"));
    }

    [TestMethod]
    public void UnrelatedMappedParameterInContractImplementation_RemainsConsistent()
    {
        var names = TestNames with
        {
            Interfaces = [.. TestNames.Interfaces, "IPlugin"],
        };
        var map = UuidRenameMap.Derive(
            "4263f79e-661d-54d1-bf99-6210c4ab46dd", names);
        const string source = """
            namespace Workflow.Contracts
            {
                public interface IPlugin
                {
                    int Name { get; }
                }

                public class Plugin : IPlugin
                {
                    public int Name => 3;
                    public int Echo(int Name) => Name;
                    public static int Run()
                    {
                        var plugin = new Plugin();
                        return plugin.Name + plugin.Echo(4);
                    }
                }
            }
            """;

        var result = new UuidRenameTransform(map)
            .Rewrite(CSharpSyntaxTree.ParseText(source))
            .GetRoot().ToFullString();

        StringAssert.Contains(result, $"public int {map.GetRenamed("Name")}");
        StringAssert.Contains(result, "int Echo(int Name) => Name;");
        Assert.AreEqual(7, CompileAndInvokeStatic(result, "Plugin", "Run"));
    }

    [TestMethod]
    public void ConstructorParameterAttribute_WithMappedName_PreservesExternalSymbol()
    {
        var names = new ContractNames(
            Interfaces: [],
            InterfaceMembers: ["value"],
            Types: ["ServerJob"],
            Namespaces: ["Agent.Models"],
            RecordParams: []);
        var map = UuidRenameMap.Derive(
            "bc630856-340c-574c-8530-4f45fc6d38a3", names);
        var renamedType = map.GetRenamed("ServerJob");
        const string source = """
            using System.Linq;
            using static External;

            public static class External
            {
                public const int value = 40;
            }

            namespace Agent.Models
            {
                sealed class MarkerAttribute : System.Attribute
                {
                    public MarkerAttribute(int marker) => Value = marker;
                    public int Value { get; }
                }

                public sealed class ServerJob
                {
                    public ServerJob([Marker(value)] int value) => Result = value + 2;
                    public int Result { get; }

                    public static int Run()
                    {
                        var parameter = typeof(ServerJob).GetConstructors().Single()
                            .GetParameters().Single();
                        var marker = (MarkerAttribute)parameter.GetCustomAttributes(false).Single();
                        return marker.Value + new ServerJob(3).Result;
                    }
                }
            }
            """;

        var result = new UuidRenameTransform(map)
            .Rewrite(CSharpSyntaxTree.ParseText(source))
            .GetRoot().ToFullString();

        StringAssert.Contains(result, "[Marker(value)]");
        Assert.AreEqual(45, CompileAndInvokeStatic(result, renamedType, "Run"));
    }

    private static object? CompileAndInvokeStatic(
        string source, string typeName, string methodName)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        using var stream = new MemoryStream();
        var result = CSharpCompilation.Create(
                $"UuidRenameExecutableFixture_{Guid.NewGuid():N}", [tree], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .Emit(stream);
        Assert.IsTrue(result.Success,
            string.Join(Environment.NewLine, result.Diagnostics));

        stream.Position = 0;
        var loadContext = new System.Runtime.Loader.AssemblyLoadContext(
            name: null, isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromStream(stream);
            var type = assembly.GetTypes().Single(candidate => candidate.Name == typeName);
            return type.GetMethod(methodName)!.Invoke(null, null);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static void AssertCompiles(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        using var stream = new MemoryStream();
        var result = CSharpCompilation.Create(
                "UuidRenameIdentityFixture", [tree], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .Emit(stream);
        Assert.IsTrue(result.Success,
            string.Join(Environment.NewLine, result.Diagnostics));
    }

    private static string? FindAgentModelsDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "Agent.Models");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
