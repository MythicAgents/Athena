# Custom Obfuscator Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Obfuscar with a custom two-stage obfuscation system (Roslyn source rewriting + Mono.Cecil IL rewriting) that produces polymorphic output for AV/EDR evasion.

**Architecture:** A standalone .NET CLI tool (`Obfuscator`) with two subcommands: `rewrite-source` (Roslyn syntax tree transforms for string encryption and API call hiding) and `rewrite-il` (Mono.Cecil transforms for control flow flattening and metadata mangling). Interface names are derived deterministically from the payload UUID so reflective DLLs always match their parent agent. The build pipeline integrates via MSBuild targets for IL rewriting and Python-side orchestration for source rewriting.

**Tech Stack:** .NET 10, Microsoft.CodeAnalysis (Roslyn) for source rewriting, Mono.Cecil for IL rewriting, MSBuild custom targets, Python (builder.py / load.py integration)

**Spec:** `docs/superpowers/specs/2026-03-15-custom-obfuscator-design.md`

---

## File Structure

### New Files

| Path | Responsibility |
|------|---------------|
| `agent_code/Obfuscator/Obfuscator.csproj` | CLI tool project — references Roslyn + Cecil |
| `agent_code/Obfuscator/Program.cs` | Entry point with `rewrite-source` and `rewrite-il` subcommands |
| `agent_code/Obfuscator/Config/ObfuscationConfig.cs` | Seed, UUID, transform settings, target paths |
| `agent_code/Obfuscator/Config/UuidRenameMap.cs` | Derives interface/type renames from payload UUID via SHA256 |
| `agent_code/Obfuscator/Source/SourceRewriter.cs` | Orchestrates Roslyn transforms across all .cs files |
| `agent_code/Obfuscator/Source/Transforms/StringEncryptionTransform.cs` | Roslyn CSharpSyntaxRewriter for string literals |
| `agent_code/Obfuscator/Source/Transforms/ApiCallHidingTransform.cs` | Roslyn CSharpSyntaxRewriter for sensitive API calls |
| `agent_code/Obfuscator/Source/Transforms/UuidRenameTransform.cs` | Roslyn rewriter for UUID-derived interface/type renames |
| `agent_code/Obfuscator/IL/ILRewriter.cs` | Orchestrates Cecil transforms on a compiled DLL |
| `agent_code/Obfuscator/IL/Transforms/ControlFlowTransform.cs` | Cecil-based control flow flattening + opaque predicates + bogus code |
| `agent_code/Obfuscator/IL/Transforms/MetadataManglingTransform.cs` | Cecil-based type/method/field renaming |
| `agent_code/Obfuscator/Runtime/StringDecryptor.cs` | Template .cs file injected into target assemblies |
| `agent_code/Obfuscator/Runtime/IndirectCaller.cs` | Template .cs file for reflection-based API call dispatch |
| `agent_code/Obfuscator/DeobfuscationMap.cs` | Generates JSON mapping file for both stages |
| `agent_code/Tests/Obfuscator.Tests/Obfuscator.Tests.csproj` | Test project for obfuscator |
| `agent_code/Tests/Obfuscator.Tests/StringEncryptionTests.cs` | Unit tests for string encryption transform |
| `agent_code/Tests/Obfuscator.Tests/ApiCallHidingTests.cs` | Unit tests for API call hiding transform |
| `agent_code/Tests/Obfuscator.Tests/UuidRenameTests.cs` | Unit tests for UUID-derived renaming |
| `agent_code/Tests/Obfuscator.Tests/ControlFlowTests.cs` | Unit tests for control flow flattening |
| `agent_code/Tests/Obfuscator.Tests/MetadataManglingTests.cs` | Unit tests for metadata renaming |
| `agent_code/Tests/Obfuscator.Tests/IntegrationTests.cs` | End-to-end obfuscation + load + execute tests |

### Modified Files

| Path | Change |
|------|--------|
| `agent_code/Workflow.sln` | Add Obfuscator and Obfuscator.Tests projects |
| `agent_code/ServiceHost/ServiceHost.csproj` | Remove Obfuscar PackageReference, remove placeholder comment |
| `agent_code/Directory.Build.targets` | Add ObfuscateIL MSBuild target (conditional) |
| `Payload_Type/athena/main.py` | Remove Obfuscar placeholder injection code, add obfuscator tool build |
| `Payload_Type/athena/athena/mythic/agent_functions/builder.py` | Add source rewrite step, generate ObfSeed, pass to publish |
| `Payload_Type/athena/athena/mythic/agent_functions/load.py` | Switch to compile-per-load with obfuscation, update DLL paths |

### Removed Files

| Path | Reason |
|------|--------|
| `Payload_Type/athena/agent.obfs` | Replaced by custom obfuscator |
| `Payload_Type/athena/common.obfs` | Replaced by custom obfuscator |
| `agent_code/build_utils.py` | Obfuscar wrapper, no longer needed |

---

## Chunk 1: Foundation — Project Scaffold, Config, Seed Derivation

### Task 1: Create Obfuscator CLI project scaffold

**Files:**
- Create: `agent_code/Obfuscator/Obfuscator.csproj`
- Create: `agent_code/Obfuscator/Program.cs`
- Modify: `agent_code/Workflow.sln`

- [ ] **Step 1: Create the .csproj file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>obfuscator</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
    <PackageReference Include="Mono.Cecil" Version="0.11.6" />
  </ItemGroup>
</Project>
```

Look up the latest stable versions of `Microsoft.CodeAnalysis.CSharp` and `Mono.Cecil` on NuGet before using the versions above — those are placeholders.

- [ ] **Step 2: Create Program.cs with subcommand routing**

```csharp
using System.CommandLine;

var rootCommand = new RootCommand("Athena obfuscation tool");

var rewriteSourceCmd = new Command("rewrite-source", "Roslyn source transforms");
rewriteSourceCmd.AddOption(new Option<int>("--seed", "Random seed") { IsRequired = true });
rewriteSourceCmd.AddOption(new Option<string>("--uuid", "Payload UUID") { IsRequired = true });
rewriteSourceCmd.AddOption(new Option<string>("--input", "Source directory") { IsRequired = true });
rewriteSourceCmd.AddOption(new Option<string>("--output", "Output directory") { IsRequired = true });
rewriteSourceCmd.SetHandler((int seed, string uuid, string input, string output) =>
{
    // TODO: wire up in Task 3
    Console.WriteLine($"rewrite-source: seed={seed} uuid={uuid}");
}, /* bound options */);

var rewriteIlCmd = new Command("rewrite-il", "Cecil IL transforms");
rewriteIlCmd.AddOption(new Option<int>("--seed", "Random seed") { IsRequired = true });
rewriteIlCmd.AddOption(new Option<string>("--input", "DLL path") { IsRequired = true });
rewriteIlCmd.AddOption(new Option<string>("--map", "Deobfuscation map path"));
rewriteIlCmd.SetHandler((int seed, string input, string? map) =>
{
    // TODO: wire up in Task 8
    Console.WriteLine($"rewrite-il: seed={seed} input={input}");
}, /* bound options */);

rootCommand.AddCommand(rewriteSourceCmd);
rootCommand.AddCommand(rewriteIlCmd);
return await rootCommand.InvokeAsync(args);
```

Add `System.CommandLine` to the csproj PackageReferences (look up latest stable version).

- [ ] **Step 3: Add project to solution**

Run: `dotnet sln agent_code/Workflow.sln add agent_code/Obfuscator/Obfuscator.csproj`

- [ ] **Step 4: Verify it builds and runs**

Run: `dotnet build agent_code/Obfuscator/Obfuscator.csproj`
Then: `dotnet run --project agent_code/Obfuscator -- rewrite-source --seed 12345 --uuid test-uuid --input . --output .`
Expected: prints `rewrite-source: seed=12345 uuid=test-uuid`

- [ ] **Step 5: Commit**

```
feat(obfuscator): scaffold CLI tool with rewrite-source and rewrite-il subcommands
```

### Task 2: Implement ObfuscationConfig and UuidRenameMap

**Files:**
- Create: `agent_code/Obfuscator/Config/ObfuscationConfig.cs`
- Create: `agent_code/Obfuscator/Config/UuidRenameMap.cs`
- Create: `agent_code/Tests/Obfuscator.Tests/Obfuscator.Tests.csproj`
- Create: `agent_code/Tests/Obfuscator.Tests/UuidRenameTests.cs`

- [ ] **Step 1: Create the test project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.2.0" />
    <PackageReference Include="MSTest.TestFramework" Version="3.2.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Obfuscator\Obfuscator.csproj" />
  </ItemGroup>
</Project>
```

Look up latest stable versions. Add to solution: `dotnet sln agent_code/Workflow.sln add agent_code/Tests/Obfuscator.Tests/Obfuscator.Tests.csproj`

- [ ] **Step 2: Write failing tests for UuidRenameMap**

```csharp
[TestClass]
public class UuidRenameTests
{
    [TestMethod]
    public void SameUuid_ProducesSameMapping()
    {
        var map1 = UuidRenameMap.Derive("test-uuid-1234");
        var map2 = UuidRenameMap.Derive("test-uuid-1234");
        Assert.AreEqual(map1.GetRenamed("IModule"), map2.GetRenamed("IModule"));
        Assert.AreEqual(map1.GetRenamed("Execute"), map2.GetRenamed("Execute"));
    }

    [TestMethod]
    public void DifferentUuid_ProducesDifferentMapping()
    {
        var map1 = UuidRenameMap.Derive("uuid-aaa");
        var map2 = UuidRenameMap.Derive("uuid-bbb");
        Assert.AreNotEqual(map1.GetRenamed("IModule"), map2.GetRenamed("IModule"));
    }

    [TestMethod]
    public void AllContractTypes_AreMapped()
    {
        var map = UuidRenameMap.Derive("test-uuid");
        // Interfaces
        foreach (var name in new[] {
            "IModule", "IInteractiveModule", "IFileModule",
            "IForwarderModule", "IProxyModule", "IBufferedProxyModule",
            "IChannel", "IService", "IComponentProvider", "IDataBroker",
            "IServiceConfig", "ISecurityProvider", "ILogger",
            "IRequestDispatcher", "IRuntimeExecutor", "ICredentialProvider",
            "IScriptEngine", "IServiceExtension" })
        {
            Assert.IsNotNull(map.GetRenamed(name), $"Missing mapping for {name}");
            Assert.IsTrue(map.GetRenamed(name).StartsWith("_"), $"Should start with _ for {name}");
        }
        // Interface members (method/property names)
        foreach (var name in new[] {
            "Name", "Execute", "Interact", "HandleNextMessage",
            "ForwardDelegate", "HandleDatagram", "FlushServerMessages",
            "StartBeacon", "StopBeacon", "SetTaskingReceived",
            "TryGetModule", "LoadModuleAsync", "LoadAssemblyAsync",
            "AddTaskResponse", "AddDelegateMessage", "AddInteractMessage",
            "AddDatagram", "Write", "WriteLine", "AddKeystroke",
            "AddJob", "GetJobs", "TryGetJob", "CompleteJob",
            "GetAgentResponseString", "HasResponses", "CaptureStdOut",
            "ReleaseStdOut", "StdIsBusy", "GetStdOut",
            "Spawn", "TryGetHandle",
            "AddToken", "Impersonate", "List", "Revert",
            "getIntegrity", "GetImpersonationContext",
            "RunTaskImpersonated", "HandleFilePluginImpersonated",
            "HandleInteractivePluginImpersonated",
            "LoadPyLib", "ExecuteScriptAsync", "ExecuteScript", "ClearPyLib" })
        {
            Assert.IsNotNull(map.GetRenamed(name), $"Missing member mapping for {name}");
        }
        // Contract types
        foreach (var name in new[] {
            "ServerJob", "InteractMessage", "ServerTaskingResponse",
            "DelegateMessage", "ServerDatagram", "PluginContext",
            "ITaskResponse", "Checkin", "CheckinResponse",
            "TaskingReceivedArgs", "DatagramSource", "SpawnOptions",
            "CreateToken", "TokenTaskResponse" })
        {
            Assert.IsNotNull(map.GetRenamed(name), $"Missing mapping for {name}");
        }
        // PluginContext parameter names
        foreach (var name in new[] {
            "MessageManager", "Config", "Logger",
            "TokenManager", "Spawner", "ScriptEngine" })
        {
            Assert.IsNotNull(map.GetRenamed(name), $"Missing PluginContext param mapping for {name}");
        }
        // Namespaces
        Assert.IsNotNull(map.GetRenamed("Workflow.Contracts"));
        Assert.IsNotNull(map.GetRenamed("Workflow.Models"));
    }

    [TestMethod]
    public void GeneratedNames_DoNotCollide()
    {
        var map = UuidRenameMap.Derive("collision-test-uuid");
        var allNames = map.GetAllRenamedValues();
        Assert.AreEqual(allNames.Count, allNames.Distinct().Count(),
            "Collision detected in renamed identifiers");
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test agent_code/Tests/Obfuscator.Tests/ --filter "ClassName=UuidRenameTests" -v n`
Expected: FAIL — `UuidRenameMap` does not exist

- [ ] **Step 4: Implement ObfuscationConfig**

```csharp
namespace Obfuscator.Config;

public record ObfuscationConfig(
    int Seed,
    string? Uuid,
    string InputPath,
    string OutputPath,
    string? MapPath
)
{
    public Random CreateRandom() => new Random(Seed);
}
```

- [ ] **Step 5: Implement UuidRenameMap**

Create `agent_code/Obfuscator/Config/UuidRenameMap.cs`. This class:
1. Takes a UUID string
2. Computes `SHA256(uuid + "athena-obfs")` to get a deterministic seed
3. Uses that seed to generate `_`-prefixed random identifier names for every interface, contract type, member, and namespace in the exhaustive list from the spec
4. Uses counter-based naming: start at 2 chars after `_` prefix, increment on collision
5. Exposes `GetRenamed(string originalName)` and `GetAllRenamedValues()`

Consult the spec at `docs/superpowers/specs/2026-03-15-custom-obfuscator-design.md` section "Exhaustive UUID-Derived Rename List" for the complete list. Every interface, every member of every interface, every contract type, both namespaces, and all PluginContext parameter names must be mapped.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test agent_code/Tests/Obfuscator.Tests/ --filter "ClassName=UuidRenameTests" -v n`
Expected: all PASS

- [ ] **Step 7: Commit**

```
feat(obfuscator): add ObfuscationConfig and UUID-derived rename map with tests
```

---

## Chunk 2: Source-Level Transforms — String Encryption

### Task 3: Implement StringDecryptor runtime helper template

**Files:**
- Create: `agent_code/Obfuscator/Runtime/StringDecryptor.cs`

- [ ] **Step 1: Create the template file**

This is a C# source file that will be **copied into target projects** during source rewriting. It contains a static class with one method that XOR-decrypts a byte array using a provided key.

The class name, method name, and namespace are **placeholder tokens** that the source rewriter will replace with randomized names:

```csharp
namespace __OBFS_NS__
{
    internal static class __OBFS_CLASS__
    {
        internal static string __OBFS_METHOD__(byte[] data, byte key)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                result[i] = (byte)(data[i] ^ key);
            return System.Text.Encoding.UTF8.GetString(result);
        }
    }
}
```

- [ ] **Step 2: Commit**

```
feat(obfuscator): add StringDecryptor runtime helper template
```

### Task 4: Implement StringEncryptionTransform

**Files:**
- Create: `agent_code/Obfuscator/Source/Transforms/StringEncryptionTransform.cs`
- Create: `agent_code/Tests/Obfuscator.Tests/StringEncryptionTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[TestClass]
public class StringEncryptionTests
{
    [TestMethod]
    public void LiteralString_IsReplaced()
    {
        var source = """var x = "hello world";""";
        var result = ApplyTransform(source, seed: 42);
        Assert.IsFalse(result.Contains("\"hello world\""),
            "Original string literal should be replaced");
        Assert.IsTrue(result.Contains("new byte[]"),
            "Should contain encrypted byte array");
    }

    [TestMethod]
    public void NameofExpression_IsNotReplaced()
    {
        var source = """var x = nameof(Console);""";
        var result = ApplyTransform(source, seed: 42);
        Assert.IsTrue(result.Contains("nameof(Console)"),
            "nameof should be preserved");
    }

    [TestMethod]
    public void AttributeArgument_IsNotReplaced()
    {
        var source = """[DllImport("kernel32")] static extern void Foo();""";
        var result = ApplyTransform(source, seed: 42);
        Assert.IsTrue(result.Contains("\"kernel32\""),
            "Attribute string should be preserved");
    }

    [TestMethod]
    public void ConstString_IsNotReplaced()
    {
        var source = """const string x = "constant";""";
        var result = ApplyTransform(source, seed: 42);
        Assert.IsTrue(result.Contains("\"constant\""),
            "Const string should be preserved");
    }

    [TestMethod]
    public void DifferentSeeds_ProduceDifferentOutput()
    {
        var source = """var x = "hello";""";
        var result1 = ApplyTransform(source, seed: 1);
        var result2 = ApplyTransform(source, seed: 2);
        Assert.AreNotEqual(result1, result2,
            "Different seeds should produce different encrypted output");
    }

    [TestMethod]
    public void InterpolatedString_OnlyEncryptsLiteralParts()
    {
        var source = """var x = $"Hello {name}!";""";
        var result = ApplyTransform(source, seed: 42);
        // The interpolation expression {name} should remain
        Assert.IsTrue(result.Contains("name"),
            "Interpolation expression should be preserved");
    }

    // Helper that parses source, applies transform, returns modified source text
    private string ApplyTransform(string source, int seed)
    {
        // Parse with Roslyn, apply StringEncryptionTransform, return SyntaxTree text
        // Uses a test wrapper method on StringEncryptionTransform
        throw new NotImplementedException("Implement after transform exists");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test agent_code/Tests/Obfuscator.Tests/ --filter "ClassName=StringEncryptionTests" -v n`
Expected: FAIL

- [ ] **Step 3: Implement StringEncryptionTransform**

Create `agent_code/Obfuscator/Source/Transforms/StringEncryptionTransform.cs`. This is a `CSharpSyntaxRewriter` that:

1. Overrides `VisitLiteralExpression` — if the token is a `StringLiteralExpression`:
   - Check if parent is `nameof()` expression → skip
   - Check if parent is `AttributeArgument` → skip
   - Check if it's a `const` field or local → skip
   - Otherwise: XOR-encrypt the string bytes with a key derived from `seed + stringIndex`, replace with `DecryptorClass.DecryptorMethod(new byte[]{...}, key)`
2. Overrides `VisitInterpolatedStringExpression` — for each `InterpolatedStringText` part, encrypt the literal text; leave `Interpolation` nodes unchanged
3. Tracks a string counter for key derivation

The class name and method name for the decryptor call are provided via constructor (randomized by SourceRewriter).

- [ ] **Step 4: Update the test helper `ApplyTransform` to use the real transform**

Wire up the helper to parse with Roslyn `CSharpSyntaxTree.ParseText()`, apply the transform, and return the rewritten source text.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test agent_code/Tests/Obfuscator.Tests/ --filter "ClassName=StringEncryptionTests" -v n`
Expected: all PASS

- [ ] **Step 6: Commit**

```
feat(obfuscator): implement string encryption transform with tests
```

### Task 5: Implement IndirectCaller runtime helper and ApiCallHidingTransform

**Files:**
- Create: `agent_code/Obfuscator/Runtime/IndirectCaller.cs`
- Create: `agent_code/Obfuscator/Source/Transforms/ApiCallHidingTransform.cs`
- Create: `agent_code/Tests/Obfuscator.Tests/ApiCallHidingTests.cs`

- [ ] **Step 1: Create IndirectCaller template**

Similar to StringDecryptor — a template .cs file with placeholder tokens for class/method/namespace. The class:
- Has a static method that takes encrypted type name, encrypted method name, and object[] args
- Uses reflection to resolve the type and method at runtime
- Caches resolved MethodInfo in a ConcurrentDictionary for performance
- Returns object (caller casts as needed)

- [ ] **Step 2: Write failing tests for ApiCallHidingTransform**

Test that:
- `Process.Start("cmd")` gets replaced with an indirect call
- Non-sensitive calls like `Console.WriteLine()` are NOT replaced
- `File.ReadAllBytes(path)` IS replaced
- `[DynamicDependency]` attributes are emitted for each hidden call
- Different seeds produce different helper names

- [ ] **Step 3: Run tests — verify fail**

- [ ] **Step 4: Implement ApiCallHidingTransform**

A `CSharpSyntaxRewriter` that:
1. Maintains a configurable set of sensitive API patterns (type + method name pairs)
2. On `VisitInvocationExpression`, checks if the call matches a sensitive pattern
3. If matched: replaces with `IndirectCallerClass.InvokeMethod(encryptedTypeName, encryptedMethodName, args)`
4. Adds `[DynamicDependency]` to the containing method/class to preserve trimming
5. The sensitive API list should include at minimum: `Process.Start`, `Assembly.Load`, `Assembly.LoadFrom`, `File.ReadAllBytes`, `File.ReadAllText`, `File.WriteAllBytes`, `File.WriteAllText`, `Socket.Connect`, `HttpClient.SendAsync`, `WebClient.DownloadData`

- [ ] **Step 5: Run tests — verify pass**

- [ ] **Step 6: Commit**

```
feat(obfuscator): implement API call hiding transform with tests
```

---

## Chunk 3: Source-Level Transforms — UUID Renaming and Orchestration

### Task 6: Implement UuidRenameTransform

**Files:**
- Create: `agent_code/Obfuscator/Source/Transforms/UuidRenameTransform.cs`
- Modify: `agent_code/Tests/Obfuscator.Tests/UuidRenameTests.cs` (add syntax rewriter tests)

- [ ] **Step 1: Write failing tests**

Test that:
- A .cs file containing `namespace Workflow.Contracts { public interface IModule { ... } }` gets namespace and interface renamed
- `using Workflow.Contracts;` directives are updated
- Fully qualified references like `Workflow.Contracts.IModule` are updated
- `Workflow.Models` namespace is also renamed
- Types not in the rename list (e.g., user-defined types) are NOT renamed

- [ ] **Step 2: Run tests — verify fail**

- [ ] **Step 3: Implement UuidRenameTransform**

A `CSharpSyntaxRewriter` that:
1. Takes a `UuidRenameMap` (from Task 2)
2. On `VisitNamespaceDeclaration` / `VisitFileScopedNamespaceDeclaration`: rename `Workflow.Contracts` and `Workflow.Models`
3. On `VisitUsingDirective`: rename matching using statements
4. On `VisitIdentifierName` / `VisitQualifiedName`: if the identifier matches a key in the rename map, replace with the renamed version
5. On `VisitInterfaceDeclaration`, `VisitClassDeclaration`, `VisitRecordDeclaration`, `VisitEnumDeclaration`: rename if in the map
6. On `VisitMethodDeclaration`, `VisitPropertyDeclaration`: rename if the method/property name is in the map and the containing type is a contract type

- [ ] **Step 4: Run tests — verify pass**

- [ ] **Step 5: Commit**

```
feat(obfuscator): implement UUID-derived rename transform with tests
```

### Task 7: Implement SourceRewriter orchestrator

**Files:**
- Create: `agent_code/Obfuscator/Source/SourceRewriter.cs`

- [ ] **Step 1: Implement SourceRewriter**

This class orchestrates the source-level pipeline:
1. Takes an `ObfuscationConfig` with seed, UUID, input dir, output dir
2. If input != output, copies input to output
3. Generates randomized names for StringDecryptor and IndirectCaller (class, method, namespace) using the seed
4. Copies `Runtime/StringDecryptor.cs` and `Runtime/IndirectCaller.cs` templates into the output dir, replacing placeholder tokens with randomized names
5. Walks all `.cs` files in the output directory, **excluding test project directories** (`Tests/`) — test code should not be obfuscated as it would break assertions, string comparisons, and test infrastructure
6. For each file, parses with Roslyn, applies transforms in order:
   a. UuidRenameTransform (only if `--uuid` provided)
   b. StringEncryptionTransform (skip `Workflow.Models` interface definition files)
   c. ApiCallHidingTransform
7. Writes the rewritten syntax tree back to the file
8. Emits source-level entries to the deobfuscation map

- [ ] **Step 2: Wire up to Program.cs `rewrite-source` handler**

Replace the TODO in Program.cs with actual SourceRewriter invocation.

- [ ] **Step 3: Manual smoke test**

Copy a small .cs file (e.g., `agent_code/cat/cat.cs`) to a temp dir. Run:
```
dotnet run --project agent_code/Obfuscator -- rewrite-source --seed 12345 --uuid test-uuid --input <temp> --output <temp>
```
Inspect the output file — strings should be encrypted, the runtime helpers should exist in the output dir.

- [ ] **Step 4: Commit**

```
feat(obfuscator): implement SourceRewriter orchestrator
```

---

## Chunk 4: IL-Level Transforms — Control Flow and Metadata

### Task 8: Implement ControlFlowTransform

**Files:**
- Create: `agent_code/Obfuscator/IL/Transforms/ControlFlowTransform.cs`
- Create: `agent_code/Tests/Obfuscator.Tests/ControlFlowTests.cs`

- [ ] **Step 1: Write failing tests**

Test that:
- A simple compiled method (compile a .cs to DLL in the test, then transform) produces the same output when invoked after flattening
- Methods with < 4 basic blocks are skipped
- Property getters/setters are skipped
- Async `MoveNext()` methods on `IAsyncStateMachine` types are skipped
- Iterator `MoveNext()` methods on `IEnumerator` types are skipped
- Different seeds produce different IL (compare byte arrays)

To test: compile a small C# source to a DLL using Roslyn in-memory compilation within the test, apply the Cecil transform, load via `AssemblyLoadContext`, invoke the method, assert correct result.

- [ ] **Step 2: Run tests — verify fail**

- [ ] **Step 3: Implement ControlFlowTransform**

Uses Mono.Cecil to:
1. Read a DLL with `AssemblyDefinition.ReadAssembly()`
2. For each method body, check selection criteria (>= 4 basic blocks, not a getter/setter, not MoveNext on async/iterator)
3. Build a basic block graph from IL instructions
4. Apply control flow flattening: insert a state variable, wrap blocks in a switch dispatcher, shuffle case order using the seeded Random
5. Insert opaque predicates: `if ((x*x + x) % 2 == 0)` style checks with dead branches
6. Insert bogus code: dead arithmetic, local variable shuffles
7. Write modified assembly back

Start with control flow flattening only. Add opaque predicates and bogus code as separate sub-steps after flattening works.

- [ ] **Step 4: Run tests — verify pass**

- [ ] **Step 5: Commit**

```
feat(obfuscator): implement control flow obfuscation transform with tests
```

### Task 9: Implement MetadataManglingTransform

**Files:**
- Create: `agent_code/Obfuscator/IL/Transforms/MetadataManglingTransform.cs`
- Create: `agent_code/Tests/Obfuscator.Tests/MetadataManglingTests.cs`

- [ ] **Step 1: Write failing tests**

Test that:
- Types, methods, fields, properties, parameters are renamed
- `[DllImport]` extern method names are preserved
- Entry point method name is preserved
- `[JsonPropertyName]` decorated properties are preserved
- Names start with `_` prefix
- No collisions occur (load renamed assembly, invoke methods by new names)
- Different seeds produce different names

- [ ] **Step 2: Run tests — verify fail**

- [ ] **Step 3: Implement MetadataManglingTransform**

Uses Mono.Cecil to:
1. Read a DLL
2. Collect all renameable items (types, methods, fields, properties, params, events, generic params)
3. Filter out preserved items: `[DllImport]` externs, entry point, `[JsonPropertyName]` properties, framework-inherited constructors
4. Generate new names using counter-based scheme with `_` prefix, seeded Random, collision detection with length increment
5. Apply all renames — Cecil resolves cross-references within the assembly
6. Write modified assembly
7. Append rename mappings to deobfuscation map

- [ ] **Step 4: Run tests — verify pass**

- [ ] **Step 5: Commit**

```
feat(obfuscator): implement metadata mangling transform with tests
```

### Task 10: Implement ILRewriter orchestrator and deobfuscation map

**Files:**
- Create: `agent_code/Obfuscator/IL/ILRewriter.cs`
- Create: `agent_code/Obfuscator/DeobfuscationMap.cs`

- [ ] **Step 1: Implement DeobfuscationMap**

A simple class that:
- Accumulates entries from both stages (source rewriter helper names + IL renames)
- Serializes to JSON
- Can append to an existing file (so source rewriter and IL rewriter write to the same map)

- [ ] **Step 2: Implement ILRewriter**

Orchestrates IL-level pipeline:
1. Takes seed, input DLL path, optional map path
2. Reads assembly with Cecil
3. Applies ControlFlowTransform
4. Applies MetadataManglingTransform
5. Writes assembly back to same path (in-place)
6. If map path provided, appends IL-level entries

- [ ] **Step 3: Wire up to Program.cs `rewrite-il` handler**

- [ ] **Step 4: Manual smoke test**

Compile `cat.csproj` to a DLL, then run:
```
dotnet run --project agent_code/Obfuscator -- rewrite-il --seed 12345 --input <path-to-cat.dll> --map map.json
```
Open the DLL in a decompiler (ILSpy/dnSpy) — types should be renamed, control flow should be flattened. Check map.json has entries.

- [ ] **Step 5: Commit**

```
feat(obfuscator): implement ILRewriter orchestrator and deobfuscation map
```

---

## Chunk 5: MSBuild Integration and Build Pipeline

### Task 11: MSBuild target proof-of-concept

**Files:**
- Modify: `agent_code/Directory.Build.targets`

This task validates the MSBuild hook before wiring up the full pipeline.

- [ ] **Step 1: Add ObfuscateIL target to Directory.Build.targets**

```xml
<Project>
  <Import Project="$(AthenaExternalBuildTargets)"
          Condition="'$(AthenaExternalBuildTargets)' != '' And Exists('$(AthenaExternalBuildTargets)')"/>

  <Target Name="ObfuscateIL"
          AfterTargets="ComputeFilesToPublish"
          BeforeTargets="GenerateSingleFileBundle"
          Condition="'$(Obfuscate)' == 'true' AND '$(ObfuscatorPath)' != ''">
    <ItemGroup>
      <AgentAssemblies Include="@(ResolvedFileToPublish)"
                       Condition="!$([System.String]::Copy('%(Filename)').StartsWith('System.'))
                              AND !$([System.String]::Copy('%(Filename)').StartsWith('Microsoft.'))
                              AND !$([System.String]::Copy('%(Filename)').StartsWith('runtime.'))
                              AND '%(Extension)' == '.dll'" />
    </ItemGroup>
    <Message Text="Obfuscating IL: %(AgentAssemblies.Identity)" Importance="high"
             Condition="'@(AgentAssemblies)' != ''" />
    <Exec Command="$(ObfuscatorPath) rewrite-il --seed $(ObfSeed) --input &quot;%(AgentAssemblies.Identity)&quot;"
          Condition="'@(AgentAssemblies)' != ''" />
  </Target>
</Project>
```

- [ ] **Step 2: Test the target fires at the right time**

Run a test publish with the Obfuscate flag but without a real obfuscator path (just to see the Message output):

```bash
cd agent_code
dotnet publish ServiceHost -r win-x64 -c Release --self-contained true /p:PublishSingleFile=true /p:Obfuscate=true /p:ObfuscatorPath=echo /p:ObfSeed=12345 /p:WindowsTest=True
```

Expected: should see "Obfuscating IL: ..." messages for agent DLLs but NOT for System.*/Microsoft.* DLLs. The `echo` command will print each DLL path (acting as a no-op obfuscator).

If the target doesn't fire or fires at the wrong time, adjust `AfterTargets`/`BeforeTargets`. This is the PoC the spec requires.

- [ ] **Step 3: Commit**

```
feat(obfuscator): add MSBuild ObfuscateIL target with framework DLL filtering
```

### Task 12: Remove Obfuscar and update builder.py

**Files:**
- Modify: `agent_code/ServiceHost/ServiceHost.csproj` — remove Obfuscar PackageReference and placeholder comment
- Modify: `Payload_Type/athena/main.py` — remove Obfuscar placeholder injection
- Delete: `Payload_Type/athena/agent.obfs`
- Delete: `Payload_Type/athena/common.obfs`
- Modify: `Payload_Type/athena/athena/mythic/agent_functions/builder.py` — add obfuscator invocation
- Remove `<!-- Obfuscation Replacement Placeholder Do Not Remove -->` from all other .csproj files that have it

- [ ] **Step 1: Remove Obfuscar from ServiceHost.csproj**

Remove the `<PackageReference Include="Obfuscar" ...>` block (including `<PrivateAssets>` and `<IncludeAssets>` child elements) and the `<!-- Obfuscation Replacement Placeholder Do Not Remove -->` comment.

- [ ] **Step 2: Remove all Obfuscar traces from .csproj files**

Search for and remove:
- `<!-- Obfuscation Replacement Placeholder Do Not Remove -->` comments
- Any `<Target>` blocks referencing Obfuscar or `build_utils.py`
- Any `<PackageReference Include="Obfuscar" ...>` blocks

Run: `grep -rl "Obfuscar\|Obfuscation Replacement Placeholder\|build_utils" agent_code/ --include="*.csproj"` to find all affected files.

- [ ] **Step 3: Remove Obfuscar template files and wrapper**

Delete:
- `Payload_Type/athena/agent.obfs`
- `Payload_Type/athena/common.obfs`
- `agent_code/build_utils.py` (Obfuscar wrapper)

- [ ] **Step 4: Clean up main.py**

Remove the functions: `prepare_agent_obfuscation`, `find_csproj_files`, `read_replacement_text`, `replace_placeholder_in_file`, `process_csproj_files`, the `directory` and `placeholder` variables, and the call to `process_csproj_files(directory, placeholder)`.

Keep the `mythic_container.mythic_service.start_and_run_forever()` call.

Add a build step for the obfuscator tool:
```python
import subprocess

# Build the obfuscator tool once on container start
obfuscator_dir = "/Mythic/athena/agent_code/Obfuscator"
subprocess.run(["dotnet", "build", "-c", "Release", obfuscator_dir], check=True)

mythic_container.mythic_service.start_and_run_forever()
```

- [ ] **Step 5: Update builder.py — add source rewrite step**

In the `build()` method, after the "Add Tasks" step and before the dotnet restore:

1. Generate a random seed: `obf_seed = random.randint(0, 2**32 - 1)`
2. If obfuscation is enabled:
   a. Copy agent_code to a temp directory
   b. Run the obfuscator source rewriter:
   ```python
   obfuscator_bin = os.path.join("/Mythic/athena/agent_code/Obfuscator/bin/Release/net10.0/obfuscator")
   rewrite_cmd = f"{obfuscator_bin} rewrite-source --seed {obf_seed} --uuid {self.uuid} --input {temp_source_dir} --output {temp_source_dir}"
   ```
   c. Use `temp_source_dir` as `source_dir` for all subsequent build commands
   d. Pass `/p:ObfSeed={obf_seed}` and `/p:ObfuscatorPath={obfuscator_bin}` to the dotnet publish command
   e. Pass `--map {gen_dir}/{obf_seed}-map.json` to both source rewriter and IL rewriter invocations for deobfuscation map generation
   f. Store the map file alongside the build artifacts (server-side only)
3. Log the seed in the build step output
4. Update the `obfuscate` build parameter description from `"Obfuscate the final payload with Obfuscar"` to `"Obfuscate the final payload"`

Update `getBuildCommand()` to accept and pass `ObfSeed` and `ObfuscatorPath` properties.

- [ ] **Step 6: Add AOT + obfuscation mutual exclusion check**

In `build()`, after the Windows service check, add:
```python
if self.get_parameter("obfuscate") and aot_enabled:
    return await self.returnFailure(resp, "Obfuscation and AOT are mutually exclusive", ...)
```

- [ ] **Step 7: Commit**

```
feat(obfuscator): remove Obfuscar, integrate custom obfuscator into build pipeline
```

### Task 13: Update load.py for per-load obfuscation

**Files:**
- Modify: `Payload_Type/athena/athena/mythic/agent_functions/load.py`

- [ ] **Step 1: Update compile_command to support obfuscation**

Replace the current `compile_command` method. Note: `taskData.Payload.UUID` in `create_go_tasking` is already the payload UUID (not the callback UUID), so no Mythic RPC lookup is needed. Use `asyncio.create_subprocess_exec` for consistency with builder.py's async pattern. Copy source excluding `bin/obj` to avoid stale artifacts:

```python
async def compile_command(self, plugin_folder_path, uuid):
    obf_seed = random.randint(0, 2**32 - 1)
    obfuscator_bin = "/Mythic/athena/agent_code/Obfuscator/bin/Release/net10.0/obfuscator"
    temp_dir = tempfile.mkdtemp()
    try:
        shutil.copytree(plugin_folder_path, os.path.join(temp_dir, "plugin"),
                        ignore=shutil.ignore_patterns("bin", "obj"))
        models_path = os.path.join(self.agent_code_path, "Workflow.Models")
        shutil.copytree(models_path, os.path.join(temp_dir, "Workflow.Models"),
                        ignore=shutil.ignore_patterns("bin", "obj"))
        plugin_temp = os.path.join(temp_dir, "plugin")

        # Source rewrite
        rewrite_proc = await asyncio.create_subprocess_exec(
            obfuscator_bin, "rewrite-source",
            "--seed", str(obf_seed), "--uuid", uuid,
            "--input", temp_dir, "--output", temp_dir,
            stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE)
        _, r_stderr = await rewrite_proc.communicate()
        if rewrite_proc.returncode != 0:
            raise Exception("Source rewrite failed: " + r_stderr.decode())

        # Publish with IL rewriting
        publish_proc = await asyncio.create_subprocess_exec(
            "dotnet", "publish", "-c", "Release",
            "/p:PayloadUUID=" + uuid,
            "/p:Obfuscate=true",
            "/p:ObfSeed=" + str(obf_seed),
            "/p:ObfuscatorPath=" + obfuscator_bin,
            "/p:PublishTrimmed=false",
            cwd=plugin_temp,
            stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE)
        _, p_stderr = await publish_proc.communicate()
        if publish_proc.returncode != 0:
            raise Exception("Plugin publish failed: " + p_stderr.decode())
    finally:
        shutil.rmtree(temp_dir, ignore_errors=True)
```

Add `import asyncio, tempfile, shutil, random` to the imports at the top of load.py.

- [ ] **Step 2: Update DLL path resolution**

Change `plugin_dll_platform_specific` and `plugin_dll_generic` to use `bin/Release/net10.0/publish/` instead of `bin/Release/net10.0/`.

- [ ] **Step 3: Commit**

```
feat(obfuscator): update load.py for per-load obfuscation with fresh seed
```

---

## Chunk 6: Integration Testing and Full Verification

### Task 14: Write integration tests

**Files:**
- Create: `agent_code/Tests/Obfuscator.Tests/IntegrationTests.cs`

- [ ] **Step 1: Write end-to-end test — obfuscate and load a plugin**

Test that:
1. Takes a simple test plugin source (create a minimal IModule implementation in the test)
2. Runs SourceRewriter on it
3. Compiles with `dotnet build`
4. Runs ILRewriter on the output DLL
5. Loads the DLL via `AssemblyLoadContext.LoadFromStream()`
6. Finds the type implementing the (UUID-renamed) IModule interface
7. Invokes Execute and verifies correct behavior

- [ ] **Step 2: Write polymorphism test**

Test that running the full pipeline twice with different seeds on the same source produces different DLL bytes.

- [ ] **Step 3: Write UUID isolation test**

Test that two different UUIDs produce DLLs with different interface names that cannot cross-load.

- [ ] **Step 4: Write same-UUID different-seed test**

Test that two plugins built for the same agent (same UUID) with different seeds both load correctly — interface names match (same UUID) but everything else differs.

- [ ] **Step 5: Run all tests**

Run: `dotnet test agent_code/Tests/Obfuscator.Tests/ -v n`
Expected: all PASS

- [ ] **Step 6: Commit**

```
test(obfuscator): add integration tests for end-to-end obfuscation pipeline
```

### Task 15: Run existing test suite with obfuscation

**Files:**
- No new files — verification only

- [ ] **Step 1: Run existing tests without obfuscation (baseline)**

Run: `dotnet test agent_code/Tests/Workflow.Tests/ -v n`
Expected: all current tests PASS

- [ ] **Step 2: Run existing tests with obfuscation enabled**

This requires building the test project with source rewriting applied. Run the source rewriter on a copy of agent_code, then run tests from the copy:

```bash
# Copy source (excluding build artifacts to avoid stale state)
temp=$(mktemp -d)
rsync -a --exclude='bin' --exclude='obj' agent_code/ "$temp/"

# Source rewrite (Tests/ directory is excluded by SourceRewriter, see Task 7)
dotnet run --project agent_code/Obfuscator -- rewrite-source --seed 99999 --uuid test-uuid-for-tests --input "$temp" --output "$temp"

# Restore and run tests from rewritten source
dotnet restore "$temp/Tests/Workflow.Tests/"
dotnet test "$temp/Tests/Workflow.Tests/" -v n
```

Expected: all tests PASS. If any fail, debug and fix the transform that breaks them. The test projects themselves are not obfuscated (excluded in Task 7), but they compile against obfuscated agent code and `Workflow.Models`.

- [ ] **Step 3: Fix any failures**

If tests fail, investigate which transform caused the failure. Common issues:
- String encryption replacing strings used in assertions
- UUID renaming breaking test helper classes
- API call hiding replacing calls in test code

Solutions: add exclusion patterns for test projects, or adjust transforms to handle edge cases.

- [ ] **Step 4: Commit any fixes**

```
fix(obfuscator): resolve test failures with obfuscation enabled
```

### Task 16: Final cleanup and documentation

**Files:**
- Modify: `docs/superpowers/specs/2026-03-15-custom-obfuscator-design.md` — update status to Implemented

- [ ] **Step 1: Verify all tests pass**

Run both test suites:
```bash
dotnet test agent_code/Tests/Obfuscator.Tests/ -v n
dotnet test agent_code/Tests/Workflow.Tests/ -v n
```

- [ ] **Step 2: Update spec status**

Change `**Status:** Draft` to `**Status:** Implemented`

- [ ] **Step 3: Final commit**

```
docs: mark custom obfuscator spec as implemented
```
