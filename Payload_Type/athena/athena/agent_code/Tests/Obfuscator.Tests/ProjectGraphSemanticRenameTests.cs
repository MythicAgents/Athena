using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Source;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public sealed class ProjectGraphSemanticRenameTests
{
    [TestMethod]
    public void Transform_LoadsOnlySelectedGraphAndUsesPostTransformTrees()
    {
        using var fixture = new ProjectGraphFixture();
        var rootSource = fixture.PathOf("Core/Core.cs");
        var originalTree = CSharpSyntaxTree.ParseText(File.ReadAllText(rootSource), path: rootSource);
        var postTransformTree = CSharpSyntaxTree.ParseText(
            originalTree.GetText().ToString().Replace("PriorMarker", "PostTransformName"),
            path: rootSource);

        var result = AgentSemanticProjectGraphRenamer.Transform(
            fixture.Root,
            fixture.PathOf("Core/Core.csproj"),
            new Dictionary<string, SyntaxTree>(StringComparer.Ordinal)
            {
                [rootSource] = postTransformTree,
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Configuration"] = "Release",
                ["HandlerOS"] = "linux",
                ["CryptoProvider"] = "Aes",
            },
            Guid.Parse("12345678-1234-5678-9abc-123456789abc"),
            42);

        CollectionAssert.AreEquivalent(
            new[] { "Contracts.csproj", "Core.csproj", "Linux.csproj" },
            result.ProjectPaths.Select(Path.GetFileName).ToArray());
        Assert.IsFalse(result.ProjectPaths.Any(path => path.EndsWith("Windows.csproj", StringComparison.Ordinal)));
        Assert.IsTrue(result.Plan.NamesBySymbolKey.Keys.Any(key => key.Contains("PostTransformName", StringComparison.Ordinal)));
        Assert.IsFalse(result.Plan.NamesBySymbolKey.Keys.Any(key => key.Contains("PriorMarker", StringComparison.Ordinal)));

        var rewritten = result.Documents[rootSource];
        Assert.IsFalse(rewritten.Contains("PostTransformName", StringComparison.Ordinal));
        StringAssert.Contains(rewritten, "wire_value");
        StringAssert.Contains(rewritten, "JsonProperty");
        StringAssert.Contains(File.ReadAllText(fixture.PathOf("Windows/Windows.cs")), "WindowsOnlyName");
        foreach (var compilation in result.Compilations)
            Assert.IsFalse(compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
                string.Join(Environment.NewLine, compilation.GetDiagnostics()));
    }

    [TestMethod]
    public void SourceRewriter_RebindsSharedRootNamespaceAcrossReferencedProjects()
    {
        using var fixture = new SharedRootNamespaceFixture();
        new SourceRewriter().Rewrite(new Obfuscator.Config.ObfuscationConfig(
            Seed: 42,
            Uuid: "12345678-1234-5678-9abc-123456789abc",
            InputPath: fixture.Root,
            OutputPath: fixture.Root,
            MapPath: null,
            EnableBroadSemanticRename: true,
            ProjectRoot: fixture.PathOf("Cat/Cat.csproj"),
            Configuration: "Release",
            HandlerOS: "linux",
            CryptoProvider: "Aes"));

        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            WorkingDirectory = fixture.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "run", "--project", fixture.PathOf("Cat/Cat.csproj"),
                     "--no-restore", "--nologo", "-p:Configuration=Release",
                     "-p:HandlerOS=linux", "-p:CryptoProvider=Aes" })
            start.ArgumentList.Add(argument);
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = System.Diagnostics.Process.Start(start)!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(120_000), "Nested fixture run timed out.");
        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        Assert.AreEqual(0, process.ExitCode, output + Environment.NewLine + error);
        Assert.AreEqual("42", output.Trim());
    }

    [TestMethod]
    public void SourceRewriter_RebindsRenamedTypesAcrossDirectAndTransitiveProjectReferences()
    {
        using var fixture = new RenamedTypeDependencyGraphFixture();
        new SourceRewriter().Rewrite(new Obfuscator.Config.ObfuscationConfig(
            Seed: 42,
            Uuid: "12345678-1234-5678-9abc-123456789abc",
            InputPath: fixture.Root,
            OutputPath: fixture.Root,
            MapPath: null,
            EnableBroadSemanticRename: true,
            ProjectRoot: fixture.PathOf("Core/Core.csproj"),
            Configuration: "Release",
            HandlerOS: "linux",
            CryptoProvider: "Aes"));

        var rewrittenSources = string.Join("\n", Directory.GetFiles(
            fixture.Root, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.IsFalse(rewrittenSources.Contains("IAgent", StringComparison.Ordinal));
        Assert.IsFalse(rewrittenSources.Contains("IMod", StringComparison.Ordinal));

        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            WorkingDirectory = fixture.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "run", "--project", fixture.PathOf("Core/Core.csproj"),
                     "--no-restore", "--nologo", "-p:Configuration=Release",
                     "-p:HandlerOS=linux", "-p:CryptoProvider=Aes" })
            start.ArgumentList.Add(argument);
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = System.Diagnostics.Process.Start(start)!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(120_000), "Nested fixture run timed out.");
        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        Assert.AreEqual(0, process.ExitCode, output + Environment.NewLine + error);
        Assert.AreEqual("agent:one,two", output.Trim());
    }

    [TestMethod]
    public void SourceRewriter_ExplicitGraphModeWritesCompilableSelectedWorkspace()
    {
        using var fixture = new ProjectGraphFixture();
        new SourceRewriter().Rewrite(new Obfuscator.Config.ObfuscationConfig(
            Seed: 42,
            Uuid: "12345678-1234-5678-9abc-123456789abc",
            InputPath: fixture.Root,
            OutputPath: fixture.Root,
            MapPath: null,
            EnableBroadSemanticRename: true,
            ProjectRoot: fixture.PathOf("Core/Core.csproj"),
            Configuration: "Release",
            HandlerOS: "linux",
            CryptoProvider: "Aes"));

        var selectedText = string.Join("\n", new[]
        {
            File.ReadAllText(fixture.PathOf("Core/Core.cs")),
            File.ReadAllText(fixture.PathOf("Linux/Linux.cs")),
            File.ReadAllText(fixture.PathOf("Contracts/Contract.cs")),
        });
        foreach (var original in new[] { "PriorMarker", "Run", "localValue", "LinuxWorker" })
            Assert.IsFalse(selectedText.Contains(original, StringComparison.Ordinal), original);
        StringAssert.Contains(selectedText, "wire_value");
        StringAssert.Contains(File.ReadAllText(fixture.PathOf("Windows/Windows.cs")), "WindowsOnlyName");

        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            WorkingDirectory = fixture.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "build", fixture.PathOf("Core/Core.csproj"), "--no-restore", "--nologo",
                     "-p:Configuration=Release", "-p:HandlerOS=linux", "-p:CryptoProvider=Aes" })
            start.ArgumentList.Add(argument);
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = System.Diagnostics.Process.Start(start)!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(120_000), "Nested fixture build timed out.");
        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        Assert.AreEqual(0, process.ExitCode, output + Environment.NewLine + error);
    }

    private sealed class SharedRootNamespaceFixture : IDisposable
    {
        public SharedRootNamespaceFixture()
        {
            Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"semantic_shared_root_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            Write("Models/Models.csproj", Project());
            Write("Models/AgentRoot.cs", "namespace Agent { internal sealed class ModelsRoot { } }");
            Write("Models/Model.cs", "namespace Agent.Models; public sealed class Value { public int Number => 40; }");
            Write("Utilities/Utilities.csproj", Project());
            Write("Utilities/AgentRoot.cs", "namespace Agent { internal sealed class UtilitiesRoot { } }");
            Write("Utilities/Calculator.cs", "namespace Agent.Utilities; public static class Calculator { public static int Add(int left, int right) => left + right; }");
            Write("Cat/Cat.csproj", Project("../Models/Models.csproj", "../Utilities/Utilities.csproj", executable: true));
            Write("Cat/Program.cs", """
                using Agent.Models;
                using Agent.Utilities;
                namespace Agent;
                public static class Program
                {
                    public static void Main() => Console.WriteLine(Calculator.Add(new Value().Number, 2));
                }
                """);
        }

        public string Root { get; }
        public string PathOf(string relative) =>
            System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, relative));

        private void Write(string relative, string content)
        {
            var path = PathOf(relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        private static string Project(
            string? firstReference = null,
            string? secondReference = null,
            bool executable = false)
        {
            var references = new[] { firstReference, secondReference }
                .Where(reference => reference is not null)
                .Select(reference => $"<ProjectReference Include=\"{reference}\" />");
            return $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>{(executable ? "Exe" : "Library")}</OutputType>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                  <ItemGroup>{string.Join(Environment.NewLine, references)}</ItemGroup>
                </Project>
                """;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class RenamedTypeDependencyGraphFixture : IDisposable
    {
        public RenamedTypeDependencyGraphFixture()
        {
            Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"semantic_renamed_type_graph_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            Write("Models/Models.csproj", Project());
            Write("Models/Contracts.cs", """
                namespace Agent.Models;
                public interface IAgent { string Name { get; } }
                public interface IMod { string Name { get; } }
                public sealed class AgentImpl : IAgent { public string Name => "agent"; }
                public sealed class Mod : IMod
                {
                    public Mod(string name) => Name = name;
                    public string Name { get; }
                }
                """);
            Write("Managers/Managers.csproj", Project("../Models/Models.csproj"));
            Write("Managers/Manager.cs", """
                using Agent.Models;
                namespace Agent.Managers;
                public sealed class Manager
                {
                    public IAgent Resolve() => new AgentImpl();
                    public IReadOnlyList<IMod> Mods { get; } = new IMod[] { new Mod("one"), new Mod("two") };
                }
                """);
            Write("Core/Core.csproj", Project(
                "../Models/Models.csproj", "../Managers/Managers.csproj", executable: true)
                .Replace("Include=\"../Models/Models.csproj\"",
                    "Include=\"../Models/Models.csproj\" Aliases=\"global,models\"",
                    StringComparison.Ordinal));
            Write("Core/Program.cs", """
                using Agent.Managers;
                using Agent.Models;
                namespace Agent.Core;
                public static class Program
                {
                    public static void Main()
                    {
                        var manager = new Manager();
                        IAgent agent = manager.Resolve();
                        var names = new List<string>();
                        foreach (var mod in manager.Mods)
                            names.Add(mod.Name);
                        Console.WriteLine($"{agent.Name}:{string.Join(',', names)}");
                    }
                }
                """);
        }

        public string Root { get; }
        public string PathOf(string relative) =>
            System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, relative));

        private void Write(string relative, string content)
        {
            var path = PathOf(relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        private static string Project(
            string? firstReference = null,
            string? secondReference = null,
            bool executable = false)
        {
            var references = new[] { firstReference, secondReference }
                .Where(reference => reference is not null)
                .Select(reference => $"<ProjectReference Include=\"{reference}\" />");
            return $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>{(executable ? "Exe" : "Library")}</OutputType>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                  <ItemGroup>{string.Join(Environment.NewLine, references)}</ItemGroup>
                </Project>
                """;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class ProjectGraphFixture : IDisposable
    {
        public ProjectGraphFixture()
        {
            Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"semantic_graph_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            Write("Contracts/Contracts.csproj", Project());
            Write("Contracts/Contract.cs", "namespace Contracts; public interface IContract { int Execute(int value); }");
            Write("Linux/Linux.csproj", Project("../Contracts/Contracts.csproj"));
            Write("Linux/Linux.cs", "using Contracts; namespace Platform; public sealed class LinuxWorker : IContract { public int Execute(int value) => value + 1; }");
            Write("Windows/Windows.csproj", Project("../Contracts/Contracts.csproj"));
            Write("Windows/Windows.cs", "namespace Platform; public sealed class WindowsOnlyName { }");
            Write("Core/Core.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                    <ProjectReference Include="../Contracts/Contracts.csproj" />
                    <ProjectReference Condition="'$(HandlerOS)' == 'linux'" Include="../Linux/Linux.csproj" />
                    <ProjectReference Condition="'$(HandlerOS)' == 'windows'" Include="../Windows/Windows.csproj" />
                  </ItemGroup>
                </Project>
                """);
            Write("Core/Core.cs", """
                using Contracts;
                using Newtonsoft.Json;
                using Platform;
                using System.Text.Json;
                using System.Text.Json.Serialization;
                namespace Payload;
                public sealed class PriorMarker
                {
                    [JsonProperty("wire_value")]
                    public int WireValue { get; set; }
                    public int Run(int localValue)
                    {
                        IContract worker = new LinuxWorker();
                        return worker.Execute(localValue) + WireValue;
                    }
                    public string Serialize() => System.Text.Json.JsonSerializer.Serialize(
                        this, PriorMarkerJsonContext.Default.PriorMarker);
                }
                [JsonSerializable(typeof(PriorMarker))]
                [JsonSerializable(typeof(string))]
                internal partial class PriorMarkerJsonContext : JsonSerializerContext { }
                """);
        }

        public string Root { get; }
        public string PathOf(string relative) => System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, relative));

        private void Write(string relative, string content)
        {
            var path = PathOf(relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        private static string Project(string? reference = null) => $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
              {(reference is null ? "" : $"<ItemGroup><ProjectReference Include=\"{reference}\" /></ItemGroup>")}
            </Project>
            """;

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
