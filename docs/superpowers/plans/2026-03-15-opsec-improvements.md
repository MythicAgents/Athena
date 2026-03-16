# Opsec Improvements Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden the Athena agent's operational security by sanitizing error output, protecting key material in memory, defaulting to stripped builds, and implementing encrypted sleep state.

**Architecture:** Five independent workstreams that can be parallelized. Task 1 is a bulk mechanical change across 65 plugin files. Tasks 2-3 are small targeted changes. Task 4 (collectible ALC) is exploratory with a "maybe" qualifier. Task 5 (sleep encryption) adds a new `ISleepHandler` abstraction that profiles call instead of raw `Task.Delay()`, encrypting sensitive agent state during the sleep window.

**Tech Stack:** .NET 10, C#, Autofac DI, AES-256-CBC, System.Security.Cryptography

**NOTE:** This plan is based on the working tree state as of 2026-03-15. Recent uncommitted changes include: bug fixes across multiple plugins (kill, nslookup, tail, sftp, reg, screenshot, token, ssh), core agent improvements (ServiceHost async/await fixes, Worker.cs scope fix, DebugProfile Thread.Sleep→Task.Delay, Discord `&`→`&&` bug fix, SMB async void→async Task), Crypto.cs improvements (Encrypt now uses PSK field directly, HMACSHA256 using blocks, FixedTimeEquals for HMAC), Misc.cs cleanup (removed dead code, fixed GetSleep to use static Random), test improvements (new checkin/crypto tests, removed unused test classes). None of these changes conflict with the tasks below.

---

## Chunk 1: Error Sanitization and Build Defaults (Tasks 1-3)

### Task 1: Sanitize Exception Stack Traces in Task Responses

Currently 65 plugin/core files send `e.ToString()` back to the operator via `messageManager.Write()`. This leaks full .NET stack traces (assembly names, namespaces, file paths, line numbers) into C2 traffic. Replace with `e.Message` only.

**Files (65 files — full list below, all under `Payload_Type/athena/athena/agent_code/`):**

Core files:
- Modify: `ServiceHost/Managers/TaskManager.cs` — catch block sends `e.ToString()` as `user_output`
- Modify: `Workflow.Providers.Runtime/AssemblyManager.cs:68,107` — sends `e.ToString()` in `LoadAssemblyAsync` and `LoadModuleAsync`

Plugin files (each has 1-3 catch blocks with `e.ToString()`):
- Modify: `cat/cat.cs:59`
- Modify: `caffeinate/caffeinate.cs:67`
- Modify: `bits-transfer/bits-transfer.cs:52`
- Modify: `cd/cd.cs:41`
- Modify: `config/config.cs:102`
- Modify: `cp/cp.cs:63`
- Modify: `arp/arp.cs:44,93`
- Modify: `clipboard-monitor/clipboard-monitor.cs:80`
- Modify: `coff/coff.cs:55`
- Modify: `credentials/credentials.cs:49`
- Modify: `crop/crop.cs:93,140`
- Modify: `crop/Config.cs:52,57`
- Modify: `cursed/Utilities/DebugHelper.cs:32,69,82,97,138,166,197,216`
- Modify: `dns/dns.cs:54`
- Modify: `download/download.cs:97`
- Modify: `drives/drives.cs:58`
- Modify: `ds/ds.cs:83,179`
- Modify: `event-log/event-log.cs:54`
- Modify: `execute-assembly/ConsoleApplicationExecutor.cs:51`
- Modify: `execute-module/execute-module.cs:287`
- Modify: `file-utils/file-utils.cs:61`
- Modify: `find/find.cs:67`
- Modify: `get-clipboard/getclipboard.cs:194`
- Modify: `get-sessions/get-sessions.cs:207,216`
- Modify: `get-shares/get-shares.cs:115,124`
- Modify: `hash/hash.cs:54`
- Modify: `http-request/http-request.cs:119`
- Modify: `http-server/http-server.cs:79`
- Modify: `inject-shellcode/inject-shellcode.cs:70`
- Modify: `inject-shellcode/Techniques/TestInjector.cs:67`
- Modify: `inject-shellcode-macos/inject-shellcode-macos.cs`
- Modify: `jxa/jxa.cs`
- Modify: `jxa/Mono/AppleScript.cs`
- Modify: `kerberos/kerberos.cs:54`
- Modify: `keylogger/keylogger.cs`
- Modify: `ls/LocalListing.cs:43`
- Modify: `ls/RemoteListing.cs:39`
- Modify: `mkdir/mkdir.cs:52`
- Modify: `mv/mv.cs`
- Modify: `ping/ping.cs`
- Modify: `proc-enum/proc-enum.cs`
- Modify: `ps/ps.cs`
- Modify: `reg/reg.cs`
- Modify: `rm/rm.cs`
- Modify: `screenshot/screenshot.cs`
- Modify: `sftp/sftp.cs`
- Modify: `shellcode/shellcode.cs`
- Modify: `smb/SmbLink.cs`
- Modify: `ssh/ssh.cs`
- Modify: `ssh-recon/ssh-recon.cs`
- Modify: `sysinfo/sysinfo.cs`
- Modify: `tail/tail.cs`
- Modify: `test-port/test-port.cs`
- Modify: `timestomp/timestomp.cs`
- Modify: `upload/upload.cs`
- Modify: `wget/wget.cs`
- Modify: `wmi/wmi.cs`
- Modify: `zip-dl/zip-dl.cs`
- Modify: `zip-inspect/zip-inspect.cs`
- Modify: `farmer/Farmer.cs:48,54,117,123`
- Modify: `Workflow.Providers.Script/PythonManager.cs`
- Modify: `Workflow.Providers.Windows/Resolver.cs`

**Pattern to apply everywhere:**

Before (typical):
```csharp
catch (Exception e)
{
    messageManager.Write(e.ToString(), job.task.id, true, "error");
}
```

After:
```csharp
catch (Exception e)
{
    DebugLog.Log($"{Name} error [{job.task.id}]: {e}");
    messageManager.Write(e.Message, job.task.id, true, "error");
}
```

Special cases:
- `farmer/Farmer.cs:48` uses `e.ToString().Contains("WSACancelBlockingCall")` — change to `e.Message.Contains(...)`
- `cursed/Utilities/DebugHelper.cs` uses `ReturnOutput(e.ToString(), task_id)` — change to `ReturnOutput(e.Message, task_id)`
- `arp/arp.cs:93` uses `sb.AppendLine(e.ToString())` in an inner loop — change to `sb.AppendLine(e.Message)`
- `get-shares/get-shares.cs:115` uses `sb.AppendLine(e.ToString())` — same treatment
- `crop/Config.cs:52,57` uses `Plugin.messageManager.Write(e.ToString(), ...)` — same pattern
- `TaskManager.cs` sets `user_output = e.ToString()` in a `new TaskResponse` — change to `e.Message`
- `AssemblyManager.cs:68,107` sets `user_output = e.ToString()` in a `new TaskResponse` / `new LoadTaskResponse` — change to `e.Message`

- [ ] **Step 1: Create a script or use search-replace to change all `e.ToString()` patterns**

For each of the 65 files listed above, replace:
- `e.ToString()` with `e.Message` when the value flows to `messageManager.Write`, `messageManager.WriteLine`, `ReturnOutput`, `user_output =`, or `sb.AppendLine`
- Ensure a `DebugLog.Log` call exists nearby with the full exception (most already have one; add where missing)

Do NOT change:
- `entry.EntryType.ToString()` in `event-log.cs:74` (not an exception)
- `title.ToString()` in `keylogger.cs:27` (not an exception)
- `windowHandle.ToString()` in `inject-shellcode/Techniques/TestInjector.cs:67` (not an exception — but verify context)
- Any `.ToString()` that isn't on an Exception variable

- [ ] **Step 2: Build to verify no compilation errors**

Run from `Payload_Type/athena/athena/agent_code/`:
```bash
dotnet build ServiceHost -c LocalDebugHttp --nologo -v q
```
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Run existing tests**

```bash
dotnet test Tests/Workflow.Tests --nologo -v q
```
Expected: All existing tests pass (this is a message-text-only change; no behavior change).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "security: sanitize exception stack traces in task responses

Replace e.ToString() with e.Message in all 65 plugin/core files
to prevent leaking internal .NET stack traces over C2 traffic.
Full exceptions still logged via DebugLog for dev builds."
```

---

### Task 2: Zero PSK/Key Material on Config Update and Add Secure Clearing

**Files:**
- Modify: `Workflow.Security.Aes/Crypto.cs:31-51`
- Modify: `Workflow.Models/Interfaces/IAgentConfig.cs` (add `IDisposable` if desired, or leave as-is)

The AES `SecurityProvider` holds `private byte[] PSK` and `private byte[] uuid` as instance fields. When the config updates (UUID change on checkin), the old byte arrays are replaced but never zeroed. The old values persist on the managed heap until GC collects and the OS overwrites.

- [ ] **Step 1: Add a helper to zero byte arrays in SecurityProvider**

Modify `Workflow.Security.Aes/Crypto.cs`:

```csharp
// At top of class, add helper:
private static void ZeroArray(byte[]? array)
{
    if (array is not null)
    {
        CryptographicOperations.ZeroMemory(array);
    }
}
```

- [ ] **Step 2: Zero old key material before replacing**

In the constructor (`Crypto.cs:37-45`), after the existing code, no change needed (first assignment).

In `OnServiceConfigUpdated` (`Crypto.cs:47-52`), zero before replacing.

**Note:** Recent changes fixed `Encrypt()` to use the `PSK` field directly instead of re-decoding `config.psk` each call. This makes zeroing the PSK during sleep (Task 5) even more critical, since `PSK` is now the single source of truth for the key material.

Before:
```csharp
private void OnServiceConfigUpdated(object? sender, EventArgs e)
{
    this.uuid = ASCIIEncoding.ASCII.GetBytes(config.uuid);
    PSK = Convert.FromBase64String(config.psk);
    DebugLog.Log("AES security provider config updated");
}
```

After:
```csharp
private void OnServiceConfigUpdated(object? sender, EventArgs e)
{
    ZeroArray(this.uuid);
    ZeroArray(this.PSK);
    this.uuid = ASCIIEncoding.ASCII.GetBytes(config.uuid);
    PSK = Convert.FromBase64String(config.psk);
    DebugLog.Log("AES security provider config updated");
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build ServiceHost -c LocalDebugHttp --nologo -v q
```
Expected: Build succeeded.

- [ ] **Step 4: Run existing crypto tests**

```bash
dotnet test Tests/Workflow.Tests --filter "FullyQualifiedName~AesCrypto" --nologo -v q
```
Expected: All AES tests pass.

- [ ] **Step 5: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/Workflow.Security.Aes/Crypto.cs
git commit -m "security: zero PSK and UUID byte arrays before replacement

Use CryptographicOperations.ZeroMemory to clear old key material
when config updates (e.g., UUID change on checkin). Prevents stale
keys from lingering on the managed heap."
```

---

### Task 3: Default Build Flags for Opsec

**Files:**
- Modify: `Payload_Type/athena/athena/mythic/agent_functions/builder.py:100-110`

The builder exposes `usesystemresourcekeys` (default: `False`) and `stacktracesupport` (default: `True`). For production payloads, these should default to stripped exception messages and no stack traces. Operators can still opt-in to verbose mode for debugging.

- [ ] **Step 1: Change defaults in builder.py**

At line 100-110, change:

Before:
```python
BuildParameter(
    name="usesystemresourcekeys",
    parameter_type=BuildParameterType.Boolean,
    default_value= False,
    description="Strip Exception Messages"
),
BuildParameter(
    name="stacktracesupport",
    parameter_type=BuildParameterType.Boolean,
    default_value= True,
    description="Enable Stack Trace message"
),
```

After:
```python
BuildParameter(
    name="usesystemresourcekeys",
    parameter_type=BuildParameterType.Boolean,
    default_value=True,
    description="Strip Exception Messages (disable for debugging)"
),
BuildParameter(
    name="stacktracesupport",
    parameter_type=BuildParameterType.Boolean,
    default_value=False,
    description="Enable Stack Trace support (enable for debugging)"
),
```

- [ ] **Step 2: Verify builder still loads**

```bash
cd Payload_Type/athena/athena/mythic/agent_functions
python3 -c "from builder import athena; a = athena(); print('OK')"
```
Expected: `OK` (no import errors).

- [ ] **Step 3: Commit**

```bash
git add Payload_Type/athena/athena/mythic/agent_functions/builder.py
git commit -m "security: default to stripped exceptions and no stack traces

Flip usesystemresourcekeys to True and stacktracesupport to False
by default. Operators can re-enable for debugging. Reduces info
leaked in release binaries."
```

---

## Chunk 2: Collectible AssemblyLoadContext (Task 4 — Exploratory)

### Task 4: Make AssemblyLoadContext Collectible (MAYBE)

**Context:** Currently `AssemblyLoadContext` in `ComponentProvider` and `ConsoleApplicationExecutor` is non-collectible, meaning loaded plugin assemblies can never be unloaded. Making it collectible would allow unloading after plugin execution, reducing memory forensics surface. However, this has risks: collectible ALCs require that no references to loaded types escape the context, and some plugins maintain long-lived state.

**Decision point:** `execute-assembly` is the best candidate — it loads a .NET assembly, runs it, and is done. Plugin modules (`IModule`) are trickier because they're cached in `loadedModules` and reused. This task focuses on `execute-assembly` only.

**Files:**
- Modify: `execute-assembly/ConsoleApplicationExecutor.cs:9`

- [ ] **Step 1: Read the current ConsoleApplicationExecutor implementation**

Read `execute-assembly/ConsoleApplicationExecutor.cs` fully. Confirm that:
- The ALC is created at field init (`line 9`)
- The assembly is loaded, executed, and the result captured
- No types from the loaded assembly escape the executor's scope

- [ ] **Step 2: Make the ALC collectible and dispose after use**

Before:
```csharp
private AssemblyLoadContext alc = new AssemblyLoadContext(Misc.RandomString(10));
```

After:
```csharp
private AssemblyLoadContext alc = new AssemblyLoadContext(Misc.RandomString(10), isCollectible: true);
```

Then at the end of the execution method, after the assembly has finished running, add:
```csharp
alc.Unload();
```

- [ ] **Step 3: Build and test**

```bash
dotnet build ServiceHost -c LocalDebugHttp --nologo -v q
```

If the build succeeds, run the test suite. If execute-assembly has tests, run them specifically.

**If this causes issues** (types escaping, GC not collecting, crashes), revert this change. The `ComponentProvider` ALC should NOT be made collectible yet — modules are cached and reused. That would be a larger refactor for later.

- [ ] **Step 4: Commit (if successful)**

```bash
git add Payload_Type/athena/athena/agent_code/execute-assembly/ConsoleApplicationExecutor.cs
git commit -m "security: make execute-assembly ALC collectible

Use isCollectible: true and Unload() after execution to allow
the GC to reclaim loaded assemblies. Reduces memory forensics
surface for one-shot assembly execution."
```

---

## Chunk 3: Sleep Encryption (Task 5)

### Task 5: Implement ISleepHandler with Encrypted Sleep State

**Goal:** During the sleep interval between beacon callbacks, encrypt sensitive agent state (PSK, UUID, queued responses, channel config) so memory forensics during sleep reveals nothing useful.

**Architecture:**

1. Add `ISleepHandler` interface to `Workflow.Models`
2. Add `EncryptedSleepHandler` implementation to a new project `Workflow.Sleep` (or directly in `ServiceHost`)
3. Add `ISleepable` interface that components implement to expose their sensitive state for encryption
4. Modify `ISecurityProvider` to implement `ISleepable` (it holds the PSK)
5. Modify each profile's `StartBeacon()` to call `ISleepHandler.Sleep()` instead of `Task.Delay()`
6. Register in DI container

**Design decisions:**
- The sleep handler generates a per-sleep **ephemeral key** from `RandomNumberGenerator`, encrypts state, zeros originals, sleeps, then decrypts and restores.
- The ephemeral key itself is kept in a **pinned byte array** so the GC can't move it, and is zeroed immediately after restore.
- We encrypt: PSK bytes, UUID bytes, config string fields. We do NOT attempt to encrypt all of managed memory — that's impossible in .NET.
- Profiles pass a list of `ISleepable` components to the handler. The handler orchestrates protect/unprotect around `Task.Delay`.

**Files:**
- Create: `Workflow.Models/Interfaces/ISleepHandler.cs`
- Create: `Workflow.Models/Interfaces/ISleepable.cs`
- Modify: `Workflow.Models/Interfaces/ICryptoManager.cs` — add `ISleepable` to `ISecurityProvider`
- Create: `ServiceHost/Managers/SleepHandler.cs`
- Modify: `Workflow.Security.Aes/Crypto.cs` — implement `ISleepable`
- Modify: `Workflow.Security.None/Crypto.cs` — implement `ISleepable` (no-op)
- Modify: `ServiceHost/Config/ContainerBuilder.cs` — register `SleepHandler`
- Modify: `Workflow.Channels.Http/HttpProfile.cs` — use `ISleepHandler`
- Modify: `Workflow.Channels.Websocket/WebsocketProfile.cs` — use `ISleepHandler`
- Modify: `Workflow.Channels.DebugProfile/DebugProfile.cs` — use `ISleepHandler`
- Modify: `Workflow.Channels.GitHub/GitHubProfile.cs` — use `ISleepHandler`
- Note: Discord and SMB profiles are event-driven (no fixed sleep) — skip for now

#### Step-by-step:

- [ ] **Step 1: Create ISleepable interface**

Create `Workflow.Models/Interfaces/ISleepable.cs`:

```csharp
namespace Workflow.Contracts
{
    public interface ISleepable
    {
        void Protect(byte[] key);
        void Unprotect(byte[] key);
    }
}
```

`Protect(key)` encrypts sensitive fields in-place using the provided ephemeral key and zeros the originals. `Unprotect(key)` decrypts and restores them.

- [ ] **Step 2: Create ISleepHandler interface**

Create `Workflow.Models/Interfaces/ISleepHandler.cs`:

```csharp
namespace Workflow.Contracts
{
    public interface ISleepHandler
    {
        Task Sleep(int milliseconds, CancellationToken ct);
    }
}
```

- [ ] **Step 3: Build Workflow.Models to confirm interfaces compile**

```bash
dotnet build Workflow.Models --nologo -v q
```
Expected: Build succeeded.

- [ ] **Step 4: Implement ISleepable on AES SecurityProvider**

Modify `Workflow.Security.Aes/Crypto.cs`. Add `ISleepable` to the class declaration and implement:

```csharp
public class SecurityProvider : ISecurityProvider, ISleepable
{
    // ... existing fields ...
    private byte[]? _encryptedPSK;
    private byte[]? _encryptedUuid;
    private bool _isProtected;

    public void Protect(byte[] key)
    {
        if (_isProtected) return;

        _encryptedPSK = XorBytes(PSK, key);
        _encryptedUuid = XorBytes(uuid, key);

        CryptographicOperations.ZeroMemory(PSK);
        CryptographicOperations.ZeroMemory(uuid);

        _isProtected = true;
    }

    public void Unprotect(byte[] key)
    {
        if (!_isProtected) return;

        PSK = XorBytes(_encryptedPSK!, key);
        uuid = XorBytes(_encryptedUuid!, key);

        CryptographicOperations.ZeroMemory(_encryptedPSK);
        CryptographicOperations.ZeroMemory(_encryptedUuid);
        _encryptedPSK = null;
        _encryptedUuid = null;

        _isProtected = false;
    }

    private static byte[] XorBytes(byte[] data, byte[] key)
    {
        byte[] result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ key[i % key.Length]);
        }
        return result;
    }
}
```

Note: XOR with a cryptographically random ephemeral key is sufficient here — the key is held in a pinned array for the duration of sleep only, then zeroed. This isn't encrypting data at rest for storage; it's preventing a point-in-time memory snapshot from revealing the PSK.

- [ ] **Step 5: Implement ISleepable on None SecurityProvider (no-op)**

Modify `Workflow.Security.None/Crypto.cs`:

```csharp
public class SecurityProvider : ISecurityProvider, ISleepable
{
    // ... existing code ...

    public void Protect(byte[] key) { }
    public void Unprotect(byte[] key) { }
}
```

- [ ] **Step 6: Build both security projects to verify**

```bash
dotnet build Workflow.Security.Aes --nologo -v q
dotnet build Workflow.Security.None --nologo -v q
```
Expected: Both succeed.

- [ ] **Step 7: Create SleepHandler implementation**

Create `ServiceHost/Managers/SleepHandler.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Workflow.Contracts;
using Workflow.Utilities;

namespace Workflow.Providers
{
    public class SleepHandler : ISleepHandler
    {
        private readonly ISleepable[] _sleepables;

        public SleepHandler(ISecurityProvider securityProvider)
        {
            // Collect all ISleepable components
            var sleepables = new List<ISleepable>();
            if (securityProvider is ISleepable s)
            {
                sleepables.Add(s);
            }
            _sleepables = sleepables.ToArray();
        }

        public async Task Sleep(int milliseconds, CancellationToken ct)
        {
            if (milliseconds <= 0 || _sleepables.Length == 0)
            {
                await Task.Delay(milliseconds, ct);
                return;
            }

            byte[] ephemeralKey = new byte[32];
            GCHandle pin = default;
            try
            {
                RandomNumberGenerator.Fill(ephemeralKey);
                pin = GCHandle.Alloc(ephemeralKey, GCHandleType.Pinned);

                foreach (var sleepable in _sleepables)
                {
                    sleepable.Protect(ephemeralKey);
                }

                await Task.Delay(milliseconds, ct);
            }
            catch (TaskCanceledException)
            {
                // Expected when beacon is stopping
            }
            finally
            {
                foreach (var sleepable in _sleepables)
                {
                    sleepable.Unprotect(ephemeralKey);
                }

                CryptographicOperations.ZeroMemory(ephemeralKey);
                if (pin.IsAllocated)
                {
                    pin.Free();
                }
            }
        }
    }
}
```

- [ ] **Step 8: Register SleepHandler in DI container**

Modify `ServiceHost/Config/ContainerBuilder.cs`. After the `SecurityProvider` registration (line 37) and before `DataBroker` (line 40), add:

```csharp
DebugLog.Log("Registering SleepHandler as ISleepHandler");
containerBuilder.RegisterType<SleepHandler>().As<ISleepHandler>().SingleInstance();
```

- [ ] **Step 9: Build ServiceHost to verify DI registration compiles**

```bash
dotnet build ServiceHost -c LocalDebugHttp --nologo -v q
```
Expected: Build succeeded.

- [ ] **Step 10: Modify HttpProfile to use ISleepHandler**

Modify `Workflow.Channels.Http/HttpProfile.cs`:

Add `ISleepHandler` to constructor and field:

```csharp
private ISleepHandler sleepHandler { get; set; }

public HttpProfile(IServiceConfig config, ISecurityProvider crypto,
    ILogger logger, IDataBroker messageManager, ISleepHandler sleepHandler)
{
    // ... existing constructor code ...
    this.sleepHandler = sleepHandler;
}
```

In `StartBeacon()` (line 135-137), replace:

Before:
```csharp
int sleepMs = Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000;
DebugLog.Log($"HTTP beacon sleeping {sleepMs}ms");
await Task.Delay(sleepMs);
```

After:
```csharp
int sleepMs = Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000;
DebugLog.Log($"HTTP beacon sleeping {sleepMs}ms");
await sleepHandler.Sleep(sleepMs, cancellationTokenSource.Token);
```

- [ ] **Step 11: Modify WebsocketProfile to use ISleepHandler**

Modify `Workflow.Channels.Websocket/WebsocketProfile.cs`:

Add to constructor params and field (same pattern as HTTP). Replace line 123:

Before:
```csharp
await Task.Delay(Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000);
```

After:
```csharp
int sleepMs = Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000;
await sleepHandler.Sleep(sleepMs, cancellationTokenSource.Token);
```

- [ ] **Step 12: Modify GitHubProfile to use ISleepHandler**

Modify `Workflow.Channels.GitHub/GitHubProfile.cs`:

Add to constructor and field. Replace line 202:

Before:
```csharp
await Task.Delay(Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000);
```

After:
```csharp
int sleepMs = Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000;
await sleepHandler.Sleep(sleepMs, cancellationTokenSource.Token);
```

- [ ] **Step 13: Modify DebugProfile to use ISleepHandler**

Modify `Workflow.Channels.DebugProfile/DebugProfile.cs`:

Add to constructor and field. Replace the `Task.Delay` in `StartBeacon()` (recently changed from `Thread.Sleep`):

Before:
```csharp
await Task.Delay(Misc.GetSleep(agentConfig.sleep, agentConfig.jitter)*1000);
```

After:
```csharp
int sleepMs = Misc.GetSleep(agentConfig.sleep, agentConfig.jitter) * 1000;
await sleepHandler.Sleep(sleepMs, cancellationTokenSource.Token);
```

- [ ] **Step 14: Build full solution**

```bash
dotnet build ServiceHost -c LocalDebugHttp --nologo -v q
```
Expected: Build succeeded. Autofac will inject `ISleepHandler` into all profile constructors via DI.

- [ ] **Step 15: Run all tests**

```bash
dotnet test Tests/Workflow.Tests --nologo -v q
```
Expected: All tests pass. Test profiles use `TestProfile` and the recently-added `TestNullCheckinProfile` (both in `Tests/Workflow.Tests/TestClasses/TestProfile.cs`). These test profiles don't use `ISleepHandler` since they don't actually sleep — they implement `IChannel` directly without DI. No changes needed to test profiles unless they start accepting `ISleepHandler` in their constructors, which they should not.

- [ ] **Step 16: Commit**

```bash
git add -A
git commit -m "security: implement encrypted sleep state via ISleepHandler

Add ISleepable/ISleepHandler interfaces. During sleep, the handler:
1. Generates a pinned 32-byte ephemeral key via RNG
2. XOR-encrypts PSK and UUID bytes in SecurityProvider
3. Zeros the originals
4. Sleeps
5. Restores state and zeros the ephemeral key

HTTP, WebSocket, GitHub, and Debug profiles now use ISleepHandler
instead of raw Task.Delay(). Discord/SMB are event-driven and
unchanged."
```

---

## Implementation Notes

### What this does NOT cover (future work)
- Encrypting the `IDataBroker` queued responses during sleep (would require `IDataBroker` to implement `ISleepable` — straightforward extension)
- Encrypting channel-specific tokens (Discord bot token, GitHub PAT) — would require each profile to implement `ISleepable`
- Stack encryption via P/Invoke (`SystemFunction040/041`) — Windows-only, much more complex
- NativeAOT compilation — requires verifying all plugins are AOT-compatible

### Testing strategy
- Existing tests validate that the agent's functional behavior is unchanged
- The `e.Message` change is a pure output-text change — no logic affected
- The sleep handler is transparent to profiles — same sleep duration, just with state protection around it
- The AES crypto tests verify encrypt/decrypt still works after the `ISleepable` fields are added

### Risk assessment
- **Task 1** (sanitize errors): Zero risk. Text-only change.
- **Task 2** (zero keys): Very low risk. `CryptographicOperations.ZeroMemory` is a standard .NET API.
- **Task 3** (build defaults): Zero risk. Only changes default values.
- **Task 4** (collectible ALC): Medium risk. May cause issues if types escape. Scoped to `execute-assembly` only, and marked as revertible.
- **Task 5** (sleep encryption): Low risk. The XOR protect/unprotect is symmetric and tested. Worst case on failure: agent crashes on wake (unprotect fails), which is detectable in testing.
