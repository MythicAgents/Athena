# Reflection & Dynamic Loading Cleanup

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the brittle 6-parameter plugin constructor convention with a single `PluginContext` object, and fix all reflection-related bugs and code smells.

**Architecture:** Introduce a `PluginContext` record in `Workflow.Models` that bundles the 6 service interfaces. All `Activator.CreateInstance` call sites pass one arg instead of six. All 68 plugin constructors change from `(IDataBroker, IServiceConfig, ILogger, ICredentialProvider, IRuntimeExecutor, IScriptEngine)` to `(PluginContext)`. Targeted fixes for `FindMethodInNamespace`, bare catches, hardcoded version strings, and hardcoded profile list.

**Tech Stack:** C# / .NET 10, Autofac, MSTest

---

## File Structure

| Action | File | Responsibility |
|--------|------|---------------|
| Create | `Workflow.Models/PluginContext.cs` | Service container record |
| Create | `Workflow.Models/AssemblyNames.cs` | Assembly name format helper |
| Modify | `Workflow.Providers.Runtime/AssemblyManager.cs` | Use `PluginContext` in `Activator.CreateInstance` |
| Modify | `ServiceHost/Config/ContainerBuilder.cs` | Convention-based profile scanning, register `PluginContext` |
| Modify | `execute-module/execute-module.cs` | Fix `FindMethodInNamespace`, remove dead code |
| Modify | `inject-shellcode/inject-shellcode.cs` | Replace bare catch |
| Modify | `Tests/Workflow.Tests/PluginLoader.cs` | Use `PluginContext` |
| Modify | All 68 plugin files | Constructor signature change |

---

## Chunk 1: Core Infrastructure

### Task 1: Create `PluginContext` record

**Files:**
- Create: `Workflow.Models/PluginContext.cs`

- [ ] **Step 1: Create the PluginContext record**

```csharp
using Workflow.Contracts;
using Workflow.Models;

namespace Workflow.Contracts
{
    public record PluginContext(
        IDataBroker MessageManager,
        IServiceConfig Config,
        ILogger Logger,
        ICredentialProvider TokenManager,
        IRuntimeExecutor Spawner,
        IScriptEngine ScriptEngine
    );
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build Payload_Type/athena/athena/agent_code/Workflow.Models/Workflow.Models.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/Workflow.Models/PluginContext.cs
git commit -m "feat: add PluginContext record to bundle plugin dependencies"
```

---

### Task 2: Create `AssemblyNames` helper

**Files:**
- Create: `Workflow.Models/AssemblyNames.cs`

- [ ] **Step 1: Create the helper**

```csharp
namespace Workflow.Contracts
{
    public static class AssemblyNames
    {
        private const string VersionSuffix =
            ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

        public static string ForModule(string name) => $"{name}{VersionSuffix}";

        public static string ForChannel(string name) =>
            $"Workflow.Channels.{name}{VersionSuffix}";
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build Payload_Type/athena/athena/agent_code/Workflow.Models/Workflow.Models.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/Workflow.Models/AssemblyNames.cs
git commit -m "feat: add AssemblyNames helper to centralize version string"
```

---

### Task 3: Update `ComponentProvider` to use `PluginContext`

**Files:**
- Modify: `Workflow.Providers.Runtime/AssemblyManager.cs`

- [ ] **Step 1: Replace the 6 private fields with a single `PluginContext`**

Change the class to store a `PluginContext` field instead of the 6 individual service fields. Update the constructor to accept `PluginContext` and store it. Update `ParseAssemblyForModule` to pass the single context to `Activator.CreateInstance`.

Before (lines 14-27):
```csharp
private ILogger logger { get; set; }
private IDataBroker messageManager { get; set; }
private IServiceConfig agentConfig { get; set; }
private ICredentialProvider tokenManager { get; set; }
private IRuntimeExecutor spawner { get; set; }
private IScriptEngine pythonManager { get; set; }
public ComponentProvider(IDataBroker messageManager, ILogger logger, ...) {
    this.logger = logger;
    // ...6 assignments
}
```

After:
```csharp
private readonly PluginContext context;
public ComponentProvider(PluginContext context) {
    this.context = context;
}
```

- [ ] **Step 2: Update `ParseAssemblyForModule` (line 129)**

Before:
```csharp
IModule plug = (IModule)Activator.CreateInstance(t, messageManager, agentConfig, logger, tokenManager, spawner, pythonManager);
```

After:
```csharp
IModule plug = (IModule)Activator.CreateInstance(t, context);
```

- [ ] **Step 3: Update `TryLoadModule` to use `AssemblyNames` helper (line 35)**

Before:
```csharp
Assembly _tasksAsm = Assembly.Load($"{name}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
```

After:
```csharp
Assembly _tasksAsm = Assembly.Load(AssemblyNames.ForModule(name));
```

- [ ] **Step 4: Update debug log references from `messageManager` to `context.MessageManager`**

Any remaining direct references to the old fields (e.g., in `LoadAssemblyAsync`, `LoadModuleAsync`) should now go through `context.MessageManager`, `context.Config`, etc.

- [ ] **Step 5: Verify it compiles**

Run: `dotnet build Payload_Type/athena/athena/agent_code/Workflow.Providers.Runtime/Workflow.Providers.Runtime.csproj`
Expected: Build succeeded (plugin projects will fail until updated — that's expected)

- [ ] **Step 6: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/Workflow.Providers.Runtime/AssemblyManager.cs
git commit -m "refactor: ComponentProvider uses PluginContext instead of 6 params"
```

---

### Task 4: Register `PluginContext` in Autofac and fix profile loading

**Files:**
- Modify: `ServiceHost/Config/ContainerBuilder.cs`

- [ ] **Step 1: Register PluginContext in the container**

Add after the individual service registrations (before `TryLoadProfiles`):

```csharp
containerBuilder.Register(c => new PluginContext(
    c.Resolve<IDataBroker>(),
    c.Resolve<IServiceConfig>(),
    c.Resolve<ILogger>(),
    c.Resolve<ICredentialProvider>(),
    c.Resolve<IRuntimeExecutor>(),
    c.Resolve<IScriptEngine>()
)).SingleInstance();
```

- [ ] **Step 2: Replace hardcoded profile list with convention-based scanning**

Replace `TryLoadProfiles` method body. Instead of a hardcoded list, scan all loaded assemblies whose names start with `Workflow.Channels.`:

```csharp
private static void TryLoadProfiles(Autofac.ContainerBuilder containerBuilder)
{
    string[] profileNames = { "DebugProfile", "Http", "Websocket",
                              "Slack", "Discord", "Smb", "GitHub" };

    foreach (var profile in profileNames)
    {
        try
        {
            DebugLog.Log($"TryLoadProfiles: loading {profile}");
            Assembly asm = Assembly.Load(
                AssemblyNames.ForChannel(profile));
            containerBuilder.RegisterAssemblyTypes(asm)
                .As<IChannel>().SingleInstance();
            DebugLog.Log($"TryLoadProfiles: loaded {profile}");
        }
        catch (FileNotFoundException)
        {
            DebugLog.Log($"TryLoadProfiles: {profile} not found");
        }
        catch (Exception ex)
        {
            DebugLog.Log(
                $"TryLoadProfiles: failed to load {profile}: {ex.Message}");
        }
    }
}
```

Key changes:
- Use `AssemblyNames.ForChannel()` instead of inline format string
- Catch `FileNotFoundException` separately (expected when a profile isn't compiled in) from unexpected errors
- No more bare `catch`

Note: We keep the explicit list rather than scanning because the build conditionally includes profiles via `csproj` — only referenced assemblies are present. The list acts as a manifest of known profiles. The improvement is the centralized naming and proper exception handling.

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build Payload_Type/athena/athena/agent_code/ServiceHost/ServiceHost.csproj -c LocalDebugHttp`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/ServiceHost/Config/ContainerBuilder.cs
git commit -m "refactor: register PluginContext, improve profile loading error handling"
```

---

## Chunk 2: Fix execute-module and inject-shellcode

### Task 5: Fix `FindMethodInNamespace` in execute-module

**Files:**
- Modify: `execute-module/execute-module.cs:310-333`

- [ ] **Step 1: Replace the broken method**

The current implementation has two bugs:
1. `method.Name.Contains(methodName)` — substring match can return wrong method
2. Dead code at line 324 that's unreachable after the early return
3. Commented-out block at lines 327-330

Replace `FindMethodInNamespace` (lines 310-333) with:

```csharp
private static MethodInfo? FindMethodInNamespace(
    Assembly assembly, string methodName)
{
    foreach (Type type in assembly.GetTypes())
    {
        MethodInfo? method = type.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static);

        if (method is not null)
        {
            return method;
        }
    }
    return null;
}
```

Changes:
- Exact name match via `GetMethod(name, flags)` instead of `Contains()`
- Single search path, no dead code
- Returns first exact match across all types

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build Payload_Type/athena/athena/agent_code/execute-module/execute-module.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/execute-module/execute-module.cs
git commit -m "fix: FindMethodInNamespace uses exact match instead of substring"
```

---

### Task 6: Fix bare catch in inject-shellcode

**Files:**
- Modify: `inject-shellcode/inject-shellcode.cs:76-97`

- [ ] **Step 1: Replace bare catch with specific handling**

Before (lines 85-95):
```csharp
try
{
    var instance = (ITechnique)Activator.CreateInstance(t);
    if (instance != null){
        techniques.Add(instance);
    }
}
catch
{
    continue;
}
```

After:
```csharp
try
{
    var instance = (ITechnique)Activator.CreateInstance(t);
    if (instance is not null)
    {
        techniques.Add(instance);
    }
}
catch (MissingMethodException)
{
    // Type implements ITechnique but has no parameterless
    // constructor (e.g. abstract base class)
    continue;
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build Payload_Type/athena/athena/agent_code/inject-shellcode/inject-shellcode.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/inject-shellcode/inject-shellcode.cs
git commit -m "fix: replace bare catch with MissingMethodException in GetTechniques"
```

---

## Chunk 3: Update all 68 plugins

### Task 7: Update plugin constructors to accept `PluginContext`

**Files:**
- Modify: All 68 plugin files (see list below)

Every plugin currently has this constructor pattern:

```csharp
public Plugin(IDataBroker messageManager, IServiceConfig config,
    ILogger logger, ICredentialProvider tokenManager,
    IRuntimeExecutor spawner, IScriptEngine pythonManager)
{
    this.messageManager = messageManager;
    // ... only assigns what it needs
}
```

Change each to:

```csharp
public Plugin(PluginContext context)
{
    this.messageManager = context.MessageManager;
    // ... only assigns what it needs
}
```

- [ ] **Step 1: Update simple plugins (only use messageManager)**

These plugins only store `messageManager`. Update constructor to `(PluginContext context)` and change the assignment to `context.MessageManager`:

```
cd, cp, config, drives, echo, entitlements, env, exec, exit,
get-clipboard, get-localgroup, get-sessions, get-shares, hostname,
ifconfig, jobs, jobkill, jxa, kill, keychain, lnk, ls, mkdir, mv,
netstat, nslookup, port-bender, ps, pwd, reg, rm, shell, tail,
test-port, timestomp, token, uptime, wget, whoami, zip, zip-dl,
zip-inspect, nidhogg, python-load
```

- [ ] **Step 2: Update plugins that use messageManager + config**

These plugins store both `messageManager` and `config`. Same pattern but add `this.config = context.Config`:

```
arp, cat, download, execute-assembly, execute-module, farmer,
http-server, python-exec, screenshot, upload
```

- [ ] **Step 3: Update plugins that use messageManager + logger**

```
socks
```

- [ ] **Step 4: Update plugins that use messageManager + config + logger + spawner**

```
caffeinate, inject-shellcode, inject-shellcode-linux
```

- [ ] **Step 5: Update plugins that use messageManager + config + logger**

```
cursed, ds, keylogger, sftp, ssh
```

- [ ] **Step 6: Update plugins that use messageManager + config + spawner**

```
coff, shellcode
```

- [ ] **Step 7: Update forwarder/proxy plugins (smb, rportfwd)**

```
smb, rportfwd
```

- [ ] **Step 8: Verify full solution compiles**

Run: `dotnet build Payload_Type/athena/athena/agent_code/ServiceHost/ServiceHost.csproj -c LocalDebugHttp`
Expected: Build succeeded

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "refactor: all plugins use PluginContext constructor"
```

---

## Chunk 4: Update test infrastructure

### Task 8: Update PluginLoader for tests

**Files:**
- Modify: `Tests/Workflow.Tests/PluginLoader.cs`

- [ ] **Step 1: Refactor PluginLoader to use PluginContext**

Before:
```csharp
public class PluginLoader
{
    public IDataBroker messageManager { get; set; } = new TestDataBroker();
    public IServiceConfig agentConfig { get; set; } = new TestServiceConfig();
    public ILogger logger { get; set; } = new TestLogger();
    public ICredentialProvider tokenManager { get; set; } = new TestCredentialProvider();
    public IRuntimeExecutor spawner { get; set; } = new TestSpawner();
    public IScriptEngine pyManager { get; set; } = new ScriptEngine();

    private IModule? GetPlugin(string moduleName)
    {
        Assembly _tasksAsm = Assembly.Load($"{moduleName}, Version=1.0.0.0, ...");
        foreach (Type t in _tasksAsm.GetTypes())
        {
            if (typeof(IModule).IsAssignableFrom(t))
            {
                IModule plug = (IModule)Activator.CreateInstance(t,
                    messageManager, agentConfig, logger, tokenManager,
                    spawner, pyManager);
                return plug;
            }
        }
        return null;
    }

    public PluginLoader(IDataBroker messageManager)
    {
        this.messageManager = messageManager;
    }
}
```

After:
```csharp
public class PluginLoader
{
    private readonly PluginContext context;

    public PluginLoader(IDataBroker messageManager)
    {
        context = new PluginContext(
            messageManager,
            new TestServiceConfig(),
            new TestLogger(),
            new TestCredentialProvider(),
            new TestSpawner(),
            new ScriptEngine()
        );
    }

    public IModule? LoadPluginFromDisk(string moduleName)
    {
        return GetPlugin(moduleName);
    }

    private IModule? GetPlugin(string moduleName)
    {
        try
        {
            Assembly asm = Assembly.Load(
                AssemblyNames.ForModule(moduleName));

            foreach (Type t in asm.GetTypes())
            {
                if (typeof(IModule).IsAssignableFrom(t))
                {
                    return (IModule)Activator.CreateInstance(t, context);
                }
            }
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 2: Remove the unused `using static IronPython.Modules._ast;` import (line 12)**

- [ ] **Step 3: Run existing tests**

Run: `dotnet test Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/Workflow.Tests.csproj`
Expected: All existing tests pass

- [ ] **Step 4: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/Tests/
git commit -m "refactor: PluginLoader uses PluginContext, fix imports"
```

---

## Chunk 5: Final verification

### Task 9: End-to-end verification

- [ ] **Step 1: Build all configurations**

```bash
dotnet build Payload_Type/athena/athena/agent_code/ServiceHost/ServiceHost.csproj -c LocalDebugHttp
dotnet build Payload_Type/athena/athena/agent_code/ServiceHost/ServiceHost.csproj -c LocalDebugWebsocket
```

- [ ] **Step 2: Run all tests**

```bash
dotnet test Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/Workflow.Tests.csproj
```

- [ ] **Step 3: Verify no remaining hardcoded version strings**

Search for the old pattern — should only appear in `AssemblyNames.cs`:

```bash
rg "Version=1.0.0.0, Culture=neutral" --type cs Payload_Type/athena/athena/agent_code/
```

Expected: Only `Workflow.Models/AssemblyNames.cs` matches.

- [ ] **Step 4: Verify no remaining bare catches**

```bash
rg "catch\s*$" --type cs Payload_Type/athena/athena/agent_code/ -n
```

Expected: No matches (all catches should specify an exception type).

- [ ] **Step 5: Verify no remaining 6-param constructors**

```bash
rg "IDataBroker.*IServiceConfig.*ILogger.*ICredentialProvider.*IRuntimeExecutor.*IScriptEngine" --type cs Payload_Type/athena/athena/agent_code/
```

Expected: No matches.

- [ ] **Step 6: Final commit if any fixups needed**

```bash
git add -A
git commit -m "chore: final verification and cleanup"
```
