using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Config;
using Obfuscator.Source;

namespace Obfuscator.Tests;

[TestClass]
public sealed class SourceRewriterSemanticTests
{
    [TestMethod]
    public void Rewrite_CaseDistinctLinuxPaths_AreCopiedIndependently()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("Windows paths are case-insensitive by contract.");

        var root = CreateTempDirectory();
        var input = Path.Combine(root, "agent.models");
        var output = Path.Combine(root, "Agent.Models");
        Directory.CreateDirectory(input);
        try
        {
            File.WriteAllText(Path.Combine(input, "Fixture.csproj"), ProjectFile);
            File.WriteAllText(Path.Combine(input, "Marker.txt"), "copied");
            File.WriteAllText(Path.Combine(input, "Fixture.cs"),
                "public static class Fixture { public static int Value => 7; }");

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                42, null, input, output, null));

            Assert.AreEqual("copied", File.ReadAllText(Path.Combine(output, "Marker.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(input, "Marker.txt")));
            Assert.AreNotEqual(Path.GetFullPath(input), Path.GetFullPath(output));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [TestMethod]
    public void Rewrite_ExclusionPrefixDoesNotExcludeSiblingDirectory()
    {
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "Fixture.csproj"), ProjectFile);
            var sibling = Directory.CreateDirectory(
                Path.Combine(root, "TestsSupport")).FullName;
            var sourcePath = Path.Combine(sibling, "Fixture.cs");
            File.WriteAllText(sourcePath,
                "public static class Fixture { public static string Value => \"prefix-boundary-secret\"; }");

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                42, null, root, root, null));

            Assert.IsFalse(File.ReadAllText(sourcePath).Contains("prefix-boundary-secret"),
                "A sibling whose name only starts with an excluded directory must still be rewritten.");
        }
        finally
        {
            TryDelete(root);
        }
    }

    [TestMethod]
    public void Rewrite_IsolatesSameFqnDeclarationsByNearestProject()
    {
        var root = CreateTempDirectory();
        try
        {
            var contracts = Directory.CreateDirectory(
                Path.Combine(root, "Agent.Models")).FullName;
            var selected = Directory.CreateDirectory(
                Path.Combine(root, "SelectedConsumer")).FullName;
            var other = Directory.CreateDirectory(
                Path.Combine(root, "OtherProject")).FullName;
            foreach (var project in new[] { contracts, selected, other })
                File.WriteAllText(Path.Combine(project, "Fixture.csproj"), ProjectFile);

            File.WriteAllText(Path.Combine(contracts, "Dto.cs"), """
                namespace Agent.Models
                {
                    public record WireDto(int Value);
                }
                namespace Agent.Interfaces
                {
                    public interface IPlugin { Agent.Models.WireDto Execute(); }
                }
                """);
            File.WriteAllText(Path.Combine(selected, "Consumer.cs"), """
                namespace Selected;
                public static class Consumer
                {
                    public static int Read(Agent.Models.WireDto dto) => dto.Value;
                }
                """);
            File.WriteAllText(Path.Combine(other, "Dto.cs"),
                "namespace Agent.Models; public record WireDto(int Value);");
            File.WriteAllText(Path.Combine(other, "Consumer.cs"), """
                namespace Other;
                public static class Consumer
                {
                    public static int Read(Agent.Models.WireDto dto) => dto.Value;
                }
                """);

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                42, "632ed0d2-fb2f-5904-90c6-98bd635adeda", root, root, null));

            var otherDto = File.ReadAllText(Path.Combine(other, "Dto.cs"));
            var otherConsumer = File.ReadAllText(Path.Combine(other, "Consumer.cs"));
            var selectedConsumer = File.ReadAllText(Path.Combine(selected, "Consumer.cs"));
            StringAssert.Contains(otherDto, "record WireDto(int Value)");
            StringAssert.Contains(otherConsumer, "Agent.Models.WireDto");
            StringAssert.Contains(otherConsumer, "dto.Value");
            Assert.IsFalse(selectedConsumer.Contains("Agent.Models.WireDto"));
            CompileProjectSources(other);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [TestMethod]
    public void Rewrite_IsolatesSameFqnDelegateDeclarationsByNearestProject()
    {
        var root = CreateTempDirectory();
        try
        {
            var contracts = Directory.CreateDirectory(
                Path.Combine(root, "Agent.Models")).FullName;
            var other = Directory.CreateDirectory(
                Path.Combine(root, "OtherProject")).FullName;
            foreach (var project in new[] { contracts, other })
                File.WriteAllText(Path.Combine(project, "Fixture.csproj"), ProjectFile);

            var canonicalPath = Path.Combine(contracts, "Callback.cs");
            File.WriteAllText(canonicalPath, """
                namespace Agent.Models
                {
                    public delegate int Callback(int x);
                }
                namespace Agent.Interfaces
                {
                    public interface IPlugin { Agent.Models.Callback Handler { get; } }
                }
                """);
            var localPath = Path.Combine(other, "Callback.cs");
            File.WriteAllText(localPath,
                "namespace Agent.Models; public delegate int Callback(int x);");
            var consumerPath = Path.Combine(other, "Consumer.cs");
            File.WriteAllText(consumerPath, """
                namespace Other;
                public static class Consumer
                {
                    public static int Read(Agent.Models.Callback callback) => callback(7);
                }
                """);

            const string uuid = "52e1ed55-ae1b-5868-a714-e01e9884fb5b";
            var map = UuidRenameMap.Derive(uuid, ContractScanner.Scan(contracts));
            new SourceRewriter().Rewrite(new ObfuscationConfig(
                42, uuid, root, root, null));

            var canonical = File.ReadAllText(canonicalPath);
            var local = File.ReadAllText(localPath);
            var consumer = File.ReadAllText(consumerPath);
            StringAssert.Contains(canonical,
                $"delegate int {map.GetRenamed("Callback")}(int x)");
            Assert.IsFalse(canonical.Contains("delegate int Callback(int x)"));
            StringAssert.Contains(local, "namespace Agent.Models;");
            StringAssert.Contains(local, "delegate int Callback(int x)");
            StringAssert.Contains(consumer, "Agent.Models.Callback");
            StringAssert.Contains(consumer, "callback(7)");
            CompileProjectSources(contracts, "CanonicalContracts");
            CompileProjectSources(other, "LocalConsumer");
        }
        finally
        {
            TryDelete(root);
        }
    }

    [TestMethod]
    public void Rewrite_KeepsNoncollidingDeclarationsFromMixedExternalSupportTree()
    {
        var root = CreateTempDirectory();
        try
        {
            var contracts = Directory.CreateDirectory(
                Path.Combine(root, "Agent.Models")).FullName;
            var consumer = Directory.CreateDirectory(
                Path.Combine(root, "Consumer")).FullName;
            File.WriteAllText(Path.Combine(contracts, "Agent.Models.csproj"), ProjectFile);
            File.WriteAllText(Path.Combine(consumer, "Consumer.csproj"), ProjectFile);

            var canonicalPath = Path.Combine(contracts, "Contracts.cs");
            File.WriteAllText(canonicalPath, """
                namespace Agent.Models
                {
                    public delegate int Callback(int value);
                    public sealed record WireDto(int Value);
                }
                namespace Agent.Interfaces
                {
                    public interface IPlugin
                    {
                        Agent.Models.Callback Handler { get; }
                        Agent.Models.WireDto Execute();
                    }
                }
                """);
            var localPath = Path.Combine(consumer, "Callback.cs");
            File.WriteAllText(localPath, """
                namespace Agent.Models;
                public delegate int Callback(int value);
                """);
            var consumerPath = Path.Combine(consumer, "Consumer.cs");
            File.WriteAllText(consumerPath, """
                namespace Fixture;
                public static class Consumer
                {
                    public static int Invoke(Agent.Models.Callback callback) => callback(7);
                    public static int Read(Agent.Models.WireDto dto) => dto.Value;
                }
                """);

            const string uuid = "7d847e0c-57d6-56ef-adaf-9737f68396e4";
            var map = UuidRenameMap.Derive(uuid, ContractScanner.Scan(contracts));
            new SourceRewriter().Rewrite(new ObfuscationConfig(
                42, uuid, root, root, null));

            var canonical = File.ReadAllText(canonicalPath);
            var local = File.ReadAllText(localPath);
            var rewrittenConsumer = File.ReadAllText(consumerPath);
            StringAssert.Contains(canonical,
                $"delegate int {map.GetRenamed("Callback")}(int value)");
            StringAssert.Contains(canonical,
                $"record {map.GetRenamed("WireDto")}(int {map.GetRenamed("Value")})");
            StringAssert.Contains(local, "namespace Agent.Models;");
            StringAssert.Contains(local, "delegate int Callback(int value)");
            StringAssert.Contains(rewrittenConsumer, "Agent.Models.Callback");
            Assert.IsFalse(rewrittenConsumer.Contains("Agent.Models.WireDto"),
                "The noncolliding declaration from the mixed support tree must remain bindable.");
            StringAssert.Contains(rewrittenConsumer, map.GetRenamed("WireDto"));

            var contractsBytes = CompileProjectSources(contracts, "CanonicalContracts");
            CompileProjectSources(
                consumer,
                "Consumer",
                MetadataReference.CreateFromImage(contractsBytes));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [TestMethod]
    public void Rewrite_UsesUnselectedAgentModelsDeclarationsForCrossProjectBinding()
    {
        var root = CreateTempDirectory();
        try
        {
            var models = Directory.CreateDirectory(
                Path.Combine(root, "Agent.Models")).FullName;
            var consumer = Directory.CreateDirectory(
                Path.Combine(root, "Agent.Profiles.Test")).FullName;
            File.WriteAllText(Path.Combine(models, "Agent.Models.csproj"), ProjectFile);
            File.WriteAllText(Path.Combine(consumer, "Agent.Profiles.Test.csproj"), ProjectFile);

            var selectedPath = Path.Combine(models, "PluginContract.cs");
            File.WriteAllText(selectedPath, """
                namespace Agent.Models
                {
                    public record WireDto(int Value);
                }
                namespace Agent.Interfaces
                {
                    public interface IPlugin { Agent.Models.WireDto Execute(); }
                }
                """);
            var profilePath = Path.Combine(models, "IProfile.cs");
            File.WriteAllText(profilePath, """
                namespace Agent.Interfaces
                {
                    public interface IProfile { int Priority { get; } }
                }
                """);
            var consumerPath = Path.Combine(consumer, "Profile.cs");
            File.WriteAllText(consumerPath, """
                using Agent.Interfaces;

                namespace Profiles
                {
                    public sealed class Profile : IProfile
                    {
                        public int Priority => 7;
                        public Agent.Models.WireDto? Last { get; }
                    }
                }
                """);

            const string uuid = "741e13c1-1c3f-52c4-bd12-e7bf4bc7a674";
            var contractNames = ContractScanner.Scan(models);
            var map = UuidRenameMap.Derive(uuid, contractNames);
            Assert.IsFalse(contractNames.ContractDeclarations.Any(declaration =>
                declaration.MetadataName == "Agent.Interfaces.IProfile"));

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                42, uuid, root, root, null));

            var selectedSource = File.ReadAllText(selectedPath);
            var profileSource = File.ReadAllText(profilePath);
            var consumerSource = File.ReadAllText(consumerPath);
            StringAssert.Contains(selectedSource, $"interface {map.GetRenamed("IPlugin")}");
            Assert.IsFalse(selectedSource.Contains("interface IPlugin"));
            StringAssert.Contains(profileSource, "namespace Agent.Interfaces");
            StringAssert.Contains(profileSource, "interface IProfile");

            var modelsBytes = CompileProjectSources(models, "Fixture.AgentModels");
            CompileProjectSources(
                consumer,
                "Agent.Profiles.Test",
                MetadataReference.CreateFromImage(modelsBytes));
            StringAssert.Contains(consumerSource, "global::Agent.Interfaces.IProfile");
        }
        finally
        {
            TryDelete(root);
        }
    }

    [TestMethod]
    public void Rewrite_PositionalRecordSymbolsRenameAndExecuteConsistently()
    {
        var root = CreateTempDirectory();
        var context = new AssemblyLoadContext(
            $"positional-record-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var contracts = Directory.CreateDirectory(
                Path.Combine(root, "Agent.Models")).FullName;
            File.WriteAllText(Path.Combine(contracts, "Fixture.csproj"), ProjectFile);
            File.WriteAllText(Path.Combine(contracts, "Fixture.cs"), """
                namespace Agent.Models
                {
                    public record WireDto(int Value)
                    {
                        public int Twice => Value * 2;
                        public WireDto Bump() => this with { Value = Value + 1 };
                    }
                }
                namespace Agent.Interfaces
                {
                    public interface IPlugin { Agent.Models.WireDto Execute(); }
                }
                public static class Subject
                {
                    public static int Run()
                    {
                        var dto = new Agent.Models.WireDto(Value: 3);
                        return dto.Value + dto.Twice + dto.Bump().Value;
                    }
                }
                """);

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                42, "61c40de3-9f6c-599d-b60f-60129fe5df05", root, root, null));

            var bytes = CompileProjectSources(contracts);
            var assembly = context.LoadFromStream(new MemoryStream(bytes));
            Assert.AreEqual(13,
                assembly.GetType("Subject")!.GetMethod("Run")!.Invoke(null, null));
        }
        finally
        {
            context.Unload();
            TryDelete(root);
        }
    }

    [TestMethod]
    public void Rewrite_RenamesOnlyExactContractEventImplementations()
    {
        var root = CreateTempDirectory();
        var context = new AssemblyLoadContext(
            $"exact-events-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var contracts = Directory.CreateDirectory(
                Path.Combine(root, "Agent.Models")).FullName;
            File.WriteAllText(Path.Combine(contracts, "Fixture.csproj"), ProjectFile);
            File.WriteAllText(Path.Combine(contracts, "Fixture.cs"), """
                using System;
                namespace Agent.Interfaces
                {
                    public interface IPlugin { event EventHandler Changed; }
                    public class BasePlugin : IPlugin
                    {
                        public virtual event EventHandler? Changed;
                        public void Fire() => Changed?.Invoke(this, EventArgs.Empty);
                    }
                    public sealed class DerivedPlugin : BasePlugin
                    {
                        private new event EventHandler? Changed;
                        public int FirePrivate()
                        {
                            var count = 0;
                            Changed += (_, _) => count++;
                            Changed?.Invoke(this, EventArgs.Empty);
                            return count;
                        }
                    }
                    public sealed class OverridePlugin : BasePlugin
                    {
                        public override event EventHandler? Changed;
                        public void FireOverride() => Changed?.Invoke(this, EventArgs.Empty);
                    }
                    public sealed class ExplicitPlugin : IPlugin
                    {
                        private EventHandler? handlers;
                        event EventHandler IPlugin.Changed
                        {
                            add => handlers += value;
                            remove => handlers -= value;
                        }
                    }
                }
                public static class Subject
                {
                    public static int Run() => new Agent.Interfaces.DerivedPlugin().FirePrivate();
                }
                """);

            new SourceRewriter().Rewrite(new ObfuscationConfig(
                42, "11a1a29a-b1cd-5a47-907f-2d5defc9d24b", root, root, null));

            var rewritten = File.ReadAllText(Path.Combine(contracts, "Fixture.cs"));
            StringAssert.Contains(rewritten, "private new event EventHandler? Changed;");
            StringAssert.Contains(rewritten, "Changed += (_, _) => count++;");
            var bytes = CompileProjectSources(contracts);
            var assembly = context.LoadFromStream(new MemoryStream(bytes));
            Assert.AreEqual(1,
                assembly.GetType("Subject")!.GetMethod("Run")!.Invoke(null, null));
        }
        finally
        {
            context.Unload();
            TryDelete(root);
        }
    }

    private static byte[] CompileProjectSources(
        string directory,
        string assemblyName = "Fixture",
        params MetadataReference[] extraReferences)
    {
        var trees = Directory.GetFiles(directory, "*.cs")
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
            .Append(CSharpSyntaxTree.ParseText(
                "global using System; global using System.Linq;"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).Equals(
                "Agent.Models.dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => MetadataReference.CreateFromFile(path))
            .Concat(extraReferences);
        using var stream = new MemoryStream();
        var result = CSharpCompilation.Create(
                assemblyName, trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .Emit(stream);
        Assert.IsTrue(result.Success,
            string.Join(Environment.NewLine, result.Diagnostics));
        return stream.ToArray();
    }

    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
        + "<TargetFramework>net10.0</TargetFramework>"
        + "</PropertyGroup></Project>";

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(
            Path.GetTempPath(), $"source-rewriter-semantics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDelete(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { }
    }
}
