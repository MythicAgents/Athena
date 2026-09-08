using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public sealed class AgentSemanticRenameTests
{
    private static readonly MetadataReference[] RuntimeReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();

    [TestMethod]
    public void Transform_RenamesBoundDeclarationsAndReferencesAndStillExecutes()
    {
        const string source = """
            namespace VisibleSpace;
            interface IVisible<TVisible> { int VisibleProperty { get; } }
            enum VisibleEnum { VisibleMember = 2 }
            delegate int VisibleDelegate(int visibleParameter);
            sealed class VisibleType : IVisible<int>
            {
                private readonly int visibleField = 3;
                public int VisibleProperty => visibleField;
                public event VisibleDelegate? VisibleEvent;
                public int VisibleMethod(int visibleParameter)
                {
                    int visibleLocal = visibleParameter;
                    int VisibleLocalFunction(int visibleNested) => visibleNested + visibleField;
                    VisibleEvent += value => visibleLocal += value;
                    return VisibleLocalFunction(VisibleProperty) + (int)VisibleEnum.VisibleMember;
                }
            }
            static class Program
            {
                public static int Main() => new VisibleType().VisibleMethod(4);
            }
            """;

        var result = AgentSemanticRenamer.Transform(
            CreateCompilation(source), Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), 17);

        AssertNoErrors(result.Compilation);
        Assert.AreEqual(8, ExecuteEntryPoint(result.Compilation));
        var assembly = EmitAndLoad(result.Compilation);
        var metadataNames = assembly.GetTypes().SelectMany(type =>
            new[] { type.Name, type.Namespace ?? string.Empty }
                .Concat(type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static).Select(member => member.Name)))
            .ToArray();
        foreach (var original in new[] { "VisibleType", "VisibleMethod", "VisibleProperty",
                     "visibleField", "VisibleEvent", "VisibleEnum", "VisibleMember" })
            Assert.IsFalse(metadataNames.Contains(original, StringComparer.Ordinal), original);

        var transformed = GetText(result.Compilation);
        foreach (var original in new[] { "VisibleSpace", "IVisible", "TVisible", "VisibleEnum",
                     "VisibleMember", "VisibleDelegate", "VisibleType", "visibleField",
                     "VisibleProperty", "VisibleEvent", "VisibleMethod", "visibleParameter",
                     "visibleLocal", "VisibleLocalFunction", "visibleNested", "value" })
            Assert.IsFalse(transformed.Contains(original, StringComparison.Ordinal), original);
        StringAssert.Contains(transformed, "Main");
    }

    [TestMethod]
    public void Transform_PreservesCompilerResolvableConstantReflectionContracts()
    {
        const string source = """
            using System;
            using System.Reflection;
            namespace ReflectionContracts;
            sealed class ReflectedType
            {
                public int ReflectedField = 3;
                public int ReflectedProperty => 2;
                public event Action? ReflectedEvent;
                public int ReflectedMethod() => 5;
                public void Raise() => ReflectedEvent?.Invoke();
            }
            sealed class UnrelatedType
            {
                public int UnrelatedMethod() => 11;
            }
            static class Program
            {
                public static int Main()
                {
                    var type = Type.GetType(
                        "ReflectionContracts.ReflectedType, SemanticRenameFixture",
                        throwOnError: true)!;
                    var assemblyType = Assembly.GetExecutingAssembly().GetType(
                        "ReflectionContracts.ReflectedType", throwOnError: true)!;
                    if (type != assemblyType)
                        return -1;

                    var instance = Activator.CreateInstance(type)!;
                    var method = type.GetMethod("ReflectedMethod")!;
                    var property = type.GetProperty("ReflectedProperty")!;
                    var field = type.GetField("ReflectedField")!;
                    var reflectedEvent = type.GetEvent("ReflectedEvent")!;
                    var members = type.GetMember("ReflectedMethod");
                    var invoked = type.InvokeMember("ReflectedMethod",
                        BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                        binder: null, target: instance, args: null);
                    return (int)method.Invoke(instance, null)!
                        + (int)property.GetValue(instance)!
                        + (int)field.GetValue(instance)!
                        + (reflectedEvent is null ? 0 : 1)
                        + members.Length
                        + (int)invoked!
                        + new UnrelatedType().UnrelatedMethod();
                }
            }
            """;

        var result = AgentSemanticRenamer.Transform(CreateCompilation(source), Guid.Empty, 28);
        var transformed = GetText(result.Compilation);

        foreach (var preserved in new[] { "ReflectionContracts", "ReflectedType", "ReflectedMethod",
                     "ReflectedProperty", "ReflectedField", "ReflectedEvent" })
            StringAssert.Contains(transformed, preserved);
        foreach (var renamed in new[] { "UnrelatedType", "UnrelatedMethod" })
            Assert.IsFalse(transformed.Contains(renamed, StringComparison.Ordinal), renamed);
        Assert.AreEqual(28, ExecuteEntryPoint(result.Compilation));
    }

    [TestMethod]
    public void Transform_PreservesNestedTypeReturnedByGetMember()
    {
        const string source = """
            using System;
            static class Outer
            {
                public sealed class Nested { }
            }
            sealed class UnrelatedType { }
            static class Program
            {
                public static int Main() =>
                    typeof(Outer).GetMember("Nested")[0].MemberType
                        == System.Reflection.MemberTypes.NestedType
                        ? 1
                        : 0;
            }
            """;

        var result = AgentSemanticRenamer.Transform(CreateCompilation(source), Guid.Empty, 43);
        var transformed = GetText(result.Compilation);

        StringAssert.Contains(transformed, "Outer");
        StringAssert.Contains(transformed, "Nested");
        Assert.IsFalse(transformed.Contains("UnrelatedType", StringComparison.Ordinal));
        Assert.AreEqual(1, ExecuteEntryPoint(result.Compilation));
    }

    [TestMethod]
    public void Transform_PreservesDecoratedTypeGetTypeComponents()
    {
        const string source = """
            using System;
            namespace DecoratedContracts;
            sealed class ReflectedType { }
            sealed class GenericType<T> { }
            sealed class UnrelatedType { }
            static class Program
            {
                public static int Main()
                {
                    var array = Type.GetType(
                        "DecoratedContracts.ReflectedType[], SemanticRenameFixture",
                        throwOnError: true)!;
                    var pointer = Type.GetType(
                        "DecoratedContracts.ReflectedType*, SemanticRenameFixture",
                        throwOnError: true)!;
                    var byRef = Type.GetType(
                        "DecoratedContracts.ReflectedType&, SemanticRenameFixture",
                        throwOnError: true)!;
                    var generic = Type.GetType(
                        "DecoratedContracts.GenericType`1[[DecoratedContracts.ReflectedType, SemanticRenameFixture]], SemanticRenameFixture",
                        throwOnError: true)!;
                    return array.GetElementType() == typeof(ReflectedType)
                        && pointer.GetElementType() == typeof(ReflectedType)
                        && byRef.GetElementType() == typeof(ReflectedType)
                        && generic.GetGenericTypeDefinition() == typeof(GenericType<>)
                        && generic.GetGenericArguments()[0] == typeof(ReflectedType)
                            ? 1
                            : 0;
                }
            }
            """;

        var result = AgentSemanticRenamer.Transform(CreateCompilation(source), Guid.Empty, 44);

        Assert.AreEqual(1, ExecuteEntryPoint(result.Compilation));
        var transformed = GetText(result.Compilation);
        foreach (var preserved in new[] { "DecoratedContracts", "ReflectedType", "GenericType" })
            StringAssert.Contains(transformed, preserved);
        Assert.IsFalse(transformed.Contains("UnrelatedType", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Plan_IsDeterministicAndChangesWithUuidOrSeed()
    {
        var compilation = CreateCompilation(
            "static class Program { static int Main() { int visible = 1; return visible; } }");
        var uuid = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var first = AgentSemanticRenamePlanner.Create(compilation, uuid, 9).NamesBySymbolKey;
        var second = AgentSemanticRenamePlanner.Create(compilation, uuid, 9).NamesBySymbolKey;
        var changedUuid = AgentSemanticRenamePlanner.Create(compilation, Guid.Empty, 9).NamesBySymbolKey;
        var changedSeed = AgentSemanticRenamePlanner.Create(compilation, uuid, 10).NamesBySymbolKey;

        CollectionAssert.AreEqual(first.ToArray(), second.ToArray());
        CollectionAssert.AreNotEqual(first.Values.ToArray(), changedUuid.Values.ToArray());
        CollectionAssert.AreNotEqual(first.Values.ToArray(), changedSeed.Values.ToArray());
    }

    [TestMethod]
    public void Transform_PreservesExternalAbiFamiliesAndImplicitInteropEntryPoints()
    {
        const string source = """
            using System;
            using System.Runtime.InteropServices;
            interface ISourceContract { int SourceMember(int sourceValue); }
            sealed partial class Worker : ISourceContract, IDisposable
            {
                public override string ToString() => "worker";
                public void Dispose() { }
                public int SourceMember(int sourceValue) => Ordinary(sourceValue: sourceValue);
                public int Ordinary(int sourceValue) => sourceValue + 1;
                [DllImport("native", CallingConvention = CallingConvention.Cdecl)]
                internal static extern int ImplicitNative(int sourceValue);
                [DllImport("native", EntryPoint = "kept_wire_name")]
                internal static extern int ExplicitNative(int sourceValue);
                [LibraryImport("native")]
                internal static partial int ImplicitLibrary(int sourceValue);
                internal static partial int ImplicitLibrary(int sourceValue) => 0;
            }
            static class Program
            {
                static int Main() => new Worker().SourceMember(4);
            }
            """;
        var result = AgentSemanticRenamer.Transform(
            CreateCompilation(source), Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), 18);
        var transformed = GetText(result.Compilation);

        foreach (var preserved in new[] { "Main", "ToString", "Dispose", "ImplicitNative", "ImplicitLibrary" })
            StringAssert.Contains(transformed, preserved);
        foreach (var renamed in new[] { "ISourceContract", "SourceMember", "Worker", "Ordinary", "ExplicitNative" })
            Assert.IsFalse(transformed.Contains(renamed, StringComparison.Ordinal), renamed);
        StringAssert.Contains(transformed, "kept_wire_name");
        AssertNoErrors(result.Compilation);
        Assert.AreEqual(5, ExecuteEntryPoint(result.Compilation));
    }

    [TestMethod]
    public void Transform_RetainsIdentifierTriviaAndHandlesLocalSyntaxForms()
    {
        const string source = """
            using System;
            using System.Linq;
            record VisibleRecord<TVisible>(int /*before*/ visiblePrimary /*after*/)
            {
                public int Run((int, int) visibleTuple)
                {
                    var (visibleLeft, visibleRight) = visibleTuple;
                    foreach (var visibleItem in new[] { visibleLeft })
                    {
                        try { throw new Exception(); }
                        catch (Exception visibleError) when (visibleItem > 0)
                        {
                            if (int.TryParse("2", out var visibleOut)
                                && visibleError is { Message: var visiblePattern })
                            {
                                var visibleQuery = from visibleRange in new[] { visibleOut }
                                                   let visibleLet = visibleRange + visibleRight
                                                   select visibleLet;
                                return visibleQuery.Single() + visiblePrimary;
                            }
                        }
                    }
                    return 0;
                }
            }
            static class Program { static int Main() => new VisibleRecord<int>(3).Run((1, 4)); }
            """;
        var result = AgentSemanticRenamer.Transform(
            CreateCompilation(source), Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), 19);
        var transformed = GetText(result.Compilation);

        StringAssert.Contains(transformed, "/*before*/");
        StringAssert.Contains(transformed, "/*after*/");
        foreach (var original in new[] { "VisibleRecord", "TVisible", "visiblePrimary", "visibleTuple",
                     "visibleLeft", "visibleRight", "visibleItem", "visibleError", "visibleOut",
                     "visiblePattern", "visibleQuery", "visibleRange", "visibleLet", "Run" })
            Assert.IsFalse(transformed.Contains(original, StringComparison.Ordinal), original);
        AssertNoErrors(result.Compilation);
        Assert.AreEqual(9, ExecuteEntryPoint(result.Compilation));
    }

    [TestMethod]
    public void Transform_SkipsTypeOnlyCatchWhileRenamingNearbyLocals()
    {
        const string source = """
            using System;
            static class Program
            {
                static int Main()
                {
                    int visibleLocal = 4;
                    try { throw new InvalidOperationException(); }
                    catch (InvalidOperationException) { return visibleLocal; }
                }
            }
            """;

        var result = AgentSemanticRenamer.Transform(CreateCompilation(source), Guid.Empty, 25);
        var transformed = GetText(result.Compilation);

        Assert.IsFalse(transformed.Contains("visibleLocal", StringComparison.Ordinal));
        Assert.AreEqual(4, ExecuteEntryPoint(result.Compilation));
    }

    [TestMethod]
    public void Transform_PreservesContextualVarAcrossNamespaceBoundaries()
    {
        const string source = """
            namespace Hidden.Types
            {
                sealed class Item { public int Value => 5; }
            }
            namespace Other
            {
                static class Program
                {
                    static Hidden.Types.Item Create() => new Hidden.Types.Item();
                    static Hidden.Types.Item[] CreateMany() => new[] { Create() };
                    static int Main()
                    {
                        var item = Create();
                        foreach (var entry in CreateMany())
                            return item.Value + entry.Value;
                        return 0;
                    }
                }
            }
            """;

        var result = AgentSemanticRenamer.Transform(CreateCompilation(source), Guid.Empty, 27);
        var transformed = GetText(result.Compilation);

        Assert.AreEqual(2, transformed.Split("var ", StringSplitOptions.None).Length - 1);
        foreach (var renamed in new[] { "Item", "Create", "CreateMany", "item", "entry" })
            Assert.IsFalse(transformed.Contains(renamed, StringComparison.Ordinal), renamed);
        AssertNoErrors(result.Compilation);
        Assert.AreEqual(10, ExecuteEntryPoint(result.Compilation));
    }

    [TestMethod]
    public void Plan_FailsClosedForCompilationErrors()
    {
        var compilation = CreateCompilation(
            "static class Program { static int Main() { MissingType unresolved = null; return 0; } }");

        Assert.ThrowsExactly<AgentSemanticRenameException>(() =>
            AgentSemanticRenamePlanner.Create(compilation, Guid.Empty, 1));
    }

    [TestMethod]
    public void Transform_PreservesInferredAnonymousAndTupleNames()
    {
        const string source = """
            static class Program
            {
                static int Main()
                {
                    int inferredName = 3;
                    int ordinaryLocal = 4;
                    var anonymous = new { inferredName };
                    var tuple = (inferredName, ordinaryLocal: ordinaryLocal);
                    return anonymous.inferredName + tuple.inferredName + tuple.ordinaryLocal;
                }
            }
            """;
        var result = AgentSemanticRenamer.Transform(CreateCompilation(source), Guid.Empty, 21);
        var transformed = GetText(result.Compilation);

        StringAssert.Contains(transformed, "inferredName");
        Assert.IsFalse(transformed.Contains("ordinaryLocal = 4", StringComparison.Ordinal));
        Assert.AreEqual(10, ExecuteEntryPoint(result.Compilation));
    }

    [TestMethod]
    public void Transform_PreservesLocalUsedOnlyAsInferredTupleName()
    {
        const string source = """
            static class Program
            {
                static int Main()
                {
                    int tupleOnlyName = 6;
                    var tuple = (tupleOnlyName, other: 1);
                    return tuple.tupleOnlyName;
                }
            }
            """;
        var result = AgentSemanticRenamer.Transform(CreateCompilation(source), Guid.Empty, 23);

        StringAssert.Contains(GetText(result.Compilation), "tupleOnlyName");
        Assert.AreEqual(6, ExecuteEntryPoint(result.Compilation));
    }

    [TestMethod]
    public void Transform_RewritesCrossTreeReferencesConstructorsDestructorsAndNameof()
    {
        const string declarations = """
            namespace CrossTree;
            struct VisibleStruct { public int VisibleValue; }
            class VisibleBase { public virtual int VisibleVirtual() => 2; }
            sealed class VisibleDerived : VisibleBase
            {
                private readonly VisibleStruct visibleStruct;
                public VisibleDerived(int visibleValue) => visibleStruct.VisibleValue = visibleValue;
                ~VisibleDerived() { }
                public override int VisibleVirtual() => visibleStruct.VisibleValue;
                public string VisibleName() => nameof(VisibleVirtual);
            }
            """;
        const string consumer = """
            using CrossTree;
            static class Program
            {
                static int Main()
                {
                    var visibleInstance = new VisibleDerived(7);
                    return visibleInstance.VisibleVirtual();
                }
            }
            """;
        var result = AgentSemanticRenamer.Transform(
            CreateCompilation(declarations, consumer), Guid.Empty, 22);
        var transformed = GetText(result.Compilation);

        foreach (var original in new[] { "CrossTree", "VisibleStruct", "VisibleValue", "VisibleBase",
                     "VisibleDerived", "visibleStruct", "visibleValue", "VisibleVirtual",
                     "VisibleName", "visibleInstance" })
            Assert.IsFalse(transformed.Contains(original, StringComparison.Ordinal), original);
        Assert.AreEqual(7, ExecuteEntryPoint(result.Compilation));
    }

    [TestMethod]
    public void Transform_OverloadsAndSameSimpleNamesRemainCollisionFree()
    {
        const string source = """
            using System;
            static class Program
            {
                static int Overloaded(int value) => value + 1;
                static string Overloaded(string value) => value + "!";
                static int Main()
                {
                    Func<int, int> selected = Overloaded;
                    return selected(3) + nameof(Overloaded).Length;
                }
            }
            """;

        var result = AgentSemanticRenamer.Transform(CreateCompilation(source), Guid.Empty, 24);

        Assert.IsFalse(GetText(result.Compilation).Contains("Overloaded", StringComparison.Ordinal));
        Assert.AreEqual(17, ExecuteEntryPoint(result.Compilation));
    }

    [TestMethod]
    public void Transform_PreservesExternallyAugmentedNamespaceWhileRenamingSourceMembers()
    {
        const string externalSource = """
            namespace External.Root;
            public sealed class ExternalType { public int Value => 7; }
            """;
        const string source = """
            namespace External.Root;
            sealed class LocalType
            {
                public int Run()
                {
                    int visibleLocal = new ExternalType().Value;
                    return visibleLocal;
                }
            }
            static class Program { static int Main() => new LocalType().Run(); }
            """;

        var external = CreateLibraryCompilation("ExternalFixture", externalSource);
        var externalImage = EmitBytes(external);
        var compilation = CSharpCompilation.Create(
            "AugmentingFixture",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)) },
            RuntimeReferences.Append(MetadataReference.CreateFromImage(externalImage)),
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        var result = AgentSemanticRenamer.Transform(compilation, Guid.Empty, 26);
        var transformed = GetText(result.Compilation);

        StringAssert.Contains(transformed, "namespace External.Root");
        foreach (var renamed in new[] { "LocalType", "Run", "visibleLocal" })
            Assert.IsFalse(transformed.Contains(renamed, StringComparison.Ordinal), renamed);
        AssertNoErrors(result.Compilation);

        var loadContext = new AssemblyLoadContext($"external-namespace-{Guid.NewGuid():N}", isCollectible: true);
        Load(loadContext, externalImage);
        var payload = Load(loadContext, EmitBytes(result.Compilation));
        Assert.AreEqual(7, payload.EntryPoint!.Invoke(null, null));
    }

    [TestMethod]
    public void TransformProjectSet_RenamesContractFamiliesConsistentlyForIndependentPlugin()
    {
        const string contractSource = """
            namespace Contracts;
            public interface IOperation { int Execute(int value); }
            public abstract class BaseWorker
            {
                public virtual int Calculate(int amount) => amount + 1;
                public int Calculate(string text) => text.Length;
            }
            public sealed class ContractValue
            {
                public ContractValue(int value) => Value = value;
                public int Value { get; }
            }
            """;
        const string consumerSource = """
            using Contracts;
            public sealed class Worker : BaseWorker, IOperation
            {
                public int Execute(int value) => Calculate(amount: value);
                public override int Calculate(int amount) => amount + 2;
            }
            public sealed class ExplicitWorker : IOperation
            {
                int IOperation.Execute(int value) => new ContractValue(value).Value + 3;
            }
            public sealed class Unrelated
            {
                public int Execute(int value) => value + 100;
            }
            public static class ConsumerEntry
            {
                public static int Run() =>
                    ((IOperation)new Worker()).Execute(value: 4)
                    + ((IOperation)new ExplicitWorker()).Execute(value: 5)
                    + new Worker().Calculate("abc")
                    + new Unrelated().Execute(1);
            }
            """;
        const string pluginSource = """
            using Contracts;
            public sealed class IndependentPlugin : IOperation
            {
                public int Execute(int value) => new ContractValue(value).Value * 2;
            }
            """;

        var uuid = Guid.Parse("12345678-1234-5678-9abc-123456789abc");
        var originalContract = CreateLibraryCompilation("ContractFixture", contractSource);
        var contractReference = originalContract.ToMetadataReference();
        var consumer = CreateLibraryCompilation("ConsumerFixture", consumerSource, contractReference);
        var plugin = CreateLibraryCompilation("PluginFixture", pluginSource, contractReference);

        var hostResult = AgentSemanticRenamer.TransformProjectSet(
            new[] { originalContract, consumer }, uuid, 37);
        var pluginResult = AgentSemanticRenamer.TransformProjectSet(
            new[] { originalContract, plugin }, uuid, 37);

        Assert.AreEqual(GetText(hostResult.Compilations[0]), GetText(pluginResult.Compilations[0]));

        var transformedContractImage = EmitBytes(hostResult.Compilations[0]);
        var transformedConsumer = hostResult.Compilations[1];
        var transformedPlugin = pluginResult.Compilations[1];
        var transformedConsumerImage = EmitBytes(transformedConsumer);
        var transformedPluginImage = EmitBytes(transformedPlugin);

        var loadContext = new AssemblyLoadContext($"semantic-rename-{Guid.NewGuid():N}", isCollectible: true);
        var contractAssembly = Load(loadContext, transformedContractImage);
        var consumerAssembly = Load(loadContext, transformedConsumerImage);
        var pluginAssembly = Load(loadContext, transformedPluginImage);
        var contract = contractAssembly.GetTypes().Single(type => type.IsInterface);
        var contractMethod = contract.GetMethods().Single();
        var pluginType = pluginAssembly.GetTypes().Single(type => contract.IsAssignableFrom(type));
        var pluginInstance = Activator.CreateInstance(pluginType)!;

        Assert.AreEqual(10, contractMethod.Invoke(pluginInstance, new object[] { 5 }));
        var consumerEntry = consumerAssembly.GetTypes().Single(type => type.IsAbstract && type.IsSealed);
        Assert.AreEqual(118, consumerEntry.GetMethods(BindingFlags.Public | BindingFlags.Static |
            BindingFlags.DeclaredOnly).Single().Invoke(null, null));

        var consumerText = GetText(transformedConsumer);
        Assert.IsFalse(consumerText.Contains("IOperation", StringComparison.Ordinal));
        Assert.IsFalse(consumerText.Contains("ContractValue", StringComparison.Ordinal));
        var executeNames = hostResult.Plan.NamesBySymbolKey
            .Where(pair => pair.Key.Contains("Execute", StringComparison.Ordinal))
            .Select(pair => pair.Value).Distinct(StringComparer.Ordinal).ToArray();
        Assert.IsGreaterThanOrEqualTo(2, executeNames.Length,
            "The unrelated same-spelled member must not share the contract-family name.");
    }

    [TestMethod]
    public void TransformProjectSet_RebindsEquivalentDependencyCompilationSnapshots()
    {
        const string modelsSource = """
            namespace Agent.Models;
            public interface IAgent { string Name { get; } }
            public interface IMod { string Name { get; } }
            public sealed class AgentImpl : IAgent { public string Name => "agent"; }
            public sealed class Mod : IMod { public string Name => "mod"; }
            """;
        const string managersSource = """
            using System.Collections.Generic;
            using Agent.Models;
            namespace Agent.Managers;
            public sealed class Manager
            {
                public IAgent Resolve() => new AgentImpl();
                public IReadOnlyList<IMod> Mods { get; } = new IMod[] { new Mod() };
            }
            """;
        const string coreSource = """
            using Agent.Managers;
            using Agent.Models;
            namespace Agent.Core;
            public static class Program
            {
                public static string Run()
                {
                    var manager = new Manager();
                    IAgent agent = manager.Resolve();
                    foreach (var mod in manager.Mods)
                        return agent.Name + mod.Name;
                    return agent.Name;
                }
            }
            """;

        var models = CreateLibraryCompilation("Models", modelsSource);
        var modelsReference = MetadataReference.CreateFromImage(EmitBytes(models));
        var managers = CreateLibraryCompilation("Managers", managersSource, modelsReference);
        var managersReference = MetadataReference.CreateFromImage(EmitBytes(managers));
        var core = CreateLibraryCompilation("Core", coreSource,
            modelsReference, managersReference);

        var result = AgentSemanticRenamer.TransformProjectSet(
            new[] { models, managers, core },
            Guid.Parse("12345678-1234-5678-9abc-123456789abc"), 42);

        foreach (var compilation in result.Compilations)
            AssertNoErrors(compilation);
        Assert.IsFalse(GetText(result.Compilations[0]).Contains("IAgent", StringComparison.Ordinal));
        Assert.IsFalse(GetText(result.Compilations[0]).Contains("IMod", StringComparison.Ordinal));
    }

    private static CSharpCompilation CreateCompilation(params string[] sources) =>
        CSharpCompilation.Create(
            "SemanticRenameFixture",
            sources.Select(source => CSharpSyntaxTree.ParseText(source,
                new CSharpParseOptions(LanguageVersion.Preview))),
            RuntimeReferences,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

    private static CSharpCompilation CreateLibraryCompilation(
        string assemblyName, string source, params MetadataReference[] references) =>
        CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)) },
            RuntimeReferences.Concat(references),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static byte[] EmitBytes(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.IsTrue(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        return stream.ToArray();
    }

    private static Assembly Load(AssemblyLoadContext context, byte[] image)
    {
        using var stream = new MemoryStream(image);
        return context.LoadFromStream(stream);
    }


    private static string GetText(CSharpCompilation compilation) =>
        string.Join("\n", compilation.SyntaxTrees.Select(tree => tree.ToString()));

    private static Assembly EmitAndLoad(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.IsTrue(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        return Assembly.Load(stream.ToArray());
    }

    private static object? ExecuteEntryPoint(CSharpCompilation compilation) =>
        EmitAndLoad(compilation).EntryPoint!.Invoke(null, null);

    private static void AssertNoErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.HasCount(0, errors,
            string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
    }
}
