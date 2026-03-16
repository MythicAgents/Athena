using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Config;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class UuidRenameTransformTests
{
    private static UuidRenameMap CreateMap() =>
        UuidRenameMap.Derive("test-uuid-build");

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
}
