using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Obfuscator.IL;

namespace Obfuscator.Tests;

[TestClass]
public class BatchRewriteTests
{
    [TestMethod]
    public void RewriteBatch_RenamesTypesAndAssemblies()
    {
        var dir = CreateTempDir();
        try
        {
            var libBytes = CompileToDll(
                "namespace Lib {\n"
                + "  public class Helper {\n"
                + "    public static int Add(int a, int b)"
                + " => a + b;\n"
                + "  }\n"
                + "}",
                "Workflow.Models");

            var appBytes = CompileToDll(
                "public class App {\n"
                + "  public static int Run()"
                + " => Lib.Helper.Add(3, 4);\n"
                + "}",
                "ServiceHost",
                extraAssemblyBytes: libBytes,
                extraAssemblyName: "Workflow.Models");

            File.WriteAllBytes(
                Path.Combine(dir, "Workflow.Models.dll"),
                libBytes);
            File.WriteAllBytes(
                Path.Combine(dir, "ServiceHost.dll"),
                appBytes);

            var rewriter = new ILRewriter();
            rewriter.RewriteBatch(
                dir, seed: 42, mapPath: null);

            Assert.IsFalse(
                File.Exists(Path.Combine(
                    dir, "Workflow.Models.dll")),
                "Original Workflow.Models.dll "
                + "should be renamed");

            Assert.IsFalse(
                File.Exists(Path.Combine(
                    dir, "ServiceHost.dll")),
                "Original ServiceHost.dll "
                + "should be renamed");

            foreach (var dll in
                Directory.GetFiles(dir, "*.dll"))
            {
                using var ms = new MemoryStream(
                    File.ReadAllBytes(dll));
                var asm =
                    AssemblyDefinition.ReadAssembly(ms);
                foreach (var type
                    in asm.MainModule.Types)
                {
                    if (type.Name == "<Module>") continue;
                    Assert.IsTrue(
                        type.Name.StartsWith("_"),
                        $"Type {type.Name} in {dll} "
                        + "should be mangled");
                }
            }
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RewriteBatch_SameSeed_Deterministic()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        try
        {
            var dll = CompileToDll(
                "namespace Lib "
                + "{ public class Foo {} }",
                "Workflow.Models");
            File.WriteAllBytes(
                Path.Combine(dir1, "Workflow.Models.dll"),
                dll);
            File.WriteAllBytes(
                Path.Combine(dir2, "Workflow.Models.dll"),
                dll);

            new ILRewriter().RewriteBatch(
                dir1, seed: 77, mapPath: null);
            new ILRewriter().RewriteBatch(
                dir2, seed: 77, mapPath: null);

            var files1 = Directory.GetFiles(dir1, "*.dll")
                .Select(Path.GetFileName)
                .OrderBy(f => f).ToArray();
            var files2 = Directory.GetFiles(dir2, "*.dll")
                .Select(Path.GetFileName)
                .OrderBy(f => f).ToArray();

            CollectionAssert.AreEqual(files1, files2,
                "Same seed should produce same filenames");
        }
        finally
        {
            TryDeleteDir(dir1);
            TryDeleteDir(dir2);
        }
    }

    [TestMethod]
    public void RewriteBatch_SkipFileRename_FilesNotMoved()
    {
        var dir = CreateTempDir();
        try
        {
            var dll = CompileToDll(
                "namespace Lib { public class Helper {} }",
                "Workflow.Models");
            File.WriteAllBytes(
                Path.Combine(dir, "Workflow.Models.dll"),
                dll);

            var rewriter = new ILRewriter();
            rewriter.RewriteBatch(
                dir,
                seed: 42,
                mapPath: null,
                skipFileRename: true);

            // Original filename must still exist
            Assert.IsTrue(
                File.Exists(Path.Combine(
                    dir, "Workflow.Models.dll")),
                "File should not be physically renamed "
                + "when skipFileRename=true");

            // PE identity must be obfuscated
            using var ms = new MemoryStream(
                File.ReadAllBytes(
                    Path.Combine(dir, "Workflow.Models.dll")));
            var asm = Mono.Cecil.AssemblyDefinition
                .ReadAssembly(ms);
            Assert.AreNotEqual(
                "Workflow.Models", asm.Name.Name,
                "Assembly PE identity should be obfuscated");
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RewriteBatch_SkipAssemblyRename_AssemblyRefsPreserved()
    {
        var dir = CreateTempDir();
        try
        {
            var libBytes = CompileToDll(
                "namespace Lib { public class Helper {} }",
                "Workflow.Models");
            var appBytes = CompileToDll(
                "public class App { Lib.Helper Get() => new(); }",
                "ServiceHost",
                extraAssemblyBytes: libBytes,
                extraAssemblyName: "Workflow.Models");

            File.WriteAllBytes(
                Path.Combine(dir, "Workflow.Models.dll"),
                libBytes);
            File.WriteAllBytes(
                Path.Combine(dir, "ServiceHost.dll"),
                appBytes);

            new ILRewriter().RewriteBatch(
                dir, seed: 42, mapPath: null,
                skipFileRename: true,
                skipAssemblyRename: true);

            // With skipAssemblyRename=true, ALL AssemblyRef entries in
            // ServiceHost.dll must retain their original names.
            using var ms = new MemoryStream(
                File.ReadAllBytes(
                    Path.Combine(dir, "ServiceHost.dll")));
            var asm = Mono.Cecil.AssemblyDefinition
                .ReadAssembly(ms);

            var libRef = asm.MainModule.AssemblyReferences
                .FirstOrDefault(r => r.Name == "Workflow.Models");
            Assert.IsNotNull(libRef,
                "AssemblyRef 'Workflow.Models' must retain original name "
                + "when skipAssemblyRename=true");
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RewriteBatch_SkipAssemblyRename_TypeRefNameAndNsPatchedCorrectly()
    {
        // Verifies that after MetadataManglingTransform renames types and
        // CrossReferenceTransform patches refs, the TypeRef in Consumer
        // has BOTH its namespace and type name patched (not just namespace).
        var dir = CreateTempDir();
        try
        {
            var libBytes = CompileToDll(
                "namespace Lib { public class Helper "
                + "{ public static int Val() => 1; } }",
                "Workflow.Models");
            var appBytes = CompileToDll(
                "public class App "
                + "{ public static int Run() => Lib.Helper.Val(); }",
                "ServiceHost",
                extraAssemblyBytes: libBytes,
                extraAssemblyName: "Workflow.Models");

            File.WriteAllBytes(
                Path.Combine(dir, "Workflow.Models.dll"),
                libBytes);
            File.WriteAllBytes(
                Path.Combine(dir, "ServiceHost.dll"),
                appBytes);

            new ILRewriter().RewriteBatch(
                dir, seed: 42, mapPath: null,
                skipFileRename: true,
                skipAssemblyRename: true);

            // Load ServiceHost.dll and check TypeRef for Workflow.Models
            using var ms = new MemoryStream(
                File.ReadAllBytes(
                    Path.Combine(dir, "ServiceHost.dll")));
            var asm = Mono.Cecil.AssemblyDefinition
                .ReadAssembly(ms);

            var typeRef = asm.MainModule.GetTypeReferences()
                .FirstOrDefault(t =>
                    t.Scope is Mono.Cecil.AssemblyNameReference anr
                    && anr.Name == "Workflow.Models");

            Assert.IsNotNull(typeRef,
                "TypeRef pointing to Workflow.Models must exist");
            Assert.IsTrue(
                typeRef.Namespace.StartsWith("_"),
                $"TypeRef namespace '{typeRef.Namespace}' must be obfuscated");
            Assert.IsTrue(
                typeRef.Name.StartsWith("_"),
                $"TypeRef name '{typeRef.Name}' must be obfuscated "
                + "(not just namespace)");
        }
        finally { TryDeleteDir(dir); }
    }

    // --- Helpers ---

    private static string CreateTempDir()
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "batchtest_"
            + Guid.NewGuid().ToString("N"));
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
