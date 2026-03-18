using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Obfuscator.IL.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class CrossReferenceTests
{
    [TestMethod]
    public void TypeReference_PatchedAcrossAssemblies()
    {
        var asmABytes = CompileToDll(
            "namespace Lib { public class Foo "
            + "{ public static int Bar() => 42; } }",
            "LibAsm");

        var asmBBytes = CompileToDll(
            "public class Consumer "
            + "{ public static int Call() => Lib.Foo.Bar(); }",
            "ConsumerAsm",
            extraAssemblyBytes: asmABytes,
            extraAssemblyName: "LibAsm");

        // Map keys use the RENAMED namespace for type entries, mirroring
        // MetadataManglingTransform which records keys after RenameNamespaces
        // has already run (type.FullName = "_ns1.Foo", not "Lib.Foo").
        var renameMaps =
            new Dictionary<string, Dictionary<string, string>>
        {
            ["LibAsm"] = new()
            {
                ["Lib"]      = "_ns1",  // namespace rename
                ["_ns1.Foo"] = "_abc",  // type rename (key uses renamed ns)
                ["Bar"]      = "_m1",
            }
        };

        var transform = new CrossReferenceTransform();
        var patchedB = transform.PatchReferences(
            asmBBytes, renameMaps, searchDir: null);

        using var ms = new MemoryStream(patchedB);
        var asm = AssemblyDefinition.ReadAssembly(ms);
        var fooRef = asm.MainModule.GetTypeReferences()
            .FirstOrDefault(t =>
                t.Scope is AssemblyNameReference anr
                && anr.Name == "LibAsm");

        Assert.IsNotNull(
            fooRef, "Should have a type ref to LibAsm");
        Assert.AreEqual("_abc", fooRef.Name);
        Assert.AreEqual("_ns1", fooRef.Namespace);
    }

    [TestMethod]
    public void MemberReference_PatchedAcrossAssemblies()
    {
        var asmABytes = CompileToDll(
            "namespace Lib { public class Foo "
            + "{ public static int Bar() => 42; } }",
            "LibAsm");

        var asmBBytes = CompileToDll(
            "public class Consumer "
            + "{ public static int Call() => Lib.Foo.Bar(); }",
            "ConsumerAsm",
            extraAssemblyBytes: asmABytes,
            extraAssemblyName: "LibAsm");

        var renameMaps =
            new Dictionary<string, Dictionary<string, string>>
        {
            ["LibAsm"] = new()
            {
                ["Lib"]      = "_ns1",
                ["_ns1.Foo"] = "_abc",
                ["Bar"]      = "_m1",
            }
        };

        var transform = new CrossReferenceTransform();
        var patchedB = transform.PatchReferences(
            asmBBytes, renameMaps, searchDir: null);

        using var ms = new MemoryStream(patchedB);
        var asm = AssemblyDefinition.ReadAssembly(ms);
        var memberRefs = asm.MainModule.GetMemberReferences()
            .Where(m => m.DeclaringType?.Scope
                is AssemblyNameReference anr
                && anr.Name == "LibAsm");

        Assert.IsTrue(
            memberRefs.Any(m => m.Name == "_m1"),
            "Bar should be renamed to _m1");
    }

    [TestMethod]
    public void NamespaceReference_PatchedAcrossAssemblies()
    {
        var asmABytes = CompileToDll(
            "namespace Lib { public class Foo "
            + "{ public static int Bar() => 42; } }",
            "LibAsm");

        var asmBBytes = CompileToDll(
            "public class Consumer "
            + "{ public static int Call() => Lib.Foo.Bar(); }",
            "ConsumerAsm",
            extraAssemblyBytes: asmABytes,
            extraAssemblyName: "LibAsm");

        var renameMaps =
            new Dictionary<string, Dictionary<string, string>>
        {
            ["LibAsm"] = new()
            {
                ["Lib"]      = "_ns1",
                ["_ns1.Foo"] = "_abc",
            }
        };

        var transform = new CrossReferenceTransform();
        var patchedB = transform.PatchReferences(
            asmBBytes, renameMaps, searchDir: null);

        using var ms = new MemoryStream(patchedB);
        var asm = AssemblyDefinition.ReadAssembly(ms);
        var typeRefs = asm.MainModule.GetTypeReferences()
            .Where(t => t.Scope is AssemblyNameReference anr
                && anr.Name == "LibAsm");

        Assert.IsTrue(
            typeRefs.All(t => t.Namespace != "Lib"),
            "No type ref should still use namespace 'Lib'");
    }

    [TestMethod]
    public void SameModuleRef_NotPatched()
    {
        var asmBytes = CompileToDll(
            "namespace Lib {\n"
            + "  public class Foo "
            + "{ public static int Val() => 1; }\n"
            + "  public class Bar "
            + "{ public static int Call() => Foo.Val(); }\n"
            + "}",
            "SelfAsm");

        var renameMaps =
            new Dictionary<string, Dictionary<string, string>>
        {
            ["SelfAsm"] = new()
            {
                ["Lib"]      = "_ns1",
                ["_ns1.Foo"] = "_abc",
                ["Val"]      = "_m1",
            }
        };

        var transform = new CrossReferenceTransform();
        var patched = transform.PatchReferences(
            asmBytes, renameMaps, searchDir: null);

        using var ms = new MemoryStream(patched);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        var selfModuleRefs = asm.MainModule
            .GetTypeReferences()
            .Where(t => t.Scope is ModuleDefinition)
            .ToList();

        foreach (var r in selfModuleRefs)
        {
            Assert.AreNotEqual("_abc", r.Name,
                "Same-module ref should not be patched "
                + "to cross-ref map value");
        }
    }

    /// <summary>
    /// MetadataManglingTransform stores rename keys using the ALREADY-RENAMED
    /// namespace (e.g. "_ns1.Foo" not "Lib.Foo").  CrossReferenceTransform
    /// must use the renamed namespace when building the lookup key so that
    /// both the namespace AND the type name are patched.
    /// </summary>
    [TestMethod]
    public void TypeName_PatchedWhenMapKeyUsesRenamedNamespace()
    {
        // This simulates what MetadataManglingTransform produces:
        //   RenameNamespaces: "Lib" -> "_ns1"
        //   RenameType: type.FullName (post-ns-rename) = "_ns1.Foo" -> "_abc"
        var renameMaps =
            new Dictionary<string, Dictionary<string, string>>
        {
            ["LibAsm"] = new()
            {
                ["Lib"]      = "_ns1",  // namespace rename
                ["_ns1.Foo"] = "_abc",  // type rename (key uses renamed ns!)
            }
        };

        var asmABytes = CompileToDll(
            "namespace Lib { public class Foo "
            + "{ public static int Val() => 1; } }",
            "LibAsm");

        var asmBBytes = CompileToDll(
            "public class Consumer "
            + "{ public static int Call() => Lib.Foo.Val(); }",
            "ConsumerAsm",
            extraAssemblyBytes: asmABytes,
            extraAssemblyName: "LibAsm");

        var transform = new CrossReferenceTransform();
        var patched = transform.PatchReferences(
            asmBBytes, renameMaps, searchDir: null);

        using var ms = new MemoryStream(patched);
        var asm = AssemblyDefinition.ReadAssembly(ms);
        var fooRef = asm.MainModule.GetTypeReferences()
            .FirstOrDefault(t =>
                t.Scope is AssemblyNameReference anr
                && anr.Name == "LibAsm");

        Assert.IsNotNull(fooRef,
            "Should have a type ref to LibAsm");
        Assert.AreEqual("_ns1", fooRef.Namespace,
            "Namespace should be patched");
        Assert.AreEqual("_abc", fooRef.Name,
            "Type name must be patched using renamed namespace as key");
    }

    [TestMethod]
    public void AssemblyRef_NotRenamedByCrossReferenceTransform()
    {
        var asmABytes = CompileToDll(
            "namespace Lib { public class Foo {} }",
            "LibAsm");

        var asmBBytes = CompileToDll(
            "public class Consumer { Lib.Foo Get() => new(); }",
            "ConsumerAsm",
            extraAssemblyBytes: asmABytes,
            extraAssemblyName: "LibAsm");

        var renameMaps =
            new Dictionary<string, Dictionary<string, string>>
        {
            ["LibAsm"] = new() { ["Lib"] = "_ns1", ["_ns1.Foo"] = "_abc" }
        };

        var transform = new CrossReferenceTransform();
        var patched = transform.PatchReferences(
            asmBBytes, renameMaps, searchDir: null);

        using var ms = new MemoryStream(patched);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        // The AssemblyRef entry for LibAsm must retain its original name
        var libRef = asm.MainModule.AssemblyReferences
            .FirstOrDefault(r => r.Name == "LibAsm");
        Assert.IsNotNull(libRef,
            "AssemblyRef 'LibAsm' must still exist with original name");
    }

    // --- Test helpers ---

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
                    trustedDir, "System.Collections.dll")),
        };

        if (extraAssemblyBytes is not null)
        {
            references.Add(
                MetadataReference.CreateFromImage(
                    extraAssemblyBytes));
        }

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
