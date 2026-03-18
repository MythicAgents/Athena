# Obfuscator Safety Fixes — Lessons from Obfuscar

**Date:** 2026-03-17
**Status:** Draft
**Scope:** MetadataManglingTransform, ApiCallHidingTransform, IndirectCaller

## Problem

The Athena IL-level obfuscator (`MetadataManglingTransform`) renames types, methods,
fields, and parameters aggressively but lacks safety guards that mature obfuscators
like Obfuscar implement. A codebase scan revealed concrete breakage risks:

- **Enum `value__` field** — renaming crashes the CLR (guaranteed)
- **Enum member fields** — breaks `Enum.GetName()` in `inject-shellcode/AtomBomb.cs:161`
- **Property accessors** — 92 `[JsonPropertyName]` attributes, 10+ source-generated
  `[JsonSerializable]` contexts, and reflection via `GetProperty(choice)` in
  `cursed.cs:332` all depend on property/accessor name stability
- **Serializable type fields** — 8 `[Serializable]` classes in Workflow.Models
  with fields used in binary/JSON serialization
- **Virtual method families** — `rportfwd/AsyncTcpServer` overrides
  `OnClientConnected` within the same assembly; uncoordinated renaming breaks
  polymorphism
- **IndirectCaller instance bug** — `IndirectCaller.cs:28` passes `null` as
  instance for all calls, but `Socket.Connect` and `HttpClient.SendAsync` are
  instance methods (confirmed usage in `port-bender` and `cursed`)

## Approach

Minimal safety guards added to existing code. No new architectural abstractions.
All changes are modifications to existing files.

**Trade-off:** Fix 2 (preserving all property accessors) reduces obfuscation
coverage — every `get_X`/`set_X` method retains its original name. This is an
acceptable trade-off given the 92 `[JsonPropertyName]` attributes and pervasive
`JsonSerializer` usage across ~85 files. The alternative (selective preservation)
is too fragile for this codebase.

## Fix 1: Field Preservation in MetadataManglingTransform

**File:** `Obfuscator/IL/Transforms/MetadataManglingTransform.cs`

Add a `ShouldPreserveField` method called at the top of `RenameField`:

```
Skip if:
- field.DeclaringType.IsEnum (all enum fields, including value__)
- field.DeclaringType.IsSerializable
- field.DeclaringType has [DataContract] attribute
- field has [JsonPropertyName], [DataMember], [JsonProperty],
  [XmlElement], or [XmlAttribute] attribute
- field.Name starts with "<" (compiler-generated backing fields
  like <PropertyName>k__BackingField)
```

Attribute checks use `CustomAttribute.AttributeType.Name` string matching
(same pattern as the existing `HasJsonPropertyNameAttribute` at line 307).

**Note:** The `[DataContract]` guard is conservative — it preserves all fields
on the type even though `[DataContract]` semantics only serialize
`[DataMember]`-attributed members. This avoids the complexity of checking
both type and field attributes in combination, at the cost of slightly less
obfuscation on `[DataContract]` types.

**Rationale:**
- Enum `value__` is CLR-required. Enum member fields are used by
  `Enum.GetName()` (AtomBomb.cs:161) and `ToString()` (event-log.cs:73).
- 8 `[Serializable]` types in Workflow.Models use field names for
  binary serialization.
- Compiler-generated backing fields must match their property names
  for reflection and serialization to work.

## Fix 2: Property Accessor Preservation in MetadataManglingTransform

**File:** `Obfuscator/IL/Transforms/MetadataManglingTransform.cs`

Add to `ShouldPreserveMethod`:

```
if (method.IsGetter || method.IsSetter)
    return true;
```

**No property rename pass is added.** Properties are not renamed at IL level.

**Note on existing `HasJsonPropertyNameAttribute`:** The current check at
line 307 inspects `method.CustomAttributes`, but `[JsonPropertyName]` is
typically applied to properties, not to their getter/setter methods. In IL,
custom attributes on a property live on the `PropertyDefinition`, not the
`MethodDefinition`. This means the existing check is likely dead code. With
Fix 2 preserving all accessors unconditionally, this check becomes redundant
for accessors regardless, but it remains harmless for non-accessor methods.

**Rationale:**
- 92 `[JsonPropertyName]` attributes across channel configs, cursed models,
  and response types
- 10+ `[JsonSerializable]` source-generated serializer contexts tightly
  coupled to property names
- `cursed.cs:332,401` uses `type.GetProperty(choice)` with user-supplied
  strings
- `execute-module.cs:315` uses `type.GetMethod(methodName)` for dispatch
- ~85 files use `JsonSerializer.Serialize/Deserialize` with default
  property-name-based binding
- Simpler and safer than coordinated property+accessor renaming

## Fix 3: Virtual Method Family Coordination in MetadataManglingTransform

**File:** `Obfuscator/IL/Transforms/MetadataManglingTransform.cs`

Add a pre-scan pass before renaming:

1. `BuildVirtualMethodFamilies` runs at the start of `Transform`, before
   any renaming. For each method with `IsVirtual && IsReuseSlot`, walk up
   via `GetBaseMethod()`. If the base method is in the same assembly,
   group them into a family.

2. Produces a `Dictionary<MethodDefinition, string> familyNameOverrides`.

3. In `RenameMethod`, check order is:
   a. `ShouldPreserveMethod` first — if preserved (including accessors
      from Fix 2), skip entirely. Do not add to `familyNameOverrides`.
   b. Check `familyNameOverrides` — if a family member already has a
      rename, use the same name.
   c. Otherwise, generate a new name. If this method is in a family,
      record the name in `familyNameOverrides` for future family members.

**Scope:** Only handles override chains within the assembly. Cross-assembly
overrides are already preserved by the existing `ShouldPreserveMethod` logic
(lines 234-248 of MetadataManglingTransform.cs).

**Confirmed at-risk chains:**
- `rportfwd/AsyncTcpServer.OnClientConnected` (virtual) overridden in
  derived class within same assembly
- `rportfwd/AsyncTcpClient.OnConnectedAsync`, `OnClosed`,
  `OnReceivedAsync` (virtual, could be overridden)
- `socks/TcpClient.Dispose(bool)` and `UdpClient.Dispose(bool)` (virtual)

## Fix 4: IndirectCaller Instance Method Support

**File:** `Obfuscator/Runtime/IndirectCaller.cs`

Change the invoke signature to accept an optional instance:

```csharp
internal static object? Invoke(
    string typeName, string methodName,
    object? instance, object?[] args)
```

When `instance` is non-null, pass it to `method.Invoke(instance, args)`.
When null, it's a static call as before.

**Cache key collision:** The current `_cache` uses `typeName + "." + methodName`
as key. If a type has both static and instance overloads with the same name and
parameter count, the cache could return the wrong `MethodInfo`. Fix: include
`instance == null` in the cache key to distinguish static from instance lookups:

```csharp
var key = (instance == null ? "s:" : "i:") + typeName + "." + methodName;
```

**File:** `Obfuscator/Source/Transforms/ApiCallHidingTransform.cs`

Change `SensitiveApis` from `HashSet<(string Type, string Method)>` to
`Dictionary<(string Type, string Method), bool>` where the bool indicates
`IsStatic`:

```csharp
private static readonly Dictionary<(string Type, string Method), bool>
    SensitiveApis = new()
{
    [("Process", "Start")] = true,
    [("Assembly", "Load")] = true,
    [("Assembly", "LoadFrom")] = true,
    [("Assembly", "LoadFile")] = true,
    [("File", "ReadAllBytes")] = true,
    [("File", "ReadAllText")] = true,
    [("File", "WriteAllBytes")] = true,
    [("File", "WriteAllText")] = true,
    [("Socket", "Connect")] = false,
    [("HttpClient", "SendAsync")] = false,
    [("WebClient", "DownloadData")] = false,
};
```

When building the indirect invocation for instance calls:
- The **type name** for `IndirectCaller.Invoke` still comes from the
  `FullTypeNames` dictionary (keyed by the `SensitiveApis` type identifier),
  not from the expression. For `socket.Connect(...)`, the type is `"Socket"`,
  which maps to `"System.Net.Sockets.Socket"` via `FullTypeNames`.
- The **instance argument** is the expression itself (`socket` variable).
  Pass it as the third argument to `Invoke`.
- For static calls, pass `null` as the instance argument.

Detection logic in `VisitInvocationExpression`:
1. Extract `typeName` and `methodName` from the member access as before.
2. Look up `SensitiveApis.TryGetValue((typeName, methodName), out bool isStatic)`.
3. If `isStatic`, pass `null` as instance; emit type name from `FullTypeNames`.
4. If not static, pass `memberAccess.Expression` as the instance argument;
   emit type name from `FullTypeNames` using the extracted `typeName`.

**Confirmed instance usage:**
- `port-bender/TcpForwarderSlim.cs:8` — Socket instance
- `cursed/DebugHelper.cs:113,146` — HttpClient instances

## Execution Order in MetadataManglingTransform.Transform

After all fixes, the order within `Transform` becomes:

1. Build virtual method families (new pre-scan)
2. `RenameNamespaces` (unchanged)
3. For each type via `EnumerateAllTypes`:
   - `RenameType` which internally does:
     a. Rename type name (unchanged)
     b. Rename generic parameters (unchanged)
     c. Rename fields — with `ShouldPreserveField` guard (new)
     d. Rename events (unchanged)
     e. Rename methods — check `ShouldPreserveMethod` first (includes
        new accessor check), then `familyNameOverrides`, then generate
        new name (modified)

## Test Changes

### Existing Test Modification

**`MetadataManglingTests.NamesStartWithUnderscore`** (line 221-226)

Currently asserts ALL fields start with `_`. After fix, enum fields and
compiler-generated backing fields are preserved. Update the test source
(`SimpleClassSource`) to include an enum and an auto-property, then
update the field assertion to skip:
- Fields on enum types (`field.DeclaringType.IsEnum`)
- Fields whose name starts with `<`
- Fields on `[Serializable]` types

### New Tests (in MetadataManglingTests)

**`EnumFields_ArePreserved`**
Compile an enum type, transform, verify `value__` and all member fields
retain their original names.

**`EnumWithGetName_StillWorks`**
Compile code using `Enum.GetName()`, transform, load into
AssemblyLoadContext, execute, verify correct string returned.

**`VirtualOverrideChain_GetsSameName`**
Compile a base class with `virtual void Foo()` and derived class with
`override void Foo()`, transform, verify both methods have the same
renamed name.

**`NonVirtualMethods_InInheritanceChain_RenamedIndependently`**
Compile a base class with a non-virtual method and a derived class with
a different non-virtual method of the same name (hiding). Verify they get
different renamed names (not grouped as a family).

**`PropertyAccessors_ArePreserved`**
Compile class with auto-properties, transform, verify `get_X`/`set_X`
methods retain their names.

**`CompilerGeneratedBackingFields_ArePreserved`**
Compile auto-properties, transform, verify `<X>k__BackingField` fields
are untouched.

**`SerializableTypeFields_ArePreserved`**
Compile a `[Serializable]` class with fields, transform, verify field
names are preserved.

**`DataContractTypeFields_ArePreserved`**
Compile a `[DataContract]` class with `[DataMember]` fields, transform,
verify field names are preserved.

### New Test (in ApiCallHidingTests)

**`InstanceMethodCall_PassesReceiver`**
Verify that `socket.Connect(endpoint)` is transformed to emit the
`socket` variable as the instance argument (not `null`). Check that the
emitted code has 4 arguments to the indirect caller (typeName, methodName,
instance, args).

### Existing Tests That Must Still Pass

All other existing tests should pass without modification:
- `StringEncryptionTests` (7 tests) — no changes to this transform
- `ApiCallHidingTests` (6 tests) — backward compatible (static calls
  still work; new instance test is additive)
- `UuidRenameTests` (10 tests) — no changes to UUID rename
- `UuidRenameTransformTests` (8 tests) — no changes
- `IntegrationTests` (7 tests) — full pipeline still works
- `BuildIntegrationTests` (2 tests) — obfuscated source still compiles

The `BuildIntegrationTests.ObfuscatedSource_ServiceHostWithPlugins_Builds`
test serves as the ultimate safety net — it obfuscates the real Athena
source and compiles it.

## Files Modified

| File | Change | Risk |
|------|--------|------|
| `IL/Transforms/MetadataManglingTransform.cs` | Add `ShouldPreserveField`, accessor preservation, virtual family pre-scan | Medium — reduces obfuscation coverage for safety |
| `Runtime/IndirectCaller.cs` | Add `instance` parameter, fix cache key | Medium — changes signature used by all hidden calls |
| `Source/Transforms/ApiCallHidingTransform.cs` | Change `SensitiveApis` to dict, detect static vs instance, pass receiver | Medium — must emit correct arg count/order |
| `Tests/Obfuscator.Tests/MetadataManglingTests.cs` | Update 1 test, add 8 new tests | Low |
| `Tests/Obfuscator.Tests/ApiCallHidingTests.cs` | Add 1 new test | Low |

No new files created. No changes to source-level transforms
(UuidRenameTransform, StringEncryptionTransform).

## Out of Scope

- Full Obfuscar-style InheritMap/MethodGroup infrastructure
- Property renaming at IL level
- Custom attribute named argument updates
- XAML/BAML resource scanning
- String encryption key strengthening
