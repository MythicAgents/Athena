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
            var renameMap = transform.RenameAll(dir);

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
            var renameMap = transform.RenameAll(dir);

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
                    .RenameAll(dir1);
            var map2 =
                new AssemblyRenameTransform(seed: 99)
                    .RenameAll(dir2);

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
            var renameMap = transform.RenameAll(dir);

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
                transform.RenameAll(dir, skipFileRename: true);

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
                transform.RenameAll(dir, skipFileRename: true);

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

    [TestMethod]
    public void SameNameSameResult_AcrossDifferentBatchSizes()
    {
        // IMPORTANT: uses 8-assembly and 2-assembly batches with overlap.
        // A single-assembly batch would trivially pass — different sizes are required.
        var largeDir = CreateTempDir();
        var smallDir = CreateTempDir();
        try
        {
            string[] allNames =
            [
                "Workflow.Alpha", "Workflow.Beta", "Workflow.Gamma",
                "Workflow.Delta", "Workflow.Epsilon", "Workflow.Zeta",
                "Workflow.Eta", "Workflow.Theta"
            ];
            string[] sharedNames = ["Workflow.Alpha", "Workflow.Gamma"];

            foreach (var name in allNames)
                File.WriteAllBytes(
                    Path.Combine(largeDir, name + ".dll"),
                    CompileToDll("public class C {}", name));

            foreach (var name in sharedNames)
                File.WriteAllBytes(
                    Path.Combine(smallDir, name + ".dll"),
                    CompileToDll("public class C {}", name));

            var mapLarge = new AssemblyRenameTransform(seed: 77)
                .RenameAll(largeDir);
            var mapSmall = new AssemblyRenameTransform(seed: 77)
                .RenameAll(smallDir);

            foreach (var name in sharedNames)
                Assert.AreEqual(
                    mapLarge[name], mapSmall[name],
                    $"{name} must map to the same name regardless of batch size");
        }
        finally { TryDeleteDir(largeDir); TryDeleteDir(smallDir); }
    }

    [TestMethod]
    public void NoCollisions_WorkflowAssemblySet()
    {
        // The actual Workflow.* assembly names in the Athena codebase.
        // Two assemblies mapping to the same obfuscated name would cause
        // a runtime load failure that is very hard to diagnose.
        string[] workflowAssemblies =
        [
            "Workflow.Contracts",
            "Workflow.Models",
            "Workflow.Providers.Runtime",
            "Workflow.Providers.Script",
            "Workflow.Providers.Windows",
            "Workflow.Channels.Http",
            "Workflow.Channels.Smb",
            "Workflow.Channels.Websocket",
            "Workflow.Channels.Discord",
            "Workflow.Channels.GitHub",
            "Workflow.Security.AES",
            "ServiceHost",
        ];

        foreach (var seed in new[] { 42, 99, 12345, 0x7FFFFFFF, 1_000_000 })
        {
            var generated = new HashSet<string>();
            foreach (var name in workflowAssemblies)
            {
                var dir = CreateTempDir();
                try
                {
                    File.WriteAllBytes(
                        Path.Combine(dir, name + ".dll"),
                        CompileToDll("public class C {}", name));
                    var map = new AssemblyRenameTransform(seed: seed)
                        .RenameAll(dir);
                    var newName = map[name];
                    Assert.IsTrue(
                        generated.Add(newName),
                        $"Seed {seed}: collision — {name} → '{newName}' already used");
                }
                finally { TryDeleteDir(dir); }
            }
        }
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
        string? extraAssemblyName = null)
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
                OutputKind.DynamicallyLinkedLibrary));

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
