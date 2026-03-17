# Obfuscator Coverage Expansion Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate meaningful assembly names, namespaces, and type names from all Athena assemblies in decompiled output.

**Architecture:** Remove string-based assembly loading from the agent so assembly identities are never resolved by name. Replace per-DLL IL obfuscation with a batch pass that processes all assemblies together, patches cross-assembly type/member references, and renames assembly identities. Derive the obfuscation seed deterministically from the Payload UUID so host and plugin builds produce identical rename maps.

**Tech Stack:** C# / .NET 10, Mono.Cecil 0.11.6, MSTest, System.CommandLine, Python (Mythic build scripts)

**Spec:** `docs/superpowers/specs/2026-03-17-obfuscator-coverage-expansion-design.md`

---

## File Structure

| File | Responsibility |
|------|---------------|
| `Obfuscator/IL/Transforms/CrossReferenceTransform.cs` | **New.** Patches TypeReference/MemberReference across DLLs using per-assembly rename maps |
| `Obfuscator/IL/Transforms/AssemblyRenameTransform.cs` | **New.** Renames assembly identities and patches AssemblyNameReferences, renames physical files |
| `Obfuscator/IL/ILRewriter.cs` | **Modify.** Add `RewriteBatch` method that orchestrates per-DLL mangling + cross-ref + assembly rename |
| `Obfuscator/Program.cs` | **Modify.** Add `rewrite-il-batch` CLI command |
| `Directory.Build.targets` | **Modify.** Replace `ObfuscateIL` target with `ObfuscateILBatch` |
| `ServiceHost/Config/ContainerBuilder.cs` | **Modify.** Replace string-based channel loading with interface scan |
| `Workflow.Providers.Runtime/AssemblyManager.cs` | **Modify.** Replace `TryLoadModule` string-based fallback |
| `Workflow.Models/AssemblyNames.cs` | **Delete.** |
| `Tests/Workflow.Tests/PluginLoader.cs` | **Modify.** Replace `AssemblyNames.ForModule` usage |
| `mythic/agent_functions/builder.py` | **Modify.** Derive seed from UUID via hashlib |
| `mythic/agent_functions/load.py` | **Modify.** Derive seed from UUID via hashlib, invoke batch command |
| `Tests/Obfuscator.Tests/CrossReferenceTests.cs` | **New.** Tests for cross-assembly reference patching |
| `Tests/Obfuscator.Tests/AssemblyRenameTests.cs` | **New.** Tests for assembly rename transform |
| `Tests/Obfuscator.Tests/BatchRewriteTests.cs` | **New.** Integration test for the full batch pipeline |

---

### Task 1: CrossReferenceTransform — Patch TypeReferences Across Assemblies

This is the highest-risk, most complex piece. Build it first with thorough tests.

**Files:**
- Create: `Obfuscator/IL/Transforms/CrossReferenceTransform.cs`
- Create: `Tests/Obfuscator.Tests/CrossReferenceTests.cs`

**Context:** `MetadataManglingTransform` renames types/methods/fields inside a single DLL but doesn't update references in other DLLs. This transform takes per-assembly rename maps and patches `TypeReference.Name`, `TypeReference.Namespace`, and `MemberReference.Name` in consuming assemblies. It only patches references whose `TypeReference.Scope` is an `AssemblyNameReference` (cross-assembly refs) — same-module refs (`ModuleDefinition` scope) are already handled by `MetadataManglingTransform`.

- [ ] **Step 1: Write the TypeReference patching test**

In `Tests/Obfuscator.Tests/CrossReferenceTests.cs`:

```csharp
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
        // Assembly A defines a public class
        var asmABytes = CompileToDll(
            "namespace Lib { public class Foo "
            + "{ public static int Bar() => 42; } }",
            "LibAsm");

        // Assembly B references Foo from A
        var asmBBytes = CompileToDll(
            "public class Consumer "
            + "{ public static int Call() => Lib.Foo.Bar(); }",
            "ConsumerAsm",
            extraAssemblyBytes: asmABytes,
            extraAssemblyName: "LibAsm");

        // Simulate MetadataManglingTransform renaming in A
        var renameMaps =
            new Dictionary<string, Dictionary<string, string>>
        {
            ["LibAsm"] = new()
            {
                ["Lib.Foo"] = "_abc",
                ["Lib"] = "_ns1",
                ["Bar"] = "_m1",
            }
        };

        var transform = new CrossReferenceTransform();
        var patchedB = transform.PatchReferences(
            asmBBytes, renameMaps, searchDir: null);

        // Verify B's TypeReference to Lib.Foo is now _ns1._abc
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
                ["Lib.Foo"] = "_abc",
                ["Lib"] = "_ns1",
                ["Bar"] = "_m1",
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
                ["Lib.Foo"] = "_abc",
                ["Lib"] = "_ns1",
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
                ["Lib.Foo"] = "_abc",
                ["Val"] = "_m1",
            }
        };

        var transform = new CrossReferenceTransform();
        var patched = transform.PatchReferences(
            asmBytes, renameMaps, searchDir: null);

        using var ms = new MemoryStream(patched);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        // Verify same-module TypeReferences are untouched.
        // Since we did NOT run MetadataManglingTransform,
        // original names should be intact.
        var selfModuleRefs = asm.MainModule
            .GetTypeReferences()
            .Where(t => t.Scope is ModuleDefinition)
            .ToList();

        // Any same-module refs should retain their
        // original names (Foo, Bar), not the map values
        foreach (var r in selfModuleRefs)
        {
            Assert.AreNotEqual("_abc", r.Name,
                "Same-module ref should not be patched "
                + "to cross-ref map value");
        }
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
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd Payload_Type/athena/athena/agent_code && dotnet test Tests/Obfuscator.Tests --filter "FullyQualifiedName~CrossReferenceTests" --no-build 2>&1 | head -20
```

Expected: Build error — `CrossReferenceTransform` doesn't exist. (Do NOT use `--no-build` — we need the compile error.)

- [ ] **Step 3: Implement CrossReferenceTransform**

Create `Obfuscator/IL/Transforms/CrossReferenceTransform.cs`:

```csharp
using Mono.Cecil;

namespace Obfuscator.IL.Transforms;

public sealed class CrossReferenceTransform
{
    public byte[] PatchReferences(
        byte[] assemblyBytes,
        Dictionary<string, Dictionary<string, string>>
            perAssemblyMaps,
        string? searchDir)
    {
        using var input = new MemoryStream(assemblyBytes);
        var resolver = new DefaultAssemblyResolver();
        if (searchDir is not null)
            resolver.AddSearchDirectory(searchDir);
        var readerParams = new ReaderParameters
        {
            ReadingMode = ReadingMode.Immediate,
            ReadSymbols = false,
            AssemblyResolver = resolver,
        };
        using var asm = AssemblyDefinition.ReadAssembly(
            input, readerParams);

        var module = asm.MainModule;

        // Collect all patches before applying to avoid
        // FullName key invalidation during iteration
        var typePatch = new List<(
            TypeReference Ref,
            string? NewNs,
            string? NewName)>();

        foreach (var typeRef in module.GetTypeReferences())
        {
            if (typeRef.Scope
                is not AssemblyNameReference anr)
                continue;
            if (!perAssemblyMaps.TryGetValue(
                anr.Name, out var map))
                continue;

            string? newNs = null;
            string? newName = null;

            // Build original FullName for type lookup
            var origFull =
                string.IsNullOrEmpty(typeRef.Namespace)
                    ? typeRef.Name
                    : typeRef.Namespace + "." + typeRef.Name;

            if (map.TryGetValue(origFull, out var renamed))
                newName = renamed;

            if (!string.IsNullOrEmpty(typeRef.Namespace)
                && map.TryGetValue(
                    typeRef.Namespace, out var renamedNs))
                newNs = renamedNs;

            if (newNs is not null || newName is not null)
                typePatch.Add((typeRef, newNs, newName));
        }

        foreach (var (typeRef, newNs, newName) in typePatch)
        {
            if (newNs is not null)
                typeRef.Namespace = newNs;
            if (newName is not null)
                typeRef.Name = newName;
        }

        // Patch member references
        foreach (var memberRef
            in module.GetMemberReferences())
        {
            if (memberRef.DeclaringType?.Scope
                is not AssemblyNameReference anr)
                continue;
            if (!perAssemblyMaps.TryGetValue(
                anr.Name, out var map))
                continue;
            if (map.TryGetValue(
                memberRef.Name, out var newMemberName))
                memberRef.Name = newMemberName;
        }

        using var output = new MemoryStream();
        asm.Write(output);
        return output.ToArray();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd Payload_Type/athena/athena/agent_code && dotnet test Tests/Obfuscator.Tests --filter "FullyQualifiedName~CrossReferenceTests" -v n
```

Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add Obfuscator/IL/Transforms/CrossReferenceTransform.cs Tests/Obfuscator.Tests/CrossReferenceTests.cs
git commit -m "feat: add CrossReferenceTransform for cross-assembly type/member ref patching"
```

---

### Task 2: AssemblyRenameTransform — Rename Assembly Identities

**Files:**
- Create: `Obfuscator/IL/Transforms/AssemblyRenameTransform.cs`
- Create: `Tests/Obfuscator.Tests/AssemblyRenameTests.cs`

**Context:** After cross-ref patching, this transform renames assembly identities (`AssemblyDefinition.Name.Name`) and patches `AssemblyNameReference` in all consuming DLLs. Uses `seed ^ 0x5A5A5A5A` to avoid collisions with type-level name generation.

- [ ] **Step 1: Write the assembly rename tests**

In `Tests/Obfuscator.Tests/AssemblyRenameTests.cs`:

```csharp
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

            // Verify MyPlugin's assembly ref is updated
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
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd Payload_Type/athena/athena/agent_code && dotnet test Tests/Obfuscator.Tests --filter "FullyQualifiedName~AssemblyRenameTests" --no-build 2>&1 | head -20
```

Expected: Build error — `AssemblyRenameTransform` doesn't exist. (Do NOT use `--no-build` — we need the compile error.)

- [ ] **Step 3: Implement AssemblyRenameTransform**

Create `Obfuscator/IL/Transforms/AssemblyRenameTransform.cs`:

```csharp
using System.Text;
using Mono.Cecil;

namespace Obfuscator.IL.Transforms;

public sealed class AssemblyRenameTransform
{
    private static readonly char[] AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyz0123456789"
            .ToCharArray();

    private static readonly string[] SkipPrefixes =
        ["System.", "Microsoft.", "runtime."];

    private readonly int _seed;

    public AssemblyRenameTransform(int seed)
    {
        _seed = seed;
    }

    public Dictionary<string, string> RenameAll(
        string directory)
    {
        var rng = new Random(_seed ^ 0x5A5A5A5A);
        var used = new HashSet<string>(
            StringComparer.Ordinal);
        var renameMap = new Dictionary<string, string>();

        var dllFiles =
            Directory.GetFiles(directory, "*.dll");
        Array.Sort(dllFiles, StringComparer.Ordinal);

        // Phase 1: Build rename map
        foreach (var dllPath in dllFiles)
        {
            var fileName =
                Path.GetFileNameWithoutExtension(dllPath);
            if (ShouldSkip(fileName))
                continue;

            using var stream = new MemoryStream(
                File.ReadAllBytes(dllPath));
            try
            {
                using var asm =
                    AssemblyDefinition.ReadAssembly(stream);
                var originalName = asm.Name.Name;
                if (ShouldSkip(originalName))
                    continue;

                var newName =
                    GenerateUniqueName(rng, used);
                renameMap[originalName] = newName;
            }
            catch (BadImageFormatException)
            {
                continue;
            }
        }

        // Phase 2: Rewrite identities and refs
        foreach (var dllPath in dllFiles)
        {
            var bytes = File.ReadAllBytes(dllPath);
            using var stream = new MemoryStream(bytes);

            using AssemblyDefinition asm;
            try
            {
                asm = AssemblyDefinition.ReadAssembly(
                    stream,
                    new ReaderParameters
                    {
                        ReadingMode =
                            ReadingMode.Immediate,
                        ReadSymbols = false,
                    });
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            var changed = false;

            if (renameMap.TryGetValue(
                asm.Name.Name, out var newIdentity))
            {
                asm.Name.Name = newIdentity;
                asm.MainModule.Name =
                    newIdentity + ".dll";
                changed = true;
            }

            foreach (var asmRef in
                asm.MainModule.AssemblyReferences)
            {
                if (renameMap.TryGetValue(
                    asmRef.Name, out var newRefName))
                {
                    asmRef.Name = newRefName;
                    changed = true;
                }
            }

            if (changed)
            {
                using var output = new MemoryStream();
                asm.Write(output);
                File.WriteAllBytes(
                    dllPath, output.ToArray());
            }
        }

        // Phase 3: Rename physical files
        foreach (var (original, newName) in renameMap)
        {
            var oldPath = Path.Combine(
                directory, original + ".dll");
            var newPath = Path.Combine(
                directory, newName + ".dll");
            if (File.Exists(oldPath))
                File.Move(oldPath, newPath);
        }

        return renameMap;
    }

    private static bool ShouldSkip(string name)
    {
        foreach (var prefix in SkipPrefixes)
        {
            if (name.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string GenerateUniqueName(
        Random rng, HashSet<string> used)
    {
        var length = 2;
        while (true)
        {
            var sb = new StringBuilder(length + 1);
            sb.Append('_');
            for (var i = 0; i < length; i++)
                sb.Append(
                    AlphaNumChars[
                        rng.Next(AlphaNumChars.Length)]);
            var candidate = sb.ToString();
            if (used.Add(candidate))
                return candidate;
            length++;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd Payload_Type/athena/athena/agent_code && dotnet test Tests/Obfuscator.Tests --filter "FullyQualifiedName~AssemblyRenameTests" -v n
```

Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add Obfuscator/IL/Transforms/AssemblyRenameTransform.cs Tests/Obfuscator.Tests/AssemblyRenameTests.cs
git commit -m "feat: add AssemblyRenameTransform for assembly identity renaming"
```

---

### Task 3: ILRewriter Batch Mode + CLI Command

**Files:**
- Modify: `Obfuscator/IL/ILRewriter.cs`
- Modify: `Obfuscator/Program.cs`
- Create: `Tests/Obfuscator.Tests/BatchRewriteTests.cs`

**Context:** Add a `RewriteBatch` method to `ILRewriter` that orchestrates: (1) per-DLL `MetadataManglingTransform`, (2) `CrossReferenceTransform`, (3) `AssemblyRenameTransform`. Add a `rewrite-il-batch` CLI command that invokes it.

- [ ] **Step 1: Write the batch integration test**

In `Tests/Obfuscator.Tests/BatchRewriteTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd Payload_Type/athena/athena/agent_code && dotnet test Tests/Obfuscator.Tests --filter "FullyQualifiedName~BatchRewriteTests" --no-build 2>&1 | head -20
```

Expected: Build error — `ILRewriter.RewriteBatch` doesn't exist.

- [ ] **Step 3: Add RewriteBatch to ILRewriter**

Replace the entire content of `Obfuscator/IL/ILRewriter.cs` with:

```csharp
using Obfuscator.IL.Transforms;

namespace Obfuscator.IL;

public sealed class ILRewriter
{
    private static readonly string[] SkipPrefixes =
        ["System.", "Microsoft.", "runtime."];

    public void Rewrite(
        string inputDllPath, int seed, string? mapPath)
    {
        var bytes = File.ReadAllBytes(inputDllPath);
        var searchDir = Path.GetDirectoryName(
            Path.GetFullPath(inputDllPath));

        var mmt = new MetadataManglingTransform(seed);
        bytes = mmt.Transform(bytes, searchDir);

        File.WriteAllBytes(inputDllPath, bytes);

        if (mapPath is not null)
        {
            var map = File.Exists(mapPath)
                ? DeobfuscationMap.LoadFromFile(mapPath)
                : new DeobfuscationMap();
            map.MetadataRenames = mmt.GetRenameMappings();
            map.SaveToFile(mapPath);
        }
    }

    public void RewriteBatch(
        string directory, int seed, string? mapPath)
    {
        var dllFiles =
            Directory.GetFiles(directory, "*.dll");
        Array.Sort(dllFiles, StringComparer.Ordinal);

        var qualifying = dllFiles
            .Where(f => !ShouldSkip(
                Path.GetFileNameWithoutExtension(f)))
            .ToArray();

        // Step 1: Per-DLL MetadataManglingTransform
        var perAssemblyMaps =
            new Dictionary<string,
                Dictionary<string, string>>();

        foreach (var dllPath in qualifying)
        {
            var bytes = File.ReadAllBytes(dllPath);

            // Read the assembly identity BEFORE mangling
            // (matches what AssemblyNameReference.Name
            // contains in consuming DLLs)
            string asmName;
            using (var pre = new MemoryStream(bytes))
            using (var preAsm = Mono.Cecil
                .AssemblyDefinition.ReadAssembly(pre))
            {
                asmName = preAsm.Name.Name;
            }

            var mmt = new MetadataManglingTransform(seed);
            bytes = mmt.Transform(bytes, directory);
            File.WriteAllBytes(dllPath, bytes);

            perAssemblyMaps[asmName] =
                mmt.GetRenameMappings();
        }

        // Step 2: CrossReferenceTransform
        var crossRef = new CrossReferenceTransform();
        foreach (var dllPath in qualifying)
        {
            var bytes = File.ReadAllBytes(dllPath);
            bytes = crossRef.PatchReferences(
                bytes, perAssemblyMaps, directory);
            File.WriteAllBytes(dllPath, bytes);
        }

        // Step 3: AssemblyRenameTransform
        var asmRename = new AssemblyRenameTransform(seed);
        var renameMap = asmRename.RenameAll(directory);

        if (mapPath is not null)
        {
            var map = File.Exists(mapPath)
                ? DeobfuscationMap.LoadFromFile(mapPath)
                : new DeobfuscationMap();

            var merged = new Dictionary<string, string>();
            foreach (var (_, asmMap) in perAssemblyMaps)
                foreach (var (k, v) in asmMap)
                    merged.TryAdd(k, v);
            foreach (var (k, v) in renameMap)
                merged.TryAdd("asm:" + k, v);

            map.MetadataRenames = merged;
            map.SaveToFile(mapPath);
        }
    }

    private static bool ShouldSkip(string name)
    {
        foreach (var prefix in SkipPrefixes)
        {
            if (name.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd Payload_Type/athena/athena/agent_code && dotnet test Tests/Obfuscator.Tests --filter "FullyQualifiedName~BatchRewriteTests" -v n
```

Expected: 2 tests pass.

- [ ] **Step 5: Add `rewrite-il-batch` CLI command to Program.cs**

In `Obfuscator/Program.cs`, add after line 93 (after the `rewriteIlCommand.SetAction` block):

```csharp
var batchSeedOption = new Option<int>("--seed")
{
    Description =
        "Random seed for deterministic obfuscation",
    Required = true
};

var batchDirOption = new Option<string>("--dir")
{
    Description =
        "Directory containing DLLs to process",
    Required = true
};

var batchMapOption = new Option<string?>("--map")
{
    Description =
        "Optional path to write the rename map JSON"
};

var rewriteIlBatchCommand = new Command(
    "rewrite-il-batch",
    "Batch rewrite IL in all assemblies in a directory")
{
    batchSeedOption,
    batchDirOption,
    batchMapOption
};

rewriteIlBatchCommand.SetAction((parseResult) =>
{
    var seed = parseResult.GetValue(batchSeedOption);
    var dir = parseResult.GetValue(batchDirOption)!;
    var map = parseResult.GetValue(batchMapOption);

    var rewriter = new ILRewriter();
    rewriter.RewriteBatch(dir, seed, map);
});
```

And update the root command registration (around line 95-99) to include the new command:

```csharp
var rootCommand =
    new RootCommand("Athena obfuscation tool")
{
    rewriteSourceCommand,
    rewriteIlCommand,
    rewriteIlBatchCommand
};
```

- [ ] **Step 6: Run all obfuscator tests**

```bash
cd Payload_Type/athena/athena/agent_code && dotnet test Tests/Obfuscator.Tests -v n
```

Expected: All tests pass (existing + new).

- [ ] **Step 7: Commit**

```bash
git add Obfuscator/IL/ILRewriter.cs Obfuscator/Program.cs Tests/Obfuscator.Tests/BatchRewriteTests.cs
git commit -m "feat: add ILRewriter.RewriteBatch and rewrite-il-batch CLI command"
```

---

### Task 4: Replace MSBuild ObfuscateIL Target

**Files:**
- Modify: `Directory.Build.targets`

**Context:** Replace the per-DLL `ObfuscateIL` target with `ObfuscateILBatch` that invokes the new batch command. Must fire `BeforeTargets="GenerateSingleFileBundle"` for single-file publish compatibility.

- [ ] **Step 1: Replace the MSBuild target**

Replace the entire content of `Directory.Build.targets` with:

```xml
<Project>
  <Import Project="$(AthenaExternalBuildTargets)"
          Condition="'$(AthenaExternalBuildTargets)' != ''
                     And Exists('$(AthenaExternalBuildTargets)')"/>

  <Target Name="ObfuscateILBatch"
          AfterTargets="ComputeFilesToPublish"
          BeforeTargets="GenerateSingleFileBundle"
          Condition="'$(Obfuscate)' == 'true'
                     AND '$(ObfuscatorPath)' != ''">
    <Message Text="Obfuscating IL (batch): $(PublishDir)"
             Importance="high" />
    <Exec Command="$(ObfuscatorPath) rewrite-il-batch --seed $(ObfSeed) --dir &quot;$(PublishDir)&quot;" />
  </Target>
</Project>
```

- [ ] **Step 2: Commit**

```bash
git add Directory.Build.targets
git commit -m "feat: replace per-DLL ObfuscateIL with batch ObfuscateILBatch target"
```

---

### Task 5: Remove String-Based Assembly Loading from Agent

**Files:**
- Modify: `ServiceHost/Config/ContainerBuilder.cs`
- Modify: `Workflow.Providers.Runtime/AssemblyManager.cs`
- Delete: `Workflow.Models/AssemblyNames.cs`
- Modify: `Tests/Workflow.Tests/PluginLoader.cs`

**Context:** Remove all `Assembly.Load(string)` patterns that prevent assembly renaming. Replace with interface-based or metadata-based discovery.

- [ ] **Step 1: Modify ContainerBuilder.TryLoadProfiles**

In `ServiceHost/Config/ContainerBuilder.cs`, replace the `TryLoadProfiles` method (lines 65-91) with:

```csharp
private static void TryLoadProfiles(
    Autofac.ContainerBuilder containerBuilder)
{
    var entryAsm = Assembly.GetEntryAssembly();
    if (entryAsm is null) return;

    foreach (var refName
        in entryAsm.GetReferencedAssemblies())
    {
        if (refName.Name is null) continue;
        if (refName.Name.StartsWith("System.")
            || refName.Name.StartsWith("Microsoft."))
            continue;

        try
        {
            DebugLog.Log(
                "TryLoadProfiles: scanning "
                + refName.Name);
            var asm = Assembly.Load(refName);
            containerBuilder
                .RegisterAssemblyTypes(asm)
                .Where(t => typeof(IChannel)
                    .IsAssignableFrom(t))
                .As<IChannel>().SingleInstance();
        }
        catch (FileNotFoundException)
        {
            DebugLog.Log(
                "TryLoadProfiles: "
                + refName.Name + " not found");
        }
        catch (Exception ex)
        {
            DebugLog.Log(
                "TryLoadProfiles: failed "
                + refName.Name + ": " + ex.Message);
        }
    }
}
```

- [ ] **Step 2: Modify ComponentProvider.TryLoadModule**

In `Workflow.Providers.Runtime/AssemblyManager.cs`, replace the `TryLoadModule` method (lines 31-56) with:

```csharp
private bool TryLoadModule(
    string name, out IModule? plugOut)
{
    DebugLog.Log(
        "TryLoadModule: scanning assemblies for "
        + name);
    plugOut = null;

    foreach (var asm
        in AppDomain.CurrentDomain.GetAssemblies())
    {
        ParseAssemblyForModule(asm);
    }

    return loadedModules.TryGetValue(
        name, out plugOut);
}
```

- [ ] **Step 3: Update PluginLoader.cs**

In `Tests/Workflow.Tests/PluginLoader.cs`, replace the `GetPlugin` method (lines 34-54) with:

```csharp
private IModule? GetPlugin(string moduleName)
{
    foreach (var asm
        in AppDomain.CurrentDomain.GetAssemblies())
    {
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types
                .Where(t => t is not null).ToArray()!;
        }

        foreach (var t in types)
        {
            if (typeof(IModule).IsAssignableFrom(t)
                && !t.IsAbstract && !t.IsInterface)
            {
                var plug = (IModule?)
                    Activator.CreateInstance(t, context);
                if (plug?.Name == moduleName)
                    return plug;
            }
        }
    }
    return null;
}
```

Remove the `using Workflow.Contracts;` import only if no other usages remain in the file. Also remove `Assembly.Load` import if unused. Check by looking at remaining usages.

- [ ] **Step 4: Delete AssemblyNames.cs and verify build**

Delete the file and verify everything still compiles:

```bash
cd Payload_Type/athena/athena/agent_code && rm Workflow.Models/AssemblyNames.cs && dotnet build Workflow.Models && dotnet build Workflow.Providers.Runtime && dotnet build ServiceHost -c LocalDebugHttp && dotnet build Tests/Workflow.Tests
```

Expected: All three projects build successfully. If any fail with `CS0103` errors referencing `AssemblyNames`, there are additional callers that need updating.

- [ ] **Step 5: Commit**

```bash
cd Payload_Type/athena/athena/agent_code && git add ServiceHost/Config/ContainerBuilder.cs Workflow.Providers.Runtime/AssemblyManager.cs Tests/Workflow.Tests/PluginLoader.cs && git rm Workflow.Models/AssemblyNames.cs && git commit -m "feat: remove string-based assembly loading, delete AssemblyNames.cs"
```

---

### Task 6: Build Script Changes

**Files:**
- Modify: `mythic/agent_functions/builder.py`
- Modify: `mythic/agent_functions/load.py`

**Context:** Derive the obfuscation seed from the Payload UUID using `hashlib.sha256`. Update `load.py` to invoke `rewrite-il-batch` instead of single-DLL `rewrite-il`.

- [ ] **Step 1: Update builder.py seed generation**

In `mythic/agent_functions/builder.py`, find line 551:

```python
obf_seed = random.randint(0, 2**31 - 1)
```

Replace with:

```python
import hashlib
obf_seed = int(
    hashlib.sha256(self.uuid.encode()).hexdigest(), 16
) & 0x7FFFFFFF
```

The `import hashlib` can go at the top of the file with the other imports. Check if `random` is still used elsewhere in the file before removing that import.

- [ ] **Step 2: Update load.py seed generation**

In `mythic/agent_functions/load.py`, find line 270:

```python
obf_seed = random.randint(0, 2**31 - 1)
```

Replace with:

```python
import hashlib
obf_seed = int(
    hashlib.sha256(uuid.encode()).hexdigest(), 16
) & 0x7FFFFFFF
```

- [ ] **Step 3: Update load.py to use rewrite-il-batch**

In `mythic/agent_functions/load.py`, find the IL rewrite block (lines 331-344):

```python
il_proc = await asyncio.create_subprocess_exec(
    obfuscator_bin, "rewrite-il",
    "--seed", str(obf_seed),
    "--input", dll_path,
    stdout=asyncio.subprocess.PIPE,
    stderr=asyncio.subprocess.PIPE
)
```

Replace with:

```python
il_proc = await asyncio.create_subprocess_exec(
    obfuscator_bin, "rewrite-il-batch",
    "--seed", str(obf_seed),
    "--dir", build_out,
    stdout=asyncio.subprocess.PIPE,
    stderr=asyncio.subprocess.PIPE
)
```

Where `build_out` is the variable at line 311: `os.path.join(plugin_temp, "bin", "Release", "net10.0")`.

After the batch rewrite, the plugin DLL may have been renamed. Update the DLL discovery (after the IL rewrite block) to handle renamed files. Replace the existing platform/generic DLL lookup (lines 306-328) so the discovery runs after the batch rewrite:

```python
# Build first, then obfuscate, then discover DLL
build_proc = await asyncio.create_subprocess_exec(
    "dotnet", "build", "-c", "Release",
    "/p:PayloadUUID=" + uuid,
    cwd=plugin_temp,
    stdout=asyncio.subprocess.PIPE,
    stderr=asyncio.subprocess.PIPE
)
b_stdout, b_stderr = await build_proc.communicate()
if build_proc.returncode != 0:
    output = b_stdout.decode() + b_stderr.decode()
    raise Exception(
        "Error compiling plugin: " + output
    )

build_out = os.path.join(
    plugin_temp, "bin", "Release", "net10.0"
)

if obfuscate:
    map_path = os.path.join(
        build_out, "obf-map.json")
    il_proc = await asyncio.create_subprocess_exec(
        obfuscator_bin, "rewrite-il-batch",
        "--seed", str(obf_seed),
        "--dir", build_out,
        "--map", map_path,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE
    )
    _, il_stderr = await il_proc.communicate()
    if il_proc.returncode != 0:
        raise Exception(
            "IL batch rewrite failed: "
            + il_stderr.decode()
        )

# Discover plugin DLL (may have been renamed)
if obfuscate:
    # Read the rename map to find the new name
    import json as _json
    with open(map_path, "r") as mf:
        obf_map = _json.load(mf)
    renames = obf_map.get("metadataRenames", {})

    # Look up the original plugin assembly name
    # in the asm:* entries
    orig_name = command.lower()
    asm_key = "asm:" + orig_name
    new_name = renames.get(asm_key, orig_name)
    dll_path = os.path.join(
        build_out, new_name + ".dll")
    if not os.path.isfile(dll_path):
        # Try platform-specific name
        orig_plat = f"{command.lower()}-{target_os}"
        asm_key_plat = "asm:" + orig_plat
        new_plat = renames.get(
            asm_key_plat, orig_plat)
        dll_path = os.path.join(
            build_out, new_plat + ".dll")
    if not os.path.isfile(dll_path):
        raise Exception(
            "Plugin DLL not found after batch "
            "rewrite. Map: " + str(renames)
        )
else:
    dll_name_platform = (
        f"{command.lower()}-{target_os}.dll"
    )
    dll_name_generic = f"{command.lower()}.dll"
    dll_platform = os.path.join(
        build_out, dll_name_platform)
    dll_generic = os.path.join(
        build_out, dll_name_generic)
    if os.path.isfile(dll_platform):
        dll_path = dll_platform
    elif os.path.isfile(dll_generic):
        dll_path = dll_generic
    else:
        raise Exception(
            "Failed to compile plugin, "
            "DLL not found: " + dll_generic
        )
```

- [ ] **Step 4: Commit**

```bash
git add mythic/agent_functions/builder.py mythic/agent_functions/load.py
git commit -m "feat: derive obfuscation seed from UUID, use rewrite-il-batch in plugin builds"
```

---

### Task 7: Run Full Test Suite and Verify

**Files:** None (verification only)

- [ ] **Step 1: Run all obfuscator unit tests**

```bash
cd Payload_Type/athena/athena/agent_code && dotnet test Tests/Obfuscator.Tests -v n
```

Expected: All tests pass (existing + 10 new tests from Tasks 1-3).

**Note on ChannelLoadingTests:** The spec lists a `ChannelLoadingTests.ChannelsDiscoveredByInterface` test. This requires the full Autofac + ServiceHost test infrastructure which lives in `Tests/Workflow.Tests`, not `Tests/Obfuscator.Tests`. The build integration tests (`ObfuscatedSource_ServiceHostWithPlugins_Builds`) serve as the functional validation for channel loading. A dedicated unit test can be added to `Workflow.Tests` in a follow-up if needed.

- [ ] **Step 2: Run the build integration tests**

```bash
cd Payload_Type/athena/athena/agent_code && dotnet test Tests/Obfuscator.Tests --filter "TestCategory=Integration" -v n --timeout 600000
```

Expected: Both `ObfuscatedSource_CoreProjects_Build` and `ObfuscatedSource_ServiceHostWithPlugins_Builds` pass.

**If integration tests fail:** Check for `CS0103` errors about `AssemblyNames` (missed callers). Check for `TypeLoadException` or `MissingMethodException` (cross-ref patching gaps). Check the test output for specific error messages.

- [ ] **Step 3: Run the Workflow.Tests**

```bash
cd Payload_Type/athena/athena/agent_code && dotnet test Tests/Workflow.Tests -v n
```

Expected: All pass.

- [ ] **Step 4: Final commit if any fixes were needed**

Only commit if earlier steps required fixes:

```bash
git add -A && git commit -m "fix: address issues found during integration testing"
```
