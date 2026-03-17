# Obfuscator Coverage Expansion — Full Assembly Obfuscation

**Date:** 2026-03-17
**Status:** Draft
**Scope:** IL obfuscation of Workflow.* assemblies, assembly renaming, agent-side loading changes, build script coordination

## Problem

After the safety fixes landed, decompiled Athena output still exposes:

- **Assembly names** — `Workflow.Models.dll`, `Workflow.Channels.Http.dll`, `Workflow.Providers.Windows.dll` visible as filenames
- **Namespace/type names inside Workflow.* DLLs** — `Workflow.Config`, `Workflow.Providers`, `Workflow.Models` namespaces fully readable
- **Single-DLL IL processing** — `Directory.Build.targets` line 14 excludes all `Workflow.*` from `MetadataManglingTransform`

The Workflow.* exclusion exists because the IL rewriter processes one DLL at a time. Renaming types inside `Workflow.Models` breaks `TypeReference`s in consuming DLLs (plugins, channels, ServiceHost). Assembly renaming is blocked by `AssemblyNames.ForChannel`/`ForModule` which construct assembly names as runtime strings.

## Approach

Three coordinated changes:

1. **Remove string-based assembly loading** from agent code so assembly identities are never resolved by name strings
2. **Replace per-DLL IL obfuscation with a batch pass** that processes all assemblies together
3. **Add cross-assembly reference patching and assembly renaming** as part of the batch pass

### Seed Derivation

Both `builder.py` (host) and `load.py` (plugins) currently generate independent random seeds. Change both to derive the seed deterministically from the Payload UUID using a stable hash:

```python
import hashlib
seed = int(hashlib.sha256(uuid.encode()).hexdigest(), 16) & 0x7FFFFFFF
```

Python's built-in `hash()` is randomized per-process (since Python 3.3, via `PYTHONHASHSEED`). Using `hashlib.sha256` guarantees identical output across separate process invocations without requiring environment variable configuration.

This ensures host and plugin builds produce identical renames for shared types (Workflow.Models, Workflow.Providers.Windows). The UUID is available in both build paths.

**Note:** The seed is predictable if the UUID is known. This is acceptable — obfuscation is not encryption. Its purpose is to hinder casual reverse engineering, not resist targeted analysis.

## Fix 1: Remove String-Based Assembly Loading

### ContainerBuilder.TryLoadProfiles

**File:** `ServiceHost/Config/ContainerBuilder.cs`

Replace the hardcoded profile name list + `Assembly.Load(AssemblyNames.ForChannel(name))` with a scan of referenced assemblies:

```csharp
var entryAsm = Assembly.GetEntryAssembly();
foreach (var refName in entryAsm.GetReferencedAssemblies())
{
    if (refName.Name.StartsWith("System.")
        || refName.Name.StartsWith("Microsoft."))
        continue;

    try
    {
        var asm = Assembly.Load(refName);
        containerBuilder.RegisterAssemblyTypes(asm)
            .Where(t => typeof(IChannel).IsAssignableFrom(t))
            .As<IChannel>().SingleInstance();
    }
    catch { /* assembly not deployed for this config */ }
}
```

`GetReferencedAssemblies()` returns `AssemblyName` objects from assembly metadata — which the IL rewriter will have already patched. No string matching needed.

**Safety:** The `.Where(t => typeof(IChannel).IsAssignableFrom(t))` filter is defense-in-depth. Autofac's `.As<IChannel>()` already limits registration to types assignable to `IChannel`, but the explicit `.Where` makes the intent visible and guards against accidental registration of types that implement `IChannel` from unexpected assemblies. Framework assemblies are skipped by the `StartsWith` check. Assemblies that fail to load (not deployed for this build configuration) are caught and ignored, matching current behavior.

### ComponentProvider.TryLoadModule

**File:** `Workflow.Providers.Runtime/AssemblyManager.cs`

The `TryLoadModule` method currently falls back to `Assembly.Load(AssemblyNames.ForModule(name))`. Replace with scanning loaded assemblies for the module:

```csharp
private bool TryLoadModule(string name, out IModule? plugOut)
{
    plugOut = null;

    // Scan all loaded assemblies across all contexts.
    // AppDomain.CurrentDomain.GetAssemblies() includes assemblies
    // loaded via custom AssemblyLoadContexts (this.loadContext),
    // not just the default context. Do NOT use
    // this.loadContext.Assemblies here — it would miss assemblies
    // in the default context.
    //
    // Parse ALL assemblies before checking the dictionary.
    // ParseAssemblyForModule stores discovered modules in
    // loadedModules keyed by plug.Name. We must not return
    // early on the first IModule-containing assembly — it may
    // not be the one containing the requested module.
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        ParseAssemblyForModule(asm);
    }

    return loadedModules.TryGetValue(name, out plugOut);
}
```

**Name matching:** `ParseAssemblyForModule` discovers types via `typeof(IModule).IsAssignableFrom(t)`, creates an instance, and stores it in `loadedModules` keyed by `plug.Name` — the module's self-reported name (e.g., `"ps"`). The `name` parameter passed to `TryLoadModule` is the command name from the C2 task. These match because each plugin's `IModule.Name` property returns the command name. The `TryGetValue` after `ParseAssemblyForModule` succeeds only if the assembly contains a module whose `Name` matches the requested command.

### Delete AssemblyNames.cs

**File:** `Workflow.Models/AssemblyNames.cs`

Delete entirely. Three callers exist:

1. `ServiceHost/Config/ContainerBuilder.cs:76` — `ForChannel` — replaced by interface scan (this fix)
2. `Workflow.Providers.Runtime/AssemblyManager.cs:37` — `ForModule` — replaced by assembly scan (this fix)
3. `Tests/Workflow.Tests/PluginLoader.cs:38` — `ForModule` — update `GetPlugin` to scan `AppDomain.CurrentDomain.GetAssemblies()` with the same `typeof(IModule).IsAssignableFrom(t)` pattern, matching the production `TryLoadModule` replacement

No other callers exist (confirmed by grep for `AssemblyNames` and `ForModule`/`ForChannel`).

## Fix 2: Replace Per-DLL IL Obfuscation with Batch Pass

**File:** `Directory.Build.targets`

Remove the entire `ObfuscateIL` target (lines 5-21). The per-DLL `rewrite-il` invocations are replaced by a single `rewrite-il-batch` command invoked from `builder.py` after `dotnet publish` completes.

The old target ran `AfterTargets="ComputeFilesToPublish" BeforeTargets="GenerateSingleFileBundle"` to ensure IL rewriting happened before single-file bundling. The new batch command must also run before bundling — see Fix 5 for how this is handled.

**Why remove the whole target (not just the exclusion):** If we only removed the `Workflow.*` exclusion, the existing target would run `MetadataManglingTransform` per-DLL during MSBuild. Then the batch command would run it again post-publish, double-applying obfuscation. Removing the target ensures IL obfuscation runs exactly once via the batch command.

## Fix 3: Assembly Rename Transform

**File:** `Obfuscator/IL/Transforms/AssemblyRenameTransform.cs` (new)

A post-obfuscation pass that renames assembly identities and patches all cross-references.

**Input:** A directory containing all published DLLs + the obfuscation seed.

**Algorithm:**

1. Enumerate all DLLs in the directory
2. For each non-framework DLL (skip System.*, Microsoft.*, runtime.*), generate a deterministic renamed identity using the seed
3. Build a rename map: `{ "Workflow.Models" -> "_a7x", "Workflow.Channels.Http" -> "_k3m", ... }`
4. For each DLL:
   a. If the DLL's assembly name is in the map, rewrite `AssemblyDefinition.Name.Name`
   b. For each `AssemblyNameReference` in `module.AssemblyReferences`, if the name is in the map, update it
5. Write all modified DLLs back
6. Rename physical `.dll` files on disk

**Name generation:** Same `GenerateUniqueName` pattern as `MetadataManglingTransform` — `Random` with alphanumeric characters, prefixed with `_`. Use `new Random(seed ^ 0x5A5A5A5A)` (XOR with a fixed constant) to produce a different sequence than type-level renames while remaining deterministic. Both host and plugin builds must use the same constant.

**Scope:** Only renames assemblies whose original name doesn't start with `System.`, `Microsoft.`, or `runtime.`. The main executable assembly (entry point) is also renamed — `builder.py` already sets a random assembly name, so this is additive.

**Single-file publish:** When `PublishSingleFile=true`, DLLs are bundled into the executable by `GenerateSingleFileBundle`. The batch command (including assembly rename) must run BEFORE bundling. See Fix 5 for the MSBuild integration that ensures correct ordering.

## Fix 4: Cross-Assembly Type Reference Patching

**File:** `Obfuscator/IL/Transforms/CrossReferenceTransform.cs` (new)

When `MetadataManglingTransform` renames a type in `Workflow.Models` from `TaskResponse` to `_x7k`, consuming DLLs still have `TypeReference`s pointing to `TaskResponse`. This pass patches those references.

**Input:** A directory containing all published DLLs + per-assembly rename maps from individual `MetadataManglingTransform` runs.

**Algorithm:**

1. Run `MetadataManglingTransform` on each DLL individually, collecting rename maps via `GetRenameMappings()`. Keep maps separate per assembly — do NOT merge into a single flat dictionary. Store as `Dictionary<string, Dictionary<string, string>>` keyed by assembly name.

2. `GetRenameMappings()` currently returns `Dictionary<string, string>` using `type.FullName` (namespace-qualified) as key for types, and unqualified names for members/params. For cross-assembly patching, the transform must know which assembly a rename came from. The per-assembly storage in step 1 provides this context.

3. For each DLL, scan all:
   - `TypeReference` — extract the scope assembly name from `TypeReference.Scope`:
     - If `Scope` is `AssemblyNameReference`: use `.Name`
     - If `Scope` is `ModuleDefinition`: skip (same-module reference, already handled by `MetadataManglingTransform`)
     - If `Scope` is `ModuleReference`: skip (multi-module assemblies, out of scope)
   - Look up `(scopeAssemblyName, typeRef.FullName)` in the per-assembly rename maps. If found, update `TypeReference.Name` and `TypeReference.Namespace`.
   - `MemberReference` — get the declaring type's scope assembly, look up `(scopeAssemblyName, memberRef.Name)` in the rename map. If found, update `MemberReference.Name`.

4. Write all patched DLLs back.

**Key Mono.Cecil types involved:**

- `TypeReference.Name`, `TypeReference.Namespace` — the type's simple name and namespace
- `TypeReference.Scope` — an `IMetadataScope` (cast to `AssemblyNameReference` for cross-assembly refs; skip if `ModuleDefinition` or `ModuleReference`)
- `MemberReference.Name` — method/field name
- `MemberReference.DeclaringType` — the `TypeReference` this member belongs to

**Order dependency:** This pass runs AFTER all individual `MetadataManglingTransform` runs and BEFORE the assembly rename pass (Fix 3). The assembly rename pass only changes assembly identities, not type/member names, so there is no conflict.

## Fix 5: Build Script Changes

### builder.py

**File:** `mythic/agent_functions/builder.py`

1. Change seed generation from `random.randint(0, 2**31 - 1)` to:
   ```python
   import hashlib
   obf_seed = int(
       hashlib.sha256(self.uuid.encode()).hexdigest(), 16
   ) & 0x7FFFFFFF
   ```

2. Replace the `ObfuscateIL` MSBuild target with a two-step approach:

   **Step A — MSBuild target for batch IL rewrite (pre-bundle):**

   Replace the removed `ObfuscateIL` target in `Directory.Build.targets` with a new target that invokes the batch command. This target must still fire `BeforeTargets="GenerateSingleFileBundle"` to ensure IL rewriting + assembly renaming happen before single-file bundling:

   ```xml
   <Target Name="ObfuscateILBatch"
           AfterTargets="ComputeFilesToPublish"
           BeforeTargets="GenerateSingleFileBundle"
           Condition="'$(Obfuscate)' == 'true' AND '$(ObfuscatorPath)' != ''">
     <Exec Command="$(ObfuscatorPath) rewrite-il-batch
                     --seed $(ObfSeed)
                     --dir $(PublishDir)" />
   </Target>
   ```

   This ensures correct ordering regardless of whether `PublishSingleFile` is true or false.

   **Step B — builder.py passes the seed:**

   `builder.py` continues to pass `ObfSeed` and `ObfuscatorPath` as MSBuild properties, same as today. No change to the `dotnet publish` invocation.

### load.py

**File:** `mythic/agent_functions/load.py`

1. Change seed generation from `random.randint(0, 2**31 - 1)` to:
   ```python
   import hashlib
   obf_seed = int(
       hashlib.sha256(uuid.encode()).hexdigest(), 16
   ) & 0x7FFFFFFF
   ```

2. After IL-obfuscating the plugin DLL, run the cross-reference and assembly rename passes on the build output directory. The plugin build output contains the plugin DLL plus its compiled dependencies (Workflow.Models, Workflow.Providers.Windows). The batch command processes them all:

   ```
   obfuscator rewrite-il-batch --seed <seed> --dir <build_output_dir>
   ```

   Since the seed is identical to the host build, the rename maps are identical — `Workflow.Models` maps to the same `_a7x` in both builds.

**Plugin build — why `rewrite-il-batch` works here:** The plugin's build output directory contains `Workflow.Models.dll` and `Workflow.Providers.Windows.dll` (compiled from source copies). Running the full batch command:
- Runs `MetadataManglingTransform` on each DLL (including the dependency DLLs)
- Runs `CrossReferenceTransform` to patch the plugin's `TypeReference`s to match the renamed types in the dependency DLLs
- Runs `AssemblyRenameTransform` to rename assembly identities

This produces a plugin DLL whose references are consistent with the host's obfuscated assemblies. Only the plugin DLL is sent to the agent (not the dependency DLLs) — at runtime, its references resolve against the host's already-loaded obfuscated assemblies.

## Execution Order

### Host build:

```
1. Source rewrite (UUID-based contract renames)              — existing, unchanged
2. dotnet publish (compile all)                              — existing, unchanged
3. ObfuscateILBatch MSBuild target (before single-file bundle):
   a. Per-DLL MetadataManglingTransform (now includes Workflow.*) — modified scope
   b. CrossReferenceTransform (patch TypeRef/MemberRef)          — NEW
   c. AssemblyRenameTransform (rename identities + files)        — NEW
4. GenerateSingleFileBundle (if enabled)                     — existing, unchanged
```

### Plugin build:

```
1. Copy plugin + Workflow.Models + Workflow.Providers.Windows to temp
2. Source rewrite (same UUID, same contract renames)         — existing, unchanged
3. dotnet build                                              — existing, unchanged
4. rewrite-il-batch on build output dir:
   a. Per-DLL MetadataManglingTransform on all DLLs          — includes dependency DLLs
   b. CrossReferenceTransform (patch plugin refs)            — NEW
   c. AssemblyRenameTransform (rename assembly identities)   — NEW
5. Return only the plugin DLL                                — existing, unchanged
```

## Obfuscator CLI Changes

Add a new command to the obfuscator CLI:

```
obfuscator rewrite-il-batch --seed <seed> --dir <directory> [--map <map_path>]
```

This processes all qualifying DLLs in `<directory>` in the correct order:
1. Individual `MetadataManglingTransform` per DLL (skip System.*, Microsoft.*, runtime.*)
2. `CrossReferenceTransform` across all DLLs using per-assembly rename maps
3. `AssemblyRenameTransform` across all DLLs

The `--dir` parameter also serves as the search directory for `DefaultAssemblyResolver` when processing each DLL, allowing Mono.Cecil to resolve cross-assembly type references during the `MetadataManglingTransform` pass (e.g., resolving base types for virtual method family analysis).

The existing `rewrite-il` (single DLL) command remains for backward compatibility.

## Test Changes

### New Tests

**`AssemblyRenameTests.RenamedAssembly_HasNewIdentity`**
Load a DLL, run `AssemblyRenameTransform`, verify `AssemblyDefinition.Name.Name` is changed.

**`AssemblyRenameTests.ConsumingDll_ReferencesPatched`**
Compile two DLLs (A references B), rename B, verify A's `AssemblyNameReference` for B is updated.

**`AssemblyRenameTests.PhysicalFile_Renamed`**
Run transform on a temp directory, verify `.dll` files have new names.

**`AssemblyRenameTests.DeterministicNames_SameSeed`**
Run transform twice with the same seed, verify identical rename maps.

**`CrossReferenceTests.TypeReference_PatchedAcrossAssemblies`**
Compile two DLLs (A defines `class Foo`, B references `Foo`). Rename `Foo` to `_xyz` in A. Run cross-ref transform. Verify B's `TypeReference` to `Foo` is now `_xyz`.

**`CrossReferenceTests.MemberReference_PatchedAcrossAssemblies`**
Same setup but verify method/field references are patched.

**`CrossReferenceTests.NamespaceReference_PatchedAcrossAssemblies`**
Verify `TypeReference.Namespace` is updated when the source assembly's namespace was renamed.

**`CrossReferenceTests.SameModuleRef_NotPatched`**
Compile a DLL with internal type references. Run cross-ref transform. Verify `TypeReference`s with `ModuleDefinition` scope are left untouched (already handled by `MetadataManglingTransform`).

**`ChannelLoadingTests.ChannelsDiscoveredByInterface`**
Verify that the new `TryLoadProfiles` implementation finds `IChannel` implementors from referenced assemblies without knowing assembly names.

**`BatchIntegrationTests.HostAndPlugin_SameSeed_RefsMatch`**
End-to-end test: compile a mock "host" with a Workflow.Models-like DLL and a "plugin" that references it. Run `rewrite-il-batch` on both with the same seed. Load the plugin into an `AssemblyLoadContext` alongside the host's obfuscated DLL. Verify the plugin's `TypeReference`s resolve without `TypeLoadException`.

### Existing Test Updates

**`Tests/Workflow.Tests/PluginLoader.cs`** — Update `GetPlugin` to use `AppDomain.CurrentDomain.GetAssemblies()` + `typeof(IModule).IsAssignableFrom(t)` instead of `Assembly.Load(AssemblyNames.ForModule(name))`. This mirrors the production `TryLoadModule` replacement.

**`BuildIntegrationTests.ObfuscatedSource_ServiceHostWithPlugins_Builds`** — serves as the ultimate safety net. Must still pass with all changes applied. The `ObfuscateILBatch` MSBuild target replaces `ObfuscateIL`, so the build integration test exercises the batch path automatically.

All existing obfuscator tests (MetadataManglingTests, ApiCallHidingTests, UuidRenameTests, StringEncryptionTests, IntegrationTests) should pass unchanged — they test individual transforms in isolation.

## Files Modified

| File | Change | Risk |
|------|--------|------|
| `ServiceHost/Config/ContainerBuilder.cs` | Scan by interface instead of load by name | Low |
| `Workflow.Providers.Runtime/AssemblyManager.cs` | Remove `ForModule` string-based fallback | Low |
| `Workflow.Models/AssemblyNames.cs` | Delete | Low |
| `Tests/Workflow.Tests/PluginLoader.cs` | Update to interface-based loading | Low |
| `Directory.Build.targets` | Replace `ObfuscateIL` target with `ObfuscateILBatch` | Medium |
| `Obfuscator/IL/ILRewriter.cs` | Add batch mode orchestration | Medium |
| `Obfuscator/IL/Transforms/AssemblyRenameTransform.cs` | **New** | Medium |
| `Obfuscator/IL/Transforms/CrossReferenceTransform.cs` | **New** | High |
| `mythic/agent_functions/builder.py` | Derive seed from UUID via hashlib | Medium |
| `mythic/agent_functions/load.py` | Derive seed from UUID via hashlib, invoke batch command | Medium |
| Tests | New test files for assembly rename, cross-ref, and batch integration | Low |

## Out of Scope

- Renaming the main executable assembly (builder.py already does this)
- IL obfuscation of plugin DLL internals beyond what already happens (plugin DLLs already get `MetadataManglingTransform`)
- String encryption of assembly name strings (no longer needed — strings removed)
- Control flow obfuscation
- Anti-tampering / anti-debugging
