using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Config;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class UuidRenameTransformTests
{
    private static readonly ContractNames TestNames = new(
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

    private static UuidRenameMap CreateMap() =>
        UuidRenameMap.Derive("6195450e-0b30-5f78-9d0d-57de44aff7c7", TestNames);

    private static string Rewrite(string source, UuidRenameMap? map = null)
    {
        map ??= CreateMap();
        var tree = CSharpSyntaxTree.ParseText(source);
        var transform = new UuidRenameTransform(map);
        tree = transform.Rewrite(tree);
        return tree.GetRoot().ToFullString();
    }

    [TestMethod]
    public void ContractDtoPropertiesAndFields_RenamedWithOriginalJsonNames()
    {
        var names = new ContractNames(
            ["IPlugin"], ["Execute", "Name", "started", "wire_field"],
            ["ServerJob"], ["Agent.Interfaces", "Agent.Models"], []);
        var map = UuidRenameMap.Derive("3949d6e4-2ec7-5d58-ae22-6fa24204d01e", names);
        const string source = """
            namespace Agent.Models
            {
                public class ServerJob
                {
                    public bool started { get; set; }
                    public string wire_field;
                }
            }
            """;

        var result = Rewrite(source, map);

        Assert.IsFalse(result.Contains("bool started"));
        Assert.IsFalse(result.Contains("string wire_field"));
        StringAssert.Contains(result,
            "JsonPropertyName(\"started\")");
        StringAssert.Contains(result,
            "JsonPropertyName(\"wire_field\")");
    }

    [TestMethod]
    public void ContractDtoMultiVariableFields_RenameDeclarationsAndReferencesConsistently()
    {
        var names = new ContractNames(
            Interfaces: [],
            InterfaceMembers: ["first", "second"],
            Types: ["WireDto"],
            Namespaces: ["Agent.Models"],
            RecordParams: []);
        var map = UuidRenameMap.Derive("bd6a587b-fde5-5aa1-a86d-e1c2ddbdd09c", names);
        var renamedFirst = map.GetRenamed("first");
        var renamedSecond = map.GetRenamed("second");
        const string source = """
            namespace Agent.Models;
            public class WireDto
            {
                private static int Next() => 1;
                public readonly int first = Next(), second = Next();

                public int Sum(WireDto dto)
                {
                    return this.first + dto.first + first
                        + this.second + dto.second + second;
                }
            }
            """;

        var result = Rewrite(source, map);
        var root = CSharpSyntaxTree.ParseText(result).GetRoot();
        var fields = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>()
            .Where(field => field.Declaration.Variables.Any(variable =>
                variable.Identifier.ValueText == renamedFirst
                || variable.Identifier.ValueText == renamedSecond))
            .ToArray();

        Assert.HasCount(2, fields);
        Assert.IsTrue(fields.All(field => field.Declaration.Variables.Count == 1));
        StringAssert.Contains(result, $"this.{renamedFirst}");
        StringAssert.Contains(result, $"dto.{renamedFirst}");
        StringAssert.Contains(result, $"+ {renamedFirst}");
        StringAssert.Contains(result, $"this.{renamedSecond}");
        StringAssert.Contains(result, $"dto.{renamedSecond}");
        StringAssert.Contains(result, $"+ {renamedSecond}");
        StringAssert.Contains(result, "JsonPropertyName(\"first\")");
        StringAssert.Contains(result, "JsonPropertyName(\"second\")");
        Assert.IsTrue(result.IndexOf($"{renamedFirst} = Next()", StringComparison.Ordinal)
            < result.IndexOf($"{renamedSecond} = Next()", StringComparison.Ordinal));

        var rewrittenTree = CSharpSyntaxTree.ParseText(result);
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        var diagnostics = CSharpCompilation.Create(
                "MultiFieldRewrite", [rewrittenTree], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.HasCount(0, diagnostics,
            string.Join(Environment.NewLine, diagnostics.Select(x => x.ToString())));
    }

    [TestMethod]
    public void ContractStructAndRecordStructMultiFields_RenameAndCompile()
    {
        var names = new ContractNames(
            Interfaces: [],
            InterfaceMembers: ["first", "second"],
            Types: ["WireDto", "WireRecord"],
            Namespaces: ["Agent.Models"],
            RecordParams: []);
        var map = UuidRenameMap.Derive("246090a2-1a59-57ba-b539-23b32dd09199", names);
        var renamedFirst = map.GetRenamed("first");
        var renamedSecond = map.GetRenamed("second");
        const string source = """
            namespace Agent.Models;
            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            public struct WireDto
            {
                public int first, second;
                public int Sum() => first + second;
            }
            public record struct WireRecord
            {
                public int first, second;
                public int Sum() => this.first + this.second;
            }
            """;

        var result = Rewrite(source, map);
        var tree = CSharpSyntaxTree.ParseText(result);
        var fields = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>()
            .Where(field => field.Declaration.Variables.Any(variable =>
                variable.Identifier.ValueText == renamedFirst
                || variable.Identifier.ValueText == renamedSecond))
            .ToArray();

        Assert.HasCount(4, fields);
        Assert.IsTrue(fields.All(field => field.Declaration.Variables.Count == 1));
        Assert.IsFalse(result.Contains("int first, second"));
        StringAssert.Contains(result, $"{renamedFirst} + {renamedSecond}");
        StringAssert.Contains(result, $"this.{renamedFirst} + this.{renamedSecond}");
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        var errors = CSharpCompilation.Create(
                "StructMultiField", [tree], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.HasCount(0, errors,
            string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
    }

    [TestMethod]
    public void ContractDtoConstructor_RenamesParameterReferences()
    {
        var names = new ContractNames(
            Interfaces: [],
            InterfaceMembers: ["server_id", "bdata", "exit"],
            Types: ["ServerDatagram"],
            Namespaces: ["Agent.Models"],
            RecordParams: []);
        var map = UuidRenameMap.Derive("be859266-b2e8-51a4-ba19-f0cafcec28c0", names);
        var renamedServerId = map.GetRenamed("server_id");
        var renamedBdata = map.GetRenamed("bdata");
        var renamedExit = map.GetRenamed("exit");
        const string source = """
            namespace Agent.Models
            {
                public class ServerDatagram
                {
                    public int server_id { get; set; }
                    public byte[] bdata { get; set; }
                    public bool exit { get; set; }

                    public ServerDatagram(
                        int server_id, byte[] bdata, bool exit)
                    {
                        this.exit = exit;
                        this.server_id = server_id;
                        this.bdata = bdata;
                        if (bdata is not null) { }
                    }
                }
            }
            """;

        var result = Rewrite(source, map);

        StringAssert.Contains(result,
            $"int {renamedServerId}, byte[] {renamedBdata}, bool {renamedExit}");
        StringAssert.Contains(result, $"this.{renamedExit} = {renamedExit};");
        StringAssert.Contains(result,
            $"this.{renamedServerId} = {renamedServerId};");
        StringAssert.Contains(result, $"this.{renamedBdata} = {renamedBdata};");
        StringAssert.Contains(result, $"if ({renamedBdata} is not null)");
    }

    [TestMethod]
    public void ContractClassPrimaryConstructor_RenamesParameterAndBoundReferences()
    {
        var names = new ContractNames(
            Interfaces: [],
            InterfaceMembers: ["Name"],
            Types: ["ServerJob"],
            Namespaces: ["Agent.Models"],
            RecordParams: []);
        var map = UuidRenameMap.Derive(
            "04d65877-492c-5474-9b28-67e9d26d5eb4", names);
        var renamedParameter = map.GetRenamed("Name");
        const string source = """
            namespace Agent.Models;
            public class ServerJob(int Name)
            {
                public int Value => Name;
            }
            public class Unrelated
            {
                public int Run(int Name) => Name + 1;
            }
            public static class Subject
            {
                public static int Run() =>
                    new ServerJob(6).Value + new Unrelated().Run(10);
            }
            """;

        var result = Rewrite(source, map);

        StringAssert.Contains(result, $"class {map.GetRenamed("ServerJob")}(int {renamedParameter})");
        StringAssert.Contains(result, $"Value => {renamedParameter}");
        StringAssert.Contains(result, "int Run(int Name) => Name + 1");

        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        using var stream = new MemoryStream();
        var emit = CSharpCompilation.Create(
                "ClassPrimaryConstructorFixture", [CSharpSyntaxTree.ParseText(result)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .Emit(stream);
        Assert.IsTrue(emit.Success,
            string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        Assert.AreEqual(17, assembly.GetType($"{map.GetRenamed("Agent.Models")}.Subject")!
            .GetMethod("Run")!.Invoke(null, null));
    }

    [TestMethod]
    public void ContractStructPrimaryConstructor_RenamesParameterAndBoundReferences()
    {
        var names = new ContractNames(
            Interfaces: [],
            InterfaceMembers: ["Name"],
            Types: ["ServerDatagram"],
            Namespaces: ["Agent.Models"],
            RecordParams: []);
        var map = UuidRenameMap.Derive(
            "5f94cd17-5965-55b7-9482-b47110f29937", names);
        var renamedParameter = map.GetRenamed("Name");
        const string source = """
            namespace Agent.Models;
            public struct ServerDatagram(int Name)
            {
                public int Value => Name;
            }
            public struct Unrelated
            {
                public int Run(int Name) => Name + 2;
            }
            public static class Subject
            {
                public static int Run() =>
                    new ServerDatagram(5).Value + new Unrelated().Run(10);
            }
            """;

        var result = Rewrite(source, map);

        StringAssert.Contains(result, $"struct {map.GetRenamed("ServerDatagram")}(int {renamedParameter})");
        StringAssert.Contains(result, $"Value => {renamedParameter}");
        StringAssert.Contains(result, "int Run(int Name) => Name + 2");

        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        using var stream = new MemoryStream();
        var emit = CSharpCompilation.Create(
                "StructPrimaryConstructorFixture", [CSharpSyntaxTree.ParseText(result)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .Emit(stream);
        Assert.IsTrue(emit.Success,
            string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        Assert.AreEqual(17, assembly.GetType($"{map.GetRenamed("Agent.Models")}.Subject")!
            .GetMethod("Run")!.Invoke(null, null));
    }

    [TestMethod]
    public void StructField_NamedLikeInterfaceMember_NotRenamed()
    {
        const string source = """
            using System.Runtime.InteropServices;
            public struct IMAGE_SECTION_HEADER
            {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
                public char[] Name;
                public string Section => new(Name);
            }
            """;

        var result = Rewrite(source);

        Assert.IsTrue(
            result.Contains("char[] Name"),
            "Struct field 'Name' should not be renamed");
        Assert.IsTrue(
            result.Contains("new(Name)"),
            "Reference to struct field 'Name' should not be renamed");
    }

    [TestMethod]
    public void EventFieldDeclaration_InContractType_Renamed()
    {
        var map = CreateMap();
        var renamedEvent = map.GetRenamed("SetTaskingReceived");
        var renamedInterface = map.GetRenamed("IChannel");
        var renamedArgs = map.GetRenamed("TaskingReceivedArgs");

        var source = $$"""
            using System;
            namespace Workflow.Contracts
            {
                public class TaskingReceivedArgs : EventArgs { }
                public interface IChannel
                {
                    public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
                }
            }
            namespace Workflow.Channels
            {
                using Workflow.Contracts;
                public class HttpProfile : IChannel
                {
                    public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
                    public void Start()
                    {
                        this.SetTaskingReceived(null, new TaskingReceivedArgs());
                    }
                }
            }
            """;

        var result = Rewrite(source, map);

        Assert.IsFalse(
            result.Contains("SetTaskingReceived"),
            "Event 'SetTaskingReceived' should be renamed everywhere");
        Assert.IsTrue(
            result.Contains(renamedEvent),
            $"Event should be renamed to '{renamedEvent}'");
    }

    [TestMethod]
    public void CustomEvents_RenameDeclarationsAndAccessesButLeaveUnrelatedEvent()
    {
        var names = new ContractNames(
            Interfaces: ["IPlugin"],
            InterfaceMembers: ["Changed"],
            Types: ["WireDto"],
            Namespaces: ["Agent.Interfaces", "Agent.Models"],
            RecordParams: []);
        var map = UuidRenameMap.Derive("b5bb4cbd-8606-50a4-90e5-072d0c1416a0", names);
        var renamed = map.GetRenamed("Changed");
        const string source = """
            using System;
            namespace Agent.Interfaces
            {
                public interface IPlugin
                {
                    event EventHandler Changed { add { } remove { } }
                }
                public class Runner : IPlugin
                {
                    private EventHandler? handlers;
                    event EventHandler IPlugin.Changed
                    {
                        add { handlers += value; }
                        remove { handlers -= value; }
                    }
                    public void Fire() => handlers?.Invoke(this, EventArgs.Empty);
                }
            }
            namespace Agent.Models
            {
                public class WireDto
                {
                    private EventHandler? handlers;
                    public event EventHandler Changed
                    {
                        add { handlers += value; }
                        remove { handlers -= value; }
                    }
                }
            }
            public class Unrelated
            {
                public event EventHandler Changed { add { } remove { } }
            }
            public static class Subject
            {
                public static int Run()
                {
                    Agent.Interfaces.IPlugin plugin = new Agent.Interfaces.Runner();
                    var count = 0;
                    EventHandler handler = (_, _) => count++;
                    plugin.Changed += handler;
                    ((Agent.Interfaces.Runner)plugin).Fire();
                    plugin.Changed -= handler;
                    return count;
                }
            }
            """;

        var result = Rewrite(source, map);

        StringAssert.Contains(result,
            $"{map.GetRenamed("IPlugin")}.{renamed}");
        StringAssert.Contains(result, $"plugin.{renamed} += handler");
        StringAssert.Contains(result, "event EventHandler Changed { add { } remove { } }");
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        using var stream = new MemoryStream();
        var emit = CSharpCompilation.Create(
                "CustomEventFixture", [CSharpSyntaxTree.ParseText(result)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .Emit(stream);
        Assert.IsTrue(emit.Success,
            string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        Assert.AreEqual(1, assembly.GetType("Subject")!
            .GetMethod("Run")!.Invoke(null, null));
    }

    [TestMethod]
    public void OutVariable_ContractTyped_MemberAccessRenamed()
    {
        var map = CreateMap();
        var renamedExecute = map.GetRenamed("Execute");
        var renamedInterface = map.GetRenamed("IModule");
        var renamedTryGet = map.GetRenamed("TryGetModule");

        var source = $$"""
            namespace Workflow.Contracts
            {
                public interface IModule
                {
                    void Execute();
                }
                public interface IComponentProvider
                {
                    bool TryGetModule(string name, out IModule mod);
                }
            }
            namespace Test
            {
                using Workflow.Contracts;
                public class Runner
                {
                    private IComponentProvider provider;
                    public void Run()
                    {
                        if (this.provider.TryGetModule("test", out IModule plug))
                        {
                            plug.Execute();
                        }
                    }
                }
            }
            """;

        var result = Rewrite(source, map);

        Assert.IsFalse(
            result.Contains(".Execute("),
            "Execute should be renamed at call site");
        Assert.IsTrue(
            result.Contains($".{renamedExecute}("),
            $"Execute should become '{renamedExecute}' at call site");
    }

    [TestMethod]
    public void GenericMethodCall_OnContractType_Renamed()
    {
        var map = CreateMap();
        var renamedTryGet = map.GetRenamed("TryGetModule");

        var source = $$"""
            namespace Workflow.Contracts
            {
                public interface IModule { }
                public interface IFileModule : IModule { }
                public interface IComponentProvider
                {
                    bool TryGetModule<T>(string name, out T mod) where T : IModule;
                }
            }
            namespace Test
            {
                using Workflow.Contracts;
                public class Handler
                {
                    private IComponentProvider mgr;
                    public void Handle()
                    {
                        this.mgr.TryGetModule<IFileModule>("dl", out var plugin);
                    }
                }
            }
            """;

        var result = Rewrite(source, map);

        Assert.IsFalse(
            result.Contains(".TryGetModule<"),
            "Generic method TryGetModule<T> should be renamed");
        Assert.IsTrue(
            result.Contains($".{renamedTryGet}<"),
            $"Generic method should become '{renamedTryGet}<'");
    }

    [TestMethod]
    public void OutVar_FromGenericCall_MemberAccessRenamed()
    {
        var map = CreateMap();
        var renamedHandleMsg = map.GetRenamed("HandleNextMessage");

        var source = $$"""
            namespace Workflow.Contracts
            {
                public interface IModule { }
                public interface IFileModule : IModule
                {
                    void HandleNextMessage(object msg);
                }
                public interface IComponentProvider
                {
                    bool TryGetModule<T>(string n, out T m)
                        where T : IModule;
                }
            }
            namespace Test
            {
                using Workflow.Contracts;
                public class Runner
                {
                    private IComponentProvider mgr;
                    public void Run()
                    {
                        if (this.mgr.TryGetModule<IFileModule>(
                            "x", out var plugin))
                        {
                            plugin.HandleNextMessage(null);
                        }
                    }
                }
            }
            """;

        var result = Rewrite(source, map);

        Assert.IsFalse(
            result.Contains(".HandleNextMessage("),
            "HandleNextMessage should be renamed on out var "
            + "from generic call");
        Assert.IsTrue(
            result.Contains($".{renamedHandleMsg}("),
            $"Should become '{renamedHandleMsg}' at call site");
    }

    [TestMethod]
    public void NonContractClass_MethodNamedExecute_NotRenamed()
    {
        var source = """
            public class MyHelper
            {
                public void Execute() { }
                public void Run()
                {
                    this.Execute();
                }
            }
            """;

        var result = Rewrite(source);

        Assert.IsTrue(
            result.Contains("void Execute()"),
            "Execute on non-contract class should not be renamed");
        Assert.IsTrue(
            result.Contains("this.Execute()"),
            "Call to Execute on non-contract this should not be renamed");
    }

    /// <summary>
    /// When a sub-namespace segment (e.g. "Commands" in
    /// "Workflow.Models.Commands") shares its name with a contract type,
    /// the namespace declaration must NOT rename the sub-segment — only the
    /// known namespace prefix is renamed.  Without the fix, both the type
    /// and the sub-segment become "_xx", producing CS0118.
    /// </summary>
    [TestMethod]
    public void NamespaceDeclaration_SubSegmentMatchesContractType_SubSegmentNotRenamed()
    {
        // Custom map: "Commands" is a contract type; "Workflow.Models" is a
        // known namespace.  UuidRenameMap will give them distinct obfuscated
        // names because they are different keys.
        var names = new ContractNames(
            Interfaces: [],
            InterfaceMembers: [],
            Types: ["Commands"],
            Namespaces: ["Workflow.Models"],
            RecordParams: []);

        var map = UuidRenameMap.Derive("efeeeab9-8cd3-55d3-a797-543f4a765011", names);
        var renamedType = map.GetRenamed("Commands");
        var renamedNsPrefix = map.GetRenamed("Workflow.Models");

        var source = """
            namespace Workflow.Models.Commands
            {
                public class Commands { }
            }
            """;

        var result = Rewrite(source, map);

        // The class type must be renamed.
        Assert.IsTrue(
            result.Contains($"class {renamedType}"),
            $"Contract type 'Commands' should be renamed to '{renamedType}'");

        // The namespace must keep the sub-segment as the original text
        // "Commands", not the type's obfuscated name.
        Assert.IsTrue(
            result.Contains($"namespace {renamedNsPrefix}.Commands"),
            $"Namespace sub-segment must stay as 'Commands', not be renamed; "
            + $"expected 'namespace {renamedNsPrefix}.Commands'");

        // Ensure the type's obfuscated name is NOT used as a namespace segment.
        Assert.IsFalse(
            result.Contains($"namespace {renamedNsPrefix}.{renamedType}"),
            "Namespace sub-segment must NOT be renamed to the type's obfuscated value");
    }

    [TestMethod]
    public void ContractTypeCollision_RenamesOnlySemanticContractSymbol()
    {
        var names = new ContractNames(
            Interfaces: [],
            InterfaceMembers: [],
            Types: ["ServerJob"],
            Namespaces: ["Agent.Models"],
            RecordParams: []);
        var map = UuidRenameMap.Derive("b489972f-568c-514c-96a5-60f870d3f6fe", names);
        var renamedType = map.GetRenamed("ServerJob");
        var renamedNamespace = map.GetRenamed("Agent.Models");
        const string source = """
            using ContractJob = global::Agent.Models.ServerJob;

            namespace Agent.Models
            {
                public class ServerJob { }
            }

            namespace Consumer
            {
                public class Holder
                {
                    private record ServerJob(int Value);

                    private ServerJob local = new(1);
                    private ContractJob aliased = new();
                    private global::Agent.Models.ServerJob qualified = new();
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "Agent.Models",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var semanticModel = compilation.GetSemanticModel(tree);

        var result = new UuidRenameTransform(map)
            .Rewrite(tree, semanticModel)
            .GetRoot()
            .ToFullString();

        StringAssert.Contains(result, $"class {renamedType}");
        StringAssert.Contains(result,
            $"{renamedNamespace}.{renamedType}");
        StringAssert.Contains(result, "record ServerJob(int Value)");
        StringAssert.Contains(result, "private ServerJob local = new(1)");
        Assert.IsFalse(result.Contains($"record {renamedType}"));
    }

    [TestMethod]
    public void EnumMembers_RenameDeclarationsAndReferencesConsistently()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uuid_enum_{Guid.NewGuid():N}");
        var contracts = Path.Combine(root, "Agent.Models");
        Directory.CreateDirectory(contracts);
        var contractPath = Path.Combine(contracts, "Contracts.cs");
        var consumerPath = Path.Combine(root, "Consumer.cs");
        const string contractSource = """
            namespace Agent.Interfaces
            {
                public interface IPlugin
                {
                    Agent.Models.DatagramSource Source { get; }
                }
            }
            namespace Agent.Models
            {
                public enum DatagramSource
                {
                    Socks5 = 7
                }
            }
            """;
        const string consumerSource = """
            using Agent.Models;

            namespace Consumer
            {
                public enum UnrelatedSource
                {
                    Socks5 = 11
                }

                public static class Subject
                {
                    public static int Run() =>
                        (int)DatagramSource.Socks5
                        + (int)UnrelatedSource.Socks5;
                }
            }
            """;

        File.WriteAllText(contractPath, contractSource);
        File.WriteAllText(consumerPath, consumerSource);
        try
        {
            var map = UuidRenameMap.Derive(
                "aa17590d-13a6-5b36-ad5e-60fb4b999a35",
                ContractScanner.Scan(contracts));
            var renamedMember = map.GetRenamed("Socks5");
            var trees = new[]
            {
                CSharpSyntaxTree.ParseText(contractSource, path: contractPath),
                CSharpSyntaxTree.ParseText(consumerSource, path: consumerPath),
            };
            var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(
                "EnumMemberFixture", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var rewrittenTrees = trees.Select(tree => new UuidRenameTransform(map)
                    .Rewrite(tree, compilation.GetSemanticModel(tree, ignoreAccessibility: true)))
                .ToArray();
            var rewrittenContract = rewrittenTrees[0].GetRoot().ToFullString();
            var rewrittenConsumer = rewrittenTrees[1].GetRoot().ToFullString();

            using var stream = new MemoryStream();
            var emit = CSharpCompilation.Create(
                    "EnumMemberFixture", rewrittenTrees, references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .Emit(stream);
            Assert.IsTrue(emit.Success,
                string.Join(Environment.NewLine, emit.Diagnostics));

            StringAssert.Contains(rewrittenContract, $"{renamedMember} = 7");
            StringAssert.Contains(rewrittenConsumer, $".{renamedMember}");
            StringAssert.Contains(rewrittenConsumer, "UnrelatedSource.Socks5");

            var assembly = System.Reflection.Assembly.Load(stream.ToArray());
            Assert.AreEqual(18, assembly.GetType("Consumer.Subject")!
                .GetMethod("Run")!.Invoke(null, null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ScannerProvenance_RenamesShiftedPartialDeclarationsButNotSameFqnDuplicate()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uuid_provenance_{Guid.NewGuid():N}");
        var contracts = Path.Combine(root, "Agent.Models");
        var unrelated = Path.Combine(root, "OtherProject");
        Directory.CreateDirectory(contracts);
        Directory.CreateDirectory(unrelated);
        var firstPath = Path.Combine(contracts, "ServerJob.Part1.cs");
        var secondPath = Path.Combine(contracts, "ServerJob.Part2.cs");
        var duplicatePath = Path.Combine(unrelated, "ServerJob.cs");
        try
        {
            File.WriteAllText(Path.Combine(contracts, "IPlugin.cs"),
                "namespace Agent.Interfaces; public interface IPlugin { Agent.Models.ServerJob Execute(); }");
            File.WriteAllText(firstPath,
                "namespace Agent.Models; public partial class ServerJob { public int Value; }");
            File.WriteAllText(secondPath,
                "namespace Agent.Models; public partial class ServerJob { public int Other; }");
            File.WriteAllText(duplicatePath,
                "namespace Agent.Models; public class ServerJob { public int Value; }");

            var map = UuidRenameMap.Derive(
                "dd4dc422-50dd-59c0-936c-1a851d041c30", ContractScanner.Scan(contracts));
            var renamed = map.GetRenamed("ServerJob");
            var trees = new[]
            {
                CSharpSyntaxTree.ParseText("// shifted\n" + File.ReadAllText(firstPath), path: firstPath),
                CSharpSyntaxTree.ParseText("// shifted again\n" + File.ReadAllText(secondPath), path: secondPath),
                CSharpSyntaxTree.ParseText(File.ReadAllText(duplicatePath), path: duplicatePath),
            };
            var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(
                "ProvenanceFixture", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var rewritten = trees.Select(tree => new UuidRenameTransform(map)
                    .Rewrite(tree, compilation.GetSemanticModel(tree, ignoreAccessibility: true))
                    .GetRoot().ToFullString())
                .ToArray();

            StringAssert.Contains(rewritten[0], $"partial class {renamed}");
            StringAssert.Contains(rewritten[1], $"partial class {renamed}");
            StringAssert.Contains(rewritten[2], "class ServerJob");
            Assert.IsFalse(rewritten[2].Contains($"class {renamed}"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ScannerProvenance_RenamesOnlyNamespaceDeclarationContainingCanonicalType()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uuid_namespace_provenance_{Guid.NewGuid():N}");
        var contracts = Path.Combine(root, "Agent.Models");
        var unrelated = Path.Combine(root, "OtherProject");
        Directory.CreateDirectory(contracts);
        Directory.CreateDirectory(unrelated);
        var contractPath = Path.Combine(contracts, "ServerJob.cs");
        var interfacePath = Path.Combine(contracts, "IPlugin.cs");
        var unrelatedPath = Path.Combine(unrelated, "Unrelated.cs");
        var consumerPath = Path.Combine(root, "Consumer.cs");
        const string contractSource = """
            namespace Agent.Models;
            public class ServerJob(int value)
            {
                public int Value => value;
                public static ServerJob Identity(ServerJob job) => job;
            }
            public class Companion(int value)
            {
                public int Value => value;
            }
            """;
        const string unrelatedSource = """
            namespace Agent.Models
            {
                public static class Unrelated
                {
                    public static ServerJob Create() =>
                        ServerJob.Identity(new ServerJob(11));
                    public static Companion CreateCompanion() => new Companion(13);
                }
            }
            """;
        const string consumerSource = """
            public static class Subject
            {
                public static int Run() =>
                    new Agent.Models.ServerJob(7).Value
                    + Agent.Models.Unrelated.Create().Value
                    + Agent.Models.Unrelated.CreateCompanion().Value;
            }
            """;

        File.WriteAllText(contractPath, contractSource);
        File.WriteAllText(interfacePath,
            "namespace Agent.Interfaces; public interface IPlugin { Agent.Models.ServerJob Execute(); }");
        File.WriteAllText(unrelatedPath, unrelatedSource);
        File.WriteAllText(consumerPath, consumerSource);
        try
        {
            var map = UuidRenameMap.Derive(
                "9bdd18a2-5bc7-56b0-a1a2-70d054660fb0",
                ContractScanner.Scan(contracts));
            var renamedNamespace = map.GetRenamed("Agent.Models");
            var trees = new[]
            {
                CSharpSyntaxTree.ParseText(contractSource, path: contractPath),
                CSharpSyntaxTree.ParseText(unrelatedSource, path: unrelatedPath),
                CSharpSyntaxTree.ParseText(consumerSource, path: consumerPath),
            };
            var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(
                "NamespaceProvenanceFixture", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var rewrittenTrees = trees.Select(tree => new UuidRenameTransform(map)
                    .Rewrite(tree, compilation.GetSemanticModel(tree, ignoreAccessibility: true)))
                .ToArray();
            var rewrittenContract = rewrittenTrees[0].GetRoot().ToFullString();
            var rewrittenUnrelated = rewrittenTrees[1].GetRoot().ToFullString();

            StringAssert.Contains(rewrittenContract, $"namespace {renamedNamespace};");
            StringAssert.Contains(rewrittenUnrelated, "namespace Agent.Models");
            StringAssert.Contains(rewrittenUnrelated, "static class Unrelated");
            StringAssert.Contains(rewrittenUnrelated,
                $"global::{renamedNamespace}.{map.GetRenamed("ServerJob")}");
            StringAssert.Contains(rewrittenUnrelated,
                $"global::{renamedNamespace}.Companion");
            Assert.IsFalse(map.GetAllMappings().ContainsKey("Companion"));
            Assert.IsFalse(rewrittenUnrelated.Contains($"namespace {renamedNamespace}"));

            using var stream = new MemoryStream();
            var emit = CSharpCompilation.Create(
                    "NamespaceProvenanceFixture", rewrittenTrees, references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .Emit(stream);
            Assert.IsTrue(emit.Success,
                string.Join(Environment.NewLine, emit.Diagnostics));
            var assembly = System.Reflection.Assembly.Load(stream.ToArray());
            Assert.AreEqual(31, assembly.GetType("Subject")!
                .GetMethod("Run")!.Invoke(null, null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ScannerProvenance_NestedCanonicalNamespaceMovesToMappedDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uuid_nested_namespace_{Guid.NewGuid():N}");
        var contracts = Path.Combine(root, "Agent.Models");
        Directory.CreateDirectory(contracts);
        var contractPath = Path.Combine(contracts, "Contracts.cs");
        var consumerPath = Path.Combine(root, "Consumer.cs");
        const string contractSource = """
            namespace Agent /* outer */
            {
                namespace Models /* canonical */
                {
                    public class ServerJob(int value)
                    {
                        public int Value => value;
                    }
                }

                namespace Sibling /* preserved */
                {
                    public static class Marker
                    {
                        public static int Value => 5;
                    }
                }
            }

            namespace Agent.Interfaces
            {
                public interface IPlugin
                {
                    Agent.Models.ServerJob Execute();
                }
            }
            """;
        const string consumerSource = """
            public static class Subject
            {
                public static int Run() =>
                    new Agent.Models.ServerJob(7).Value + Agent.Sibling.Marker.Value;
            }
            """;

        File.WriteAllText(contractPath, contractSource);
        File.WriteAllText(consumerPath, consumerSource);
        try
        {
            var map = UuidRenameMap.Derive(
                "f1606684-8591-5ed3-92b5-9a55f19a934c",
                ContractScanner.Scan(contracts));
            var trees = new[]
            {
                CSharpSyntaxTree.ParseText(contractSource, path: contractPath),
                CSharpSyntaxTree.ParseText(consumerSource, path: consumerPath),
            };
            var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(
                "NestedNamespaceFixture", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var rewrittenTrees = trees.Select(tree => new UuidRenameTransform(map)
                    .Rewrite(tree, compilation.GetSemanticModel(tree, ignoreAccessibility: true)))
                .ToArray();
            var rewrittenContract = rewrittenTrees[0].GetRoot();
            var rewrittenText = rewrittenContract.ToFullString();

            StringAssert.Contains(rewrittenText, "/* outer */");
            StringAssert.Contains(rewrittenText, "/* canonical */");
            StringAssert.Contains(rewrittenText, "namespace Sibling /* preserved */");
            var rewrittenCompilation = CSharpCompilation.Create(
                "NestedNamespaceFixture", rewrittenTrees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var rewrittenModel = rewrittenCompilation.GetSemanticModel(rewrittenTrees[0]);
            var classes = rewrittenContract.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                .ToDictionary(declaration => declaration.Identifier.ValueText);
            var renamedType = map.GetRenamed("ServerJob");
            Assert.AreEqual(map.GetRenamed("Agent.Models"),
                rewrittenModel.GetDeclaredSymbol(classes[renamedType])!
                    .ContainingNamespace.ToDisplayString());
            Assert.AreEqual("Agent.Sibling",
                rewrittenModel.GetDeclaredSymbol(classes["Marker"])!
                    .ContainingNamespace.ToDisplayString());
            Assert.IsInstanceOfType<Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax>(
                classes[renamedType].Parent);

            using var stream = new MemoryStream();
            var emit = rewrittenCompilation.Emit(stream);
            Assert.IsTrue(emit.Success,
                string.Join(Environment.NewLine, emit.Diagnostics));
            var assembly = System.Reflection.Assembly.Load(stream.ToArray());
            Assert.AreEqual(12, assembly.GetType("Subject")!
                .GetMethod("Run")!.Invoke(null, null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ScannerProvenance_QualifiesSameNamespaceMetadataTypeAfterMove()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uuid_metadata_namespace_{Guid.NewGuid():N}");
        var contracts = Path.Combine(root, "Agent.Models");
        Directory.CreateDirectory(contracts);
        var contractPath = Path.Combine(contracts, "Contracts.cs");
        const string source = """
            namespace Agent.Models
            {
                public sealed class ServerJob
                {
                    private readonly ExternalType external = new();
                    private readonly Other.ExternalControl control = new();

                    public string Run() => external.Name + ":"
                        + typeof(System.Text.StringBuilder).Name + ":" + control.Name;
                }
            }

            namespace Agent.Interfaces
            {
                public interface IPlugin
                {
                    Agent.Models.ServerJob Execute();
                }
            }

            public static class Subject
            {
                public static string Run() => new Agent.Models.ServerJob().Run();
            }
            """;
        const string metadataSource = """
            namespace Agent.Models
            {
                public sealed class ExternalType
                {
                    public string Name => "external";
                }
            }
            namespace Other
            {
                public sealed class ExternalControl
                {
                    public string Name => "control";
                }
            }
            """;

        File.WriteAllText(contractPath, source);
        try
        {
            var platformReferences = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToArray();
            using var metadataStream = new MemoryStream();
            var metadataEmit = CSharpCompilation.Create(
                    "MetadataNamespaceFixture",
                    [CSharpSyntaxTree.ParseText(metadataSource)], platformReferences,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .Emit(metadataStream);
            Assert.IsTrue(metadataEmit.Success,
                string.Join(Environment.NewLine, metadataEmit.Diagnostics));
            var metadataBytes = metadataStream.ToArray();
            var metadataReference = MetadataReference.CreateFromImage(metadataBytes);

            var map = UuidRenameMap.Derive(
                "53c7a426-1191-5107-a223-c9f38470d810",
                ContractScanner.Scan(contracts));
            var tree = CSharpSyntaxTree.ParseText(source, path: contractPath);
            var compilation = CSharpCompilation.Create(
                "MetadataConsumerFixture", [tree],
                platformReferences.Append(metadataReference),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var rewrittenTree = new UuidRenameTransform(map)
                .Rewrite(tree, compilation.GetSemanticModel(tree, ignoreAccessibility: true));
            var rewritten = rewrittenTree.GetRoot().ToFullString();

            StringAssert.Contains(rewritten, "global::Agent.Models.ExternalType");
            StringAssert.Contains(rewritten, "Other.ExternalControl");
            StringAssert.Contains(rewritten, "System.Text.StringBuilder");
            Assert.IsFalse(rewritten.Contains("global::Other.ExternalControl"));
            Assert.IsFalse(rewritten.Contains("global::System.Text.StringBuilder"));

            using var stream = new MemoryStream();
            var emit = CSharpCompilation.Create(
                    "MetadataConsumerFixture", [rewrittenTree],
                    platformReferences.Append(metadataReference),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .Emit(stream);
            Assert.IsTrue(emit.Success,
                string.Join(Environment.NewLine, emit.Diagnostics));
            var context = new System.Runtime.Loader.AssemblyLoadContext(
                $"metadata-namespace-{Guid.NewGuid():N}", isCollectible: true);
            try
            {
                _ = context.LoadFromStream(new MemoryStream(metadataBytes));
                var assembly = context.LoadFromStream(new MemoryStream(stream.ToArray()));
                Assert.AreEqual("external:StringBuilder:control", assembly.GetType("Subject")!
                    .GetMethod("Run")!.Invoke(null, null));
            }
            finally
            {
                context.Unload();
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ScannerProvenance_QualifiesPreservedTypeFromMovedNamespace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uuid_reverse_xref_{Guid.NewGuid():N}");
        var contracts = Path.Combine(root, "Contracts");
        Directory.CreateDirectory(contracts);
        var contractPath = Path.Combine(contracts, "Contracts.cs");
        var preservedPath = Path.Combine(root, "Preserved.cs");
        const string contractSource = """
            namespace Agent.Models
            {
                public class ServerJob { }
            }
            namespace Agent.Interfaces
            {
                using Agent.Models;

                public interface IPlugin
                {
                    ServerJob Execute();
                }

                public static class Subject
                {
                    public static int Run() => new UnmappedType(23).Value;
                }
            }
            """;
        const string preservedSource = """
            namespace Agent.Models
            {
                public class UnmappedType(int value)
                {
                    public int Value => value;
                }
            }
            """;

        File.WriteAllText(contractPath, contractSource);
        File.WriteAllText(preservedPath, preservedSource);
        try
        {
            var map = UuidRenameMap.Derive(
                "afd36786-c409-518f-8560-6c1295be0f61",
                ContractScanner.Scan(contracts));
            var trees = new[]
            {
                CSharpSyntaxTree.ParseText(contractSource, path: contractPath),
                CSharpSyntaxTree.ParseText(preservedSource, path: preservedPath),
            };
            var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(
                "ReverseNamespaceFixture", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var rewrittenTrees = trees.Select(tree => new UuidRenameTransform(map)
                    .Rewrite(tree, compilation.GetSemanticModel(tree, ignoreAccessibility: true)))
                .ToArray();

            using var stream = new MemoryStream();
            var emit = CSharpCompilation.Create(
                    "ReverseNamespaceFixture", rewrittenTrees, references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .Emit(stream);
            Assert.IsTrue(emit.Success,
                string.Join(Environment.NewLine, emit.Diagnostics));

            var rewrittenContract = rewrittenTrees[0].GetRoot().ToFullString();
            var rewrittenPreserved = rewrittenTrees[1].GetRoot().ToFullString();
            StringAssert.Contains(rewrittenContract, "global::Agent.Models.UnmappedType");
            StringAssert.Contains(rewrittenPreserved, "namespace Agent.Models");
            StringAssert.Contains(rewrittenPreserved, "class UnmappedType");
            Assert.IsFalse(map.GetAllMappings().ContainsKey("UnmappedType"));

            var assembly = System.Reflection.Assembly.Load(stream.ToArray());
            Assert.AreEqual(23, assembly
                .GetType($"{map.GetRenamed("Agent.Interfaces")}.Subject")!
                .GetMethod("Run")!.Invoke(null, null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ScannerProvenance_QualifiesGenericCanonicalTypeFromPreservedNamespace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uuid_generic_xref_{Guid.NewGuid():N}");
        var contracts = Path.Combine(root, "Agent.Models");
        Directory.CreateDirectory(contracts);
        var contractPath = Path.Combine(contracts, "Wrapper.cs");
        var unrelatedPath = Path.Combine(root, "Unrelated.cs");
        const string contractSource = """
            namespace Agent.Interfaces
            {
                public interface IPlugin { Agent.Models.Wrapper<int> Execute(); }
            }
            namespace Agent.Models
            {
                public class Wrapper<T>(T value) { public T Value => value; }
            }
            """;
        const string unrelatedSource = """
            namespace Agent.Models
            {
                public static class Other
                {
                    public static Wrapper<int> Create() => new Wrapper<int>(18);
                }
            }
            public static class GenericSubject
            {
                public static int Run() => Agent.Models.Other.Create().Value;
            }
            """;

        File.WriteAllText(contractPath, contractSource);
        File.WriteAllText(unrelatedPath, unrelatedSource);
        try
        {
            var map = UuidRenameMap.Derive(
                "cb48975c-8747-5fa7-b053-978c229764ff",
                ContractScanner.Scan(contracts));
            var trees = new[]
            {
                CSharpSyntaxTree.ParseText(contractSource, path: contractPath),
                CSharpSyntaxTree.ParseText(unrelatedSource, path: unrelatedPath),
            };
            var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(
                "GenericNamespaceFixture", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var rewrittenTrees = trees.Select(tree => new UuidRenameTransform(map)
                    .Rewrite(tree, compilation.GetSemanticModel(tree, ignoreAccessibility: true)))
                .ToArray();
            var rewrittenUnrelated = rewrittenTrees[1].GetRoot().ToFullString();
            StringAssert.Contains(rewrittenUnrelated,
                $"global::{map.GetRenamed("Agent.Models")}.{map.GetRenamed("Wrapper")}<int>");
            StringAssert.Contains(rewrittenUnrelated, "namespace Agent.Models");

            using var stream = new MemoryStream();
            var emit = CSharpCompilation.Create(
                    "GenericNamespaceFixture", rewrittenTrees, references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .Emit(stream);
            Assert.IsTrue(emit.Success,
                string.Join(Environment.NewLine, emit.Diagnostics));
            var assembly = System.Reflection.Assembly.Load(stream.ToArray());
            Assert.AreEqual(18, assembly.GetType("GenericSubject")!
                .GetMethod("Run")!.Invoke(null, null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void UsingDirective_SubSegmentMatchesContractType_SubSegmentNotRenamed()
    {
        var names = new ContractNames(
            Interfaces: [],
            InterfaceMembers: [],
            Types: ["Commands"],
            Namespaces: ["Workflow.Models"],
            RecordParams: []);

        var map = UuidRenameMap.Derive("d5ab6cfe-8059-5c8e-bc75-34f689a24441", names);
        var renamedType = map.GetRenamed("Commands");
        var renamedNsPrefix = map.GetRenamed("Workflow.Models");

        var source = """
            using Workflow.Models.Commands;
            public class Foo { }
            """;

        var result = Rewrite(source, map);

        Assert.IsTrue(
            result.Contains($"using {renamedNsPrefix}.Commands;"),
            $"Using directive sub-segment must stay as 'Commands'; "
            + $"expected 'using {renamedNsPrefix}.Commands;'");

        Assert.IsFalse(
            result.Contains($"using {renamedNsPrefix}.{renamedType};"),
            "Using directive sub-segment must NOT be renamed to the type's obfuscated value");
    }

    [TestMethod]
    public void CanonicalDeclarationRenames_PreserveIdentifierTrivia()
    {
        const string source = """
            namespace Agent.Interfaces
            {
                using Agent.Models;

                public interface IPlugin
                {
                    ServerJob Job { get; }
                    InteractMessage Message { get; }
                    DatagramSource Source { get; }
                    SpawnOptions Options { get; }
                    DelegateMessage Callback { get; }
                }

                public interface /*interface-leading*/ IModule /*interface-trailing*/ : IPlugin
                {
                    int Execute();
                    int Name { get; }
                }
            }

            namespace Agent.Models
            {
                public class /*class-leading*/ ServerJob /*class-trailing*/
                {
                    public ServerJob /*constructor-trailing*/ () { }
                    public int Execute /*method-trailing*/ () => 0;
                    public int Name /*property-trailing*/ => 0;
                }

                public record /*record-leading*/ InteractMessage /*record-trailing*/ (int Value);
                public enum /*enum-leading*/ DatagramSource /*enum-trailing*/ { Local }
                public struct /*struct-leading*/ SpawnOptions /*struct-trailing*/ { }
                public delegate void /*delegate-leading*/ DelegateMessage /*delegate-trailing*/ ();
            }
            """;
        var declarationNames = new[]
        {
            "IModule", "ServerJob", "InteractMessage", "DatagramSource",
            "SpawnOptions", "DelegateMessage", "Execute", "Name",
        };
        var sentinels = new[]
        {
            "/*interface-leading*/", "/*interface-trailing*/",
            "/*class-leading*/", "/*class-trailing*/",
            "/*constructor-trailing*/", "/*method-trailing*/",
            "/*property-trailing*/", "/*record-leading*/",
            "/*record-trailing*/", "/*enum-leading*/", "/*enum-trailing*/",
            "/*struct-leading*/", "/*struct-trailing*/",
            "/*delegate-leading*/", "/*delegate-trailing*/",
        };

        var root = Path.Combine(
            Path.GetTempPath(), $"uuid_declaration_trivia_{Guid.NewGuid():N}");
        var contracts = Path.Combine(root, "Contracts");
        var sourcePath = Path.Combine(contracts, "CanonicalDeclarations.cs");
        Directory.CreateDirectory(contracts);
        File.WriteAllText(sourcePath, source);
        try
        {
            var map = UuidRenameMap.Derive(
                "6c58f266-66e8-5103-bd46-4a50cc8d9430",
                ContractScanner.Scan(contracts));
            var tree = CSharpSyntaxTree.ParseText(source, path: sourcePath);
            var rewrittenRoot = new UuidRenameTransform(map)
                .Rewrite(tree)
                .GetRoot();
            var result = rewrittenRoot.ToFullString();

            foreach (var declarationName in declarationNames)
                StringAssert.Contains(result, map.GetRenamed(declarationName));
            Assert.AreEqual(
                map.GetRenamed("ServerJob"),
                rewrittenRoot.DescendantNodes()
                    .OfType<ConstructorDeclarationSyntax>()
                    .Single()
                    .Identifier
                    .ValueText);
            Assert.HasCount(2, rewrittenRoot.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.Identifier.ValueText == map.GetRenamed("Execute"))
                .ToArray());
            Assert.HasCount(2, rewrittenRoot.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Where(property => property.Identifier.ValueText == map.GetRenamed("Name"))
                .ToArray());
            foreach (var sentinel in sentinels)
                StringAssert.Contains(result, sentinel);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
