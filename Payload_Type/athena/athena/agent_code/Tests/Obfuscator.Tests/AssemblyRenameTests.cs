using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Obfuscator.IL.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class AssemblyRenameTests
{
    [TestMethod]
    public void EchoIdentity_MatchesKnownAgentModelsVector()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllBytes(
                Path.Combine(dir, "echo.dll"),
                CompileToDll("public class Echo {}", "echo"));

            var map = new AssemblyRenameTransform(546960503)
                .RenameAll(dir, ["echo"], [], skipFileRename: true);

            Assert.AreEqual("_a0lHf", map["echo"]);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void Identity_IsIndependentOfBatchContentsAndMatchesAgentModels()
    {
        const string uuid = "b42cd19c-8dee-5d8b-bb17-58588168d229";
        const int seed = 1959619248;
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        try
        {
            var echo = CompileToDll("public class Echo {}", "echo");
            File.WriteAllBytes(Path.Combine(dir1, "echo.dll"), echo);
            File.WriteAllBytes(Path.Combine(dir2, "echo.dll"), echo);
            File.WriteAllBytes(
                Path.Combine(dir2, "alpha.dll"),
                CompileToDll("public class Alpha {}", "alpha"));

            var map1 = new AssemblyRenameTransform(seed)
                .RenameAll(dir1, ["echo"], [], skipFileRename: true);
            var map2 = new AssemblyRenameTransform(seed)
                .RenameAll(dir2, ["echo"], [], skipFileRename: true);
            var agentModelsName = Agent.Utilities.AssemblyIdentity
                .GetObfuscatedName(uuid, "echo");

            Assert.AreEqual(agentModelsName, map1["echo"]);
            Assert.AreEqual(agentModelsName, map2["echo"]);
        }
        finally
        {
            TryDeleteDir(dir1);
            TryDeleteDir(dir2);
        }
    }

    [TestMethod]
    public void ExternalDependencies_AreNotRenamed()
    {
        var dir = CreateTempDir();
        try
        {
            foreach (var name in new[]
                     {
                         "Renci.SshNet",
                         "BouncyCastle.Cryptography",
                     })
            {
                File.WriteAllBytes(
                    Path.Combine(dir, name + ".dll"),
                    CompileToDll("public class Dependency {}", name));
            }

            var map = new AssemblyRenameTransform(42)
                .RenameAll(dir, [], []);

            Assert.IsFalse(map.ContainsKey("Renci.SshNet"));
            Assert.IsFalse(map.ContainsKey("BouncyCastle.Cryptography"));
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void QualifierShapedUnrelatedLiteral_DoesNotSuppressAssemblyRename()
    {
        var dir = CreateTempDir();
        try
        {
            const string source = """
                public static class Entry
                {
                    public static string Read() => "note, CurrentAssembly";
                }
                """;
            File.WriteAllBytes(Path.Combine(dir, "CurrentAssembly.dll"),
                CompileToDll(source, "CurrentAssembly"));

            var map = new AssemblyRenameTransform(seed: 42).RenameAll(
                dir, ["CurrentAssembly"], [], skipFileRename: true);

            Assert.IsTrue(map.ContainsKey("CurrentAssembly"));
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void BranchedLocalTypeName_PreservesAssemblyIdentityConservatively()
    {
        var dir = CreateTempDir();
        try
        {
            const string source = """
                using System;
                public sealed class Target { }
                public static class Entry
                {
                    public static Type Resolve(bool local)
                    {
                        string name;
                        if (local)
                            name = "Target, FlowAssembly";
                        else
                            name = "System.String, System.Private.CoreLib";
                        return Type.GetType(name, throwOnError: true)!;
                    }
                }
                """;
            File.WriteAllBytes(Path.Combine(dir, "FlowAssembly.dll"),
                CompileToDll(source, "FlowAssembly"));

            var map = new AssemblyRenameTransform(seed: 42).RenameAll(
                dir, ["FlowAssembly"], [], skipFileRename: true);

            Assert.IsFalse(map.ContainsKey("FlowAssembly"));
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void DirectStackMergeWithSelfQualifiedType_PreservesWorkingAssemblyIdentity()
    {
        var dir = CreateTempDir();
        try
        {
            const string source = """
                using System;
                public sealed class Target { }
                public static class Entry
                {
                    public static Type Resolve(bool flag) => Type.GetType(
                        !flag
                            ? "System.String, System.Private.CoreLib"
                            : "Target, CurrentAssembly",
                        throwOnError: true)!;
                }
                """;
            var path = Path.Combine(dir, "CurrentAssembly.dll");
            File.WriteAllBytes(path, CompileToDll(
                source, "CurrentAssembly",
                optimizationLevel: OptimizationLevel.Release));

            var map = new AssemblyRenameTransform(seed: 42).RenameAll(
                dir, ["CurrentAssembly"], [], skipFileRename: true);

            var loaded = Assembly.Load(File.ReadAllBytes(path));
            var resolve = loaded.GetType("Entry")!.GetMethod("Resolve")!;
            var local = (Type)resolve.Invoke(null, [true])!;
            var framework = (Type)resolve.Invoke(null, [false])!;

            Assert.AreEqual("Target", local.FullName);
            Assert.AreEqual("CurrentAssembly", local.Assembly.GetName().Name);
            Assert.AreEqual(typeof(string), framework);
            Assert.IsFalse(map.ContainsKey("CurrentAssembly"));
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void SelfAssemblyQualifiedTypeName_PreservesAssemblyIdentity()
    {
        var dir = CreateTempDir();
        try
        {
            const string source = """
                using System;
                public sealed class Target { }
                public static class Entry
                {
                    public static Type Resolve() =>
                        Type.GetType("Target, QualifiedIdentity", true)!;
                }
                """;
            File.WriteAllBytes(Path.Combine(dir, "QualifiedIdentity.dll"),
                CompileToDll(source, "QualifiedIdentity"));

            var map = new AssemblyRenameTransform(seed: 42).RenameAll(
                dir, ["QualifiedIdentity"], [], skipFileRename: true);

            Assert.IsFalse(map.ContainsKey("QualifiedIdentity"));
            using var stream = File.OpenRead(
                Path.Combine(dir, "QualifiedIdentity.dll"));
            using var assembly = AssemblyDefinition.ReadAssembly(stream);
            Assert.AreEqual("QualifiedIdentity", assembly.Name.Name);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RenamedAssembly_HasNewIdentity()
    {
        var dir = CreateTempDir();
        try
        {
            var dll = CompileToDll(
                "public class Foo {}",
                "Workflow.Models");
            File.WriteAllBytes(
                Path.Combine(dir, "Workflow.Models.dll"), dll);

            var transform =
                new AssemblyRenameTransform(seed: 42);
            var renameMap = transform.RenameAll(
                dir, ["Workflow.Models"], []);

            Assert.IsTrue(
                renameMap.ContainsKey("Workflow.Models"),
                "Should have renamed Workflow.Models");

            var newName = renameMap["Workflow.Models"];
            Assert.IsTrue(
                newName.StartsWith("_"),
                "Renamed name should start with _");

            var newPath = Path.Combine(
                dir, newName + ".dll");
            Assert.IsTrue(
                File.Exists(newPath),
                $"Renamed file {newPath} should exist");

            using var ms = new MemoryStream(
                File.ReadAllBytes(newPath));
            var asm = AssemblyDefinition.ReadAssembly(ms);
            Assert.AreEqual(newName, asm.Name.Name);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void ConsumingDll_ReferencesPatched()
    {
        var dir = CreateTempDir();
        try
        {
            var asmA = CompileToDll(
                "namespace Lib { public class Foo "
                + "{ public static int X() => 1; } }",
                "Workflow.Models");
            var asmB = CompileToDll(
                "public class Bar "
                + "{ public static int Y() "
                + "=> Lib.Foo.X(); }",
                "MyPlugin",
                extraAssemblyBytes: asmA,
                extraAssemblyName: "Workflow.Models");

            File.WriteAllBytes(
                Path.Combine(dir, "Workflow.Models.dll"),
                asmA);
            File.WriteAllBytes(
                Path.Combine(dir, "MyPlugin.dll"), asmB);

            var transform =
                new AssemblyRenameTransform(seed: 42);
            var renameMap = transform.RenameAll(
                dir, ["Workflow.Models", "MyPlugin"], []);

            var newModelName =
                renameMap["Workflow.Models"];

            var pluginNewName = renameMap["MyPlugin"];
            var pluginPath = Path.Combine(
                dir, pluginNewName + ".dll");
            using var ms = new MemoryStream(
                File.ReadAllBytes(pluginPath));
            var asm = AssemblyDefinition.ReadAssembly(ms);
            var refs = asm.MainModule.AssemblyReferences
                .Select(r => r.Name).ToList();

            Assert.IsTrue(
                refs.Contains(newModelName),
                "Should reference the renamed name");
            Assert.IsFalse(
                refs.Contains("Workflow.Models"),
                "Should not reference old name");
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void DeterministicNames_SameSeed()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        try
        {
            var dll = CompileToDll(
                "public class Foo {}",
                "Workflow.Models");
            File.WriteAllBytes(
                Path.Combine(dir1, "Workflow.Models.dll"),
                dll);
            File.WriteAllBytes(
                Path.Combine(dir2, "Workflow.Models.dll"),
                dll);

            var map1 =
                new AssemblyRenameTransform(seed: 99)
                    .RenameAll(dir1, ["Workflow.Models"], []);
            var map2 =
                new AssemblyRenameTransform(seed: 99)
                    .RenameAll(dir2, ["Workflow.Models"], []);

            Assert.AreEqual(
                map1["Workflow.Models"],
                map2["Workflow.Models"]);
        }
        finally
        {
            TryDeleteDir(dir1);
            TryDeleteDir(dir2);
        }
    }

    [TestMethod]
    public void FrameworkAssemblies_NotRenamed()
    {
        var dir = CreateTempDir();
        try
        {
            var dll = CompileToDll(
                "public class Sys {}",
                "System.Runtime");
            File.WriteAllBytes(
                Path.Combine(dir, "System.Runtime.dll"),
                dll);

            var transform =
                new AssemblyRenameTransform(seed: 42);
            var renameMap = transform.RenameAll(dir, [], []);

            Assert.IsFalse(
                renameMap.ContainsKey("System.Runtime"),
                "Framework assemblies should be skipped");
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void SkipFileRename_FilesNotMoved()
    {
        var dir = CreateTempDir();
        try
        {
            var dll = CompileToDll(
                "public class Foo {}",
                "Workflow.Models");
            File.WriteAllBytes(
                Path.Combine(dir, "Workflow.Models.dll"), dll);

            var transform =
                new AssemblyRenameTransform(seed: 42);
            var renameMap =
                transform.RenameAll(
                    dir, ["Workflow.Models"], [], skipFileRename: true);

            // Map should be populated
            Assert.IsTrue(
                renameMap.ContainsKey("Workflow.Models"),
                "Rename map should contain original name");

            // Original file should still exist
            Assert.IsTrue(
                File.Exists(Path.Combine(
                    dir, "Workflow.Models.dll")),
                "Original file should not be moved");

            var newName = renameMap["Workflow.Models"];
            // Renamed file should NOT exist
            Assert.IsFalse(
                File.Exists(Path.Combine(
                    dir, newName + ".dll")),
                "Physical rename should be skipped");
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void SkipFileRename_PeIdentityStillObfuscated()
    {
        var dir = CreateTempDir();
        try
        {
            var dll = CompileToDll(
                "public class Foo {}",
                "Workflow.Models");
            File.WriteAllBytes(
                Path.Combine(dir, "Workflow.Models.dll"), dll);

            var transform =
                new AssemblyRenameTransform(seed: 42);
            var renameMap =
                transform.RenameAll(
                    dir, ["Workflow.Models"], [], skipFileRename: true);

            var newName = renameMap["Workflow.Models"];
            var originalPath = Path.Combine(
                dir, "Workflow.Models.dll");

            // File still at original path — read its PE identity
            using var ms = new MemoryStream(
                File.ReadAllBytes(originalPath));
            var asm = Mono.Cecil.AssemblyDefinition
                .ReadAssembly(ms);

            Assert.AreEqual(
                newName, asm.Name.Name,
                "PE identity should be obfuscated "
                + "even when file rename is skipped");
        }
        finally { TryDeleteDir(dir); }
    }

    // --- Helpers ---

    private static string CreateTempDir()
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "asmrename_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDir(string path)
    {
        try { Directory.Delete(path, true); } catch { }
    }

    private static byte[] CompileToDll(
        string source,
        string assemblyName,
        byte[]? extraAssemblyBytes = null,
        string? extraAssemblyName = null,
        OptimizationLevel optimizationLevel = OptimizationLevel.Debug)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var trustedDir = Path.GetDirectoryName(
            typeof(object).Assembly.Location)!;

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(
                typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(
                Path.Combine(
                    trustedDir,
                    "System.Collections.dll")),
        };

        if (extraAssemblyBytes is not null)
            references.Add(
                MetadataReference.CreateFromImage(
                    extraAssemblyBytes));

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: optimizationLevel));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d =>
                    d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            throw new InvalidOperationException(
                "Compilation failed:\n"
                + string.Join("\n", errors));
        }
        return ms.ToArray();
    }
}
