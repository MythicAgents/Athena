# Custom Obfuscator for Athena

**Date:** 2026-03-15
**Status:** Implemented
**Goal:** Replace Obfuscar with a custom two-stage obfuscation system (Roslyn source rewriting + Mono.Cecil IL rewriting) that produces polymorphic output for AV/EDR evasion.

## Context

Athena is a .NET 10 Mythic C2 agent with a plugin architecture. Commands are separate DLLs implementing `IModule`. They can be built-in (compiled as project references into a self-contained single-file executable) or loaded reflectively at runtime via `AssemblyLoadContext.LoadFromStream()`.

The existing obfuscation uses Obfuscar (IL-level, optional), XOR-encoded configs, and assembly name randomization. This design replaces Obfuscar entirely with a custom system that provides string encryption, API call hiding, control flow obfuscation, and metadata mangling — all polymorphic per build.

## Design

### Architecture

A standalone .NET CLI tool (`Obfuscator`) in the Athena repo with two subcommands:

- `obfuscator rewrite-source` — Roslyn-based source transforms (pre-compilation)
- `obfuscator rewrite-il` — Mono.Cecil-based IL transforms (post-compilation)

The Obfuscator tool is built once (via `dotnet build`) when the Mythic container starts and cached as a compiled binary. It is not rebuilt per payload build.

```
agent_code/
  Obfuscator/
    Obfuscator.csproj
    Program.cs
    Config/
      ObfuscationConfig.cs
    Source/
      SourceRewriter.cs
      Transforms/
        StringEncryptionTransform.cs
        ApiCallHidingTransform.cs
    IL/
      ILRewriter.cs
      Transforms/
        ControlFlowTransform.cs
        MetadataManglingTransform.cs
    Runtime/
      StringDecryptor.cs
      IndirectCaller.cs
```

### Two-Stage Pipeline

```
                    dotnet publish
                   ┌──────────────────────────────────────┐
Source Rewriter     │  compile ──► IL Rewriter ──► bundle  │
(Roslyn, external)  │              (Cecil, MSBuild target)  │
       │           └──────────────────────────────────────┘
       v                                                      v
.cs sources ──► rewritten .cs ──► dotnet publish ──────────► single-file exe
(original)      (temp dir)        (IL rewrite happens        (final output)
       │                           inside publish via
 String encryption                 MSBuild target)
 API call hiding                        │
                                  Control flow flattening
                                  Metadata mangling
```

Source rewriting happens externally before `dotnet publish`. IL rewriting happens inside the publish pipeline via a custom MSBuild target that runs after compilation but before single-file bundling.

### Source-Level Transforms (Roslyn)

#### String Encryption

1. Walk the syntax tree, find all `StringLiteralExpression` nodes
2. For each string, generate a unique encryption key (derived from master seed + string index)
3. Replace the literal with a call to an injected decryptor: `"hello"` becomes `StringDecryptor.D(new byte[]{...}, key)`
4. Decryptor uses XOR with a per-string key — fast, small footprint, and sufficient since the goal is signature evasion (not cryptographic strength)

Exclusions:
- `nameof()` expressions
- Attribute arguments (e.g., `[DllImport("kernel32")]`)
- `const string` declarations and fields used in switch/case pattern matching
- String interpolation: only the literal portions of `$"..."` are encrypted, not the interpolation expressions

The decryptor class and the indirect caller class are injected by **copying their `.cs` source files into the rewritten output directory** as additional compilation units. Their class names, method names, and namespaces are randomized per seed before copying. This ensures they compile naturally with the rest of the project, survive IL trimming (they are directly referenced), and get bundled into single-file output. Both helpers are internal to avoid polluting the public API surface.

#### API Call Hiding

1. Maintain a configurable list of sensitive API patterns (P/Invoke, `Process.Start`, `Assembly.Load`, `File.ReadAllBytes`, socket APIs, etc.)
2. Walk the syntax tree, find matching `InvocationExpression` nodes
3. Replace with indirect calls via the injected helper that resolves methods through reflection and caches delegates

The helper class name, method name, and stored type/method strings are all randomized and encrypted via the string encryption transform.

**Trimming interaction:** The indirect caller uses reflection to resolve methods at runtime. To prevent the trimmer from removing the targets, the source rewriter also emits `[DynamicDependency]` attributes on the caller for each hidden API, preserving the reflection targets.

### IL-Level Transforms (Mono.Cecil)

#### Control Flow Obfuscation

Three techniques applied together:

**Control Flow Flattening:** Convert sequential blocks (A -> B -> C -> D) into a switch dispatcher (`while(true) { switch(state) { ... } }`). Cases are shuffled randomly per seed.

**Opaque Predicates:** Insert conditional branches that always evaluate one way but are hard to prove statically. Dead branches contain valid but unreachable IL. Predicate formulas derived from seed.

**Bogus Code Insertion:** Insert dead-but-valid IL instructions (arithmetic, local variable shuffles, unreachable calls) between real blocks to inflate method bodies and confuse pattern matching.

**Method selection criteria:**
- Applied to methods with 4 or more IL basic blocks (skip trivial methods)
- Skip property getters/setters
- Skip constructors of types inheriting from framework types
- **Skip async state machine `MoveNext()` methods** — the runtime expects specific IL patterns in async state machines; flattening them causes runtime failures. Detection: skip any method named `MoveNext` on a type implementing `IAsyncStateMachine`.
- Skip `iterator MoveNext()` methods (same reasoning — types implementing `IEnumerator`)

#### Metadata Mangling

Renamed: type names, method names, field names, parameter names, property names, namespace names, event names, generic parameter names.

Preserved:
- All UUID-derived contract types and members (see exhaustive list below)
- `[DllImport]` extern method names
- Entry point method
- Serialized property/field names used in JSON deserialization (decorated with `[JsonPropertyName]` or matching Mythic protocol field names)

Renaming strategy: generate random strings using a counter-based scheme seeded by the build seed. All generated identifiers are prefixed with `_` to avoid collisions with C# keywords (e.g., `_a3`, `_xf2`). Start at 2 characters after the prefix; if a collision is detected within the same scope (namespace, type, or method), increment length by 1. Cecil resolves all references and renames consistently within each assembly.

### Interface Obfuscation via Payload UUID

**Problem:** If every obfuscated DLL has a type implementing `IModule` with `Name` and `Execute`, that's a stable detection signature.

**Solution:** Derive interface/type rename mappings from the payload UUID.

- At agent build time: `builder.py` has the payload UUID, derives deterministic names via `SHA256(UUID + "athena-obfs")`. The salt is the hardcoded constant string `"athena-obfs"` — same on agent build and server-side DLL compilation.
- Compiles `Workflow.Models` with renamed interfaces. All agent code and built-in plugins compile against these.
- At reflective DLL load time: server looks up the payload UUID for the requesting agent's callback, derives the same names, compiles the plugin against the same renamed `Workflow.Models`.

The agent never recalculates interface names — they were baked in at compile time. The payload UUID is stable (unlike the agent/callback UUID which replaces it after checkin).

Two agents from different payloads have completely different interface signatures.

#### Exhaustive UUID-Derived Rename List

Every type and member below is renamed deterministically from the payload UUID. Missing any one will cause `TypeLoadException` at runtime.

**Scope rule:** All non-framework types in `Workflow.Contracts` and `Workflow.Models` namespaces that appear in interface signatures, plugin constructors, or cross-assembly boundaries are UUID-renamed. Both namespaces are renamed.

**Namespace note:** `IScriptEngine` is currently declared in `Workflow.Models` (in `IPythonManager.cs`) while all other interfaces are in `Workflow.Contracts`. Both namespaces are subject to UUID-derived renaming. The source rewriter must update all `using` directives, fully-qualified references, and namespace declarations across all projects in the copied source tree.

**Interfaces:**

| Interface | Namespace | Members |
|-----------|-----------|---------|
| `IModule` | `Workflow.Contracts` | `Name` (string property), `Execute(ServerJob)` -> Task |
| `IInteractiveModule` | `Workflow.Contracts` | `Interact(InteractMessage)` -> void |
| `IFileModule` | `Workflow.Contracts` | `HandleNextMessage(ServerTaskingResponse)` -> Task |
| `IForwarderModule` | `Workflow.Contracts` | `ForwardDelegate(DelegateMessage)` -> Task |
| `IProxyModule` | `Workflow.Contracts` | `HandleDatagram(ServerDatagram)` -> Task |
| `IBufferedProxyModule` | `Workflow.Contracts` | `FlushServerMessages()` -> Task |
| `IChannel` | `Workflow.Contracts` | `StartBeacon()` -> Task, `StopBeacon()` -> bool, `Checkin(Checkin)` -> Task\<CheckinResponse\>, `SetTaskingReceived` event |
| `IService` | `Workflow.Contracts` | all members |
| `IComponentProvider` | `Workflow.Contracts` | `TryGetModule<T>`, `LoadModuleAsync`, `LoadAssemblyAsync` |
| `IDataBroker` | `Workflow.Contracts` | `AddTaskResponse`, `AddDelegateMessage`, `AddInteractMessage`, `AddDatagram`, `Write`, `WriteLine`, `AddKeystroke`, `AddJob`, `GetJobs`, `TryGetJob`, `CompleteJob`, `GetAgentResponseString`, `HasResponses`, `CaptureStdOut`, `ReleaseStdOut`, `StdIsBusy`, `GetStdOut` |
| `IServiceConfig` | `Workflow.Contracts` | all members |
| `ISecurityProvider` | `Workflow.Contracts` | all members |
| `ILogger` | `Workflow.Contracts` | all members |
| `IRequestDispatcher` | `Workflow.Contracts` | all members |
| `IRuntimeExecutor` | `Workflow.Contracts` | `Spawn(SpawnOptions)` -> Task\<bool\>, `TryGetHandle` |
| `ICredentialProvider` | `Workflow.Contracts` | `AddToken(SafeAccessTokenHandle, CreateToken, string)`, `Impersonate`, `List`, `Revert`, `getIntegrity`, `GetImpersonationContext`, `RunTaskImpersonated`, `HandleFilePluginImpersonated`, `HandleInteractivePluginImpersonated` |
| `IScriptEngine` | `Workflow.Models` | `LoadPyLib`, `ExecuteScriptAsync`, `ExecuteScript`, `ClearPyLib` |
| `IServiceExtension` | `Workflow.Contracts` | all members |

**Contract types (parameter/return types crossing the plugin boundary):**

| Type | Namespace | Notes |
|------|-----------|-------|
| `ServerJob` | `Workflow.Models` | Parameter to `IModule.Execute` |
| `InteractMessage` | `Workflow.Models` | Parameter to `IInteractiveModule.Interact` |
| `ServerTaskingResponse` | `Workflow.Models` | Parameter to `IFileModule.HandleNextMessage` |
| `DelegateMessage` | `Workflow.Models` | Parameter to `IForwarderModule.ForwardDelegate` |
| `ServerDatagram` | `Workflow.Models` | Parameter to `IProxyModule.HandleDatagram` |
| `PluginContext` | `Workflow.Contracts` | Record; all parameter/property names (`MessageManager`, `Config`, `Logger`, `TokenManager`, `Spawner`, `ScriptEngine`) are also renamed |
| `ITaskResponse` | `Workflow.Models` | Interface for task responses |
| `Checkin` | `Workflow.Models` | Parameter to `IChannel.Checkin` |
| `CheckinResponse` | `Workflow.Models` | Return type from `IChannel.Checkin` |
| `TaskingReceivedArgs` | `Workflow.Models` | Event args for `IChannel.SetTaskingReceived` |
| `DatagramSource` | `Workflow.Models` | Enum, parameter to `IDataBroker.AddDatagram` |
| `SpawnOptions` | `Workflow.Models` | Parameter to `IRuntimeExecutor.Spawn` |
| `CreateToken` | `Workflow.Models` | Parameter to `ICredentialProvider.AddToken` |
| `TokenTaskResponse` | `Workflow.Models` | Return type from `ICredentialProvider.AddToken` |

**Framework types** (`SafeAccessTokenHandle`, `SafeProcessHandle`, `EventHandler<T>`, etc.) are NOT renamed — they come from the .NET runtime and must retain their original names.

**Both the `Workflow.Contracts` and `Workflow.Models` namespaces** are renamed.

The obfuscator tool takes `--uuid <payload-uuid>` and derives all of these names deterministically. The same derivation runs in `builder.py` (agent build) and `load.py` (reflective DLL load).

### Seed Management

**Build seed:** Random 32-bit integer generated per build/load. Drives all non-interface randomness: string encryption keys, rename mappings, opaque predicates, control flow shuffling, bogus code.

**UUID-derived seed:** Deterministic, derived from `SHA256(payload_uuid + "athena-obfs")`. Drives only interface/contract renaming. Stable across all builds for the same payload.

**Deterministic output:** Same build seed + same source = same output. Useful for debugging — reproduce a broken build with its seed. Seeds are logged server-side but never embedded in output.

### Build Pipeline Integration

#### Multi-Project Source Rewriting Strategy

The source rewriter operates on a **copy** of the entire `agent_code` directory tree. It never modifies the original source. This is critical for concurrent builds — Mythic can build multiple payloads simultaneously, and modifying the shared source directory would corrupt parallel builds.

The rewriter processes all `.cs` files across all projects (`ServiceHost`, `Workflow.Models`, `Workflow.Providers.*`, `Workflow.Channels.*`, `Workflow.Security.*`, and all plugin projects). Transform applicability:

- **String encryption:** All projects except `Workflow.Models` interface definitions (the contracts themselves don't contain user-facing strings worth encrypting, and their structure must remain clean for UUID-derived renaming).
- **API call hiding:** All projects. Sensitive API patterns are most common in plugins and providers.
- **UUID-derived renaming (source-level pass):** Applied to `Workflow.Models` during source rewriting — interface names, type names, namespace rewritten in the copied `.cs` files before compilation.

#### Main Agent Build (builder.py)

Current: `generate configs -> dotnet publish (single-file, self-contained) -> zip`

New:
1. `builder.py` generates a random build seed and derives interface names from payload UUID
2. Copy `agent_code` to a temp directory
3. Source rewrite: `obfuscator rewrite-source --seed <seed> --uuid <payload-uuid> --input <temp_dir> --output <temp_dir>` (in-place on the copy)
4. `dotnet publish` from the temp directory, with the IL rewrite MSBuild target active:

```xml
<Target Name="ObfuscateIL"
        AfterTargets="ComputeFilesToPublish"
        BeforeTargets="GenerateSingleFileBundle"
        Condition="'$(Obfuscate)' == 'true'">
  <ItemGroup>
    <AgentAssemblies Include="@(ResolvedFileToPublish)"
                     Condition="!$([System.String]::Copy('%(Filename)').StartsWith('System.'))
                            AND !$([System.String]::Copy('%(Filename)').StartsWith('Microsoft.'))
                            AND '%(Extension)' == '.dll'" />
  </ItemGroup>
  <Exec Command="$(ObfuscatorPath) rewrite-il --seed $(ObfSeed) --input %(AgentAssemblies.Identity)"
        Condition="'@(AgentAssemblies)' != ''" />
</Target>
```

This invokes the obfuscator once per agent DLL (MSBuild item batching). For builds with many built-in plugins this means dozens of invocations, but since the obfuscator is a pre-compiled binary (not `dotnet run`), each invocation is fast. If this proves to be a bottleneck, the CLI can be extended to accept multiple `--input` paths in a single invocation.

The target filters to only agent assemblies — framework and runtime DLLs are excluded by prefix (`System.*`, `Microsoft.*`). This avoids attempting to rewrite framework code and handles ReadyToRun/crossgen'd assemblies by simply not touching them.

5. Single-file bundler picks up rewritten DLLs, produces final executable
6. Clean up temp directory
7. Zip and deliver

#### Reflective DLL Loads (load.py)

Current flow in `load.py`:
```python
async def compile_command(self, plugin_folder_path, uuid):
    p = subprocess.Popen(["dotnet", "build", "-c", "Release",
                          "/p:PayloadUUID={}".format(uuid)],
                         cwd=plugin_folder_path)
```

New flow:
1. Server receives load request from agent with known callback UUID
2. Look up payload UUID for this callback (via Mythic RPC)
3. Generate fresh random build seed
4. Copy plugin source to a temp directory (including `Workflow.Models` as a project reference dependency)
5. Run source rewriter on the copy: `obfuscator rewrite-source --seed <seed> --uuid <payload-uuid> --input <temp_dir> --output <temp_dir>`
6. Switch from `dotnet build` to `dotnet publish` to enable the IL rewrite MSBuild target: `dotnet publish -c Release /p:Obfuscate=true /p:ObfSeed=<seed> /p:PayloadUUID=<uuid> /p:PublishTrimmed=false`
7. Read the compiled DLL from `bin/Release/net10.0/publish/<command>.dll` (publish output, not build output — update `plugin_dll_platform_specific` and `plugin_dll_generic` paths in load.py)
8. Send bytes to agent (no caching — every load is fresh)
9. Clean up temp directory

**Trimming for plugin DLLs:** Plugin publishes use `/p:PublishTrimmed=false`. Trimming individual plugin DLLs would remove types only used via reflection by the agent. Trimming is only appropriate for the full agent build where the entire dependency graph is visible.

Every load produces a unique DLL. No two agents receive identical plugin binaries.

#### Build Parameter

Single `obfuscate` boolean toggle replaces the current Obfuscar toggle. Same UX in Mythic build dialog.

#### Trimming Interaction

The .NET trimmer runs during `dotnet publish`. The ordering is:

1. Source rewriting (external, before publish)
2. Compilation (inside publish)
3. Trimming (inside publish — removes unreachable code)
4. IL rewriting (MSBuild target, after trimming, before bundling)

This means:
- Bogus code inserted by the IL rewriter happens **after** trimming, so the trimmer cannot remove it
- `[DynamicDependency]` attributes from API call hiding preserve reflection targets during trimming
- Runtime helpers (`StringDecryptor`, `IndirectCaller`) are directly referenced and survive trimming naturally

### Error Handling

If obfuscation fails at any stage:
- **Source rewrite failure:** The temp directory is cleaned up. The build fails with a clear error message identifying which file and transform failed. The operator sees the error in the Mythic build log.
- **IL rewrite failure:** The MSBuild target returns a non-zero exit code, causing `dotnet publish` to fail. Same error reporting.
- **No silent fallback to unobfuscated builds.** If the operator requested obfuscation and it fails, the build fails. This prevents accidentally deploying unobfuscated payloads.

The build seed is logged in the Mythic build output so that failures can be reproduced for debugging.

### De-obfuscation Map

When obfuscation runs, both stages emit mapping data into a single JSON file (`<seed>-map.json`):
- **Source rewriter:** records randomized helper names (StringDecryptor class/method name, IndirectCaller class/method name)
- **IL rewriter:** records all metadata renames (original name -> obfuscated name)

This file is stored server-side only (never embedded in the payload). It enables:
- Translating obfuscated stack traces from agent errors back to meaningful names
- Debugging specific builds by reproducing with the logged seed

### AOT Compatibility

Obfuscation (`obfuscate=true`) and AOT compilation (`PublishAot=true`) are **mutually exclusive**. The builder rejects builds that enable both. API call hiding uses runtime reflection which is incompatible with AOT, and control flow flattening produces IL patterns that the AOT compiler may reject. This restriction can be revisited if AOT support becomes a priority.

### What Gets Removed

- Obfuscar NuGet dependency
- `Payload_Type/athena/agent.obfs` template
- `Payload_Type/athena/common.obfs` template
- `<!-- Obfuscation Replacement Placeholder Do Not Remove -->` comments in all .csproj files
- Obfuscar-related MSBuild targets in .csproj files (some are already commented out — remove all traces)
- Obfuscar-related code in `main.py` (`process_csproj_files`, `find_csproj_files`, `replace_placeholder_in_file`, `read_replacement_text`)
- `build_utils.py` if it exists and is solely an Obfuscar wrapper

### Restrictions

The current builder rejects `obfuscate=true` for Windows service output type. This restriction carries forward for the initial implementation. The custom obfuscator should work with Windows services in principle, but validating it is deferred to a follow-up.

## Testing Strategy

### Existing Test Suite (Gate)

The full existing test suite (`Workflow.Tests`) must pass when built with obfuscation enabled. This is the primary correctness gate — if obfuscation breaks any existing behavior, it's caught here. The CI matrix should run tests in both modes:
- `dotnet test` with `Obfuscate=false` (existing baseline)
- `dotnet test` with `Obfuscate=true` (obfuscated build)

Both must pass before any PR is merged.

### New Unit Tests (Obfuscator-specific)

- **String encryption:** Verify round-trip (encrypt then decrypt produces original). Verify different seeds produce different ciphertext. Verify exclusions (nameof, attributes, const, interpolation expressions) are not transformed.
- **API call hiding:** Verify transformed code resolves and invokes the correct method at runtime. Verify `[DynamicDependency]` attributes are emitted.
- **Control flow:** Verify method produces same output before and after flattening. Verify async `MoveNext()` methods are skipped. Verify iterator `MoveNext()` methods are skipped.
- **Metadata:** Verify renamed assemblies load and interface contracts resolve. Verify collision avoidance works with many identifiers. Verify `_`-prefixed names don't collide with C# keywords.
- **UUID-derived renaming:** Verify same UUID produces same interface names. Verify different UUIDs produce different names. Verify all types in the exhaustive list are renamed.
- **De-obfuscation map:** Verify the map file is generated and contains entries for both source-level and IL-level transforms.

### New Integration Tests

- Build a minimal test plugin with obfuscation enabled, load it reflectively via `AssemblyLoadContext.LoadFromStream()`, execute it, verify correct output.
- Build the full agent with obfuscation enabled (single-file, self-contained), verify it starts and checks in.
- Build two agents with different seeds, binary-diff to confirm they are substantially different.
- Build two plugins for the same agent (same payload UUID), verify both load correctly with different build seeds.
- Build two plugins for different agents (different payload UUIDs), verify they have different interface signatures and cannot cross-load.
- Build with `Obfuscate=true` and `PublishAot=true`, verify the builder rejects the combination.

### Regression Tests

- Maintain a set of "known tricky" code patterns (switch expressions, pattern matching, interpolated strings, LINQ, async/await) and verify they survive obfuscation without breaking.

### MSBuild Target Validation

Before full implementation, create a proof-of-concept that validates the MSBuild target hook (`AfterTargets="ComputeFilesToPublish" BeforeTargets="GenerateSingleFileBundle"`) works correctly with .NET 10 single-file publish. This PoC should:
- Confirm the target runs at the right time in the pipeline
- Confirm modified DLLs are picked up by the bundler
- Confirm framework assemblies can be reliably filtered out
