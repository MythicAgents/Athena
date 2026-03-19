# Bundle Assembly Name Obfuscation — Design Spec

**Date:** 2026-03-19
**Branch:** `dev-bundle-assembly-rename` (new, branching from `dev`)
**Status:** Approved, pending implementation

---

## Problem

The Athena single-file bundle exe exposes `Workflow.*` assembly names in its embedded
manifest and in the `AssemblyDef` PE metadata headers of every embedded DLL (e.g.,
`Workflow.Security.AES`, `Workflow.Providers.Runtime`). A static analyst can see these
names without decompiling any code.

The existing `AssemblyRenameTransform` handles non-bundle publishes correctly — it
renames assembly identities, all cross-assembly references, and physical DLL filenames.
It is disabled for single-file bundles (`--skip-assembly-rename`) because:

1. The .NET single-file bundle format embeds a manifest listing files by their original
   names. Mono.Cecil cannot reach this manifest — it is a custom binary structure
   separate from PE metadata tables.
2. Dynamically loaded command DLLs reference `Workflow.Contracts` etc. by name; if the
   bundle renames those to `_k7` the CLR cannot match the reference against the
   already-loaded (still-named) assembly.

---

## Goals

- Hide all `Workflow.*` (and other non-skipped) assembly names from the single-file
  bundle binary.
- Dynamically loaded command DLLs must continue to work — their `AssemblyRef` entries
  must match the renamed names used inside the bundle.
- No persistent storage of rename maps across container reinstalls.
- Minimal changes to the existing obfuscation pipeline.

---

## Non-Goals

- Renaming `System.*` / `Microsoft.*` / third-party assemblies (already excluded).
- Hiding the entry assembly name (the apphost has this baked in; renaming it breaks boot).
- Patching `.deps.json` assembly names (managed resolution in single-file bundles goes
  through the bundle ALC, not normal probing; `.deps.json` is not the resolution source).

---

## Design

### Core Insight: Per-Name-Salted RNG

The current `AssemblyRenameTransform` generates names using a positional counter against
a seeded RNG. The Nth assembly processed gets the Nth RNG output. This means:

- Main bundle batch (50 DLLs): `Workflow.Contracts` → 15th RNG value → `_k7`
- Command DLL batch (3 DLLs): `Workflow.Contracts` → 1st RNG value → `_a2` ← **mismatch**

**Fix:** Derive each assembly's new name from a seed salted with that assembly's own
name, making the mapping order-independent:

```csharp
var salt = BitConverter.ToInt32(
    SHA256.HashData(Encoding.UTF8.GetBytes(originalAssemblyName)), 0);
var perNameRng = new Random(_seed ^ salt);
var newName = GenerateName(perNameRng);
```

`Workflow.Contracts` → `_k7` in every batch, every build, without any stored state.
This eliminates the need to persist or restore assembly rename maps across container
reinstalls.

---

### Component 1: `AssemblyRenameTransform` — salted RNG

**File:** `Obfuscator/IL/Transforms/AssemblyRenameTransform.cs`

Change the internal `GenerateAssemblyName(string originalName)` method to derive a
per-name `Random` instance using `new Random(_seed ^ nameHash)` rather than the shared
sequential RNG instance. Collision detection (ensure uniqueness within the batch)
remains unchanged.

**Tests:** Existing unit tests must pass unchanged. Add one new test:
`AssemblyRename_SameNameSameResult_AcrossDifferentBatches` — runs
`AssemblyRenameTransform` on two different-sized in-memory module sets and asserts that
assemblies present in both batches receive identical new names.

---

### Component 2: `obfuscator patch-bundle` CLI subcommand

**New file:** `Obfuscator/IL/BundlePatcher.cs`
**Updated file:** `Obfuscator/Program.cs`

```
obfuscator patch-bundle
  --input <exe>    Single-file bundle exe (modified in place)
  --seed  <int>    RNG seed (same seed used for the rest of the build)
  [--map  <path>]  Optional path to write/merge assembly rename entries
```

**Algorithm:**

1. `BundleExtractor.Extract(inputExe, tempDir)` — extracts all embedded files to a
   temp directory, preserving subdirectory structure.
2. Identify the **entry assembly**: parse the `.deps.json` filename from the bundle
   manifest entries (its name is `<AppName>.deps.json`); derive entry assembly name as
   `<AppName>.dll`. Add it to the skip list for this run.
3. Run `AssemblyRenameTransform` on all `.dll` files in `tempDir` (applying the usual
   `SkipPrefixes` plus the entry assembly name). This renames `AssemblyDef` names in PE
   metadata AND renames physical files on disk.
4. Run `CrossReferenceTransform` on the same set to patch `AssemblyRef` entries across
   all extracted DLLs.
5. Write assembly rename entries to `--map` if provided.
6. Build the new `FileSpec` list: for each original bundle entry, if its filename was
   renamed, use the new filename as both the source path (from tempDir) and the
   `bundleRelativePath`; otherwise use the original.
7. `new Bundler(...).Bundle(appHostBytes, fileSpecs, outputPath)` — creates the new exe.
8. Replace `--input` atomically (write to `.tmp`, then `File.Move(..., overwrite: true)`).

**NuGet dependency:** Add `Microsoft.NET.HostModel` to `Obfuscator/Obfuscator.csproj`.
Pin to the version matching the target .NET SDK (currently `net10.0`).

---

### Component 3: `Directory.Build.targets` — post-bundle target

**File:** `Payload_Type/athena/athena/agent_code/Directory.Build.targets`

Add a new MSBuild target after the existing `ObfuscateIL_Bundle`:

```xml
<Target Name="ObfuscateBundleNames"
        AfterTargets="GenerateSingleFileBundle"
        Condition="'$(Obfuscate)' == 'true'
                   AND '$(ObfuscatorPath)' != ''
                   AND '$(PublishSingleFile)' == 'true'">
  <PropertyGroup>
    <_BundleExe>$(PublishDir)$(AssemblyName)$(NativeExecutableExtension)</_BundleExe>
  </PropertyGroup>
  <Message Text="Patching bundle assembly names: $(_BundleExe)" Importance="high" />
  <Exec Command="$(ObfuscatorPath) patch-bundle
      --seed $(ObfSeed)
      --input &quot;$(_BundleExe)&quot;" />
</Target>
```

---

### Component 4: `load.py` — remove `--skip-assembly-rename`

**File:** `Payload_Type/athena/athena/mythic/agent_functions/load.py`

Remove `"--skip-assembly-rename"` from the `rewrite-il-batch` subprocess call (line
~339). The command DLL build output directory contains `Workflow.Contracts.dll`,
`Workflow.Models.dll`, and other direct-dependency DLLs as build outputs. With the
per-name-salted RNG, `AssemblyRenameTransform` produces identical names for these
assemblies regardless of batch size, so the command DLL's `AssemblyRef` entries will
match the names the bundle host has loaded.

---

### Component 5: `Obfuscation.md` — update docs

Update the "Not Obfuscated" table to remove the single-file bundle caveat row, and
add a row noting that the entry assembly name is preserved (apphost requirement).

---

## Data Flow

```
builder.py
  └─ rewrite-source (unchanged)
  └─ dotnet publish --self-contained --single-file
       └─ ObfuscateIL_Bundle target (unchanged — types/methods/fields renamed)
       └─ GenerateSingleFileBundle (Workflow.* names still in manifest at this point)
       └─ ObfuscateBundleNames target  ← NEW
            └─ obfuscator patch-bundle
                 ├─ BundleExtractor  → tempDir/
                 ├─ AssemblyRenameTransform (salted RNG)  → renames Workflow.* → _k7 etc.
                 ├─ CrossReferenceTransform → patches AssemblyRef entries
                 └─ Bundler → new exe (manifest has _k7.dll etc.)

load.py (per load task)
  └─ rewrite-source (unchanged)
  └─ dotnet build (command DLL + direct deps copied to build output)
  └─ rewrite-il-batch (no --skip-assembly-rename)  ← CHANGED
       ├─ AssemblyRenameTransform (salted RNG)
       │    Workflow.Contracts → _k7  (same salt → same name as in bundle)
       │    Workflow.Models    → _a2  (same)
       └─ CrossReferenceTransform → patches AssemblyRef in command DLL
```

---

## Testing

### Unit tests (Obfuscator.Tests)

1. **`AssemblyRename_SameNameSameResult_AcrossDifferentBatches`** — two in-memory
   module batches of different sizes; assert shared assembly names produce identical
   new names.
2. Existing 91 tests must remain green.

### Integration test

`ObfuscatedSource_CoreProjects_Build` already validates that the full obfuscated build
compiles and runs. It must continue to pass.

### Live E2E (per `test-obfuscated-payload` skill)

Build obfuscated payload with `commands: ["load", "exit"]`. After callback:
- ILSpy MCP: `search_members_by_name` for `Workflow` → zero matches in assembly names
  as well as type names.
- Load and execute the full test matrix: `jobs`, `pwd`, `ls`, `sysinfo`, `proc-enum`.
- All commands must return `status: success` with non-empty output.

---

## Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| `Microsoft.NET.HostModel` API changes with .NET version | Pin package version to SDK version; update on each .NET upgrade (same cadence as `Mono.Cecil`) |
| Entry assembly detection fails (no `.deps.json` in bundle) | Fallback: skip assemblies whose name matches the exe basename; log a warning |
| Salted-RNG collision produces duplicate names | Collision detection loop already present in `GenerateName`; per-name RNG restarts guarantee determinism on retry |
| `Bundler` apphost extraction requires exact host byte alignment | Use `BundleExtractor`'s returned host slice rather than manual offset arithmetic |
| Command DLL's direct deps don't include all `Workflow.*` assemblies referenced transitively | Not a problem: only DIRECT AssemblyRef entries in the command DLL's IL matter; transitive refs are resolved from the already-loaded bundle |
