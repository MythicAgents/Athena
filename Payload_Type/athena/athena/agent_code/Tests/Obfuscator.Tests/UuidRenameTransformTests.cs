using Microsoft.CodeAnalysis.CSharp;
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
        UuidRenameMap.Derive("test-uuid-build", TestNames);

    private static string Rewrite(string source, UuidRenameMap? map = null)
    {
        map ??= CreateMap();
        var tree = CSharpSyntaxTree.ParseText(source);
        var transform = new UuidRenameTransform(map);
        tree = transform.Rewrite(tree);
        return tree.GetRoot().ToFullString();
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

        var map = UuidRenameMap.Derive("test-collision-ns", names);
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
    public void UsingDirective_SubSegmentMatchesContractType_SubSegmentNotRenamed()
    {
        var names = new ContractNames(
            Interfaces: [],
            InterfaceMembers: [],
            Types: ["Commands"],
            Namespaces: ["Workflow.Models"],
            RecordParams: []);

        var map = UuidRenameMap.Derive("test-collision-using", names);
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
}
