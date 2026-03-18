# Obfuscator Safety Fixes Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add safety guards to MetadataManglingTransform, fix IndirectCaller instance method bug, and update ApiCallHidingTransform to distinguish static vs instance calls.

**Architecture:** Minimal changes to 3 existing files (MetadataManglingTransform.cs, IndirectCaller.cs, ApiCallHidingTransform.cs) plus test updates. No new files. TDD approach — tests first, then implementation.

**Tech Stack:** C#, Mono.Cecil (IL manipulation), Roslyn (source transforms), MSTest

**Spec:** `docs/superpowers/specs/2026-03-17-obfuscator-safety-fixes-design.md`

---

All file paths below are relative to `Payload_Type/athena/athena/agent_code/`.

### Task 1: Field Preservation — Enum, Serializable, and Backing Fields

**Files:**
- Test: `Tests/Obfuscator.Tests/MetadataManglingTests.cs`
- Modify: `Obfuscator/IL/Transforms/MetadataManglingTransform.cs`

- [ ] **Step 1: Add enum field preservation test**

Add to `MetadataManglingTests.cs` after the existing `ExternalInterface_TransformDoesNotThrow` test (after line 521):

```csharp
private const string EnumSource = """
    public enum Color
    {
        Red,
        Green,
        Blue
    }
    public class EnumUser
    {
        public static string GetName(Color c)
        {
            return System.Enum.GetName(typeof(Color), c);
        }
    }
    """;

[TestMethod]
public void EnumFields_ArePreserved()
{
    var dll = CompileToDll(EnumSource);
    var transform = new MetadataManglingTransform(seed: 42);
    var transformed = transform.Transform(dll);

    using var ms = new MemoryStream(transformed);
    var asm = AssemblyDefinition.ReadAssembly(ms);

    var expectedNames = new HashSet<string>
        { "value__", "Red", "Green", "Blue" };

    foreach (var type in asm.MainModule.Types)
    {
        if (!type.IsEnum)
            continue;
        foreach (var field in type.Fields)
        {
            Assert.IsTrue(
                expectedNames.Contains(field.Name),
                $"Enum field '{field.Name}' must retain "
                + "its original name (expected one of: "
                + string.Join(", ", expectedNames) + ")");
        }
    }
}

[TestMethod]
public void EnumWithGetName_StillWorks()
{
    var dll = CompileToDll(EnumSource);
    var transform = new MetadataManglingTransform(seed: 42);
    var transformed = transform.Transform(dll);

    var alc = new AssemblyLoadContext(
        $"EnumTest_{Guid.NewGuid():N}",
        isCollectible: true);
    try
    {
        var asm = alc.LoadFromStream(
            new MemoryStream(transformed));

        var enumType = asm.GetTypes()
            .First(t => t.IsEnum);
        var userType = asm.GetTypes()
            .First(t => !t.IsEnum
                && t.Name != "<Module>"
                && t.FullName
                    != "System.Runtime.CompilerServices"
                    + ".RefSafetyRulesAttribute");

        var method = userType.GetMethods(
                BindingFlags.Public | BindingFlags.Static)
            .First(m => m.ReturnType == typeof(string));

        var enumVal = Enum.ToObject(enumType, 1);
        var result = method.Invoke(
            null, new[] { enumVal });
        Assert.AreEqual(
            "Green", result,
            "Enum.GetName should return 'Green' "
            + "for value 1");
    }
    finally
    {
        alc.Unload();
    }
}
```

- [ ] **Step 2: Add serializable field preservation test**

Add after the enum tests:

```csharp
private const string SerializableSource = """
    [System.Serializable]
    public class Config
    {
        public string Host = "localhost";
        public int Port = 8080;
    }
    """;

[TestMethod]
public void SerializableTypeFields_ArePreserved()
{
    var dll = CompileToDll(SerializableSource);
    var transform = new MetadataManglingTransform(seed: 42);
    var transformed = transform.Transform(dll);

    using var ms = new MemoryStream(transformed);
    var asm = AssemblyDefinition.ReadAssembly(ms);

    foreach (var type in asm.MainModule.Types)
    {
        if (type.Name == "<Module>")
            continue;
        if (!type.IsSerializable)
            continue;
        foreach (var field in type.Fields)
        {
            Assert.IsFalse(
                field.Name.StartsWith("_"),
                $"[Serializable] field '{field.Name}' "
                + "should not be renamed");
        }
    }
}
```

- [ ] **Step 3: Add compiler-generated backing field preservation test**

```csharp
private const string AutoPropSource = """
    public class AutoProps
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }
    """;

[TestMethod]
public void CompilerGeneratedBackingFields_ArePreserved()
{
    var dll = CompileToDll(AutoPropSource);
    var transform = new MetadataManglingTransform(seed: 42);
    var transformed = transform.Transform(dll);

    using var ms = new MemoryStream(transformed);
    var asm = AssemblyDefinition.ReadAssembly(ms);

    foreach (var type in asm.MainModule.Types)
    {
        if (type.Name == "<Module>")
            continue;
        foreach (var field in type.Fields)
        {
            if (field.Name.StartsWith("<"))
            {
                Assert.IsTrue(
                    field.Name.Contains(
                        "k__BackingField"),
                    $"Backing field '{field.Name}' "
                    + "should be preserved");
            }
        }
    }
}
```

- [ ] **Step 4: Add DataContract field preservation test**

```csharp
private const string DataContractSource = """
    using System.Runtime.Serialization;
    [DataContract]
    public class Message
    {
        [DataMember]
        public string Content = "hello";
        [DataMember]
        public int Id = 1;
    }
    """;

[TestMethod]
public void DataContractTypeFields_ArePreserved()
{
    var trustedDir = Path.GetDirectoryName(
        typeof(object).Assembly.Location)!;
    var extraRefs = new MetadataReference[]
    {
        MetadataReference.CreateFromFile(
            Path.Combine(trustedDir,
                "System.Runtime.Serialization"
                + ".Primitives.dll")),
    };
    var dll = CompileToDll(
        DataContractSource, "TestAsm", extraRefs);
    var transform = new MetadataManglingTransform(
        seed: 42);
    var transformed = transform.Transform(dll);

    using var ms = new MemoryStream(transformed);
    var asm = AssemblyDefinition.ReadAssembly(ms);

    foreach (var type in asm.MainModule.Types)
    {
        if (type.Name == "<Module>")
            continue;
        var hasDataContract = type.CustomAttributes
            .Any(a => a.AttributeType.Name
                == "DataContractAttribute");
        if (!hasDataContract)
            continue;
        foreach (var field in type.Fields)
        {
            Assert.IsFalse(
                field.Name.StartsWith("_"),
                $"[DataContract] field '{field.Name}' "
                + "should not be renamed");
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they all fail**

```bash
cd Payload_Type/athena/athena/agent_code
dotnet build Tests/Obfuscator.Tests/
dotnet test Tests/Obfuscator.Tests/ --no-build --filter "EnumFields_ArePreserved|EnumWithGetName_StillWorks|SerializableTypeFields_ArePreserved|CompilerGeneratedBackingFields_ArePreserved|DataContractTypeFields_ArePreserved"
```

Expected: FAIL — enum fields, serializable fields, and backing fields are currently renamed.

- [ ] **Step 6: Implement ShouldPreserveField in MetadataManglingTransform**

Add to `MetadataManglingTransform.cs` after the `HasJsonPropertyNameAttribute` method (after line 318):

```csharp
private static bool ShouldPreserveField(
    FieldDefinition field)
{
    if (field.DeclaringType.IsEnum)
        return true;

    if (field.DeclaringType.IsSerializable)
        return true;

    if (field.Name.StartsWith("<"))
        return true;

    if (HasSerializationAttribute(field.DeclaringType))
        return true;

    if (HasFieldSerializationAttribute(field))
        return true;

    return false;
}

private static bool HasSerializationAttribute(
    TypeDefinition type)
{
    if (!type.HasCustomAttributes)
        return false;
    foreach (var attr in type.CustomAttributes)
    {
        if (attr.AttributeType.Name
            == "DataContractAttribute")
            return true;
    }
    return false;
}

private static bool HasFieldSerializationAttribute(
    FieldDefinition field)
{
    if (!field.HasCustomAttributes)
        return false;
    foreach (var attr in field.CustomAttributes)
    {
        var name = attr.AttributeType.Name;
        if (name == "JsonPropertyNameAttribute"
            || name == "DataMemberAttribute"
            || name == "JsonPropertyAttribute"
            || name == "XmlElementAttribute"
            || name == "XmlAttributeAttribute")
            return true;
    }
    return false;
}
```

- [ ] **Step 7: Guard RenameField with ShouldPreserveField**

Replace the `RenameField` method body (find `private void RenameField`):

Old:
```csharp
private void RenameField(
    FieldDefinition field,
    Random rng,
    HashSet<string> used)
{
    var original = field.Name;
    var newName = GenerateUniqueName(rng, used);
    _renameMappings[original] = newName;
    field.Name = newName;
}
```

New:
```csharp
private void RenameField(
    FieldDefinition field,
    Random rng,
    HashSet<string> used)
{
    if (ShouldPreserveField(field))
        return;

    var original = field.Name;
    var newName = GenerateUniqueName(rng, used);
    _renameMappings[original] = newName;
    field.Name = newName;
}
```

- [ ] **Step 8: Run field preservation tests to verify they pass**

```bash
dotnet test Tests/Obfuscator.Tests/ --filter "EnumFields_ArePreserved|EnumWithGetName_StillWorks|SerializableTypeFields_ArePreserved|CompilerGeneratedBackingFields_ArePreserved|DataContractTypeFields_ArePreserved"
```

Expected: PASS

- [ ] **Step 9: Update NamesStartWithUnderscore test**

The existing test asserts ALL fields start with `_`. Find the field assertion loop in `NamesStartWithUnderscore` and replace it.

Old:
```csharp
foreach (var field in type.Fields)
{
    Assert.IsTrue(
        field.Name.StartsWith("_"),
        $"Field '{field.Name}' must start with '_'");
}
```

New:
```csharp
foreach (var field in type.Fields)
{
    if (type.IsEnum
        || type.IsSerializable
        || field.Name.StartsWith("<"))
        continue;

    Assert.IsTrue(
        field.Name.StartsWith("_"),
        $"Field '{field.Name}' must start with '_'");
}
```

- [ ] **Step 10: Run full existing test suite**

```bash
dotnet test Tests/Obfuscator.Tests/
```

Expected: All tests PASS (including updated `NamesStartWithUnderscore`).

- [ ] **Step 11: Commit**

```bash
git add Tests/Obfuscator.Tests/MetadataManglingTests.cs \
  Obfuscator/IL/Transforms/MetadataManglingTransform.cs
git commit -m "feat(obfuscator): preserve enum, serializable, and backing fields

Add ShouldPreserveField guard to MetadataManglingTransform.RenameField
that skips renaming for enum fields (prevents CLR crash), [Serializable]
type fields, [DataContract] type fields, fields with serialization
attributes, and compiler-generated backing fields."
```

---

### Task 2: Property Accessor Preservation

**Files:**
- Test: `Tests/Obfuscator.Tests/MetadataManglingTests.cs`
- Modify: `Obfuscator/IL/Transforms/MetadataManglingTransform.cs`

- [ ] **Step 1: Add property accessor preservation test**

Add to `MetadataManglingTests.cs` (reuses `AutoPropSource` from Task 1):

```csharp
[TestMethod]
public void PropertyAccessors_ArePreserved()
{
    var dll = CompileToDll(AutoPropSource);
    var transform = new MetadataManglingTransform(seed: 42);
    var transformed = transform.Transform(dll);

    using var ms = new MemoryStream(transformed);
    var asm = AssemblyDefinition.ReadAssembly(ms);

    foreach (var type in asm.MainModule.Types)
    {
        if (type.Name == "<Module>")
            continue;
        foreach (var method in type.Methods)
        {
            if (method.IsConstructor)
                continue;
            if (method.IsGetter || method.IsSetter)
            {
                Assert.IsTrue(
                    method.Name.StartsWith("get_")
                    || method.Name.StartsWith("set_"),
                    $"Accessor '{method.Name}' should "
                    + "retain its original name");
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test Tests/Obfuscator.Tests/ --filter "PropertyAccessors_ArePreserved"
```

Expected: FAIL — accessors are currently renamed.

- [ ] **Step 3: Add accessor check to ShouldPreserveMethod**

In `MetadataManglingTransform.cs`, find the `ShouldPreserveMethod` method. Add the following check before the final `return false;`:

```csharp
// Keep property getters and setters — renaming breaks
// JSON serialization, reflection, and source generators
if (method.IsGetter || method.IsSetter)
    return true;
```

- [ ] **Step 4: Run accessor test to verify it passes**

```bash
dotnet test Tests/Obfuscator.Tests/ --filter "PropertyAccessors_ArePreserved"
```

Expected: PASS

- [ ] **Step 5: Run full test suite**

```bash
dotnet test Tests/Obfuscator.Tests/
```

Expected: All tests PASS.

- [ ] **Step 6: Commit**

```bash
git add Tests/Obfuscator.Tests/MetadataManglingTests.cs \
  Obfuscator/IL/Transforms/MetadataManglingTransform.cs
git commit -m "feat(obfuscator): preserve property accessor methods

Add IsGetter/IsSetter check to ShouldPreserveMethod so get_X/set_X
methods retain their names. Prevents breakage of JsonPropertyName
attributes, source-generated serializers, and reflection-based
property access."
```

---

### Task 3: Virtual Method Family Coordination

**Files:**
- Test: `Tests/Obfuscator.Tests/MetadataManglingTests.cs`
- Modify: `Obfuscator/IL/Transforms/MetadataManglingTransform.cs`

- [ ] **Step 1: Add virtual override chain test**

Add to `MetadataManglingTests.cs`:

```csharp
private const string VirtualChainSource = """
    public class Animal
    {
        public virtual string Speak()
        {
            return "...";
        }
        public int NonVirtual() { return 1; }
    }
    public class Dog : Animal
    {
        public override string Speak()
        {
            return "Woof";
        }
        public int AnotherNonVirtual() { return 2; }
    }
    """;

[TestMethod]
public void VirtualOverrideChain_GetsSameName()
{
    var dll = CompileToDll(VirtualChainSource);
    var transform = new MetadataManglingTransform(seed: 42);
    var transformed = transform.Transform(dll);

    using var ms = new MemoryStream(transformed);
    var asm = AssemblyDefinition.ReadAssembly(ms);

    var types = asm.MainModule.Types
        .Where(t => t.Name != "<Module>")
        .ToList();

    var baseSpeakName = types[0].Methods
        .First(m => m.IsVirtual
            && !m.IsConstructor
            && !PreservedNames.Contains(m.Name))
        .Name;
    var derivedSpeakName = types[1].Methods
        .First(m => m.IsVirtual
            && !m.IsConstructor
            && !PreservedNames.Contains(m.Name))
        .Name;

    Assert.AreEqual(
        baseSpeakName, derivedSpeakName,
        "Virtual method and its override must have "
        + "the same renamed name");
    Assert.IsTrue(
        baseSpeakName.StartsWith("_"),
        "Virtual method should be renamed");
}

private static readonly HashSet<string> PreservedNames =
    ["ToString", "GetHashCode", "Equals",
     "Dispose", "GetEnumerator", "MoveNext",
     "get_Current"];

[TestMethod]
public void NonVirtualMethods_RenamedIndependently()
{
    var dll = CompileToDll(VirtualChainSource);
    var transform = new MetadataManglingTransform(seed: 42);
    var transformed = transform.Transform(dll);

    using var ms = new MemoryStream(transformed);
    var asm = AssemblyDefinition.ReadAssembly(ms);

    var types = asm.MainModule.Types
        .Where(t => t.Name != "<Module>")
        .ToList();

    var baseNonVirtual = types[0].Methods
        .Where(m => !m.IsVirtual
            && !m.IsConstructor
            && !PreservedNames.Contains(m.Name))
        .Select(m => m.Name)
        .ToHashSet();

    var derivedNonVirtual = types[1].Methods
        .Where(m => !m.IsVirtual
            && !m.IsConstructor
            && !PreservedNames.Contains(m.Name))
        .Select(m => m.Name)
        .ToHashSet();

    Assert.IsFalse(
        baseNonVirtual.Overlaps(derivedNonVirtual),
        "Non-virtual methods in different types "
        + "should get different renamed names");
}
```

- [ ] **Step 2: Run tests to verify VirtualOverrideChain fails**

```bash
dotnet test Tests/Obfuscator.Tests/ --filter "VirtualOverrideChain_GetsSameName|NonVirtualMethods_RenamedIndependently"
```

Expected: `VirtualOverrideChain_GetsSameName` FAILS (base and derived get different names). `NonVirtualMethods_RenamedIndependently` may pass already.

- [ ] **Step 3: Add instance fields and BuildVirtualMethodFamilies**

In `MetadataManglingTransform.cs`, add two instance fields after `_renameMappings` (after `private Dictionary<string, string> _renameMappings = new();`):

```csharp
private Dictionary<MethodDefinition, MethodDefinition>
    _virtualFamilyRoot = new();
private Dictionary<MethodDefinition, string>
    _familyNameOverrides = new();
```

Add the `BuildVirtualMethodFamilies` static method after `GetRenameMappings`:

```csharp
private static Dictionary<MethodDefinition, MethodDefinition>
    BuildVirtualMethodFamilies(ModuleDefinition module)
{
    var rootMap = new Dictionary<
        MethodDefinition, MethodDefinition>();

    foreach (var type in EnumerateAllTypes(module))
    {
        foreach (var method in type.Methods)
        {
            if (!method.IsVirtual || !method.IsReuseSlot)
                continue;

            MethodDefinition baseMethod;
            try
            {
                baseMethod = method.GetBaseMethod();
            }
            catch (AssemblyResolutionException)
            {
                continue;
            }

            if (baseMethod == method)
                continue;
            if (baseMethod.DeclaringType.Scope
                is AssemblyNameReference)
                continue;

            rootMap[method] = baseMethod;
        }
    }

    return rootMap;
}
```

- [ ] **Step 4: Call pre-scan in Transform and update RenameMethod**

In the `Transform` method, add before the `RenameNamespaces` call (before the line `RenameNamespaces(asm.MainModule, rng, usedGlobal);`):

```csharp
_virtualFamilyRoot = BuildVirtualMethodFamilies(
    asm.MainModule);
_familyNameOverrides =
    new Dictionary<MethodDefinition, string>();
```

Replace the entire `RenameMethod` method with:

```csharp
private void RenameMethod(
    MethodDefinition method,
    Random rng,
    HashSet<string> used)
{
    if (ShouldPreserveMethod(method))
        return;

    // If a family member was already renamed and
    // recorded a name for this method, use it
    if (_familyNameOverrides.TryGetValue(
        method, out var familyName))
    {
        _renameMappings[method.Name] = familyName;
        method.Name = familyName;
        RenameGenericParameters(
            method.GenericParameters, rng, used);
        foreach (var param in method.Parameters)
            RenameParameter(param, rng, used);
        return;
    }

    var original = method.Name;
    var newName = GenerateUniqueName(rng, used);
    _renameMappings[original] = newName;
    method.Name = newName;

    // If this method is a virtual family root,
    // pre-record the name for all its overrides.
    // Also if this method is an override whose root
    // hasn't been processed yet, record for siblings.
    RecordFamilyName(method, newName);

    RenameGenericParameters(
        method.GenericParameters, rng, used);
    foreach (var param in method.Parameters)
        RenameParameter(param, rng, used);
}

private void RecordFamilyName(
    MethodDefinition method, string newName)
{
    // Case 1: method is a root — record for all
    // derived methods that map to it
    foreach (var (derived, root) in _virtualFamilyRoot)
    {
        if (root == method
            && !_familyNameOverrides
                .ContainsKey(derived))
        {
            _familyNameOverrides[derived] = newName;
        }
    }

    // Case 2: method is a derived method — record
    // for its root AND all sibling overrides, so
    // processing order doesn't matter
    if (_virtualFamilyRoot.TryGetValue(
        method, out var myRoot))
    {
        if (!_familyNameOverrides.ContainsKey(myRoot))
            _familyNameOverrides[myRoot] = newName;

        foreach (var (sibling, root)
            in _virtualFamilyRoot)
        {
            if (root == myRoot
                && sibling != method
                && !_familyNameOverrides
                    .ContainsKey(sibling))
            {
                _familyNameOverrides[sibling] = newName;
            }
        }
    }
}
```

This handles the case where derived types are processed before base types — whichever family member is renamed first records the name for all other members.

- [ ] **Step 5: Run virtual chain tests to verify they pass**

```bash
dotnet test Tests/Obfuscator.Tests/ --filter "VirtualOverrideChain_GetsSameName|NonVirtualMethods_RenamedIndependently"
```

Expected: PASS

- [ ] **Step 6: Run full test suite**

```bash
dotnet test Tests/Obfuscator.Tests/
```

Expected: All tests PASS.

- [ ] **Step 7: Commit**

```bash
git add Tests/Obfuscator.Tests/MetadataManglingTests.cs \
  Obfuscator/IL/Transforms/MetadataManglingTransform.cs
git commit -m "feat(obfuscator): coordinate virtual method family renames

Add pre-scan pass that groups virtual override chains within the assembly.
When renaming, all methods in a family get the same name regardless of
processing order, preserving polymorphism for rportfwd/AsyncTcpServer
and similar override chains."
```

---

### Task 4: IndirectCaller Instance Method Support

**Files:**
- Modify: `Obfuscator/Runtime/IndirectCaller.cs`
- Modify: `Obfuscator/Source/Transforms/ApiCallHidingTransform.cs`
- Test: `Tests/Obfuscator.Tests/ApiCallHidingTests.cs`

**Design note:** The `ApiCallHidingTransform` matches API calls by syntax identifier
text (e.g., `Socket` in `Socket.Connect(...)`). For instance method calls like
`socket.Connect(...)`, `ExtractTypeName` returns the *variable name* (`socket`),
not the type name (`Socket`). This means instance calls are only matched when
written with the type name as the receiver (e.g., `Socket.Connect(...)` — a
static-style invocation). To handle `variable.Method()` style calls would require
semantic model resolution, which is out of scope for this fix. The test uses the
type name as receiver to match existing transform behavior.

- [ ] **Step 1: Add instance method call test**

Add to `ApiCallHidingTests.cs`:

```csharp
[TestMethod]
public void InstanceMethodCall_PassesReceiver()
{
    // Use type-name receiver to match how
    // ExtractTypeName works (syntax-based matching)
    var source = """
        class C {
            void M() {
                Socket.Connect("127.0.0.1", 80);
            }
        }
        """;
    var result = ApplyTransform(source);

    Assert.IsFalse(
        result.Contains("Socket.Connect("),
        "Instance call should be replaced");
    Assert.IsTrue(
        result.Contains("_Invoke"),
        "Should use indirect invocation");

    // For non-static APIs, the receiver expression
    // (Socket) should be passed as the instance arg,
    // not null
    Assert.IsTrue(
        result.Contains("Socket,"),
        "Instance receiver 'Socket' should be passed "
        + "as third argument to the indirect caller");
}

[TestMethod]
public void StaticMethodCall_PassesNullInstance()
{
    var source = """
        class C {
            void M() {
                System.Diagnostics.Process.Start("cmd");
            }
        }
        """;
    var result = ApplyTransform(source);

    Assert.IsTrue(
        result.Contains("null,"),
        "Static calls should pass null as instance");
    Assert.IsTrue(
        result.Contains("_Invoke"),
        "Should use indirect invocation");
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test Tests/Obfuscator.Tests/ --filter "InstanceMethodCall_PassesReceiver|StaticMethodCall_PassesNullInstance"
```

Expected: FAIL — current code emits 3 arguments, not 4.

- [ ] **Step 3: Update IndirectCaller.cs to accept instance parameter**

Replace the entire content of `Obfuscator/Runtime/IndirectCaller.cs`:

```csharp
using System.Collections.Concurrent;
using System.Reflection;

namespace __OBFS_NS__
{
    internal static class __OBFS_CALLER_CLASS__
    {
        private static readonly
            ConcurrentDictionary<string, MethodInfo>
            _cache = new();

        internal static object? __OBFS_INVOKE_METHOD__(
            string typeName,
            string methodName,
            object? instance,
            object?[] args)
        {
            var key =
                (instance == null ? "s:" : "i:")
                + typeName + "." + methodName;
            var method = _cache.GetOrAdd(key, _ =>
            {
                var type = Type.GetType(
                    typeName, throwOnError: true)!;
                var methods = type.GetMethods(
                    BindingFlags.Public
                    | BindingFlags.Static
                    | BindingFlags.Instance
                    | BindingFlags.NonPublic);
                foreach (var m in methods)
                {
                    if (m.Name == methodName
                        && m.GetParameters().Length
                            == args.Length)
                        return m;
                }
                throw new MissingMethodException(
                    typeName, methodName);
            });
            return method.Invoke(instance, args);
        }
    }
}
```

- [ ] **Step 4: Change SensitiveApis from HashSet to Dictionary**

In `ApiCallHidingTransform.cs`, replace the `SensitiveApis` field:

Old:
```csharp
private static readonly HashSet<(string Type, string Method)> SensitiveApis =
[
    ("Process", "Start"),
    ("Assembly", "Load"),
    ("Assembly", "LoadFrom"),
    ("Assembly", "LoadFile"),
    ("File", "ReadAllBytes"),
    ("File", "ReadAllText"),
    ("File", "WriteAllBytes"),
    ("File", "WriteAllText"),
    ("Socket", "Connect"),
    ("HttpClient", "SendAsync"),
    ("WebClient", "DownloadData"),
];
```

New:
```csharp
// Value = true means static, false means instance
private static readonly
    Dictionary<(string Type, string Method), bool>
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

- [ ] **Step 5: Update VisitInvocationExpression**

Replace the `VisitInvocationExpression` method:

Old:
```csharp
public override SyntaxNode? VisitInvocationExpression(
    InvocationExpressionSyntax node)
{
    if (node.Expression is MemberAccessExpressionSyntax memberAccess
        && memberAccess.Kind() == SyntaxKind.SimpleMemberAccessExpression)
    {
        var methodName = memberAccess.Name.Identifier.Text;
        var typeName = ExtractTypeName(memberAccess.Expression);

        if (typeName is not null
            && SensitiveApis.Contains((typeName, methodName)))
        {
            _hiddenCalls.Add((typeName, methodName));
            var invocation = BuildIndirectInvocation(
                typeName, methodName, node);
```

New:
```csharp
public override SyntaxNode? VisitInvocationExpression(
    InvocationExpressionSyntax node)
{
    if (node.Expression
        is MemberAccessExpressionSyntax memberAccess
        && memberAccess.Kind()
            == SyntaxKind.SimpleMemberAccessExpression)
    {
        var methodName =
            memberAccess.Name.Identifier.Text;
        var typeName = ExtractTypeName(
            memberAccess.Expression);

        if (typeName is not null
            && SensitiveApis.TryGetValue(
                (typeName, methodName),
                out var isStatic))
        {
            _hiddenCalls.Add((typeName, methodName));

            ExpressionSyntax? instanceExpr = isStatic
                ? null
                : memberAccess.Expression;

            var invocation = BuildIndirectInvocation(
                typeName, methodName,
                instanceExpr, node);
```

The rest of the method (the `if (node.Parent is ExpressionStatementSyntax)` block and the cast/return) stays the same.

- [ ] **Step 6: Update BuildIndirectInvocation signature and body**

Replace the `BuildIndirectInvocation` method:

Old signature: `BuildIndirectInvocation(string typeName, string methodName, InvocationExpressionSyntax original)`

New:
```csharp
private InvocationExpressionSyntax BuildIndirectInvocation(
    string typeName,
    string methodName,
    ExpressionSyntax? instanceExpr,
    InvocationExpressionSyntax original)
{
    var callerAccess = MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName(_callerNamespace),
            IdentifierName(_callerClassName)),
        IdentifierName(_invokeMethodName));

    var fullTypeName = FullTypeNames.TryGetValue(
        typeName, out var fqn) ? fqn : typeName;

    var typeNameArg = Argument(
        LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            Literal(fullTypeName)));

    var methodNameArg = Argument(
        LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            Literal(methodName)));

    var instanceArg = Argument(
        instanceExpr
            ?? LiteralExpression(
                SyntaxKind.NullLiteralExpression));

    var originalArgs =
        original.ArgumentList.Arguments;
    var arrayElements = originalArgs.Select(a =>
        (ExpressionSyntax)CastExpression(
            NullableType(PredefinedType(
                Token(SyntaxKind.ObjectKeyword))),
            ParenthesizedExpression(a.Expression)));

    var argsArray = ArrayCreationExpression(
        Token(SyntaxTriviaList.Empty,
            SyntaxKind.NewKeyword,
            TriviaList(Space)),
        ArrayType(
            NullableType(PredefinedType(
                Token(SyntaxKind.ObjectKeyword))),
            SingletonList(
                ArrayRankSpecifier(
                    SingletonSeparatedList<
                        ExpressionSyntax>(
                        OmittedArraySizeExpression())))),
        InitializerExpression(
            SyntaxKind.ArrayInitializerExpression,
            SeparatedList<ExpressionSyntax>(
                arrayElements)));

    return InvocationExpression(
        callerAccess,
        ArgumentList(SeparatedList(new[]
        {
            typeNameArg,
            methodNameArg,
            instanceArg,
            Argument(argsArray),
        })));
}
```

- [ ] **Step 7: Run new tests to verify they pass**

```bash
dotnet test Tests/Obfuscator.Tests/ --filter "InstanceMethodCall_PassesReceiver|StaticMethodCall_PassesNullInstance"
```

Expected: PASS

- [ ] **Step 8: Run full test suite**

```bash
dotnet test Tests/Obfuscator.Tests/
```

Expected: All tests PASS. Existing `ApiCallHidingTests` pass because static calls now emit `null` as the third argument (backward compatible).

- [ ] **Step 9: Commit**

```bash
git add Obfuscator/Runtime/IndirectCaller.cs \
  Obfuscator/Source/Transforms/ApiCallHidingTransform.cs \
  Tests/Obfuscator.Tests/ApiCallHidingTests.cs
git commit -m "fix(obfuscator): pass instance to IndirectCaller for instance methods

Change IndirectCaller.Invoke to accept an instance parameter and use it
in method.Invoke(). Update ApiCallHidingTransform to distinguish static
vs instance calls via SensitiveApis dictionary. Static calls pass null,
instance calls pass the receiver expression. Fix cache key collision
with s:/i: prefix."
```

---

### Task 5: Final Verification

**Files:**
- All files from Tasks 1-4

- [ ] **Step 1: Run the complete test suite**

```bash
dotnet test Tests/Obfuscator.Tests/ -v normal
```

Expected: All tests PASS (existing + new).

- [ ] **Step 2: Run integration tests specifically**

```bash
dotnet test Tests/Obfuscator.Tests/ --filter "TestCategory=Integration" -v normal
```

Expected: Both `ObfuscatedSource_CoreProjects_Build` and `ObfuscatedSource_ServiceHostWithPlugins_Builds` PASS. This is the ultimate safety net — it obfuscates real Athena source and compiles it.

- [ ] **Step 3: Verify test count**

Expected test count: 60 existing + 10 new = 70 tests total.

New tests added:
1. `EnumFields_ArePreserved`
2. `EnumWithGetName_StillWorks`
3. `SerializableTypeFields_ArePreserved`
4. `CompilerGeneratedBackingFields_ArePreserved`
5. `DataContractTypeFields_ArePreserved`
6. `PropertyAccessors_ArePreserved`
7. `VirtualOverrideChain_GetsSameName`
8. `NonVirtualMethods_RenamedIndependently`
9. `InstanceMethodCall_PassesReceiver`
10. `StaticMethodCall_PassesNullInstance`

Existing test modified:
- `NamesStartWithUnderscore` — field assertion updated to skip preserved fields
