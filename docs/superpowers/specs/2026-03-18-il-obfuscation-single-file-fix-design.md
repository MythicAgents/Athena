# Design: Fix IL Obfuscation for Single-File Publish Builds

**Date:** 2026-03-18
**Branch:** dev-obfuscation-fix
**Status:** Approved

---

## Problem Statement

The Athena agent's `ObfuscateILBatch` MSBuild target is a no-op for
`PublishSingleFile=true` builds (the default). As a result, all
`Workflow.*` namespace names, assembly names (e.g. `cursed`,
`Workflow.Channels.Http`), type names, method names, and field names
remain in plaintext in the published binary.

### Root Cause

`ObfuscateILBatch` runs on `$(PublishDir)`. For single-file publish,
the .NET 10 SDK routes bundled DLLs directly from each project's build
output directory into the `@(FilesToBundle)` item group. They are
**never copied to `$(PublishDir)`**. The target fires on an empty
directory and silently does nothing.

### .NET 10 Publish Pipeline (relevant targets)

```
_ComputeFilesToBundle         → populates @(_FilesToBundle) from @(ResolvedFileToPublish)
PrepareForBundle              → DependsOnTargets="_ComputeFilesToBundle"
                                promotes @(_FilesToBundle) → @(FilesToBundle) (public)
GenerateSingleFileBundle      → DependsOnTargets="_ComputeFilesToBundle;PrepareForBundle;..."
                                calls GenerateBundle task with @(FilesToBundle)
CopyFilesToPublishDirectory   → copies non-bundled files to $(PublishDir)
GeneratePublishDependencyFile → generates .deps.json from publish metadata
```

For non-single-file builds, `CopyFilesToPublishDirectory` copies all
DLLs to `$(PublishDir)` as expected.

---

## Solution: Two-Target Approach

Replace the single `ObfuscateILBatch` target (and its debug `ls -la`
diagnostic exec) with two mutually-exclusive condition-guarded targets.

### Target 1 — Non-Bundle Builds

**Target name:** `ObfuscateIL_NonBundle`
**Hook:** `AfterTargets="CopyFilesToPublishDirectory" BeforeTargets="GeneratePublishDependencyFile"`
**Condition:** `'$(Obfuscate)' == 'true' AND '$(ObfuscatorPath)' != '' AND '$(PublishSingleFile)' != 'true'`

DLLs are in `$(PublishDir)` after `CopyFilesToPublishDirectory`.
The `BeforeTargets="GeneratePublishDependencyFile"` preserves the
ordering guarantee of the original target. Full obfuscation runs
including `AssemblyRenameTransform` Phase 3 (physical file rename).

### Target 2 — Single-File Bundle Builds

**Target name:** `ObfuscateIL_Bundle`
**Hook:** `AfterTargets="PrepareForBundle" BeforeTargets="GenerateSingleFileBundle"`
**Condition:** `'$(Obfuscate)' == 'true' AND '$(ObfuscatorPath)' != '' AND '$(PublishSingleFile)' == 'true'`

`@(FilesToBundle)` is fully populated at this point. Each item has
`FullPath` (source path) and `RelativePath` metadata.

**Steps:**

1. Create staging directory: `$(IntermediateOutputPath)obf-stage/`
2. Copy every `.dll` item from `@(FilesToBundle)` flat into staging
3. Run `rewrite-il-batch --seed $(ObfSeed) --dir <staging> --skip-file-rename`
4. Copy modified DLLs from staging back over originals (see copy-back below)

**Why `--skip-file-rename`:** `AssemblyRenameTransform` Phase 3
renames physical `.dll` files on disk. If Phase 3 runs in staging, the
renamed filenames no longer match the original `FullPath` values —
copy-back would have no way to identify the correct destination.
Skipping Phase 3 keeps filenames stable while Phases 1 and 2 still
obfuscate PE assembly identity and all cross-assembly references.

**Bundle entry filename vs. PE identity:** The single-file bundle
header stores entry filenames (e.g. `Workflow.Channels.Http.dll`).
The CLR uses the internal PE identity for assembly resolution, not the
bundle entry filename. Internal PE identity is obfuscated by Phase 2.

### Copy-Back Mechanism

After the batch rewrite, each staging DLL has the same filename as its
source. The copy-back uses an MSBuild `<ItemGroup>` transform to build
the destination list:

```xml
<!-- Build a mapping: staging filename → original FullPath -->
<ItemGroup>
  <_ObfStagedDll Include="$(IntermediateOutputPath)obf-stage\*.dll" />
  <!-- Match each staged DLL back to its FilesToBundle source by filename -->
  <_ObfCopyBack
    Include="@(_ObfStagedDll)"
    DestinationFiles="@(FilesToBundle->'%(FullPath)'->
      WithMetadataValue('Filename', '%(Filename)')->'%(FullPath)')" />
</ItemGroup>
<Copy
  SourceFiles="@(_ObfStagedDll)"
  DestinationFiles="@(_ObfStagedDll->'%(FullPath)' ->
    /* resolved per-item via item transform below */)" />
```

Because flat copy is safe (each filename is unique in
`@(FilesToBundle)` — see Staging section), each staged DLL maps
unambiguously to its original `FullPath`. The concrete MSBuild
expression builds a `<Copy>` task where `SourceFiles` is the staged
DLL list and `DestinationFiles` is the corresponding original source
paths, keyed by `%(Filename)%(Extension)`.

---

## New CLI Flag: `--skip-file-rename`

A no-argument boolean switch added to the `rewrite-il-batch` subcommand
in `Program.cs` using `System.CommandLine`:

```csharp
var skipFileRenameOption = new Option<bool>("--skip-file-rename")
{
    Description = "Skip physical file rename (Phase 3 of AssemblyRenameTransform)"
};
```

Propagation path:

```
Program.cs (CLI, --skip-file-rename flag, default false)
  → ILRewriter.RewriteBatch(dir, seed, map, skipFileRename = false)
    → AssemblyRenameTransform.RenameAll(directory, skipFileRename)
      → (skip Phase 3 File.Move when skipFileRename == true)
```

Phases 1 (build rename map) and 2 (rewrite PE identities and assembly
references) always run regardless of this flag. The flag defaults to
`false` to preserve the existing behavior of `load.py` and any other
caller.

---

## Plugin Dynamic Loading Compatibility

`load.py` calls `rewrite-il-batch` directly on the plugin's build
output directory (not via the MSBuild target):

```python
obf_seed = int(hashlib.sha256(uuid.encode()).hexdigest(), 16) & 0x7FFFFFFF
# runs: rewrite-il-batch --seed <obf_seed> --dir <build_out> --map obf-map.json
# (no --skip-file-rename)
new_name = renames.get("asm:" + orig_name, orig_name)
dll_path = os.path.join(build_out, new_name + ".dll")
```

`load.py` depends on Phase 3 (physical file rename) to locate the
plugin DLL by its renamed filename. **`load.py` must never pass
`--skip-file-rename`.** The flag is only passed from the
`ObfuscateIL_Bundle` MSBuild target. The `load.py` invocation is
unchanged by this design.

Same UUID-derived seed → same rename map → plugin assembly references
(e.g. `Workflow.Models` → `_a7x`) match the host's loaded obfuscated
assemblies at runtime.

---

## Staging Directory Layout

All DLLs from `@(FilesToBundle)` are copied flat into
`$(IntermediateOutputPath)obf-stage/`. The flat layout is **required**
for two reasons:

1. **Cecil assembly resolver:** `ILRewriter.RewriteBatch` passes
   `directory` as the `searchDir` to both `MetadataManglingTransform`
   and `CrossReferenceTransform`, which use Mono.Cecil's
   `DefaultAssemblyResolver` with that path. All DLLs must be
   co-located so cross-assembly references resolve correctly. Preserving
   subdirectories would break the resolver.

2. **`AssemblyRenameTransform` needs all assemblies together:** The
   transform builds a complete rename map across all assemblies in a
   single pass. All DLLs must be in one directory.

**Filename uniqueness:** MSBuild's `@(FilesToBundle)` is built from
`@(ResolvedFileToPublish)`, which the SDK deduplicates by
`RelativePath`. Each assembly appears at most once. Flat copy is safe.

---

## Files Changed

| File | Change |
|------|--------|
| `agent_code/Directory.Build.targets` | Remove `ObfuscateILBatch` (including debug `ls -la` exec). Add `ObfuscateIL_NonBundle` and `ObfuscateIL_Bundle` with explicit Condition guards |
| `agent_code/Obfuscator/Program.cs` | Add `--skip-file-rename` no-argument boolean option to `rewrite-il-batch` command; pass to `ILRewriter.RewriteBatch` |
| `agent_code/Obfuscator/IL/ILRewriter.cs` | Add `bool skipFileRename = false` parameter to `RewriteBatch`; pass to `AssemblyRenameTransform.RenameAll` |
| `agent_code/Obfuscator/IL/Transforms/AssemblyRenameTransform.cs` | Add `bool skipFileRename = false` parameter to `RenameAll`; wrap Phase 3 `File.Move` block in `if (!skipFileRename)` |

---

## Validation Plan

1. Build an obfuscated Release payload via Mythic
2. Use ILSpy MCP to inspect the output binary — confirm `Workflow.*`
   namespaces, assembly names, type names, and method names are
   obfuscated
3. Confirm the agent callbacks successfully to Mythic
4. Build and load a plugin via `load.py` — confirm plugin loads and
   executes commands successfully
