using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Obfuscator.Config;
using Obfuscator.IL;
using Obfuscator.Source;

namespace Obfuscator.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SourceStringEncryptionPipelineTests
{
    [TestMethod]
    public void FullPipeline_DerivedPluginContractPreservesInheritedDispatch()
    {
        const string contracts = """
            using System.Threading.Tasks;
            namespace Agent.Models { public sealed class ServerJob { } }
            namespace Agent.Interfaces
            {
                using Agent.Models;
                public interface IPlugin
                {
                    string Name { get; }
                    Task Execute(ServerJob job);
                }
            }
            """;
        const string derivedContract = """
            namespace Agent.Interfaces
            {
                public interface IFilePlugin : IPlugin
                {
                    void HandleNextMessage();
                }
            }
            """;
        const string leafContract = """
            namespace Agent.Interfaces
            {
                public interface IStreamingFilePlugin : IFilePlugin { }
            }
            """;
        const string plugin = """
            using System.Threading.Tasks;
            using Agent.Interfaces;
            using Agent.Models;
            namespace Fixture
            {
                public sealed class Plugin : IStreamingFilePlugin
                {
                    public string Name => "derived-command";
                    public string Unrelated => "encrypt-derived-unrelated";
                    public Task Execute(ServerJob job) => Task.CompletedTask;
                    public void HandleNextMessage() { }
                }
            }
            """;

        var directory = CreateRewriteDirectory();
        try
        {
            var models = Directory.CreateDirectory(
                Path.Combine(directory, "Agent.Models", "Interfaces")).FullName;
            File.WriteAllText(Path.Combine(models, "IPlugin.cs"), contracts);
            File.WriteAllText(Path.Combine(models, "IFilePlugin.cs"), derivedContract);
            File.WriteAllText(Path.Combine(models, "IStreamingFilePlugin.cs"), leafContract);
            File.WriteAllText(Path.Combine(directory, "Plugin.cs"), plugin);

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                Seed: 42, Uuid: null, InputPath: directory,
                OutputPath: directory, MapPath: null));
            var assemblyPath = CompileDirectory(directory);

            using (var preflight = AssemblyDefinition.ReadAssembly(assemblyPath))
            {
                AssertRawConstantGetter(preflight, "derived-command");
                var literals = preflight.MainModule.Types
                    .SelectMany(EnumerateTypes)
                    .SelectMany(type => type.Methods)
                    .Where(method => method.HasBody)
                    .SelectMany(method => method.Body.Instructions)
                    .Where(instruction => instruction.OpCode == OpCodes.Ldstr)
                    .Select(instruction => (string)instruction.Operand)
                    .ToArray();
                CollectionAssert.DoesNotContain(
                    literals, "encrypt-derived-unrelated");
            }

            new ILRewriter().Rewrite(assemblyPath, seed: 42, mapPath: null);
            var context = new AssemblyLoadContext(
                $"derived-plugin-{Guid.NewGuid():N}", isCollectible: true);
            var assembly = context.LoadFromStream(
                new MemoryStream(File.ReadAllBytes(assemblyPath)));
            var baseContract = assembly.GetTypes().Single(type =>
                type.IsInterface
                && type.GetProperties().Any(property => property.Name == "Name"));
            var implementation = assembly.GetTypes().Single(type =>
                type.IsClass && baseContract.IsAssignableFrom(type));
            var instance = Activator.CreateInstance(implementation);
            Assert.AreEqual("derived-command",
                baseContract.GetProperty("Name")!.GetValue(instance));
            baseContract.GetMethod("Execute")!.Invoke(instance,
                [Activator.CreateInstance(baseContract.GetMethod("Execute")!
                    .GetParameters()[0].ParameterType)]);
            context.Unload();
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_PreservesActualPluginNameGetterConstants()
    {
        const string source = """
            using System.Threading.Tasks;
            namespace Agent.Models { public sealed class ServerJob { } }
            namespace Agent.Interfaces
            {
                using Agent.Models;
                public interface IPlugin
                {
                    string Name { get; }
                    Task Execute(ServerJob job);
                }
            }
            namespace Fixture
            {
                using Agent.Interfaces;
                using Agent.Models;
                public sealed class Plugin : IPlugin
                {
                    public string Name => "command";
                    public string Other => "encrypt-this";
                    public Task Execute(ServerJob job) => Task.CompletedTask;
                }
                public sealed class ExplicitPlugin : IPlugin
                {
                    string IPlugin.Name => "explicit-command";
                    public Task Execute(ServerJob job) => Task.CompletedTask;
                }
            }
            """;

        var (directory, assemblyPath) = RewriteAndCompile(
            source, uuid: "47380b98-11e7-5354-a4e6-8053801f5849");
        try
        {
            var rewrittenSource = File.ReadAllText(Directory.GetFiles(
                directory, "Fixture.cs", SearchOption.AllDirectories).Single());
            Assert.IsTrue(rewrittenSource.Contains("\"command\""), rewrittenSource);
            Assert.IsTrue(rewrittenSource.Contains("\"explicit-command\""), rewrittenSource);
            using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
            AssertRawConstantGetter(assembly, "command");
            AssertRawConstantGetter(assembly, "explicit-command");

            var plaintext = assembly.MainModule.Types
                .SelectMany(EnumerateTypes)
                .SelectMany(type => type.Methods)
                .Where(method => method.HasBody)
                .SelectMany(method => method.Body.Instructions)
                .Where(instruction => instruction.OpCode == OpCodes.Ldstr)
                .Select(instruction => (string)instruction.Operand)
                .ToArray();
            CollectionAssert.DoesNotContain(plaintext, "encrypt-this");
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_SemanticContractTypeCollision_Compiles()
    {
        const string contract = """
            namespace Agent.Models
            {
                public sealed class ServerJob { }
            }
            """;
        const string pluginContract = """
            namespace Agent.Interfaces
            {
                public interface IPlugin
                {
                    void Execute(global::Agent.Models.ServerJob job);
                }
            }
            """;
        const string consumer = """
            using ContractJob = global::Agent.Models.ServerJob;
            namespace Fixture
            {
                public sealed class Holder
                {
                    private sealed record ServerJob(int Value);
                    private ServerJob local = new(1);
                    private ContractJob aliased = new();
                    private global::Agent.Models.ServerJob qualified = new();
                }
            }
            """;

        var directory = CreateRewriteDirectory();
        try
        {
            var models = Directory.CreateDirectory(
                Path.Combine(directory, "Agent.Models")).FullName;
            var interfaces = Directory.CreateDirectory(
                Path.Combine(models, "Interfaces")).FullName;
            File.WriteAllText(Path.Combine(models, "ServerJob.cs"), contract);
            File.WriteAllText(Path.Combine(interfaces, "IPlugin.cs"), pluginContract);
            File.WriteAllText(Path.Combine(directory, "Consumer.cs"), consumer);

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                Seed: 42,
                Uuid: "89df18b9-b562-509c-8e25-37dcd76caadf",
                InputPath: directory,
                OutputPath: directory,
                MapPath: null));
            _ = CompileDirectory(directory);

            var rewritten = File.ReadAllText(
                Path.Combine(directory, "Consumer.cs"));
            StringAssert.Contains(rewritten, "record ServerJob(int Value)");
            StringAssert.Contains(rewritten, "private ServerJob local = new(1)");
            Assert.IsFalse(rewritten.Contains("Agent.Models.ServerJob"));
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_CrossFileContractDtoReferences_RotateAndCompile()
    {
        const string contract = """
            namespace Agent.Models
            {
                public sealed class ServerJob
                {
                    public ServerJob? Next { get; set; }
                }
            }
            """;
        const string pluginContract = """
            namespace Agent.Interfaces
            {
                public interface IPlugin
                {
                    global::System.Collections.Generic.IReadOnlyList<
                        global::Agent.Models.ServerJob> Execute();
                }
            }
            """;
        const string consumer = """
            using System.Collections.Generic;
            using Agent.Models;
            using ContractJob = global::Agent.Models.ServerJob;

            namespace Fixture
            {
                public sealed class Holder
                {
                    private ServerJob simple = new();
                    private Agent.Models.ServerJob qualified = new();
                    private global::Agent.Models.ServerJob globalQualified = new();
                    private ContractJob aliased = new();
                    private List<ServerJob> generic = new();
                    private ServerJob Member(ServerJob value) => value.Next ?? value;
                }
            }
            """;

        var directory = CreateRewriteDirectory();
        try
        {
            var models = Directory.CreateDirectory(
                Path.Combine(directory, "Agent.Models")).FullName;
            var interfaces = Directory.CreateDirectory(
                Path.Combine(models, "Interfaces")).FullName;
            File.WriteAllText(Path.Combine(models, "ServerJob.cs"), contract);
            File.WriteAllText(Path.Combine(interfaces, "IPlugin.cs"), pluginContract);
            File.WriteAllText(Path.Combine(directory, "Consumer.cs"), consumer);

            var names = ContractScanner.Scan(models);
            var map = UuidRenameMap.Derive("9fda8ba3-21b6-5f7c-9d95-e9b0ac5e95be", names);
            var renamedType = map.GetRenamed("ServerJob");

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                Seed: 42,
                Uuid: "9fda8ba3-21b6-5f7c-9d95-e9b0ac5e95be",
                InputPath: directory,
                OutputPath: directory,
                MapPath: null));

            _ = CompileDirectory(directory);
            var rewrittenContract = File.ReadAllText(
                Path.Combine(models, "ServerJob.cs"));
            var rewrittenConsumer = File.ReadAllText(
                Path.Combine(directory, "Consumer.cs"));
            StringAssert.Contains(rewrittenContract, $"class {renamedType}");
            Assert.IsFalse(rewrittenConsumer.Contains("ServerJob"),
                "Every real simple, qualified, global, alias, generic, and member reference must rotate.");
            StringAssert.Contains(rewrittenConsumer, renamedType);
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_FullyQualifiedGenericShellWithNestedContract_RotatesAndExecutes()
    {
        const string contract = """
            namespace Agent.Models;
            public sealed class ServerJob
            {
                public int Value { get; set; }
            }
            """;
        const string pluginContract = """
            namespace Agent.Interfaces;
            public interface IPlugin
            {
                System.Collections.Generic.List<Agent.Models.ServerJob> Jobs { get; }
            }
            """;
        const string consumer = """
            namespace Fixture;
            public sealed class Plugin : Agent.Interfaces.IPlugin
            {
                public System.Collections.Generic.List<Agent.Models.ServerJob> Jobs { get; }
                    = new() { new Agent.Models.ServerJob { Value = 7 } };
            }
            public static class Entry
            {
                public static int Run() => new Plugin().Jobs[0].Value;
            }
            """;

        var directory = CreateRewriteDirectory();
        var context = new AssemblyLoadContext(
            $"qualified-generic-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var models = Directory.CreateDirectory(
                Path.Combine(directory, "Agent.Models")).FullName;
            var interfaces = Directory.CreateDirectory(
                Path.Combine(models, "Interfaces")).FullName;
            File.WriteAllText(Path.Combine(models, "ServerJob.cs"), contract);
            File.WriteAllText(Path.Combine(interfaces, "IPlugin.cs"), pluginContract);
            File.WriteAllText(Path.Combine(directory, "Consumer.cs"), consumer);

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                Seed: 42,
                Uuid: "551b60aa-c20a-53a2-b408-915d9f56bf66",
                InputPath: directory,
                OutputPath: directory,
                MapPath: null));

            var assemblyPath = CompileDirectory(directory);
            var rewrittenContract = File.ReadAllText(
                Path.Combine(interfaces, "IPlugin.cs"));
            StringAssert.Contains(rewrittenContract,
                "System.Collections.Generic.List<");
            Assert.IsFalse(rewrittenContract.Contains("Agent.Models.ServerJob"));

            var assembly = context.LoadFromStream(
                new MemoryStream(File.ReadAllBytes(assemblyPath)));
            var run = assembly.GetTypes().Single(type => type.Name == "Entry")
                .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;
            Assert.AreEqual(7, run.Invoke(null, null));
        }
        finally
        {
            context.Unload();
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_DelegateOnlyCanonicalNamespaceMovesWithoutMovingUnrelatedTree()
    {
        const string pluginContract = """
            namespace Agent.Interfaces;
            public interface IPlugin
            {
                Agent.Models.Callback Handler { get; }
            }
            """;
        const string contractDelegate = """
            namespace Agent.Models;
            public delegate int Callback(int value);
            """;
        const string consumer = """
            namespace Fixture;
            public sealed class Plugin : Agent.Interfaces.IPlugin
            {
                public Agent.Models.Callback Handler => value => value + 1;
            }
            public static class Entry
            {
                public static int Run() => new Plugin().Handler(6);
            }
            """;
        const string unrelated = """
            namespace Agent.Models;
            public delegate void UnrelatedCallback(string value);
            """;

        var directory = CreateRewriteDirectory();
        var context = new AssemblyLoadContext(
            $"delegate-only-provenance-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var models = Directory.CreateDirectory(
                Path.Combine(directory, "Agent.Models")).FullName;
            var interfaces = Directory.CreateDirectory(
                Path.Combine(models, "Interfaces")).FullName;
            var delegatePath = Path.Combine(models, "Callback.cs");
            var unrelatedPath = Path.Combine(models, "Unrelated.cs");
            File.WriteAllText(delegatePath, contractDelegate);
            File.WriteAllText(unrelatedPath, unrelated);
            File.WriteAllText(Path.Combine(interfaces, "IPlugin.cs"), pluginContract);
            File.WriteAllText(Path.Combine(directory, "Consumer.cs"), consumer);

            var names = ContractScanner.Scan(models);
            var map = UuidRenameMap.Derive(
                "c23c58e3-62c7-5738-9691-bbfd00b17011", names);
            var renamedNamespace = map.GetRenamed("Agent.Models");
            var renamedDelegate = map.GetRenamed("Callback");

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                Seed: 42,
                Uuid: "c23c58e3-62c7-5738-9691-bbfd00b17011",
                InputPath: directory,
                OutputPath: directory,
                MapPath: null));

            var assemblyPath = CompileDirectory(directory);
            var rewrittenDelegate = File.ReadAllText(delegatePath);
            var rewrittenConsumer = File.ReadAllText(
                Path.Combine(directory, "Consumer.cs"));
            var rewrittenUnrelated = File.ReadAllText(unrelatedPath);
            StringAssert.Contains(rewrittenDelegate,
                $"namespace {renamedNamespace}");
            StringAssert.Contains(rewrittenDelegate,
                $"delegate int {renamedDelegate}");
            Assert.IsFalse(rewrittenConsumer.Contains("Agent.Models.Callback"));
            StringAssert.Contains(rewrittenConsumer, renamedDelegate);
            StringAssert.Contains(rewrittenUnrelated, "namespace Agent.Models;");
            StringAssert.Contains(rewrittenUnrelated,
                "delegate void UnrelatedCallback(string value)");

            var assembly = context.LoadFromStream(
                new MemoryStream(File.ReadAllBytes(assemblyPath)));
            var run = assembly.GetTypes().Single(type => type.Name == "Entry")
                .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;
            Assert.AreEqual(7, run.Invoke(null, null));
        }
        finally
        {
            context.Unload();
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_ContractDelegateDeclarationAndReferences_RotateAndCompile()
    {
        const string pluginContract = """
            namespace Agent.Interfaces;
            public interface IPlugin
            {
                Agent.Models.Callback<Agent.Models.Payload> Handler { get; }
            }
            """;
        const string contractDelegate = """
            namespace Agent.Models;
            public delegate Result Callback<T>(T payload, Envelope envelope);
            public sealed class Payload { }
            public sealed class Envelope { }
            public sealed class Result { }
            """;
        const string consumer = """
            using Agent.Models;
            using ContractAlias = global::Agent.Models.Callback<Agent.Models.Payload>;
            namespace Fixture;
            public sealed class Holder
            {
                private Callback<Payload>? simple;
                private Agent.Models.Callback<Payload>? qualified;
                private global::Agent.Models.Callback<Payload>? globalQualified;
                private ContractAlias? aliased;
            }
            """;
        const string unrelated = """
            namespace Fixture;
            public delegate void Callback<T>(T value);
            public sealed class Unrelated
            {
                private Callback<int>? callback;
            }
            """;

        var directory = CreateRewriteDirectory();
        try
        {
            var models = Directory.CreateDirectory(
                Path.Combine(directory, "Agent.Models")).FullName;
            var interfaces = Directory.CreateDirectory(
                Path.Combine(models, "Interfaces")).FullName;
            File.WriteAllText(Path.Combine(models, "Callback.cs"), contractDelegate);
            File.WriteAllText(Path.Combine(interfaces, "IPlugin.cs"), pluginContract);
            File.WriteAllText(Path.Combine(directory, "Consumer.cs"), consumer);
            File.WriteAllText(Path.Combine(directory, "Unrelated.cs"), unrelated);

            var names = ContractScanner.Scan(models);
            var map = UuidRenameMap.Derive("498a7a6d-3e7a-57fc-a6ff-0d4835345150", names);
            var renamedDelegate = map.GetRenamed("Callback");

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                Seed: 42,
                Uuid: "498a7a6d-3e7a-57fc-a6ff-0d4835345150",
                InputPath: directory,
                OutputPath: directory,
                MapPath: null));

            _ = CompileDirectory(directory);
            var rewrittenContract = File.ReadAllText(
                Path.Combine(models, "Callback.cs"));
            var rewrittenConsumer = File.ReadAllText(
                Path.Combine(directory, "Consumer.cs"));
            var rewrittenUnrelated = File.ReadAllText(
                Path.Combine(directory, "Unrelated.cs"));
            StringAssert.Contains(rewrittenContract,
                $"delegate {map.GetRenamed("Result")} {renamedDelegate}<T>");
            StringAssert.Contains(rewrittenConsumer,
                $"private Callback<global::{map.GetRenamed("Agent.Models")}.{map.GetRenamed("Payload")}>? simple;");
            Assert.IsFalse(rewrittenConsumer.Contains("Agent.Models.Callback"),
                "Qualified, global, and alias references to the scanned delegate must rotate.");
            StringAssert.Contains(rewrittenConsumer, renamedDelegate);
            StringAssert.Contains(rewrittenUnrelated, "delegate void Callback<T>");
            StringAssert.Contains(rewrittenUnrelated, "Callback<int>");
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_OnlyExactScannedContractDeclarationRotates()
    {
        const string source = """
            namespace Agent.Models
            {
                public sealed class ServerJob { }
            }

            namespace Agent.Models.Fakes
            {
                public sealed class ServerJob { }
            }

            namespace Agent.Interfaces
            {
                public interface IPlugin
                {
                    void Execute(global::Agent.Models.ServerJob job);
                }
            }

            namespace Fixture
            {
                public sealed class Holder
                {
                    private sealed class ServerJob { }
                    private global::Agent.Models.ServerJob real = new();
                    private global::Agent.Models.Fakes.ServerJob fake = new();
                    private ServerJob nested = new();
                }
            }
            """;

        var (directory, _) = RewriteAndCompile(
            source, uuid: "669aa7f8-76c3-575a-a8b9-c23f44cf9c41");
        try
        {
            var rewritten = File.ReadAllText(Directory.GetFiles(
                directory, "Fixture.cs", SearchOption.AllDirectories).Single());
            StringAssert.Contains(rewritten, ".Fakes.ServerJob");
            StringAssert.Contains(rewritten, "class ServerJob");
            StringAssert.Contains(rewritten, "private ServerJob nested");
            Assert.AreEqual(4,
                rewritten.Split("ServerJob", StringSplitOptions.None).Length - 1,
                "Only the fake declaration/reference and nested declaration/reference remain canonical.");
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_NestedRecordMemberMatchesContractMember_Compiles()
    {
        const string source = """
            namespace Agent.Interfaces
            {
                public interface IPlugin
                {
                    string Name { get; }
                }
            }
            namespace Fixture
            {
                using Agent.Interfaces;
                public sealed class Host
                {
                    private readonly record struct PreflightPlugin(string Name);

                    private static string Read(PreflightPlugin plugin) => plugin.Name;
                    private static string ReadContract(IPlugin plugin) => plugin.Name;
                }
            }
            """;

        var (directory, _) = RewriteAndCompile(
            source, uuid: "aa250f42-c765-564c-921d-ae41df435611");
        try
        {
            var rewrittenSource = File.ReadAllText(Directory.GetFiles(
                directory, "Fixture.cs", SearchOption.AllDirectories).Single());
            StringAssert.Contains(rewrittenSource, "record struct PreflightPlugin(string Name)");
            StringAssert.Contains(rewrittenSource, "plugin.Name;");
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_PreservesRuntimeExtensionAndTypeLookupNames()
    {
        const string source = """
            using System;
            using System.Reflection;
            public sealed class Target
            {
                public int Value = 17;
                public static string Read()
                {
                    return RuntimeReflectionExtensions.GetRuntimeField(
                        typeof(Target), "Value")!.Name
                        + Type.GetType("Target")!.Name;
                }
            }
            """;

        var (directory, assemblyPath) = RewriteAndCompile(source);
        try
        {
            using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
            var literals = assembly.MainModule.Types
                .SelectMany(EnumerateTypes)
                .SelectMany(type => type.Methods)
                .Where(method => method.HasBody)
                .SelectMany(method => method.Body.Instructions)
                .Where(instruction => instruction.OpCode == OpCodes.Ldstr)
                .Select(instruction => (string)instruction.Operand)
                .ToArray();
            CollectionAssert.Contains(literals, "Value");
            CollectionAssert.Contains(literals, "Target");
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void FullPipeline_PreservesReflectionMemberNameLiteralsAndLookups()
    {
        const string source = """
            using System;
            using System.Reflection;
            public interface IEntryPoint { string Run(); }
            public sealed class Target
            {
                private string PropertyTarget { get; set; } = "value";
                private int FieldTarget = 7;
                private void MethodTarget() { }
                private event EventHandler? EventTarget;
                private string GeneralTarget { get; set; } = "general";
                private void Raise() => EventTarget?.Invoke(this, EventArgs.Empty);
            }
            public sealed class EntryPoint : IEntryPoint
            {
                public string Run()
                {
                    var type = typeof(Target);
                    var info = type.GetTypeInfo();
                    return string.Join("|", new[]
                    {
                        type.GetProperty(bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance, name: "PropertyTarget")?.Name,
                        type.GetField(bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance, name: "FieldTarget")?.Name,
                        type.GetMethod(name: "MethodTarget", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance, binder: null, types: Type.EmptyTypes, modifiers: null)?.Name,
                        type.GetEvent(bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance, name: "EventTarget")?.Name,
                        type.GetMember(bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance, name: "GeneralTarget")[0].Name,
                        info.GetDeclaredProperty(name: "PropertyTarget")?.Name,
                        info.GetDeclaredField(name: "FieldTarget")?.Name,
                        info.GetDeclaredMethod(name: "MethodTarget")?.Name,
                        info.GetDeclaredEvent(name: "EventTarget")?.Name,
                    })!;
                }
            }
            """;

        var (directory, assemblyPath) = RewriteAndCompile(source);
        var context = new AssemblyLoadContext(
            $"source-string-pipeline-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            new ILRewriter().Rewrite(assemblyPath, seed: 42, mapPath: null);
            using (var rewrittenAssembly = AssemblyDefinition.ReadAssembly(assemblyPath))
            {
                var literals = rewrittenAssembly.MainModule.Types
                    .SelectMany(EnumerateTypes)
                    .SelectMany(type => type.Methods)
                    .Where(method => method.HasBody)
                    .SelectMany(method => method.Body.Instructions)
                    .Where(instruction => instruction.OpCode == OpCodes.Ldstr)
                    .Select(instruction => (string)instruction.Operand)
                    .ToArray();
                CollectionAssert.DoesNotContain(literals, "value");
                CollectionAssert.DoesNotContain(literals, "general");
            }
            var assembly = context.LoadFromStream(
                new MemoryStream(File.ReadAllBytes(assemblyPath)));
            var run = assembly.GetTypes()
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static))
                .Single(method => method.Name == "Run"
                    && method.GetParameters().Length == 0
                    && !method.DeclaringType!.IsInterface
                    && !method.IsAbstract);
            var instance = Activator.CreateInstance(run.DeclaringType!);
            var result = (string)run.Invoke(instance, null)!;

            Assert.AreEqual(
                "PropertyTarget|FieldTarget|MethodTarget|EventTarget|GeneralTarget|"
                + "PropertyTarget|FieldTarget|MethodTarget|EventTarget",
                result);
        }
        finally
        {
            context.Unload();
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void FullPipeline_PreservesReflectionNamesPassedThroughLocals()
    {
        const string source = """
            using System;
            using System.Reflection;
            public interface IEntryPoint { string Run(); }
            public sealed class Target
            {
                private string PropertyTarget { get; } = "property-value";
                private int FieldTarget = 7;
                private string MethodTarget() => "method-value";
                private event EventHandler? EventTarget;
                private string GeneralTarget { get; } = "general-value";
                private string DeclaredPropertyTarget { get; } = "declared-property-value";
                private int DeclaredFieldTarget = 11;
                private string DeclaredMethodTarget() => "declared-method-value";
                private event EventHandler? DeclaredEventTarget;
                private void Raise() { EventTarget?.Invoke(this, EventArgs.Empty); DeclaredEventTarget?.Invoke(this, EventArgs.Empty); }
            }
            public sealed class EntryPoint : IEntryPoint
            {
                public string Run()
                {
                    var type = typeof(Target);
                    var info = type.GetTypeInfo();
                    var propertyName = "PropertyTarget";
                    var fieldName = "FieldTarget";
                    var methodName = "MethodTarget";
                    var eventName = "EventTarget";
                    var memberName = "GeneralTarget";
                    var declaredPropertyName = "DeclaredPropertyTarget";
                    var declaredFieldName = "DeclaredFieldTarget";
                    var declaredMethodName = "DeclaredMethodTarget";
                    var declaredEventName = "DeclaredEventTarget";
                    return string.Join("|", new[]
                    {
                        type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance)?.Name,
                        type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?.Name,
                        type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)?.Name,
                        type.GetEvent(eventName, BindingFlags.NonPublic | BindingFlags.Instance)?.Name,
                        type.GetMember(memberName, BindingFlags.NonPublic | BindingFlags.Instance)[0].Name,
                        info.GetDeclaredProperty(declaredPropertyName)?.Name,
                        info.GetDeclaredField(declaredFieldName)?.Name,
                        info.GetDeclaredMethod(declaredMethodName)?.Name,
                        info.GetDeclaredEvent(declaredEventName)?.Name,
                    })!;
                }
            }
            """;

        var (directory, assemblyPath) = RewriteAndCompile(source);
        var context = new AssemblyLoadContext(
            $"reflection-locals-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            new ILRewriter().Rewrite(assemblyPath, seed: 42, mapPath: null);
            var assembly = context.LoadFromStream(
                new MemoryStream(File.ReadAllBytes(assemblyPath)));
            var run = assembly.GetTypes().SelectMany(type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                .Single(method => method.Name == "Run" && !method.DeclaringType!.IsInterface);
            var result = (string)run.Invoke(
                Activator.CreateInstance(run.DeclaringType!), null)!;
            Assert.AreEqual(
                "PropertyTarget|FieldTarget|MethodTarget|EventTarget|GeneralTarget|"
                + "DeclaredPropertyTarget|DeclaredFieldTarget|DeclaredMethodTarget|DeclaredEventTarget",
                result);
        }
        finally
        {
            context.Unload();
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void FullPipeline_PreservesNestedInterfaceDeclaredAndInvokeMemberLookups()
    {
        const string source = """
            using System;
            using System.Linq;
            using System.Reflection;
            public interface IEntryPoint { string Run(string name); }
            namespace Contracts { internal interface ILookup { } }
            namespace OtherContracts { internal interface IUnused { } }
            public sealed class Outer
            {
                private sealed class NestedTarget { }
                private sealed class DeclaredNestedTarget { }
                private sealed class UnrelatedNested { }
            }
            public sealed class Target : Contracts.ILookup
            {
                private string DynamicField = "dynamic-value";
                private string DeclaredMethod() => "declared";
                private string UnrelatedMethod() => "unrelated";
            }
            public sealed class EntryPoint : IEntryPoint
            {
                public string Run(string name)
                {
                    var nestedName = "NestedTarget";
                    var declaredNestedName = "DeclaredNestedTarget";
                    var declaredMethodName = "DeclaredMethod";
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    return string.Join("|", new[]
                    {
                        typeof(Outer).GetNestedType(nestedName, BindingFlags.NonPublic)?.Name,
                        typeof(Outer).GetTypeInfo().GetDeclaredNestedType(declaredNestedName)?.Name,
                        typeof(Target).GetInterface("Contracts.ILookup")?.FullName,
                        typeof(Target).GetTypeInfo().GetDeclaredMethods(declaredMethodName).Single().Name,
                        (string)typeof(Target).InvokeMember(name,
                            flags | BindingFlags.GetField, null, new Target(), null)!,
                    })!;
                }
            }
            """;

        var (directory, assemblyPath) = RewriteAndCompile(source);
        var context = new AssemblyLoadContext(
            $"expanded-reflection-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            new ILRewriter().Rewrite(assemblyPath, seed: 42, mapPath: null);
            using (var definition = AssemblyDefinition.ReadAssembly(assemblyPath))
            {
                var names = definition.MainModule.Types
                    .SelectMany(EnumerateTypes)
                    .Select(type => type.Name)
                    .ToArray();
                CollectionAssert.DoesNotContain(names, "UnrelatedNested");
                CollectionAssert.DoesNotContain(names, "IUnused");
            }

            var assembly = context.LoadFromStream(
                new MemoryStream(File.ReadAllBytes(assemblyPath)));
            var entryContract = assembly.GetTypes().Single(type => type.IsInterface
                && type.GetMethods().Any(method => method.GetParameters().Length == 1));
            var implementation = assembly.GetTypes().Single(type => type.IsClass
                && entryContract.IsAssignableFrom(type));
            var run = entryContract.GetMethods().Single();
            var result = (string)run.Invoke(
                Activator.CreateInstance(implementation), ["DynamicField"])!;
            Assert.AreEqual(
                "NestedTarget|DeclaredNestedTarget|Contracts.ILookup|DeclaredMethod|dynamic-value",
                result);
        }
        finally
        {
            context.Unload();
            TryDelete(directory);
        }
    }

    private static (string directory, string assemblyPath) RewriteAndCompile(
        string source,
        string? uuid = null)
    {
        var directory = Path.Combine(
            Path.GetTempPath(), $"source-string-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Fixture.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
            + "<TargetFramework>net10.0</TargetFramework>"
            + "</PropertyGroup></Project>");
        var sourceDirectory = uuid is null
            ? directory
            : Directory.CreateDirectory(Path.Combine(directory, "Agent.Models")).FullName;
        File.WriteAllText(Path.Combine(sourceDirectory, "Fixture.cs"), source);

        new SourceRewriter().Rewrite(new ObfuscationConfig(
            Seed: 42,
            Uuid: uuid,
            InputPath: directory,
            OutputPath: directory,
            MapPath: null));

        var trees = Directory.GetFiles(
                directory, "*.cs", SearchOption.AllDirectories)
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
            .Append(CSharpSyntaxTree.ParseText(
                "global using System; global using System.Linq;"))
            .ToArray();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Fixture",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var assemblyPath = Path.Combine(directory, "Fixture.dll");
        var result = compilation.Emit(assemblyPath);
        Assert.IsTrue(
            result.Success,
            "Compilation failed:\n" + string.Join("\n", result.Diagnostics));
        return (directory, assemblyPath);
    }

    private static string CreateRewriteDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), $"source-string-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Fixture.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
            + "<TargetFramework>net10.0</TargetFramework>"
            + "</PropertyGroup></Project>");
        return directory;
    }

    private static string CompileDirectory(string directory)
    {
        var trees = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
            .Append(CSharpSyntaxTree.ParseText(
                "global using System; global using System.Linq;"))
            .ToArray();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Fixture", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var assemblyPath = Path.Combine(directory, "Fixture.dll");
        var result = compilation.Emit(assemblyPath);
        Assert.IsTrue(result.Success,
            "Compilation failed:\n" + string.Join("\n", result.Diagnostics));
        return assemblyPath;
    }

    private static void AssertRawConstantGetter(
        AssemblyDefinition assembly,
        string expectedValue)
    {
        var getter = assembly.MainModule.Types
            .SelectMany(EnumerateTypes)
            .SelectMany(type => type.Methods)
            .Single(method => method.HasBody
                && method.Body.Instructions.Any(instruction =>
                    instruction.OpCode == OpCodes.Ldstr
                    && Equals(instruction.Operand, expectedValue)));
        var instructions = getter.Body.Instructions
            .Where(instruction => instruction.OpCode != OpCodes.Nop)
            .ToArray();
        Assert.AreEqual(2, instructions.Length);
        Assert.AreEqual(OpCodes.Ldstr, instructions[0].OpCode);
        Assert.AreEqual(expectedValue, instructions[0].Operand);
        Assert.AreEqual(OpCodes.Ret, instructions[1].OpCode);
    }

    private static IEnumerable<TypeDefinition> EnumerateTypes(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes.SelectMany(EnumerateTypes))
            yield return nested;
    }

    private static void TryDelete(string directory)
    {
        try { Directory.Delete(directory, recursive: true); } catch { }
    }
}
