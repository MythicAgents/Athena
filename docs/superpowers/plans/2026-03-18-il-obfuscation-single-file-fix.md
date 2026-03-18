# IL Obfuscation Single-File Publish Fix — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the IL obfuscation pipeline so that `PublishSingleFile=true` builds (the default) actually have their DLL type/method/namespace names obfuscated in the final binary.

**Architecture:** Replace the broken single `ObfuscateILBatch` MSBuild target (which fires on an empty `$(PublishDir)`) with two condition-guarded targets: one for non-bundle builds that hooks `AfterTargets="CopyFilesToPublishDirectory"`, and one for single-file builds that hooks `AfterTargets="PrepareForBundle" BeforeTargets="GenerateSingleFileBundle"` using a staging directory. Add a `--skip-file-rename` flag to prevent `AssemblyRenameTransform` from physically renaming files in the staging directory (which would break copy-back), while Phases 1 and 2 (type/namespace/method renaming and assembly identity rewrite) still run.

**Tech Stack:** C# / .NET 10, MSBuild XML, MSTest + Roslyn (in-memory compilation) for tests, Mono.Cecil for IL manipulation, System.CommandLine for CLI.

**Spec:** `docs/superpowers/specs/2026-03-18-il-obfuscation-single-file-fix-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `Payload_Type/athena/athena/agent_code/Obfuscator/IL/Transforms/AssemblyRenameTransform.cs` | Modify | Add `skipFileRename` param; gate Phase 3 `File.Move` behind it |
| `Payload_Type/athena/athena/agent_code/Obfuscator/IL/ILRewriter.cs` | Modify | Add `skipFileRename` param to `RewriteBatch`; pass through to `AssemblyRenameTransform` |
| `Payload_Type/athena/athena/agent_code/Obfuscator/Program.cs` | Modify | Add `--skip-file-rename` no-arg bool option to `rewrite-il-batch` subcommand |
| `Payload_Type/athena/athena/agent_code/Directory.Build.targets` | Modify | Replace `ObfuscateILBatch` (+ debug `ls -la`) with `ObfuscateIL_NonBundle` and `ObfuscateIL_Bundle` |
| `Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/AssemblyRenameTests.cs` | Modify | Add `SkipFileRename_*` tests |
| `Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/BatchRewriteTests.cs` | Modify | Add `RewriteBatch_SkipFileRename_*` test |

---

## Task 1: Gate Phase 3 behind `skipFileRename` in `AssemblyRenameTransform`

**Files:**
- Modify: `Payload_Type/athena/athena/agent_code/Obfuscator/IL/Transforms/AssemblyRenameTransform.cs`
- Modify: `Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/AssemblyRenameTests.cs`

### Background

`AssemblyRenameTransform.RenameAll` has three phases:
1. Build a `renameMap` (original name → obfuscated name)
2. Rewrite PE assembly identity and assembly references in each DLL
3. `File.Move` each DLL to its obfuscated filename

Phase 3 breaks the MSBuild copy-back step when obfuscating the bundle staging directory, because `@(FilesToBundle)` paths use the original filenames as keys. We need to skip Phase 3 for the staging path only, while keeping the default behavior for `load.py` and non-bundle builds.

The current `RenameAll` signature is:
```csharp
public Dictionary<string, string> RenameAll(string directory)
```

- [ ] **Step 1: Write failing tests in `AssemblyRenameTests.cs`**

Add two new test methods to the existing `AssemblyRenameTests` class. These go at the end of the class, before the `// --- Helpers ---` comment.

```csharp
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
```

- [ ] **Step 2: Run to verify tests fail**

```
dotnet test Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/Obfuscator.Tests.csproj --filter "SkipFileRename" -v normal
```

Expected: compile error — `RenameAll` has no `skipFileRename` parameter.

- [ ] **Step 3: Add `skipFileRename` parameter to `AssemblyRenameTransform.RenameAll`**

In `AssemblyRenameTransform.cs`, change:

```csharp
// FROM:
public Dictionary<string, string> RenameAll(
    string directory)

// TO:
public Dictionary<string, string> RenameAll(
    string directory,
    bool skipFileRename = false)
```

Then find the Phase 3 block (the `foreach` loop at the bottom of `RenameAll` that calls `File.Move`) and wrap it:

```csharp
// Phase 3: Rename physical files
if (!skipFileRename)
{
    foreach (var (original, newName) in renameMap)
    {
        var oldPath = Path.Combine(
            directory, original + ".dll");
        var newPath = Path.Combine(
            directory, newName + ".dll");
        if (File.Exists(oldPath))
            File.Move(oldPath, newPath);
    }
}
```

- [ ] **Step 4: Run to verify tests pass**

```
dotnet test Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/Obfuscator.Tests.csproj --filter "SkipFileRename" -v normal
```

Expected: 2 tests PASS.

- [ ] **Step 5: Run full test suite to verify no regressions**

```
dotnet test Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/Obfuscator.Tests.csproj -v normal
```

Expected: all tests PASS.

- [ ] **Step 6: Commit**

```bash
git add \
  Payload_Type/athena/athena/agent_code/Obfuscator/IL/Transforms/AssemblyRenameTransform.cs \
  Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/AssemblyRenameTests.cs
git commit -m "feat: add skipFileRename param to AssemblyRenameTransform.RenameAll"
```

---

## Task 2: Thread `skipFileRename` through `ILRewriter.RewriteBatch`

**Files:**
- Modify: `Payload_Type/athena/athena/agent_code/Obfuscator/IL/ILRewriter.cs`
- Modify: `Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/BatchRewriteTests.cs`

### Background

`ILRewriter.RewriteBatch` calls `AssemblyRenameTransform.RenameAll(directory)` at Step 3. It needs to accept and forward `skipFileRename` so the CLI and MSBuild targets can control Phase 3 behavior.

Current signature:
```csharp
public void RewriteBatch(
    string directory, int seed, string? mapPath)
```

- [ ] **Step 1: Write failing test in `BatchRewriteTests.cs`**

Add after `RewriteBatch_SameSeed_Deterministic`:

```csharp
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
```

- [ ] **Step 2: Run to verify test fails**

```
dotnet test Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/Obfuscator.Tests.csproj --filter "RewriteBatch_SkipFileRename" -v normal
```

Expected: compile error — `RewriteBatch` has no `skipFileRename` parameter.

- [ ] **Step 3: Update `ILRewriter.RewriteBatch` signature and pass-through**

```csharp
// FROM:
public void RewriteBatch(
    string directory, int seed, string? mapPath)

// TO:
public void RewriteBatch(
    string directory,
    int seed,
    string? mapPath,
    bool skipFileRename = false)
```

Then in the body, update the `AssemblyRenameTransform` call:

```csharp
// Step 3: AssemblyRenameTransform
var asmRename = new AssemblyRenameTransform(seed);
var renameMap = asmRename.RenameAll(
    directory, skipFileRename);
```

- [ ] **Step 4: Run to verify test passes**

```
dotnet test Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/Obfuscator.Tests.csproj --filter "RewriteBatch_SkipFileRename" -v normal
```

Expected: PASS.

- [ ] **Step 5: Run full test suite**

```
dotnet test Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/Obfuscator.Tests.csproj -v normal
```

Expected: all tests PASS.

- [ ] **Step 6: Commit**

```bash
git add \
  Payload_Type/athena/athena/agent_code/Obfuscator/IL/ILRewriter.cs \
  Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/BatchRewriteTests.cs
git commit -m "feat: thread skipFileRename through ILRewriter.RewriteBatch"
```

---

## Task 3: Add `--skip-file-rename` CLI flag to `Program.cs`

**Files:**
- Modify: `Payload_Type/athena/athena/agent_code/Obfuscator/Program.cs`

### Background

`Program.cs` defines the `rewrite-il-batch` subcommand with three options: `--seed`, `--dir`, `--map`. The `SetAction` callback calls `rewriter.RewriteBatch(dir, seed, map)`. We need to add a no-argument boolean option `--skip-file-rename` and wire it through.

System.CommandLine no-argument bool options default to `false` when the flag is absent and `true` when present (like `--verbose` flags). Declare with `new Option<bool>("--skip-file-rename")`.

- [ ] **Step 1: Locate the `rewrite-il-batch` section in `Program.cs`**

The file is at `Payload_Type/athena/athena/agent_code/Obfuscator/Program.cs`. The `rewrite-il-batch` section starts around line 95 with `var batchSeedOption = new Option<int>("--seed")`.

- [ ] **Step 2: Add the `--skip-file-rename` option declaration**

After `var batchMapOption` (around line 109) and before `var rewriteIlBatchCommand`, add:

```csharp
var batchSkipFileRenameOption =
    new Option<bool>("--skip-file-rename")
    {
        Description =
            "Skip physical file rename (Phase 3 of "
            + "AssemblyRenameTransform). Used for "
            + "single-file bundle builds where source "
            + "paths must remain stable."
    };
```

- [ ] **Step 3: Add the option to the command**

Change the `rewriteIlBatchCommand` initializer from:

```csharp
var rewriteIlBatchCommand = new Command(
    "rewrite-il-batch",
    "Batch rewrite IL in all assemblies in a directory")
{
    batchSeedOption,
    batchDirOption,
    batchMapOption
};
```

to:

```csharp
var rewriteIlBatchCommand = new Command(
    "rewrite-il-batch",
    "Batch rewrite IL in all assemblies in a directory")
{
    batchSeedOption,
    batchDirOption,
    batchMapOption,
    batchSkipFileRenameOption
};
```

- [ ] **Step 4: Update the `SetAction` callback**

Change:

```csharp
rewriteIlBatchCommand.SetAction((parseResult) =>
{
    var seed = parseResult.GetValue(batchSeedOption);
    var dir = parseResult.GetValue(batchDirOption)!;
    var map = parseResult.GetValue(batchMapOption);

    var rewriter = new ILRewriter();
    rewriter.RewriteBatch(dir, seed, map);
});
```

to:

```csharp
rewriteIlBatchCommand.SetAction((parseResult) =>
{
    var seed = parseResult.GetValue(batchSeedOption);
    var dir = parseResult.GetValue(batchDirOption)!;
    var map = parseResult.GetValue(batchMapOption);
    var skipFileRename =
        parseResult.GetValue(batchSkipFileRenameOption);

    var rewriter = new ILRewriter();
    rewriter.RewriteBatch(dir, seed, map, skipFileRename);
});
```

- [ ] **Step 5: Build to verify no compile errors**

```
dotnet build Payload_Type/athena/athena/agent_code/Obfuscator/Obfuscator.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Smoke-test the flag appears in help**

```
dotnet run --project Payload_Type/athena/athena/agent_code/Obfuscator/Obfuscator.csproj -- rewrite-il-batch --help
```

Expected: output includes `--skip-file-rename`.

- [ ] **Step 7: Run tests to check nothing regressed**

```
dotnet test Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/Obfuscator.Tests.csproj -v normal
```

Expected: all tests PASS.

- [ ] **Step 8: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/Obfuscator/Program.cs
git commit -m "feat: add --skip-file-rename flag to rewrite-il-batch CLI"
```

---

## Task 4: Replace `Directory.Build.targets` with two-target approach

**Files:**
- Modify: `Payload_Type/athena/athena/agent_code/Directory.Build.targets`

### Background

The current file contains one broken target:

```xml
<Target Name="ObfuscateILBatch"
        BeforeTargets="GeneratePublishDependencyFile;GenerateSingleFileBundle"
        Condition="'$(Obfuscate)' == 'true'
                   AND '$(ObfuscatorPath)' != ''">
  <Message Text="Obfuscating IL (batch): $(PublishDir)" Importance="high" />
  <Exec Command="ls -la &quot;$(PublishDir)&quot;*.dll || true" IgnoreExitCode="true" />
  <Exec Command="$(ObfuscatorPath) rewrite-il-batch --seed $(ObfSeed) --dir &quot;$(PublishDir)&quot;" />
</Target>
```

Problems:
- For `PublishSingleFile=true` (default): runs on empty `$(PublishDir)` — no-op
- Has a debug `ls -la` diagnostic exec that should be removed
- No `AfterTargets` hook, so ordering for non-bundle builds is fragile

Replace with:

**`ObfuscateIL_NonBundle`** — for non-bundle builds where DLLs are in `$(PublishDir)`:
- `AfterTargets="CopyFilesToPublishDirectory"` — DLLs are present
- `BeforeTargets="GeneratePublishDependencyFile"` — preserves ordering guarantee
- Condition: `PublishSingleFile != true`
- Runs `rewrite-il-batch --dir $(PublishDir)` with full rename (Phase 3 enabled)

**`ObfuscateIL_Bundle`** — for single-file builds:
- `AfterTargets="PrepareForBundle"` — `@(FilesToBundle)` is fully populated
- `BeforeTargets="GenerateSingleFileBundle"` — runs before bundling
- Condition: `PublishSingleFile == true`
- Steps: create staging dir → copy DLLs flat → `rewrite-il-batch --skip-file-rename` → copy back

The copy-back uses an MSBuild pattern that filters `@(FilesToBundle)` to `.dll` items that exist in staging, then copies from staging → `%(FullPath)`:

```xml
<ItemGroup>
  <_BundleDllsToCopyBack
    Include="@(FilesToBundle)"
    Condition="'%(Extension)' == '.dll'
      AND Exists('$(ObfStagingDir)%(Filename)%(Extension)')" />
</ItemGroup>
<Copy
  SourceFiles="@(_BundleDllsToCopyBack->
    '$(ObfStagingDir)%(Filename)%(Extension)')"
  DestinationFiles="@(_BundleDllsToCopyBack->
    '%(FullPath)')" />
```

- [ ] **Step 1: Replace the entire `Directory.Build.targets` content**

The full replacement (write exactly this to the file):

```xml
<Project>
  <Import Project="$(AthenaExternalBuildTargets)"
          Condition="'$(AthenaExternalBuildTargets)' != ''
                     And Exists('$(AthenaExternalBuildTargets)')"/>

  <!-- Non-bundle publish: DLLs are in $(PublishDir) after copy -->
  <Target Name="ObfuscateIL_NonBundle"
          AfterTargets="CopyFilesToPublishDirectory"
          BeforeTargets="GeneratePublishDependencyFile"
          Condition="'$(Obfuscate)' == 'true'
                     AND '$(ObfuscatorPath)' != ''
                     AND '$(PublishSingleFile)' != 'true'">
    <Message Text="Obfuscating IL (non-bundle): $(PublishDir)"
             Importance="high" />
    <Exec Command="$(ObfuscatorPath) rewrite-il-batch --seed $(ObfSeed) --dir &quot;$(PublishDir)&quot;" />
  </Target>

  <!-- Single-file bundle publish: stage DLLs, obfuscate, copy back -->
  <PropertyGroup>
    <ObfStagingDir>$(IntermediateOutputPath)obf-stage/</ObfStagingDir>
  </PropertyGroup>

  <Target Name="ObfuscateIL_Bundle"
          AfterTargets="PrepareForBundle"
          BeforeTargets="GenerateSingleFileBundle"
          Condition="'$(Obfuscate)' == 'true'
                     AND '$(ObfuscatorPath)' != ''
                     AND '$(PublishSingleFile)' == 'true'">
    <Message Text="Obfuscating IL (bundle): staging to $(ObfStagingDir)"
             Importance="high" />

    <!-- Step 1: Create staging directory -->
    <MakeDir Directories="$(ObfStagingDir)" />

    <!-- Step 2: Copy all bundle DLLs flat into staging -->
    <Copy SourceFiles="@(FilesToBundle)"
          DestinationFolder="$(ObfStagingDir)"
          Condition="'%(FilesToBundle.Extension)' == '.dll'" />

    <!-- Step 3: Obfuscate in place (skip physical file rename) -->
    <Exec Command="$(ObfuscatorPath) rewrite-il-batch --seed $(ObfSeed) --dir &quot;$(ObfStagingDir)&quot; --skip-file-rename" />

    <!-- Step 4: Copy obfuscated DLLs back to original source paths -->
    <ItemGroup>
      <_BundleDllsToCopyBack
        Include="@(FilesToBundle)"
        Condition="'%(Extension)' == '.dll'
          AND Exists('$(ObfStagingDir)%(Filename)%(Extension)')" />
    </ItemGroup>
    <Copy
      SourceFiles="@(_BundleDllsToCopyBack->'$(ObfStagingDir)%(Filename)%(Extension)')"
      DestinationFiles="@(_BundleDllsToCopyBack->'%(FullPath)')" />
  </Target>
</Project>
```

- [ ] **Step 2: Verify the file looks correct**

Read `Payload_Type/athena/athena/agent_code/Directory.Build.targets` and confirm:
- No `ObfuscateILBatch` target remains
- No `ls -la` exec remains
- Both new targets have explicit `AND '$(PublishSingleFile)' == ...` conditions
- `ObfStagingDir` property is defined in the `<PropertyGroup>`

- [ ] **Step 3: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/Directory.Build.targets
git commit -m "fix: replace ObfuscateILBatch with two-target IL obfuscation for single-file builds"
```

---

## Task 5: End-to-End Validation

### Background

This task validates the full pipeline on a real Mythic build. All code changes are done; this task is verification only.

**Prerequisites:** Mythic server running at `10.30.26.108`, Athena container running.

- [ ] **Step 1: Patch the running Athena container with updated files**

```bash
# Copy changed files into the running container
docker cp \
  Payload_Type/athena/athena/agent_code/Obfuscator/IL/Transforms/AssemblyRenameTransform.cs \
  athena:/Mythic/athena/agent_code/Obfuscator/IL/Transforms/AssemblyRenameTransform.cs

docker cp \
  Payload_Type/athena/athena/agent_code/Obfuscator/IL/ILRewriter.cs \
  athena:/Mythic/athena/agent_code/Obfuscator/IL/ILRewriter.cs

docker cp \
  Payload_Type/athena/athena/agent_code/Obfuscator/Program.cs \
  athena:/Mythic/athena/agent_code/Obfuscator/Program.cs

docker cp \
  Payload_Type/athena/athena/agent_code/Directory.Build.targets \
  athena:/Mythic/athena/agent_code/Directory.Build.targets
```

- [ ] **Step 2: Build a new obfuscated Release payload via Mythic MCP**

Use `mcp__mythic-mcp__create_payload` with the HTTP c2 profile and obfuscation enabled. Record the payload UUID from the response.

- [ ] **Step 3: Retrieve the built payload**

Use `mcp__mythic-mcp__download_payload` with the payload UUID. The payload is saved as a `.zip` on the Mythic server at `/tmp/`. Extract the `.exe` via SFTP or `docker exec`.

- [ ] **Step 4: Inspect with ILSpy MCP**

For a single-file bundle, ILSpy cannot inspect the `.exe` directly. Instead, examine the build artifacts on the container to confirm obfuscation ran:

```bash
# Check build log for the obfuscation message
docker exec athena bash -c "find /tmp -name '*.log' | xargs grep -l 'Obfuscating IL (bundle)' 2>/dev/null | head -5"
```

Alternatively, inspect the obf-map.json if it was written:

```bash
docker exec athena bash -c "find /tmp -name 'obf-map.json' | head -3 | xargs -I{} cat {}"
```

Confirm the map contains obfuscated names (keys like `Workflow.Channels.Http` → values like `_a7x`).

- [ ] **Step 5: Verify agent callbacks**

Use `mcp__mythic-mcp__list_callbacks` to confirm a new callback appears after running the payload. Check callback output for normal operation.

- [ ] **Step 6: Test plugin dynamic loading (optional but recommended)**

If a plugin build is available, load it via `load.py` through Mythic. Use `mcp__mythic-mcp__issue_task` to issue a `load` command with a plugin DLL. Confirm the command executes successfully.

- [ ] **Step 7: Final commit**

If any minor fixes were needed during validation, commit them:

```bash
git add -A
git commit -m "fix: validation fixes for IL obfuscation single-file build"
```
