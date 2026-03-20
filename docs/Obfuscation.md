# Athena Obfuscation System

## Overview

The Athena obfuscation system transforms the agent's source code and compiled IL to remove
recognizable identifiers before deployment. It runs as a two-phase pipeline:

1. **Source phase** — rewrites C# source files before compilation
2. **IL phase** — rewrites compiled PE metadata after `dotnet build`/`dotnet publish`

Both phases are orchestrated by the `Obfuscator` project
(`Payload_Type/athena/athena/agent_code/Obfuscator/`), a standalone CLI tool invoked by
`builder.py` during Mythic payload generation.

---

## CLI Reference

```
obfuscator rewrite-source
  --seed <int>     Deterministic RNG seed
  --uuid <string>  Agent UUID (payload identity)
  --input <dir>    Source directory to rewrite
  --output <dir>   Directory for rewritten source files
  [--map <path>]   Optional path to write obf-map.json

obfuscator rewrite-il
  --seed <int>     Deterministic RNG seed
  --input <path>   Single assembly (.dll/.exe) to rewrite
  [--map <path>]   Optional path to write/merge obf-map.json

obfuscator rewrite-il-batch
  --seed <int>              Deterministic RNG seed
  --dir <dir>               Directory of compiled assemblies
  [--map <path>]            Optional path to write/merge obf-map.json
  [--skip-file-rename]      Do not rename DLL files on disk after IL rewrite
  [--skip-assembly-rename]  Do not rewrite PE assembly identity names
                            (required for single-file bundles — manifest uses
                            original names for probing)
```

The `builder.py` in the Mythic container runs:
1. `rewrite-source` on the agent source tree
2. `dotnet build` on the rewritten source
3. `rewrite-il-batch --skip-assembly-rename` on the publish output directory

For dynamically loaded command DLLs, `load.py` runs the same sequence per DLL:
1. `rewrite-source` (same seed derived from payload UUID)
2. `dotnet build`
3. `rewrite-il-batch --skip-assembly-rename`

---

## Seed and Determinism

The RNG seed is derived from the agent's UUID so that every build of the same payload,
including all command DLLs loaded at runtime, produces identical renames:

```python
# Python (builder.py / load.py)
seed = int(hashlib.sha256(uuid.encode()).hexdigest(), 16) & 0x7FFFFFFF
```

```csharp
// C# (UuidRenameMap.cs)
var hash = SHA256.HashData(Encoding.UTF8.GetBytes(uuid + "athena-obfs"));
var seed = BitConverter.ToInt32(hash, 0);
```

The source phase and IL phase use the same `seed` value. The `AssemblyRenameTransform`
uses `seed ^ 0x5A5A5A5A` for a separate name space to avoid collision with type renames.

**Critical property**: Given the same UUID, the obfuscated name of every type, method,
field, assembly, and string is identical across the main agent and every command DLL it
will ever load. This is what allows `IsAssignableFrom(IModule)` to succeed at runtime
when a dynamically loaded DLL's renamed type is checked against the host's renamed
interface.

---

## Phase 1: Source Rewriting

Transforms are applied to every `.cs` file under `--input`, excluding:
- `Tests/` directory
- `Obfuscator/` directory itself

Transforms run in this order:

### 1. UuidRenameTransform

Renames contract-facing identifiers — the shared language between the agent host and
plugin DLLs. Uses a UUID-derived map so the same names appear in both the host and every
command DLL.

**What gets renamed:**
- Plugin-facing interface names (`IModule`, `ITaskResponse`, etc.)
- Types appearing in plugin-facing interface signatures (parameter types, return types)
- Namespaces containing the above (`Workflow.Contracts`, `Workflow.Models`)
- Interface member names (methods, properties, events) within contract-typed contexts
- Record constructor parameters for contract types

**What is NOT renamed by this transform:**
- Implementation class names (renamed later by IL mangling)
- Non-contract namespaces (e.g. `Workflow.Providers.Runtime`)
- Members accessed on non-contract-typed expressions

**Contract detection (`ContractScanner`):**
The scanner examines `Workflow.Contracts` and `Workflow.Models` to find plugin-facing
interfaces. An interface is "plugin-facing" if it is:
- In the `IModule` hierarchy, OR
- A parameter type of a plugin-facing interface method (excluding `IServiceConfig` and
  `ILogger`), OR
- `IComponentProvider`

Types found in the signatures of plugin-facing interfaces are "contract types" whose
members (but not names) are also renamed inside contract-typed expressions.

**Name generation:**
Names are `_xx` style (underscore + 2+ alphanumeric chars). Length increases when a
collision occurs within the same scope. The map is seeded with `SHA256(uuid + "athena-obfs")`.

**Namespace handling:**
Only the longest recognized namespace prefix is renamed. If a using directive or namespace
block spans multiple renamed segments, each segment is replaced independently.
`CorrectedNamespaceName` ensures partial-match segments don't corrupt unrecognized suffixes.

### 2. ApiCallHidingTransform

Replaces direct calls to security-sensitive APIs with reflection-based dispatch through
the generated `IndirectCaller` class. This breaks static analysis tools that look for
known dangerous API call patterns.

**Sensitive APIs intercepted:**

| Class | Methods |
|-------|---------|
| `System.Diagnostics.Process` | `Start` (static) |
| `System.Reflection.Assembly` | `Load`, `LoadFrom`, `LoadFile` (static) |
| `System.IO.File` | `ReadAllBytes`, `ReadAllText`, `WriteAllBytes`, `WriteAllText` (static) |
| `System.Net.Sockets.Socket` | `Connect` (instance) |
| `System.Net.Http.HttpClient` | `SendAsync` (instance) |
| `System.Net.WebClient` | `DownloadData` (instance) |

**Rewrite pattern (expression context):**
```csharp
// Before
var asm = Assembly.Load(bytes);

// After
Assembly asm = (Assembly)((dynamic)_Ns._Class._Method(
    "System.Reflection.Assembly", "Load", null, new object?[] { (object?)bytes }));
```

Note: `var` is replaced with `Assembly` (explicit type). If `var` were kept, the compiler
would infer `dynamic` from the `((dynamic)...)` cast, causing the DLR to dispatch
`ParseAssemblyForModule` by original name on the renamed type at runtime — a bug that
caused `RuntimeBinderException` in obfuscated builds.

**Rewrite pattern (statement/void context):**
```csharp
// Before
Process.Start("cmd.exe");

// After
_Ns._Class._Method("System.Diagnostics.Process", "Start", null,
    new object?[] { (object?)"cmd.exe" });
```

No `dynamic` cast for statement-position calls (avoids CS0201 — only expressions can
appear as statements).

**Trimmer protection:**
Each intercepted call site gets a `[System.Diagnostics.CodeAnalysis.DynamicDependency]`
attribute on the enclosing method or class so the trimmer doesn't remove the target API.

### 3. StringEncryptionTransform

Rewrites string literals as XOR-encrypted byte arrays decoded at runtime by the generated
`StringDecryptor` class.

**Key computation:**
```csharp
key = (byte)((seed + stringIndex) & 0xFF);
if (key == 0) key = 1;  // never use zero key
```

`stringIndex` increments for each string encountered during the syntax walk, so every
string gets a unique key even with the same seed.

**Encryption:**
UTF-8 bytes of the string are XOR'd with the key byte-by-byte:
```csharp
encrypted[i] = (byte)(utf8[i] ^ key);
```

**Rewrite pattern:**
```csharp
// Before
var s = "Hello";

// After
var s = _Ns._Class._Method(new byte[] { 0x41, 0x0A, 0x07, 0x07, 0x00 }, (byte)0x29);
```

**Interpolated string handling:**
Text spans in interpolated strings are encrypted and converted to interpolation holes:
```csharp
// Before
var s = $"Hello {name}!";

// After
var s = $"{_Ns._Class._Method(new byte[]{...}, (byte)0xKK)}{name}{_Ns._Class._Method(new byte[]{...}, (byte)0xLL)}";
```

**Exclusions — strings that are NOT encrypted:**
- Empty strings (`""`)
- Strings inside `nameof(...)` expressions
- Strings inside attribute arguments (`[SomeAttribute("value")]`)
- Strings in `const` field or local declarations
- Strings in switch case labels (`case "value":`)
- Strings in `when` pattern expressions (`case { P: "value" }`)

The exclusions exist because the compiler requires these to be compile-time constants;
a method call cannot appear in these positions.

### Runtime Templates Injected by SourceRewriter

After applying the transforms, `SourceRewriter` injects two generated files into every
project directory under `--input`:

**`_generated_decryptor.cs`** (from `Runtime/StringDecryptor.cs` template):
```csharp
// Placeholders replaced with seeded random names
namespace __OBFS_NS__ {
    internal static class __OBFS_CLASS__ {
        internal static string __OBFS_METHOD__(byte[] data, byte key) {
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                result[i] = (byte)(data[i] ^ key);
            return System.Text.Encoding.UTF8.GetString(result);
        }
    }
}
```

**`_generated_caller.cs`** (from `Runtime/IndirectCaller.cs` template):
```csharp
namespace __OBFS_NS__ {
    internal static class __OBFS_CALLER_CLASS__ {
        private static readonly ConcurrentDictionary<string, MethodInfo> _cache = new();

        internal static object? __OBFS_INVOKE_METHOD__(
            string typeName, string methodName, object? instance, object?[] args)
        {
            // Cache key includes arg types to handle overloads
            // (e.g. Assembly.Load has string, AssemblyName, and byte[] overloads)
            var argSig = string.Join(",", args.Select(a => a?.GetType().FullName ?? "null"));
            var key = (instance == null ? "s:" : "i:") + typeName + "." + methodName
                      + "(" + argSig + ")";
            var method = _cache.GetOrAdd(key, _ => {
                var type = Type.GetType(typeName, throwOnError: true)!;
                // Type-aware overload dispatch via IsAssignableFrom
                foreach (var m in type.GetMethods(...))
                    if (m.Name == methodName && ParametersMatch(m, args))
                        return m;
                throw new MissingMethodException(typeName, methodName);
            });
            return method.Invoke(instance, args);
        }
    }
}
```

Helper names (namespace, class name, method name) are 8-character `_`-prefixed
alphanumeric strings generated from the seeded RNG.

---

## Phase 2: IL Rewriting

Operates on compiled PE files (`.dll`, `.exe`) using Mono.Cecil. Processes assemblies in
three ordered steps; all Step 1 writes are deferred until every assembly has been processed
to prevent stale-read issues during cross-assembly interface resolution.

### Skip Prefixes

The following assembly name prefixes are never obfuscated:
```
System.
Microsoft.
runtime.
Autofac
IronPython
BouncyCastle
H.
Renci
Mono.
NamedPipe
```

Assemblies matching any prefix are skipped entirely in all IL steps.

Native DLLs (those that throw `BadImageFormatException` when opened by Cecil) are also
silently skipped.

### Step 1: MetadataManglingTransform

Renames identifiers in PE metadata within a single assembly. Applied to every non-skipped
assembly; disk writes deferred until all assemblies are processed.

**Processing order (deterministic):**
1. Namespaces (sorted alphabetically)
2. For each type (sorted by `FullName`):
   - Type name
   - Fields (sorted)
   - Events (sorted)
   - Methods (sorted by `FullName`)
   - Generic parameters

**Rename key format:**
Method renames use qualified keys: `"TypeFullName::MethodName"` where `TypeFullName`
uses the already-renamed namespace. This prevents a same-named method in one type from
accidentally renaming a different type's same-named method.

**Name generation:**
`_` prefix + 2+ chars from `[a-z0-9A-Z]`, seeded from the global RNG. Length increments
on collision within the same module. The same name is never assigned twice in the same
assembly.

**Preserved methods — never renamed:**

| Category | Examples / Rule |
|----------|-----------------|
| Well-known framework overrides | `ToString`, `GetHashCode`, `Equals`, `Dispose`, `GetEnumerator`, `MoveNext`, `get_Current` |
| JSON serialization | `Read`, `Write` (required by `JsonConverter<T>` generic base — Cecil cannot resolve generic instantiated bases) |
| Interface implementations | Any method that implements an interface defined in a skipped (external) assembly |
| Constructors | `.ctor`, `.cctor` |
| P/Invoke externs | Any `extern` method with `DllImport` |
| Entry point | `Main` or the assembly entry point |
| Virtual overrides of external base | A virtual override where the base method is from a skipped assembly |
| Delegates | Delegate `Invoke`, `BeginInvoke`, `EndInvoke` |
| Property accessors | `get_X`, `set_X` (identified by `IsGetter`/`IsSetter` flags) |
| Serialization attributes | Any method on a type with `[JsonPropertyName]` |

**Preserved fields — never renamed:**

| Category | Rule |
|----------|------|
| Enum members | All fields of enum types |
| Serializable types | Fields on types with `[Serializable]` or `[DataContract]` |
| Compiler-generated backing fields | Fields whose name starts with `<` (auto-property backing, state machine fields) |

**Virtual method family coordination:**
When a virtual method hierarchy spans multiple types in the same assembly, all overrides
must share the same renamed name. `BuildVirtualMethodFamilies` identifies override chains;
`RecordFamilyName` propagates the chosen name to every member of the chain before
individual renaming occurs. Methods marked `ShouldPreserveMethod=true` cause the entire
family to keep the original name.

### Step 2: CrossReferenceTransform

After all MetadataManglingTransform passes are written, CrossReferenceTransform patches
TypeRefs and MemberRefs in each assembly that point into other (now-renamed) assemblies.

**TypeRef patching:**
For each `TypeReference` in a foreign module scope:
1. Look up the namespace rename using the source-assembly's namespace mapping
2. Use the (now-renamed) namespace + original type name as the lookup key into the rename
   map to find the new type name
3. Update `typeRef.Namespace` and `typeRef.Name`

The two-step lookup (namespace first, then FullName) is critical: `MetadataManglingTransform`
records keys under the already-renamed namespace, so the key for type `ContainerBuilder`
in namespace `Autofac` (skipped) would be stored as-is, while `Workflow.Contracts.IModule`
(renamed namespace `_k7`) would be stored as `"_k7.IModule"`.

**MemberRef patching:**
- Method references: looked up by `"DeclFullName::MethodName"` qualified key
- Field references: looked up by unqualified method name (fields do not have overloads)

**GenericInstanceMethod (MethodSpec) patching:**
This is the critical fix for `MissingMethodException` in generic method calls. Cecil's
`module.GetMemberReferences()` returns `MemberReference` objects, but for MethodSpec
entries (generic method instantiations like `InvokeFunc<nint>()`), Cecil creates a
separate `MethodReference` object for `GenericInstanceMethod.ElementMethod` that is NOT
shared with the `GetMemberReferences()` collection.

The fix iterates all method body instructions and, for any instruction whose operand is a
`GenericInstanceMethod`, patches `((GenericInstanceMethod)operand).ElementMethod.Name`
directly:

```csharp
foreach (var instruction in body.Instructions)
{
    if (instruction.Operand is GenericInstanceMethod gim)
    {
        var key = $"{resolvedDeclName}::{gim.ElementMethod.Name}";
        if (renames.TryGetValue(key, out var newName))
            gim.ElementMethod.Name = newName;
    }
}
```

### Step 3: AssemblyRenameTransform

Renames PE assembly identity names (the `AssemblyDefinition.Name.Name` field) and all
`AssemblyNameReference` entries pointing to renamed assemblies. Optionally renames the
physical `.dll` files on disk.

**Skipped entirely** when `--skip-assembly-rename` is passed — required for single-file
bundles because the bundle manifest encodes the original assembly names for probing; the
.NET runtime must be able to find embedded assemblies by their original names.

**Skip prefixes** (assembly names, not file names):
```
System.
Microsoft.
runtime.
```

**RNG seed:** `seed ^ 0x5A5A5A5A` — separate from the MetadataMangling seed to avoid
name collision between assembly names and type names.

**Three phases:**
1. Build rename map: collect all non-skipped assembly names → generate new names
2. Patch PE metadata: update `AssemblyDefinition.Name.Name` and all `AssemblyNameReference.Name` entries
3. Rename physical files on disk (skipped with `--skip-file-rename`)

**Write retry:** All PE writes use `WriteWithRetry` (5 attempts, 200ms × attempt
backoff) to handle transient file locks from AV scanners.

---

## Deobfuscation Map (`obf-map.json`)

Written per payload by the obfuscator and consumed by `load.py` to locate renamed command
DLL files.

**Format:**
```json
{
  "seed": 1234567,
  "uuid": "37eb846a-12b9-45d5-a49c-8e10754cc0ba",
  "stringDecryptor": {
    "namespace": "_ab3x",
    "className": "_q7rk",
    "methodName": "_m2np"
  },
  "indirectCaller": {
    "namespace": "_ab3x",
    "className": "_j9wq",
    "methodName": "_r4tz"
  },
  "uuidRenames": {
    "IModule": "_3f",
    "ITaskResponse": "_7a",
    "Workflow.Contracts": "_k7",
    ...
  },
  "metadataRenames": {
    "_k7._3f::Execute": "_x2",
    "_k7._7a::Complete": "_p5",
    "asm:Workflow.Providers.Runtime": "_5t",
    ...
  }
}
```

**Merge semantics:** The source phase writes `seed`, `uuid`, `stringDecryptor`,
`indirectCaller`, and `uuidRenames`. The IL phase loads the existing file and merges
`metadataRenames` (and `asm:*` entries for assembly renames) on top. Non-null fields
always overwrite. The file is written without UTF-8 BOM (Python's `json.load` rejects BOM).

**`load.py` usage:**
```python
# Find the renamed DLL file on disk
original_asm_name = f"{command_name}"  # e.g. "jobs"
renamed_asm_name = obf_map["metadataRenames"].get(f"asm:{original_asm_name}")
dll_path = os.path.join(publish_dir, f"{renamed_asm_name}.dll")
```

---

## What Is and Is Not Obfuscated

### Obfuscated

| Item | Transform |
|------|-----------|
| Plugin-facing interface names | UuidRenameTransform (source) |
| Plugin-facing interface member names | UuidRenameTransform (source) |
| Contract type names and namespaces | UuidRenameTransform (source) |
| String literals (most) | StringEncryptionTransform (source) |
| Direct calls to sensitive APIs | ApiCallHidingTransform (source) |
| All non-preserved type names | MetadataManglingTransform (IL) |
| All non-preserved method names | MetadataManglingTransform (IL) |
| All non-preserved field names | MetadataManglingTransform (IL) |
| Namespaces | MetadataManglingTransform (IL) |
| Cross-assembly TypeRefs and MemberRefs | CrossReferenceTransform (IL) |
| Assembly identity names (non-single-file) | AssemblyRenameTransform (IL) |
| DLL filenames on disk (non-single-file) | AssemblyRenameTransform (IL) |

### Not Obfuscated

| Item | Reason |
|------|--------|
| Third-party assembly contents | Skipped by prefix: `Autofac`, `IronPython`, `BouncyCastle`, `H.`, `Renci`, `Mono.`, `NamedPipe` |
| .NET runtime assemblies | Skipped by prefix: `System.`, `Microsoft.`, `runtime.` |
| Entry assembly name in single-file bundles | Apphost has this baked in at publish time; renaming breaks boot |
| Const string literals | Compiler requires compile-time constants |
| Attribute argument strings | Compiler requires compile-time constants |
| Switch case label strings | Compiler requires compile-time constants |
| `nameof(...)` strings | Compile-time constant operator |
| Empty strings | No value in encrypting |
| Enum field names | Preserve serialization semantics |
| Backing fields (`<Name>k__BackingField`) | Preserve compiler-generated patterns |
| Fields on `[Serializable]`/`[DataContract]` types | Preserve wire format compatibility |
| Interface implementations of external interfaces | CLR requires name match |
| Property accessors (`get_X`, `set_X`) | CLR requires name match for property metadata |
| Constructor names (`.ctor`, `.cctor`) | IL mandated names |
| P/Invoke extern methods | OS requires exact name for `GetProcAddress` |
| Standard overrides (`ToString`, `GetHashCode`, `Equals`, `Dispose`) | Framework contracts |
| `Read`/`Write` on `JsonConverter<T>` subclasses | Generic base traversal limitation in Cecil |

---

## Known Limitations and Constraints

### Single-File Bundle Constraint

The main agent is published as a single-file self-extracting bundle. The .NET runtime
has the entry assembly name (e.g., "ServiceHost") hardcoded in the apphost stub at
publish time. After single-file publish, the `ObfuscateBundleNames` MSBuild target
post-processes the bundle via `patch-bundle`:

1. Extracts the single-file bundle (unpacks embedded assemblies)
2. Applies `AssemblyRenameTransform` to rename embedded assembly identities using
   `SHA256(seed:name)` (same hash as the IL phase)
3. Re-bundles the renamed assemblies

This means:
- Assembly identity names **are obfuscated** in the bundle
- The entry assembly name (e.g., "ServiceHost") **remains unchanged** because the apphost
  has it baked in (renaming it would break the bootstrap)
- All other embedded assemblies (channels, providers, command DLLs) are renamed

The `--skip-assembly-rename` flag is still used for the initial IL phase because the
manifest isn't readable until post-extraction; `patch-bundle` handles the rename
separately.

Command DLLs loaded at runtime are individual `.dll` files (not bundles). They use
`--skip-assembly-rename` for consistency, but the DLL filename is what matters for
`Assembly.Load(AssemblyName)` probing.

### Cecil Generic Base Traversal

Cecil's `GetBaseMethod()` fails to traverse `GenericInstanceType` bases (e.g.,
`JsonConverter<List<string>>`). As a result, the obfuscator cannot automatically detect
that `Read` and `Write` override `JsonConverter<T>.Read` and `JsonConverter<T>.Write`.
These are hardcoded in `PreservedMethodNames`.

### Cross-Assembly Interface Resolution Ordering

`MetadataManglingTransform` must complete for ALL assemblies before `CrossReferenceTransform`
runs. If any assembly is written to disk mid-pass, a later pass may read the partially-obfuscated
version and produce incorrect cross-references. The `ILRewriter.RewriteBatch` defers all
disk writes until every `MetadataManglingTransform` has completed in memory.

### Dynamic Dispatch and `var`

When `ApiCallHidingTransform` wraps a call in `((dynamic)IndirectCaller.Invoke(...))`,
the result type is `dynamic`. Any `var` binding of this result propagates `dynamic` to
all subsequent uses via the DLR. If a subsequent call passes a `dynamic` receiver to a
renamed method, the DLR looks up the method by its original name at runtime, which no
longer exists. All indirect call results must be bound to explicit types.

### Overload Resolution

`IndirectCaller` dispatches overloads using `IsAssignableFrom` on runtime argument types.
This means `null` arguments match any parameter type (the null check is skipped), which
can cause ambiguity if multiple overloads have the same arity and the only distinguishing
argument is null. In practice this has not caused issues for the intercepted APIs.

---

## Debugging Obfuscated Builds

### Verify with ILSpy MCP

After a build, use the ILSpy MCP tools to inspect compiled DLLs:

```
mcp__ilspy-mcp__list_assembly_types    — confirm no Workflow.* type names
mcp__ilspy-mcp__search_members_by_name — search "Workflow", "Athena", "IModule"
```

Pass criteria: zero matches for original names.

Note: ILSpy MCP cannot open single-file executables. Use extracted DLLs from a publish
directory (non-single-file mode) or the DLLs extracted from a prior build.

### Common Failure Modes

**`MissingMethodException` at runtime:**
A cross-assembly MemberRef was not patched. Check `CrossReferenceTransform` — verify the
assembly's rename map has an entry for the method, and that the MethodSpec path (generic
method instantiations) is covered.

**`TypeLoadException` at runtime:**
A TypeRef was not patched, or an interface name mismatch between host and DLL. If the
agent and a command DLL were built with different seeds, `IsAssignableFrom` will return
false because the interface names no longer match.

**`RuntimeBinderException` (DLR):**
A `var` binding captured `dynamic` from an indirect call result. The DLR tried to resolve
a method by original name on the renamed type. Fix: use explicit type annotation.

**`InvalidCastException` on `Assembly.Load`:**
IndirectCaller selected the wrong overload (e.g., `Load(string)` when `Load(byte[])` was
intended). Check that argument runtime types are correct and that `IsAssignableFrom`
selects the right overload.

**"Module not found" after load task:**
The `load` task completed but the agent cannot find the `IModule` implementation. Most
likely cause: seed mismatch between host and command DLL. The renamed `IModule` interface
name in the DLL does not match the host's copy — `IsAssignableFrom` returns false. Verify
both used the same UUID-derived seed.
