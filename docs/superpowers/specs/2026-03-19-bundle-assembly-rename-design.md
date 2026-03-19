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
- Patching `.deps.json` assembly names. For self-contained single-file bundles, the .NET
  runtime resolves managed assemblies through the bundle's `AssemblyLoadContext`, which
  maps embedded file paths from the manifest — not through `.deps.json` probing. The
  `.deps.json` is used only for native dependency location and framework version metadata.
  Renaming assembly names in `.deps.json` would require parsing a complex JSON schema and
  is unnecessary for correct runtime behavior.

---

## Design

### Core Insight: Stateless Per-Name Hash Derivation

The current `AssemblyRenameTransform` generates names using a positional counter against
a seeded RNG. The Nth assembly processed gets the Nth RNG output. This means:

- Main bundle batch (50 DLLs): `Workflow.Contracts` is 15th → `_k7`
- Command DLL batch (3 DLLs): `Workflow.Contracts` is 1st → `_a2` ← **mismatch**

A retry-loop approach with a shared `used` set does **not** solve this: if assembly P
takes name `_k7` in batch A but is absent from batch B, assembly Q's collision behaviour
differs between batches, producing a different name. Any algorithm that consults a
shared `used` set is batch-membership-dependent in the collision case.

**Fix:** Derive each assembly's new name purely from `(seed, originalName)` with no
shared state whatsoever:

```csharp
private static string GenerateAssemblyName(int seed, string originalName)
{
    // Name is a pure function of (seed, originalName) — no shared state,
    // no retry loop, no used-set dependency. Batch membership is irrelevant.
    var input = Encoding.UTF8.GetBytes($"{seed}:{originalName}");
    var hash  = SHA256.HashData(input);

    // Consume 5 hash bytes → 5-character base62 name with _ prefix.
    // 62^5 = 916M possibilities; P(collision | 100 assemblies) < 0.001%.
    const string Chars = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    var sb = new StringBuilder("_");
    for (var i = 0; i < 5; i++)
        sb.Append(Chars[hash[i] % Chars.Length]);
    return sb.ToString();
}
```

`Workflow.Contracts` → `_k7a3b` in every batch, every build, with zero shared state.
Collision probability for the Athena assembly set (~50 DLLs) is negligible (~0.0003%).
A unit test must verify that all actual Athena assembly names produce unique outputs for
the representative seed range.

**Why dropping the `used` set is safe:** Assembly names are not security-sensitive
identifiers — a collision (two assemblies receiving the same new name) would cause a
runtime load failure, which would be caught immediately by the E2E test. The
astronomically low collision probability, combined with the deterministic unit test over
the actual assembly set, provides sufficient assurance.

---

### Skip Prefixes — Alignment Required

`AssemblyRenameTransform` currently defines its own `SkipPrefixes` array covering only
`System.`, `Microsoft.`, and `runtime.`. `ILRewriter.RewriteBatch` uses a superset
that also covers `Autofac`, `IronPython`, `BouncyCastle`, `H.`, `Renci`, `Mono.`,
`NamedPipe`. These third-party assemblies ARE embedded in the single-file bundle for
self-contained publishing.

**Component 1 must also align `AssemblyRenameTransform.SkipPrefixes` with
`ILRewriter.SkipPrefixes`**, or extract them to a shared constant. Failing to do so
means `patch-bundle` would attempt to rename `BouncyCastle.Cryptography.dll` →
`_x9.dll` while patching its `AssemblyRef` entries everywhere — but `CrossReference`
won't know about this rename (it was computed inside `patch-bundle`, not the original
`rewrite-il-batch` pass), corrupting all third-party assembly references.

---

### Component 1: `AssemblyRenameTransform` — salted RNG + skip prefix alignment

**File:** `Obfuscator/IL/Transforms/AssemblyRenameTransform.cs`

Two changes:

1. Replace the positional `_rng` usage for assembly name generation with the
   stateless hash derivation described in the Core Insight section above.
2. Align `SkipPrefixes` with `ILRewriter.SkipPrefixes` — either by extracting to a
   shared static constant in a new `ObfuscatorConstants.cs` file, or by having
   `AssemblyRenameTransform` accept the skip-prefix list as a constructor parameter
   (preferred: makes it testable in isolation).

**Tests:** Existing tests must pass (verify exact count with `dotnet test --list-tests`
before writing the new tests). Two new tests:
- `AssemblyRename_SameNameSameResult_AcrossDifferentBatches` — run on two genuinely
  different-sized in-memory module sets (e.g., 8 assemblies and 3 assemblies with
  overlap); assert shared assembly names produce identical new names. Must use different
  batch sizes to be a meaningful guard — a single-assembly batch trivially passes.
- `AssemblyRename_NoCollisions_ActualAssemblySet` — run `GenerateAssemblyName` for
  every known `Workflow.*` assembly name at a representative seed; assert all outputs
  are distinct. This validates the negligible-probability collision assumption for the
  real Athena assembly set. (The earlier `CollisionDeterminism` test concept is
  superseded by this: with a stateless hash there is no collision-resolution mechanism
  to test — uniqueness is verified empirically instead.)

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

Note: `--map` is intentionally optional and omitted in the `Directory.Build.targets`
invocation. The rename map is re-derived deterministically at command DLL build time
(see Component 4). The `--map` option is retained for debugging and for future use.

**NuGet dependency:** Add `Microsoft.NET.HostModel` to `Obfuscator/Obfuscator.csproj`.
Pin to the exact version shipped with the .NET 10 SDK. Determine the version by running
`dotnet --list-sdks` and checking
`%DOTNET_ROOT%\sdk\<version>\Microsoft.NET.HostModel.dll` or the SDK's `.deps.json`.
At the time of writing, target `10.0.x` (implementer to confirm exact patch version
before coding). Follow the same exact-version pinning discipline as `Mono.Cecil 0.11.6`.

**Algorithm:**

1. `new Extractor(inputExe, tempDir).Extract(extractAllFiles: true)` — extracts all
   embedded files to `tempDir`, preserving relative paths.

2. Identify the **entry assembly**: find the bundle entry whose filename ends with
   `.deps.json`; derive entry assembly name as that filename with `.deps.json` replaced
   by `.dll`. If no `.deps.json` entry exists, fall back to `Path.GetFileNameWithoutExtension(inputExe) + ".dll"` and log a warning. If the entry assembly DLL is
   not found in `tempDir`, this is a **hard error** — abort and leave the original exe
   unchanged.

3. Run `AssemblyRenameTransform` on all `.dll` files in `tempDir` using the updated
   skip prefix list plus the entry assembly name. This:
   - Renames `AssemblyDef` names in PE metadata for all non-skipped managed DLLs
   - Renames all `AssemblyRef` entries in all DLLs to match
   - Renames physical files on disk in `tempDir`

   Note: `AssemblyRenameTransform` already handles `AssemblyRef` patching internally
   as part of its phase 2. A separate `CrossReferenceTransform` pass is **not** needed
   here — that transform handles TypeRef/MemberRef patches for type and method renames,
   which were already applied during the earlier `ObfuscateIL_Bundle` target and must
   not be re-applied. Running `CrossReferenceTransform` again on these DLLs would
   attempt to re-patch already-patched type references and could corrupt them.

4. Write assembly rename entries to `--map` if provided.

5. Build the `FileSpec` list for re-bundling: for each original bundle entry, look up
   its original filename in the rename map. If renamed, use the new filename as both
   the source path (from `tempDir`) and the `bundleRelativePath`. Non-DLL entries
   (`.deps.json`, `.runtimeconfig.json`, native binaries, `.pdb` files) are passed
   through unchanged — their source paths in `tempDir` are unmodified because
   `AssemblyRenameTransform` skips non-managed or skipped-prefix files.

6. Obtain the apphost binary for re-bundling. The `Bundler` constructor takes a path to
   the host executable template. For self-contained single-file bundles, the apphost is
   the native leading portion of the original exe (bytes before the bundle data). The
   implementer must determine the bundle start offset to split host from bundle — options:
   - Read the 8-byte bundle footer magic (`0x12, 0x68, 0x6F, 0x73, 0x74, 0x66, 0x78,
     0x72`) from near the end of the exe file and walk back to find the header offset
     (documented in the .NET runtime `src/installer/managed/Microsoft.NET.HostModel`
     source), OR
   - Use the .NET SDK's apphost template directly from
     `$DOTNET_ROOT/sdk/<version>/AppHostTemplate/apphost[.exe]` — valid because the
     Mythic container already has the matching .NET SDK installed.
   The SDK template approach is simpler and avoids manual byte-slicing. Either approach
   is acceptable; the SDK template path should be resolved at runtime from `dotnet --info`
   output or the `DOTNET_ROOT` environment variable.

7. `new Bundler(apphostPath, appBinaryName, ...).GenerateBundle(fileSpecs, outputPath)`
   — creates the new exe. `appBinaryName` is the entry assembly DLL name (from step 2).

8. Replace `--input` atomically: write to `<input>.tmp`, then
   `File.Move(tmp, input, overwrite: true)`.

---

### Component 3: `Directory.Build.targets` — post-bundle target

**File:** `Payload_Type/athena/athena/agent_code/Directory.Build.targets`

The existing `ObfuscateIL_Bundle` target uses both `AfterTargets="PrepareForBundle"` and
`BeforeTargets="GenerateSingleFileBundle"` — the `AfterTargets` anchor ensures
`@(FilesToBundle)` is populated before it runs. The new `ObfuscateBundleNames` target
fires after bundling and needs no `BeforeTargets` or `@(FilesToBundle)` access — it
operates on the already-written exe file — so only `AfterTargets` is required.
`GenerateSingleFileBundle` is confirmed as the correct .NET 10 SDK target name by the
fact that `ObfuscateIL_Bundle`'s `BeforeTargets` references it successfully.

```xml
<Target Name="ObfuscateBundleNames"
        AfterTargets="GenerateSingleFileBundle"
        Condition="'$(Obfuscate)' == 'true'
                   AND '$(ObfuscatorPath)' != ''
                   AND '$(PublishSingleFile)' == 'true'">
  <!-- $(AssemblyName) reflects any $(RandomName) override set by builder.py —
       this is the correct name for the published exe. -->
  <PropertyGroup>
    <_BundleExe>$(PublishDir)$(AssemblyName)$(NativeExecutableExtension)</_BundleExe>
  </PropertyGroup>
  <Message Text="Patching bundle assembly names: $(_BundleExe)" Importance="high" />
  <!-- Guard: fail loudly if the exe does not exist rather than silently no-op -->
  <Error Condition="!Exists('$(_BundleExe)')"
         Text="patch-bundle: bundle exe not found at $(_BundleExe)" />
  <Exec Command="$(ObfuscatorPath) patch-bundle
      --seed $(ObfSeed)
      --input &quot;$(_BundleExe)&quot;" />
</Target>
```

Note on `$(AssemblyName)`: `builder.py` sets `-p:RandomName=<uuid-derived-name>` on the
`dotnet publish` command line, and `ServiceHost.csproj` conditionally assigns
`<AssemblyName>$(RandomName)</AssemblyName>`. At the time this target runs,
`$(AssemblyName)` therefore resolves to the randomized exe name — exactly the filename
the bundler produced.

---

### Component 4: `load.py` — remove `--skip-assembly-rename`

**File:** `Payload_Type/athena/athena/mythic/agent_functions/load.py`

Remove `"--skip-assembly-rename"` from the `rewrite-il-batch` subprocess call (line ~339).

**Seed consistency:** `load.py` derives the seed as:
```python
obf_seed = int(hashlib.sha256(uuid.encode()).hexdigest(), 16) & 0x7FFFFFFF
```
`builder.py` uses the identical derivation to produce `$(ObfSeed)`. The same UUID →
the same seed in both paths — this is the existing invariant for all other obfuscation
transforms and is unchanged by this feature.

The command DLL build output directory contains `Workflow.Contracts.dll`,
`Workflow.Models.dll`, and other direct-dependency DLLs as `dotnet build` outputs.
With per-name-salted RNG, `AssemblyRenameTransform` produces identical names for these
assemblies regardless of batch size. The command DLL's `AssemblyRef` entries are patched
to match the renamed names the bundle host loaded them as — enabling the CLR to resolve
them at runtime.

---

### Component 5: `Obfuscation.md` — update docs

- In the "Not Obfuscated" table: remove the single-file bundle caveat row.
- Add a row noting that the entry assembly name is preserved (apphost requirement).
- Update the Known Limitations section: the single-file bundle assembly name limitation
  is resolved; remove or update the relevant paragraph.

---

## Data Flow

```
builder.py
  └─ rewrite-source (unchanged)
  └─ dotnet publish --self-contained --single-file
       └─ ObfuscateIL_Bundle target (unchanged — types/methods/fields renamed,
       |                             AssemblyRef entries for type-scope left unchanged)
       └─ GenerateSingleFileBundle (Workflow.* names still in manifest at this point)
       └─ ObfuscateBundleNames target  ← NEW
            └─ obfuscator patch-bundle
                 ├─ BundleExtractor  → tempDir/
                 ├─ AssemblyRenameTransform (per-name-salted RNG, aligned SkipPrefixes)
                 │    Workflow.Security.AES → _x9.dll  (PE identity + AssemblyRef in all DLLs)
                 │    Workflow.Contracts   → _k7.dll
                 │    ... (all non-skipped, non-entry assemblies)
                 └─ Bundler → new exe (manifest entries: _x9.dll, _k7.dll, etc.)

load.py (per load task, same payload UUID → same seed)
  └─ rewrite-source (unchanged)
  └─ dotnet build (command DLL + direct deps in build output)
  └─ rewrite-il-batch (--skip-assembly-rename REMOVED)  ← CHANGED
       └─ AssemblyRenameTransform (per-name-salted RNG)
            Workflow.Contracts → _k7  (same (seed, name) → same result as bundle)
            Workflow.Models    → _a2
            → patches AssemblyRef entries in command DLL
```

---

## Testing

### Unit tests (Obfuscator.Tests)

1. **`AssemblyRename_SameNameSameResult_AcrossDifferentBatches`** — two in-memory
   module batches of different sizes; assert shared assembly names produce identical
   new names.
2. **`AssemblyRename_CollisionDeterminism_AcrossBatches`** — construct two assemblies
   whose `(seed, name)` first-draw candidates collide; verify both receive stable
   distinct names regardless of batch composition and ordering.
3. Existing tests must remain green (verify count before starting with `dotnet test --list-tests`).

### Integration test

`ObfuscatedSource_CoreProjects_Build` must continue to pass. Additionally, add
`ObfuscatedBundle_AssemblyNamesHidden` integration test that:
1. Runs `patch-bundle` on a built bundle
2. Asserts the output exe boots correctly (launch and check exit code)
3. Uses `BundleExtractor` to enumerate manifest entries and asserts zero entries match
   the `Workflow.*` pattern

### Live E2E (per `test-obfuscated-payload` skill)

Build obfuscated payload with `commands: ["load", "exit"]`. After callback:
- ILSpy MCP: `search_members_by_name` for `Workflow` → zero matches in both assembly
  names and type names.
- Load and execute full test matrix: `jobs`, `pwd`, `ls`, `sysinfo whoami`,
  `sysinfo env`, `proc-enum ps`.
- All commands must return `status: success` with non-empty output.

---

## Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| `Microsoft.NET.HostModel` API changes with .NET version | Pin exact version; update on each .NET SDK upgrade (same cadence as `Mono.Cecil`) |
| `GenerateSingleFileBundle` target name changes in future SDK | The existing `ObfuscateIL_Bundle` target successfully uses this name; same name confirmed for net10.0. If it disappears, the `<Error>` guard in the new target will fail loudly rather than silently. |
| Assembly name collision | Pure `(seed, name)` hash derivation — no shared state, no batch dependency. Collision probability < 0.001% for ~50 assemblies; validated by `AssemblyRename_NoCollisions_ActualAssemblySet` unit test. |
| Entry assembly not detected in bundle | Hard error (not silent skip); fallback to exe basename with logged warning |
| `.deps.json` assembly name mismatch at runtime | Not a risk for self-contained single-file bundles: ALC resolves assemblies from manifest, not `.deps.json` probing. Documented in Non-Goals. |
| `Bundler` API breaking changes (`BundleOptions` enum) | Pin exact `Microsoft.NET.HostModel` version; review changelog on each .NET SDK upgrade |
| `AssemblyRenameTransform` renames third-party assemblies in bundle | Resolved by aligning skip prefixes with `ILRewriter.SkipPrefixes` in Component 1 |
| `CrossReferenceTransform` corruption if run twice | Avoided: `patch-bundle` calls only `AssemblyRenameTransform`, not `CrossReferenceTransform`. Type/method refs were already patched by the earlier `ObfuscateIL_Bundle` pass. |
