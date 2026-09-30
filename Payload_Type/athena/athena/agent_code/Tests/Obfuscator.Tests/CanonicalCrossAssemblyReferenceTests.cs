using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.IL;

namespace Obfuscator.Tests;

[TestClass]
public class CanonicalCrossAssemblyReferenceTests
{
    [TestMethod]
    public void RewriteBatch_PreservesSameNamedFieldsAndNestedTypeReferences()
    {
        const string producerSource = """
            namespace Producer;

            public static class Alpha
            {
                public static int Value = 11;
            }

            public static class Beta
            {
                public static int Value = 31;
            }

            public class Outer
            {
                public class Nested
                {
                    public int Number;
                    public Nested(int number) { Number = number; }
                }
            }
            """;
        const string consumerSource = """
            public static class Consumer
            {
                public static int Run()
                {
                    var nested = new Producer.Outer.Nested(7);
                    return Producer.Alpha.Value * 10000
                        + Producer.Beta.Value * 100
                        + nested.Number;
                }
            }
            """;

        var directory = Path.Combine(
            Path.GetTempPath(), $"canonical_xref_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var producer = CompileToDll(producerSource, "Producer");
            var consumer = CompileToDll(
                consumerSource,
                "Consumer",
                MetadataReference.CreateFromImage(producer));
            var producerPath = Path.Combine(directory, "Producer.dll");
            var consumerPath = Path.Combine(directory, "Consumer.dll");
            File.WriteAllBytes(producerPath, producer);
            File.WriteAllBytes(consumerPath, consumer);

            new ILRewriter().RewriteBatch(
                directory,
                seed: 42,
                mapPath: null,
                firstPartyAssemblyNames: ["Producer", "Consumer"],
                skipFileRename: true,
                skipAssemblyRename: true);

            var loadContext = new AssemblyLoadContext(
                $"canonical_xref_{Guid.NewGuid():N}", isCollectible: true);
            loadContext.Resolving += (_, name) =>
                name.Name == "Producer"
                    ? loadContext.LoadFromAssemblyPath(producerPath)
                    : null;
            try
            {
                var assembly = loadContext.LoadFromAssemblyPath(consumerPath);
                var run = assembly.GetTypes()
                    .SelectMany(type => type.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Static))
                    .Single(method => method.ReturnType == typeof(int)
                        && method.GetParameters().Length == 0);

                Assert.AreEqual(113107, run.Invoke(null, null));
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch { }
        }
    }

    [TestMethod]
    public void RewriteBatch_PatchesOverloadedMethodsAndIndexersAcrossAssemblies()
    {
        const string producerSource = """
            using System.Runtime.CompilerServices;
            [assembly: InternalsVisibleTo("Consumer")]

            namespace Producer;

            internal static class Overloads
            {
                internal static int Select(int value) => value + 10;
                internal static int Select(string value) => value.Length + 20;
            }

            internal sealed class IndexedValues
            {
                internal int this[int value] => value + 30;
                internal int this[string value] => value.Length + 40;
            }
            """;
        const string consumerSource = """
            public static class ConsumerEntry
            {
                public static int Run()
                {
                    var indexed = new Producer.IndexedValues();
                    return Producer.Overloads.Select(1) * 1000000
                        + Producer.Overloads.Select("ab") * 10000
                        + indexed[3] * 100
                        + indexed["wxyz"];
                }
            }
            """;

        var directory = Path.Combine(
            Path.GetTempPath(), $"overload_xref_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var producer = CompileToDll(producerSource, "Producer");
            var consumer = CompileToDll(
                consumerSource,
                "Consumer",
                MetadataReference.CreateFromImage(producer));
            var producerPath = Path.Combine(directory, "Producer.dll");
            var consumerPath = Path.Combine(directory, "Consumer.dll");
            File.WriteAllBytes(producerPath, producer);
            File.WriteAllBytes(consumerPath, consumer);

            new ILRewriter().RewriteBatch(
                directory,
                seed: 42,
                mapPath: null,
                firstPartyAssemblyNames: ["Producer", "Consumer"],
                skipFileRename: true,
                skipAssemblyRename: true);

            var loadContext = new AssemblyLoadContext(
                $"overload_xref_{Guid.NewGuid():N}", isCollectible: true);
            loadContext.Resolving += (_, name) =>
                name.Name == "Producer"
                    ? loadContext.LoadFromAssemblyPath(producerPath)
                    : null;
            try
            {
                var assembly = loadContext.LoadFromAssemblyPath(consumerPath);
                var run = assembly.GetTypes()
                    .SelectMany(type => type.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Static))
                    .Single(method => method.ReturnType == typeof(int)
                        && method.GetParameters().Length == 0);

                Assert.AreEqual(11223344, run.Invoke(null, null));
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch { }
        }
    }

    [TestMethod]
    public void RewriteBatch_PatchesInternalPropertyAccessorsAcrossAssemblies()
    {
        const string producerSource = """
            using System.Runtime.CompilerServices;
            [assembly: InternalsVisibleTo("Consumer")]

            namespace Producer;
            public static class State
            {
                internal static int Value { get; set; }
            }
            """;
        const string consumerSource = """
            public static class ConsumerEntry
            {
                public static int Run()
                {
                    Producer.State.Value = 42;
                    return Producer.State.Value;
                }
            }
            """;

        var directory = Path.Combine(
            Path.GetTempPath(), $"property_xref_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var producer = CompileToDll(producerSource, "Producer");
            var consumer = CompileToDll(
                consumerSource,
                "Consumer",
                MetadataReference.CreateFromImage(producer));
            var producerPath = Path.Combine(directory, "Producer.dll");
            var consumerPath = Path.Combine(directory, "Consumer.dll");
            File.WriteAllBytes(producerPath, producer);
            File.WriteAllBytes(consumerPath, consumer);

            new ILRewriter().RewriteBatch(
                directory,
                seed: 42,
                mapPath: null,
                firstPartyAssemblyNames: ["Producer", "Consumer"],
                skipFileRename: true,
                skipAssemblyRename: true);

            var loadContext = new AssemblyLoadContext(
                $"property_xref_{Guid.NewGuid():N}", isCollectible: true);
            loadContext.Resolving += (_, name) =>
                name.Name == "Producer"
                    ? loadContext.LoadFromAssemblyPath(producerPath)
                    : null;
            try
            {
                var assembly = loadContext.LoadFromAssemblyPath(consumerPath);
                var run = assembly.GetTypes()
                    .SelectMany(type => type.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Static))
                    .Single(method => method.ReturnType == typeof(int)
                        && method.GetParameters().Length == 0);

                Assert.AreEqual(42, run.Invoke(null, null));
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch { }
        }
    }

    [TestMethod]
    public void RewriteBatch_DistinguishesAssemblyScopedComplexSignatures()
    {
        const string dependencySource = """
            namespace Shared;
            public sealed class Value { }
            """;
        const string producerSource = """
            extern alias DependencyA;
            extern alias DependencyB;
            using System.Runtime.CompilerServices;
            [assembly: InternalsVisibleTo("Consumer")]

            namespace Producer;

            internal sealed class Envelope<T>
            {
                internal sealed class Nested<U> { }
            }

            internal static class Overloads
            {
                internal static int Select(DependencyA::Shared.Value value) => 1;
                internal static int Select(DependencyB::Shared.Value value) => 2;

                internal static int Select<T>(
                    Envelope<DependencyA::Shared.Value>.Nested<T> value,
                    ref DependencyA::Shared.Value[,] items) => 3;

                internal static int Select<T>(
                    Envelope<DependencyB::Shared.Value>.Nested<T> value,
                    ref DependencyB::Shared.Value[,] items) => 4;

                internal static int Select<T, U>(Envelope<T>.Nested<U> value) => 5;
            }

            internal sealed class IndexedValues
            {
                internal int this[DependencyA::Shared.Value value] => 6;
                internal int this[DependencyB::Shared.Value value] => 7;
            }
            """;
        const string consumerSource = """
            extern alias DependencyA;
            extern alias DependencyB;

            public static class ConsumerEntry
            {
                public static int Run()
                {
                    var a = new DependencyA::Shared.Value();
                    var b = new DependencyB::Shared.Value();
                    var aArray = new DependencyA::Shared.Value[1, 1];
                    var bArray = new DependencyB::Shared.Value[1, 1];
                    var indexed = new Producer.IndexedValues();
                    return Producer.Overloads.Select(a) * 1000000
                        + Producer.Overloads.Select(b) * 100000
                        + Producer.Overloads.Select<int>(
                            new Producer.Envelope<DependencyA::Shared.Value>.Nested<int>(),
                            ref aArray) * 10000
                        + Producer.Overloads.Select<int>(
                            new Producer.Envelope<DependencyB::Shared.Value>.Nested<int>(),
                            ref bArray) * 1000
                        + Producer.Overloads.Select<int, string>(
                            new Producer.Envelope<int>.Nested<string>()) * 100
                        + indexed[a] * 10
                        + indexed[b];
                }
            }
            """;

        var directory = Path.Combine(
            Path.GetTempPath(), $"assembly_scope_xref_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var dependencyA = CompileToDll(dependencySource, "DependencyA");
            var dependencyB = CompileToDll(dependencySource, "DependencyB");
            var dependencyAReference = MetadataReference.CreateFromImage(
                dependencyA,
                new MetadataReferenceProperties(aliases: ["DependencyA"]));
            var dependencyBReference = MetadataReference.CreateFromImage(
                dependencyB,
                new MetadataReferenceProperties(aliases: ["DependencyB"]));
            var producer = CompileToDll(
                producerSource, "Producer",
                dependencyAReference, dependencyBReference);
            var consumer = CompileToDll(
                consumerSource, "Consumer",
                MetadataReference.CreateFromImage(producer),
                dependencyAReference, dependencyBReference);

            var dependencyAPath = Path.Combine(directory, "DependencyA.dll");
            var dependencyBPath = Path.Combine(directory, "DependencyB.dll");
            var producerPath = Path.Combine(directory, "Producer.dll");
            var consumerPath = Path.Combine(directory, "Consumer.dll");
            File.WriteAllBytes(dependencyAPath, dependencyA);
            File.WriteAllBytes(dependencyBPath, dependencyB);
            File.WriteAllBytes(producerPath, producer);
            File.WriteAllBytes(consumerPath, consumer);

            new ILRewriter().RewriteBatch(
                directory,
                seed: 42,
                mapPath: null,
                firstPartyAssemblyNames:
                    ["DependencyA", "DependencyB", "Producer", "Consumer"],
                skipFileRename: true,
                skipAssemblyRename: true);

            var loadContext = new AssemblyLoadContext(
                $"assembly_scope_xref_{Guid.NewGuid():N}", isCollectible: true);
            loadContext.Resolving += (_, name) => name.Name switch
            {
                "DependencyA" => loadContext.LoadFromAssemblyPath(dependencyAPath),
                "DependencyB" => loadContext.LoadFromAssemblyPath(dependencyBPath),
                "Producer" => loadContext.LoadFromAssemblyPath(producerPath),
                _ => null,
            };
            try
            {
                var assembly = loadContext.LoadFromAssemblyPath(consumerPath);
                var run = assembly.GetTypes()
                    .SelectMany(type => type.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Static))
                    .Single(method => method.ReturnType == typeof(int)
                        && method.GetParameters().Length == 0);

                Assert.AreEqual(1234567, run.Invoke(null, null));
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch { }
        }
    }

    private static byte[] CompileToDll(
        string source,
        string assemblyName,
        params MetadataReference[] extraReferences)
    {
        var trustedDirectory = Path.GetDirectoryName(
            typeof(object).Assembly.Location)!;
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(
                Path.Combine(trustedDirectory, "System.Collections.dll")),
        };
        references.AddRange(extraReferences);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                "Compilation failed:\n"
                + string.Join("\n", result.Diagnostics
                    .Where(diagnostic =>
                        diagnostic.Severity == DiagnosticSeverity.Error)));
        }

        return stream.ToArray();
    }
}
