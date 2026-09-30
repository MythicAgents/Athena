using System.Reflection;
using System.Runtime.Loader;
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
    public void RewriteBatch_CaseDistinctLinuxPathsAreTransformedIndependently()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Inconclusive("Case-distinct path identity is Linux-specific.");

        var dir = CreateTempDir();
        try
        {
            var upperPath = Path.Combine(dir, "A.dll");
            var lowerPath = Path.Combine(dir, "a.dll");
            var upper = CompileToDll("public class UpperValue {}", "Upper.Assembly");
            var lower = CompileToDll("public class LowerValue {}", "Lower.Assembly");
            File.WriteAllBytes(upperPath, upper);
            File.WriteAllBytes(lowerPath, lower);

            new ILRewriter().RewriteBatch(
                dir, seed: 42, mapPath: null,
                firstPartyAssemblyNames: ["Upper.Assembly", "Lower.Assembly"],
                skipFileRename: true,
                skipAssemblyRename: true);

            CollectionAssert.AreNotEqual(upper, File.ReadAllBytes(upperPath));
            CollectionAssert.AreNotEqual(lower, File.ReadAllBytes(lowerPath));
        }
        finally { TryDeleteDir(dir); }
    }

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
                dir, seed: 42, mapPath: null,
                firstPartyAssemblyNames: ["Workflow.Models", "ServiceHost"]);

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
                dir1, seed: 77, mapPath: null,
                firstPartyAssemblyNames: ["Workflow.Models"]);
            new ILRewriter().RewriteBatch(
                dir2, seed: 77, mapPath: null,
                firstPartyAssemblyNames: ["Workflow.Models"]);

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
                firstPartyAssemblyNames: ["Workflow.Models"],
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
                firstPartyAssemblyNames: ["Workflow.Models", "ServiceHost"],
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
                firstPartyAssemblyNames: ["Workflow.Models", "ServiceHost"],
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

    [TestMethod]
    public void RewriteBatch_MatchesAssemblyReferencesIgnoringCase()
    {
        var dir = CreateTempDir();
        var context = new AssemblyLoadContext(
            $"case-map-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var library = CompileToDll(
                "namespace Shared { public class Helper "
                + "{ public static int Value() => 73; } }",
                "MixedCase.Library");
            var consumer = CompileToDll(
                "public class Consumer { public static int Run() "
                + "=> Shared.Helper.Value(); }",
                "Consumer",
                extraAssemblyBytes: library,
                extraAssemblyName: "MixedCase.Library");
            using (var input = new MemoryStream(consumer))
            using (var assembly = AssemblyDefinition.ReadAssembly(input))
            using (var output = new MemoryStream())
            {
                assembly.MainModule.AssemblyReferences
                    .Single(reference => reference.Name == "MixedCase.Library")
                    .Name = "mixedcase.library";
                assembly.Write(output);
                consumer = output.ToArray();
            }

            var libraryPath = Path.Combine(dir, "MixedCase.Library.dll");
            var consumerPath = Path.Combine(dir, "Consumer.dll");
            File.WriteAllBytes(libraryPath, library);
            File.WriteAllBytes(consumerPath, consumer);

            new ILRewriter().RewriteBatch(
                dir, seed: 42, mapPath: null,
                firstPartyAssemblyNames: ["MixedCase.Library", "Consumer"],
                skipFileRename: true,
                skipAssemblyRename: true);

            _ = context.LoadFromStream(new MemoryStream(
                File.ReadAllBytes(libraryPath)));
            var loadedConsumer = context.LoadFromStream(new MemoryStream(
                File.ReadAllBytes(consumerPath)));
            var run = loadedConsumer.GetTypes()
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Static))
                .Single(method => method.ReturnType == typeof(int)
                    && method.GetParameters().Length == 0);
            Assert.AreEqual(73, run.Invoke(null, null));
        }
        finally
        {
            context.Unload();
            TryDeleteDir(dir);
        }
    }

    [TestMethod]
    public void RewriteBatch_OnlyAllowlistedManagedAssembliesAreMutated()
    {
        var dir = CreateTempDir();
        try
        {
            var listed = CompileToDll(
                "namespace Listed { public class Value {} }",
                "Listed.Component");
            var unknown = CompileToDll(
                "namespace Athena.Plugin { public class Value {} }",
                "Athena.Plugin.Unknown");
            var external = CompileToDll(
                "namespace Renci.SshNet { public class Value {} }",
                "Renci.SshNet");
            var native = CreateMinimalNativePe();
            File.WriteAllBytes(Path.Combine(dir, "Listed.Component.dll"), listed);
            File.WriteAllBytes(Path.Combine(dir, "Athena.Plugin.Unknown.dll"), unknown);
            File.WriteAllBytes(Path.Combine(dir, "Renci.SshNet.dll"), external);
            File.WriteAllBytes(Path.Combine(dir, "native.dll"), native);

            new ILRewriter().RewriteBatch(
                dir, seed: 42, mapPath: null,
                firstPartyAssemblyNames: ["listed.component"]);

            CollectionAssert.AreEqual(
                unknown, File.ReadAllBytes(Path.Combine(dir, "Athena.Plugin.Unknown.dll")));
            CollectionAssert.AreEqual(
                external, File.ReadAllBytes(Path.Combine(dir, "Renci.SshNet.dll")));
            CollectionAssert.AreEqual(
                native, File.ReadAllBytes(Path.Combine(dir, "native.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(dir, "Listed.Component.dll")));
            var transformed = Directory.GetFiles(dir, "_*.dll");
            Assert.HasCount(1, transformed);
            CollectionAssert.AreNotEqual(listed, File.ReadAllBytes(transformed[0]));
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

    internal static byte[] CreateMinimalNativePe()
    {
        var bytes = new byte[0x400];
        using var stream = new MemoryStream(bytes, writable: true);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0x5a4d);
        stream.Position = 0x3c;
        writer.Write(0x80);
        stream.Position = 0x80;
        writer.Write(0x00004550u);
        writer.Write((ushort)0x014c);
        writer.Write((ushort)1);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write((ushort)0x00e0);
        writer.Write((ushort)0x2102);
        writer.Write((ushort)0x010b);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write(0u);
        writer.Write(0x200u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0x1000u);
        writer.Write(0x1000u);
        writer.Write(0x00400000u);
        writer.Write(0x1000u);
        writer.Write(0x200u);
        writer.Write((ushort)4);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)4);
        writer.Write((ushort)0);
        writer.Write(0u);
        writer.Write(0x2000u);
        writer.Write(0x200u);
        writer.Write(0u);
        writer.Write((ushort)3);
        writer.Write((ushort)0);
        writer.Write(0x100000u);
        writer.Write(0x1000u);
        writer.Write(0x100000u);
        writer.Write(0x1000u);
        writer.Write(0u);
        writer.Write(16u);
        for (var i = 0; i < 16; i++)
        {
            writer.Write(0u);
            writer.Write(0u);
        }
        writer.Write(new byte[] { (byte)'.', (byte)'d', (byte)'a', (byte)'t', (byte)'a', 0, 0, 0 });
        writer.Write(1u);
        writer.Write(0x1000u);
        writer.Write(0x200u);
        writer.Write(0x200u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(0x40000040u);
        return bytes;
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
