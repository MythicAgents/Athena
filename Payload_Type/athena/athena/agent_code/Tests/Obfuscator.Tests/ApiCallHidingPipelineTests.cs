using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Config;
using Obfuscator.Source;

namespace Obfuscator.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ApiCallHidingPipelineTests
{
    [TestMethod]
    public void SourcePipeline_ExpressionBodiedVoidSensitiveCallCompilesAndExecutes()
    {
        const string source = """
            public static class Subject
            {
                public static void Save(string path) =>
                    System.IO.File.WriteAllText(path, "data");
            }
            """;
        var directory = CreateRewriteDirectory(source);
        var context = new AssemblyLoadContext(
            $"api-expression-bodied-void-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            new SourceRewriter().Rewrite(new ObfuscationConfig(
                Seed: 42,
                Uuid: null,
                InputPath: directory,
                OutputPath: directory,
                MapPath: null));
            var assemblyPath = CompileDirectory(directory);
            var assembly = context.LoadFromStream(
                new MemoryStream(File.ReadAllBytes(assemblyPath)));
            var outputPath = Path.Combine(directory, "output.txt");
            assembly.GetType("Subject")!.GetMethod(
                "Save", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [outputPath]);

            Assert.AreEqual("data", File.ReadAllText(outputPath));
        }
        finally
        {
            context.Unload();
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_OutOfOrderNamedArgumentsBindToDeclaredParameters()
    {
        const string source = """
            public static class Subject
            {
                public static void Run(string directory)
                {
                    var target = System.IO.Path.Combine(directory, "expected.txt");
                    var content = System.IO.Path.Combine(directory, "wrong.txt");
                    System.IO.File.WriteAllText(contents: content, path: target);
                }
            }
            """;
        var directory = CreateRewriteDirectory(source);
        var context = new AssemblyLoadContext(
            $"api-named-arguments-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            new SourceRewriter().Rewrite(new ObfuscationConfig(
                Seed: 42,
                Uuid: null,
                InputPath: directory,
                OutputPath: directory,
                MapPath: null));
            var assemblyPath = CompileDirectory(directory);
            var assembly = context.LoadFromStream(
                new MemoryStream(File.ReadAllBytes(assemblyPath)));
            assembly.GetType("Subject")!.GetMethod(
                "Run", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [directory]);

            var expectedPath = Path.Combine(directory, "expected.txt");
            var wrongPath = Path.Combine(directory, "wrong.txt");
            Assert.IsTrue(File.Exists(expectedPath));
            Assert.AreEqual(wrongPath, File.ReadAllText(expectedPath));
            Assert.IsFalse(File.Exists(wrongPath));
        }
        finally
        {
            context.Unload();
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_PreservesStaticReturnTypeForExtensionBinding()
    {
        const string source = """
            public static class Subject
            {
                public static string Run()
                {
                    var profileAssembly = System.Reflection.Assembly.Load(
                        typeof(Subject).Assembly.GetName());
                    return new AssemblyRegistry().Describe(profileAssembly);
                }
            }

            public sealed class AssemblyRegistry;

            public static class AssemblyExtensions
            {
                public static string Describe(
                    this AssemblyRegistry registry,
                    System.Reflection.Assembly assembly) =>
                    assembly.GetName().Name!;
            }
            """;
        var directory = CreateRewriteDirectory(source);
        var context = new AssemblyLoadContext(
            $"api-static-return-type-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            new SourceRewriter().Rewrite(new ObfuscationConfig(
                Seed: 42,
                Uuid: null,
                InputPath: directory,
                OutputPath: directory,
                MapPath: null));
            var assemblyPath = CompileDirectory(directory);
            var assembly = context.LoadFromStream(
                new MemoryStream(File.ReadAllBytes(assemblyPath)));
            var result = assembly.GetType("Subject")!.GetMethod(
                "Run", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, null);

            Assert.AreEqual("Fixture", result);
        }
        finally
        {
            context.Unload();
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void SourcePipeline_ReorderedNamedArgumentsPreserveSourceEvaluationOrder()
    {
        const string source = """
            public static class Subject
            {
                private static readonly System.Collections.Generic.List<string> Trace = [];

                private static string Mark(string label, string value)
                {
                    Trace.Add(label);
                    return value;
                }

                public static string Run(string directory)
                {
                    var target = System.IO.Path.Combine(directory, "expected.txt");
                    var content = System.IO.Path.Combine(directory, "wrong.txt");
                    System.IO.File.WriteAllText(
                        contents: Mark("content", content),
                        path: Mark("path", target));
                    return string.Join(",", Trace);
                }
            }
            """;
        var directory = CreateRewriteDirectory(source);
        var context = new AssemblyLoadContext(
            $"api-evaluation-order-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            new SourceRewriter().Rewrite(new ObfuscationConfig(
                Seed: 42,
                Uuid: null,
                InputPath: directory,
                OutputPath: directory,
                MapPath: null));
            var assemblyPath = CompileDirectory(directory);
            var assembly = context.LoadFromStream(
                new MemoryStream(File.ReadAllBytes(assemblyPath)));
            var trace = assembly.GetType("Subject")!.GetMethod(
                "Run", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [directory]);

            Assert.AreEqual("content,path", trace);
            var expectedPath = Path.Combine(directory, "expected.txt");
            var wrongPath = Path.Combine(directory, "wrong.txt");
            Assert.IsTrue(File.Exists(expectedPath));
            Assert.AreEqual(wrongPath, File.ReadAllText(expectedPath));
            Assert.IsFalse(File.Exists(wrongPath));
        }
        finally
        {
            context.Unload();
            TryDelete(directory);
        }
    }

    private static string CreateRewriteDirectory(string source)
    {
        var directory = Path.Combine(
            Path.GetTempPath(), $"api-call-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Fixture.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
            + "<TargetFramework>net10.0</TargetFramework>"
            + "</PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(directory, "Fixture.cs"), source);
        return directory;
    }

    private static string CompileDirectory(string directory)
    {
        var trees = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(path => CSharpSyntaxTree.ParseText(
                File.ReadAllText(path), path: path))
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

    private static void TryDelete(string directory)
    {
        try { Directory.Delete(directory, recursive: true); } catch { }
    }
}
