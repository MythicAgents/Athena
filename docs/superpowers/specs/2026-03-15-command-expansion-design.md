# Athena Agent Command Expansion & Testing Overhaul

**Date:** 2026-03-15
**Author:** @checkymander + Claude
**Status:** Draft
**Branch:** dev

## Overview

Comprehensive expansion of Athena's command set, testing infrastructure, and plugin architecture. Adds 72 new Mythic commands (19 plugin DLLs + 53 Mythic-only wrappers), tests for existing untested plugins, and replaces the hardcoded plugin dependency system with a convention-based approach.

## Goals

1. Close capability gaps vs competing C2 agents (find, sysinfo, hash, etc.)
2. Expand Linux/macOS coverage (privesc checks, credential recon, jxa/keychain revival)
3. Achieve meaningful test coverage across all plugins (depth-where-it-matters)
4. Modernize the plugin dependency system to scale with new commands
5. Zero large third-party dependencies — BCL + P/Invoke only (except ~300KB from 2 Microsoft packages)

## Non-Goals

- Channel/provider testing (HTTP, WebSocket, Discord, SMB, GitHub) — separate effort
- Replacing existing BOF commands with native equivalents for scheduled tasks or services
- Adding `powershell` command (`System.Management.Automation` is ~15MB — operators use `shell` instead)

## Constraints

- .NET 10 target
- No large third-party NuGet packages
- Native .NET preferred over BOFs, but BOFs acceptable when significantly easier
- Avoid shelling out — use file parsing, P/Invoke, or subtasking to existing plugins
- Follow existing naming conventions (lowercase, hyphenated)

---

## Architecture: Plugin Dependency System Refactor

### Problem

Plugin sub-command relationships are managed via hardcoded arrays in `plugin_utilities.py` (`get_coff_commands()`, `get_ds_commands()`, etc.) referenced by `load.py` and `builder.py`. This requires updating multiple files for every new wrapper command and doesn't scale.

### Solution

Convention-based dependency declaration on each command's `CommandBase` subclass.

### New Attributes

Every command `.py` file gains two optional class attributes:

```python
class SuidFindCommand(CommandBase):
    cmd = "suid-find"
    depends_on = "find"          # Parent plugin DLL this subtasks to (None if standalone)
    plugin_libraries = []        # External DLLs needed (only on DLL-owning commands)
    script_only = True
    # ...
```

For DLL-owning commands with external dependencies:

```python
class WmiCommand(CommandBase):
    cmd = "wmi"
    depends_on = None
    plugin_libraries = ["System.Management.dll"]
    # ...
```

Defaults: `depends_on = None`, `plugin_libraries = []`. Existing commands that don't set these continue to work unchanged.

### Discovery Utility

New file: `athena_utils/plugin_registry.py`

Uses Python's `__init_subclass__` hook on `CommandBase` (or iterates all imported command classes after Mythic's auto-discovery completes) to build the dependency graph. Must handle commands in flat `.py` files, subdirectories (`nidhogg_commands/`, `outflank_bofs/`, `trusted_sec_bofs/`, `trusted_sec_remote_bofs/`, `misc_bofs/`), and new wrapper command files. The graph is built once at container startup and cached.

```python
def get_subcommands(plugin_name: str) -> list[str]
    # Returns all command names where depends_on == plugin_name

def get_libraries(command_name: str) -> list[str]
    # Returns plugin_libraries for the command

def is_subcommand(command_name: str) -> bool
    # Returns True if depends_on is set

def get_parent(command_name: str) -> str | None
    # Returns the depends_on value
```

### load.py Refactor

Replace hardcoded checks with discovery calls:

```python
# Before (hardcoded)
bof_commands = plugin_utilities.get_coff_commands()
if command in bof_commands:
    raise Exception("Please load coff to enable this command")

# After (discovery-based)
parent = plugin_registry.get_parent(command)
if parent:
    raise Exception(f"Please load {parent} to enable this command")
```

Library loading:

```python
# Before (hardcoded dict)
command_libraries = {
    "ds": [{"libraryname": "System.DirectoryServices.Protocols.dll", ...}],
}

# After (from command attribute)
for lib in plugin_registry.get_libraries(command):
    # subtask load-assembly
```

Sub-command registration:

```python
# Before (hardcoded dict)
command_plugins = {"coff": bof_commands, "ds": ds_commands, ...}

# After (discovery)
subcommands = plugin_registry.get_subcommands(command)
if subcommands:
    await SendMythicRPCCallbackAddCommand(...)
```

### builder.py Refactor

Same pattern — replace `get_*_commands()` calls with `plugin_registry.get_subcommands()`.

### Migration of Existing Commands

| Command | Add `depends_on` |
|---------|-----------------|
| All BOF commands (adcs-enum, add-machine-account, etc.) | `"coff"` |
| ds-query, ds-connect | `"ds"` |
| All nidhogg-* commands | `"nidhogg"` |
| inject-assembly | `"inject-shellcode"` |

| Command | Add `plugin_libraries` |
|---------|----------------------|
| ds | `["System.DirectoryServices.Protocols.dll"]` |
| ssh | `["Renci.SshNet.dll", "BouncyCastle.Cryptography.dll"]` |
| sftp | `["Renci.SshNet.dll", "BouncyCastle.Cryptography.dll"]` |
| screenshot | `["System.Drawing.Common.dll"]` |

After migration, remove all `get_*_commands()` functions from `plugin_utilities.py`.

---

## Architecture: Testing Infrastructure

### PluginTestBase\<T\>

Abstract base class encapsulating test boilerplate:

```csharp
public abstract class PluginTestBase
{
    protected IModule _plugin;
    protected TestDataBroker _messageManager;
    protected PluginLoader _pluginLoader;

    protected void LoadPlugin(string moduleName) { ... }
    protected ServerJob CreateJob(string command, object parameters) { ... }
    protected async Task<TaskResponse> ExecuteAndGetResponse(ServerJob job) { ... }
    protected void AssertSuccess(TaskResponse response) { ... }
    protected void AssertError(TaskResponse response) { ... }
}
```

### JobBuilder

Fluent helper for constructing test jobs:

```csharp
var job = new JobBuilder("cat")
    .WithParameters(new { path = "/tmp/test.txt" })
    .WithTaskId("test-001")
    .Build();
```

### Extended Utilities

Add to existing `Utilities.cs`:
- `CreateTempFileWithContent(string content)` — specific content
- `CreateTempDirectoryWithStructure(Dictionary<string, string> files)` — named files with content
- `CreateLocalListener(int port)` — for network plugin tests
- Platform-aware temp paths

### Code Coverage

Add `coverlet.collector` NuGet to `Workflow.Tests.csproj`. CI workflow updated to:
- Generate coverage reports
- Fail on coverage regression

### Test Categories

```csharp
[TestCategory("FileOps")]
[TestCategory("Network")]
[TestCategory("Credentials")]
[TestCategory("SystemInfo")]
[TestCategory("Execution")]
[TestCategory("Recon")]
[TestCategory("macOS")]
```

---

## New Plugin DLLs (19 total)

### 1. `find`

**Mythic commands:** `find` (direct), `grep`, `suid-find`, `writable-paths` (wrappers)

Recursive filesystem search with filters:
- Glob pattern matching on filename
- Content search with regex (for `grep` action)
- Size filters (min/max)
- Date filters (modified/created before/after)
- Permission filters (suid, sgid, world-writable — for `suid-find`, `writable-paths`)
- Max depth limit
- Output: structured file listing compatible with Mythic file browser

**Implementation:** `Directory.EnumerateFiles` + `Regex` + `File.GetUnixFileMode()`. All BCL.

**Args:**
```json
{
  "action": "find|grep",
  "path": "/",
  "pattern": "*.conf",
  "content_pattern": "password",
  "max_depth": 5,
  "min_size": 0,
  "max_size": 1048576,
  "permissions": "suid|sgid|world-writable",
  "newer_than": "2026-01-01",
  "older_than": "2026-03-01"
}
```

### 2. `file-utils`

**Mythic commands:** `head` (direct), `touch`, `wc`, `diff`, `link`, `chmod`, `chown` (wrappers)

General file utility plugin with action routing. Named `file-utils` rather than `head` since it handles 7 distinct file operations:

| Action | Implementation |
|--------|---------------|
| `head` | `File.ReadLines().Take(n)` |
| `touch` | `File.Create` or `File.SetLastWriteTime` |
| `wc` | `StreamReader` counting lines/words/bytes |
| `diff` | Line-by-line comparison, unified diff output |
| `link` | `File.CreateSymbolicLink` / `File.CreateHardLink` (.NET 7+) |
| `chmod` | `File.SetUnixFileMode()` (.NET 7+) |
| `chown` | P/Invoke `chown()` on Linux/macOS, no-op on Windows |

**Args:**
```json
{
  "action": "head|touch|wc|diff|link|chmod|chown",
  "path": "/etc/passwd",
  "path2": "/etc/passwd.bak",
  "lines": 10,
  "mode": "755",
  "owner": "root",
  "group": "root",
  "link_type": "symbolic|hard"
}
```

### 3. `hash`

**Mythic commands:** `hash` (direct), `base64` (wrapper)

| Action | Implementation |
|--------|---------------|
| `hash` | `MD5.HashData()`, `SHA1.HashData()`, `SHA256.HashData()` on file stream |
| `base64` | `Convert.ToBase64String` / `Convert.FromBase64String` on file or string input |

All `System.Security.Cryptography` — BCL.

### 4. `sysinfo`

**Mythic commands:** `sysinfo` (direct), `id`, `container-detect`, `mount`, `package-list`, `dotnet-versions` (wrappers)

Composite system profiling plugin. Each action collects different data:

| Action | Data Collected |
|--------|---------------|
| `sysinfo` | OS, arch, hostname, domain, current user, groups, .NET version, uptime, IP addresses, drives. AV detection via registry on Windows (`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*` filtering for AV vendors), not WMI |
| `id` | uid, gid, groups, privileges. Windows: `WindowsIdentity`. Linux/macOS: P/Invoke `getuid`/`getgid`/`getgroups` |
| `container-detect` | Check `/.dockerenv`, `/run/.containerenv`, cgroup contents, `KUBERNETES_SERVICE_HOST` env |
| `mount` | `DriveInfo.GetDrives()` + parse `/proc/mounts` (Linux) / parse `/etc/fstab` and `statfs` P/Invoke (macOS) |
| `package-list` | Parse `/var/lib/dpkg/status` (Debian), `/var/lib/rpm/rpmdb.sqlite` (RHEL), `/usr/local/Cellar/` dir listing (macOS brew), Windows registry Uninstall keys |
| `dotnet-versions` | `RuntimeInformation`, scan runtime dirs (`/usr/share/dotnet/shared/`, `C:\Program Files\dotnet\shared\`) |

All BCL + P/Invoke. No WMI dependency.

### 5. `ping`

**Mythic commands:** `ping` (direct), `traceroute` (wrapper)

| Action | Implementation |
|--------|---------------|
| `ping` | `System.Net.NetworkInformation.Ping.SendPingAsync()`. Supports single host or CIDR sweep |
| `traceroute` | Send pings with incrementing TTL (1..30), record responding IPs and RTT |

**Args:**
```json
{
  "action": "ping|traceroute",
  "hosts": "192.168.1.0/24",
  "count": 1,
  "timeout": 1000,
  "max_ttl": 30
}
```

### 6. `dns`

**Mythic commands:** `dns` (direct)

Full DNS query support for all record types via P/Invoke:
- Windows: `DnsQuery_A` from `dnsapi.dll`
- Linux/macOS: `res_query` from `libresolv`

Record types: A, AAAA, MX, TXT, SRV, SOA, PTR, CNAME, NS.

**Args:**
```json
{
  "hostname": "example.com",
  "type": "A|AAAA|MX|TXT|SRV|SOA|PTR|CNAME|NS",
  "server": "8.8.8.8"
}
```

### 7. `http-request`

**Mythic commands:** `http-request` (direct), `wget` (wrapper)

Full HTTP client via `System.Net.Http.HttpClient`:
- All methods (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS)
- Custom headers
- Body (raw, JSON, form-encoded)
- Authentication (Basic, Bearer, NTLM)
- Follow/don't follow redirects
- Output: status code, headers, body
- For `wget` action: stream response body to file

**Args:**
```json
{
  "action": "request|download",
  "url": "http://internal-api/endpoint",
  "method": "GET",
  "headers": {"Authorization": "Bearer token"},
  "body": "{}",
  "body_type": "json|form|raw",
  "auth_type": "none|basic|bearer|ntlm",
  "auth_value": "user:pass",
  "follow_redirects": true,
  "output_file": "/tmp/downloaded.bin"
}
```

### 8. `wmi`

**Mythic commands:** `wmi` (direct), `installed-software`, `defender-status`, `startup-items` (wrappers via subtask)

**Windows-only.** General-purpose WMI query execution via `System.Management` (~200KB NuGet).

**External dep:** `System.Management.dll` — registered in `plugin_libraries`.

Wrapper commands are Mythic-only `.py` files with `depends_on = "wmi"` that subtask with pre-built queries:

| Wrapper | WMI Query | Namespace |
|---------|-----------|-----------|
| `installed-software` | `SELECT Name, Version, Vendor, InstallDate FROM Win32_Product` | `root/cimv2` |
| `defender-status` | `SELECT * FROM MSFT_MpComputerStatus` | `root/Microsoft/Windows/Defender` |
| `startup-items` | `SELECT Name, Command, Location FROM Win32_StartupCommand` | `root/cimv2` |

**Args:**
```json
{
  "query": "SELECT * FROM Win32_Process",
  "namespace": "root/cimv2",
  "hostname": "localhost"
}
```

### 9. `event-log`

**Mythic commands:** `event-log` (direct), `etw-control` (wrapper)

**Windows-only.** Read/query/clear Windows event logs via `System.Diagnostics.EventLog` (~100KB NuGet).

**External dep:** `System.Diagnostics.EventLog.dll` — registered in `plugin_libraries`.

| Action | Implementation |
|--------|---------------|
| `read` | Read entries from specified log (System, Security, Application, etc.) with count/filter |
| `list` | List available event logs |
| `clear` | Clear specified log |
| `etw-list` | Enumerate ETW providers via `EventSource` |
| `etw-disable` | Disable specific ETW provider via P/Invoke `EventUnregister` |

### 10. `credentials`

**Mythic commands:** `credentials` (direct), `dpapi`, `vault-enum`, `wifi-profiles`, `dns-cache`, `shadow-read`, `lsass-dump`, `sam-dump` (wrappers)

Multi-platform credential harvesting. Each action uses different native APIs:

| Action | Platform | Implementation |
|--------|----------|---------------|
| `dpapi` | Windows | `System.Security.Cryptography.ProtectedData` (BCL) for user-scope. P/Invoke `CryptUnprotectData` for machine-scope |
| `vault-enum` | Windows | P/Invoke `VaultEnumerateVaults`/`VaultEnumerateItems` from `vaultcli.dll` |
| `wifi-profiles` | Windows | P/Invoke `WlanGetProfileList`/`WlanGetProfile` from `wlanapi.dll` |
| `dns-cache` | Windows | P/Invoke `DnsGetCacheDataTable` from `dnsapi.dll` |
| `shadow-read` | Linux | Read `/etc/shadow`, parse user:hash:dates fields |
| `lsass-dump` | Windows | P/Invoke `MiniDumpWriteDump` from `dbghelp.dll` with `MiniDumpWithFullMemory` |
| `sam-dump` | Windows | Read SAM/SYSTEM registry hives via `reg` subtask or P/Invoke `RegSaveKeyEx` |

All P/Invoke or BCL — no external NuGet.

### 11. `proc-enum`

**Mythic commands:** `proc-enum` (direct), `named-pipes` (wrapper)

| Action | Platform | Implementation |
|--------|----------|---------------|
| `proc-enum` | Linux | Parse `/proc/[pid]/cmdline`, `/proc/[pid]/environ`, `/proc/[pid]/maps`, `/proc/[pid]/fd` |
| `named-pipes` | Windows | P/Invoke `FindFirstFile`/`FindNextFile` on `\\.\pipe\` or `Directory.GetFiles(@"\\.\pipe\")` |

### 12. `kerberos`

**Mythic commands:** `kerberos` (direct)

**Windows-only.** Kerberos ticket operations via SSPI P/Invoke:

| Action | Implementation |
|--------|---------------|
| `list` | `LsaCallAuthenticationPackage` with `KerbQueryTicketCacheMessage` |
| `purge` | `LsaCallAuthenticationPackage` with `KerbPurgeTicketCacheMessage` |
| `tgtdeleg` | Request delegated TGT via `AcquireCredentialsHandle`/`InitializeSecurityContext` |

All P/Invoke to `secur32.dll` and `advapi32.dll`.

### 13. `clipboard-monitor`

**Mythic commands:** `clipboard-monitor` (direct)

Long-running job that polls clipboard at configurable interval. Differs from existing `get-clipboard` (one-shot) by running as a background job.

- Windows: P/Invoke `GetClipboardData` / `AddClipboardFormatListener`
- macOS: P/Invoke `NSPasteboard` via ObjC interop
- Linux: Read X11 clipboard via `xclip`-equivalent or skip

Runs as a job (shows in `jobs`, killable via `jobkill`).

### 14. `keychain` (revived from .why)

**Mythic commands:** `keychain` (direct), `security-tool` (wrapper)

**macOS-only.** Enumerate keychain items via Security.framework P/Invoke:

| Action | Implementation |
|--------|---------------|
| `list` | `SecItemCopyMatching` with various `kSecClass` values |
| `dump` | Extract password data (requires keychain unlock or user context) |
| `certs` | Enumerate certificates via `SecCertificateCopyValues` |

### 15. `jxa` (revived from .why)

**Mythic commands:** `jxa` (direct), `spotlight`, `defaults-read`, `osascript`, `login-items` (wrappers)

**macOS-only.** Execute JavaScript for Automation via JavaScriptCore P/Invoke.

Accepts code as string or uploaded file. Wrapper commands subtask with pre-built JXA scripts:

| Wrapper | JXA Script Purpose |
|---------|--------------------|
| `spotlight` | `$.NSMetadataQuery` for file search |
| `defaults-read` | `$.NSUserDefaults` for preference domains |
| `osascript` | Execute AppleScript via `OSAScript` bridge |
| `login-items` | Read login items via `LSSharedFileList` |

### 16. `lnk` (revived from .why)

**Mythic commands:** `lnk` (direct)

**Windows-only.** Parse and create Windows .lnk shortcut files:
- Read: parse binary .lnk format, extract target path, arguments, working dir, icon
- Create/Update: write .lnk binary format with specified properties

No external deps — .lnk is a documented binary format.

### 17. `inject-shellcode-macos`

**Mythic commands:** `inject-shellcode-macos` (direct)

**macOS-only.** Shellcode injection via Mach APIs P/Invoke:
- `mach_vm_allocate` — allocate memory in target process
- `mach_vm_write` — write shellcode
- `mach_vm_protect` — set RX permissions
- `thread_create_running` — create thread at shellcode address

### 18. `bits-transfer`

**Mythic commands:** `bits-transfer` (direct)

**Windows-only.** BITS (Background Intelligent Transfer Service) job management via COM P/Invoke:
- Create download/upload jobs
- List existing jobs
- Cancel jobs

Stealthy file transfer mechanism — BITS traffic blends with Windows Update.

### 19. `ssh-recon`

**Mythic commands:** `ssh-keys` (wrapper), `authorized-keys` (wrapper)

**Linux/macOS.** SSH key enumeration and `authorized_keys` manipulation:

| Action | Implementation |
|--------|---------------|
| `ssh-keys` | Enumerate `~/.ssh/`, `/home/*/.ssh/`, `/etc/ssh/` — find private keys, public keys, known_hosts. Report key type, fingerprint, permissions |
| `authorized-keys-read` | Read `authorized_keys` for specified user or all users (if root) |
| `authorized-keys-add` | Append a public key to a user's `authorized_keys` |
| `authorized-keys-remove` | Remove a specific key from `authorized_keys` |

All file I/O via BCL. No external deps.

---

---

## Architecture: Multi-Subtask Pattern

Several wrapper commands need to subtask to multiple plugins (e.g., `capabilities` needs both `find` and `proc-enum`). Use Mythic's `SendMythicRPCTaskCreateSubtaskGroup` for this:

```python
async def create_go_tasking(self, taskData):
    # Create a group of subtasks — completion callback fires once ALL complete
    group = MythicRPCTaskCreateSubtaskGroupMessage(
        TaskID=taskData.Task.ID,
        GroupName="capabilities-check",
        GroupCallbackFunction="merge_results",
        Tasks=[
            MythicRPCTaskCreateSubtaskGroupTasks(
                CommandName="find",
                Params=json.dumps({"action": "find", "path": "/usr/bin", ...})
            ),
            MythicRPCTaskCreateSubtaskGroupTasks(
                CommandName="proc-enum",
                Params=json.dumps({"action": "proc-enum"})
            ),
        ]
    )
    await SendMythicRPCTaskCreateSubtaskGroup(group)
    return PTTaskCreateTaskingMessageResponse(TaskID=taskData.Task.ID, Success=True)
```

The completion callback receives results from all subtasks in the group and merges them:

```python
async def merge_results(completionMsg):
    response = PTTaskCompletionFunctionMessageResponse(Success=True, TaskStatus="success", Completed=True)
    responses = await SendMythicRPCResponseSearch(
        MythicRPCResponseSearchMessage(TaskID=completionMsg.SubtaskData.Task.ID)
    )
    # Merge all subtask outputs, parse, format
    merged = ""
    for output in responses.Responses:
        merged += str(output.Response)
    await SendMythicRPCResponseCreate(MythicRPCResponseCreateMessage(
        TaskID=completionMsg.TaskData.Task.ID,
        Response=format_output(merged)
    ))
    return response
```

Commands using this pattern: `capabilities`, `ld-preload`, `pam-enum`, `proxy-config`, `crontab`, `launchd-enum`.

For wrappers that subtask to built-in commands (`cat`, `env`, `reg`): these commands are always available (not dynamically loaded), so `depends_on` for the wrapper should reference the *first* non-builtin parent, or `None` if all parents are built-in. The subtask mechanism works regardless — `depends_on` only controls `load` command behavior.

---

## Architecture: Replacing Existing Plugins

### `wget` Replacement

The existing `wget` plugin (`agent_code/wget/`) is a standalone DLL using `HttpWebRequest`. This will be **deleted and replaced** with a Mythic-only wrapper that subtasks to the new `http-request` plugin with `action: "download"`. The existing `wget.csproj` and C# source files are removed. The new `wget.py` Mythic command definition sets `depends_on = "http-request"`.

### Revived Commands (`jxa`, `keychain`, `lnk`)

These commands have **existing C# code** in `agent_code/jxa/`, `agent_code/keychain/`, `agent_code/lnk/`. The `.why` files in `agent_functions/` are renamed back to `.py`. The approach:

- **Review existing C# code** — update to use the `PluginContext` pattern (constructor refactor already done in recent commits for other plugins)
- **Update to .NET 10** target framework
- **Re-enable Mythic definitions** — rename `.why` to `.py`, update to current `CommandBase` API
- **Do not rewrite from scratch** unless existing code is fundamentally broken

---

## Implementation Notes

### `dns` Plugin Complexity

The `dns` plugin P/Invoke approach requires platform-specific work:
- **Windows:** `DnsQuery_A` from `dnsapi.dll` returns structured records — relatively straightforward
- **Linux/macOS:** `res_query` from `libresolv` returns raw DNS wire format (RFC 1035) that must be parsed manually, including name compression pointers. This is significantly more code than the Windows path. Budget extra time for the Unix implementation and consider starting with A/AAAA/CNAME records, then adding MX/SRV/TXT/SOA incrementally.

### `lsass-dump` OPSEC Warning

`MiniDumpWriteDump` targeting lsass.exe is one of the most heavily monitored API call patterns in modern EDR. This technique works primarily against unmonitored targets. The Mythic command description should include an OPSEC warning.

### Phase Ordering

Phases are **sequential** — each phase may depend on DLLs created in earlier phases:
- Phase 0 must complete before any other phase (all phases depend on the dependency system)
- Phase 4 (`ssh-keys` wrapper) depends on Phase 1 (`find` plugin)
- Phase 6 (WMI wrappers like `defender-status`) depends on Phase 6's own `wmi` DLL (internal ordering)
- Phase 7 (macOS wrappers like `spotlight`) depends on Phase 7's own `jxa` DLL (internal ordering)

Within a phase, individual DLLs and wrappers are independent and can be built in parallel.

### Phase 0 Verification Gate

Phase 0 modifies `load.py`, `builder.py`, and all existing command files simultaneously. Before proceeding to Phase 1, **verify end-to-end:**
1. Build a payload with all default commands — verify it compiles
2. Build a payload with `ds` selected — verify `ds-query` and `ds-connect` are auto-included
3. Load `coff` at runtime — verify all BOF commands register
4. Load `nidhogg` at runtime — verify all nidhogg sub-commands register
5. Run existing test suite — all tests pass

---

## Mythic-Only Wrapper Commands (53 total)

All wrappers follow the `ds-query` pattern: `script_only = True`, `depends_on` set, `create_go_tasking` creates subtask with pre-configured params, completion callback parses/formats output. Multi-subtask wrappers use `SendMythicRPCTaskCreateSubtaskGroup` (see architecture section above).

**`depends_on` vs runtime subtask target:** The "Subtasks To" column in the tables below shows the *runtime* subtask target. The `depends_on` class attribute should only reference *dynamically loaded* plugins. Wrappers that subtask to built-in commands (`cat`, `ls`, `env`, `reg`, `upload`) should set `depends_on = None` since those commands are always available. Examples: `stat` subtasks to `ls` at runtime but sets `depends_on = None`. `sudo-check` subtasks to `cat` but sets `depends_on = None`.

**`file-utils` DLL naming:** The DLL is named `file-utils` but the primary Mythic command is `head`. Operators run `load file-utils` to get `head` plus all wrapper commands (`wc`, `diff`, `touch`, `link`, `chmod`, `chown`). The `load.py` plugin directory lookup uses the DLL name, not the command name.

### File Operation Wrappers

| Command | Subtasks To | Action/Params | Callback |
|---------|-------------|---------------|----------|
| `grep` | `find` | `action: "grep"`, content_pattern, path | Pass-through |
| `suid-find` | `find` | `permissions: "suid"`, common binary paths | Pass-through |
| `writable-paths` | `find` | `permissions: "world-writable"`, configurable paths | Pass-through |
| `stat` | `ls` | Single file path | Reformat to detailed metadata |
| `wc` | `file-utils` | `action: "wc"` | Pass-through |
| `diff` | `file-utils` | `action: "diff"`, path, path2 | Pass-through |
| `touch` | `file-utils` | `action: "touch"` | Pass-through |
| `link` | `file-utils` | `action: "link"` | Pass-through |
| `chmod` | `file-utils` | `action: "chmod"`, path, mode | Pass-through |
| `chown` | `file-utils` | `action: "chown"`, path, owner, group | Pass-through |
| `base64` | `hash` | `action: "base64"`, encode/decode, file/string | Pass-through |

### System Info Wrappers

| Command | Subtasks To | Action/Params | Callback |
|---------|-------------|---------------|----------|
| `id` | `sysinfo` | `action: "id"` | Format identity |
| `container-detect` | `sysinfo` | `action: "container-detect"` | Format detection |
| `mount` | `sysinfo` | `action: "mount"` | Format mount table |
| `package-list` | `sysinfo` | `action: "package-list"` | Format package list |
| `dotnet-versions` | `sysinfo` | `action: "dotnet-versions"` | Format version list |

### Network Wrappers

| Command | Subtasks To | Action/Params | Callback |
|---------|-------------|---------------|----------|
| `traceroute` | `ping` | `action: "traceroute"`, host | Format hop table |
| `wget` | `http-request` | `action: "download"`, url, output_file | Pass-through |

### Credential/Recon Wrappers

| Command | Subtasks To | Action/Params | Callback |
|---------|-------------|---------------|----------|
| `dpapi` | `credentials` | `action: "dpapi"` | Pass-through |
| `vault-enum` | `credentials` | `action: "vault-enum"` | Format entries |
| `wifi-profiles` | `credentials` | `action: "wifi-profiles"` | Format profiles |
| `dns-cache` | `credentials` | `action: "dns-cache"` | Format cache table |
| `shadow-read` | `credentials` | `action: "shadow-read"` | Parse user:hash:dates |
| `lsass-dump` | `credentials` | `action: "lsass-dump"` | Pass-through (returns file) |
| `sam-dump` | `credentials` | `action: "sam-dump"` | Pass-through (returns files) |
| `ssh-keys` | `ssh-recon` | `action: "ssh-keys"` | Format key listing |
| `authorized-keys` | `ssh-recon` | `action: "authorized-keys"`, read/write/add/remove | Pass-through |
| `named-pipes` | `proc-enum` | `action: "named-pipes"` | Format pipe list |

### Privesc/Security Wrappers

| Command | Subtasks To | Action/Params | Callback |
|---------|-------------|---------------|----------|
| `sudo-check` | `cat` | Read `/etc/sudoers`, `/etc/sudoers.d/*` | Parse rules for current user |
| `capabilities` | `find` + `proc-enum` | Subtask group: find binaries + read cap fields | Merge + format |
| `ld-preload` | `cat` + `env` | Subtask group: read `/etc/ld.so.preload` + check env var | Merge output |
| `selinux-status` | `cat` | Read `/sys/fs/selinux/enforce`, `/etc/selinux/config` | Format status |
| `pam-enum` | `find` + `cat` | Subtask group: find `/etc/pam.d/*`, read contents | Format PAM config |
| `iptables-enum` | `cat` | Read `/proc/net/ip_tables_names`, `/proc/net/nf_conntrack` | Parse rules |
| `firewall-enum` | `reg` (Win) / `cat` (Linux) | Platform-aware: registry keys or `/proc/net/` | Format rules |
| `uac-check` | `reg` | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System` | Format UAC settings |
| `amsi-status` | `reg` | `HKLM\SOFTWARE\Microsoft\AMSI\Providers\*` | Format provider list |

### Windows Recon Wrappers (via WMI)

| Command | Subtasks To | WMI Query | Callback |
|---------|-------------|-----------|----------|
| `installed-software` | `wmi` | `SELECT Name, Version, Vendor FROM Win32_Product` | Format table |
| `defender-status` | `wmi` | `SELECT * FROM MSFT_MpComputerStatus` | Format status |
| `startup-items` | `wmi` | `SELECT Name, Command, Location FROM Win32_StartupCommand` | Format table |

### Persistence/Evasion Wrappers

| Command | Subtasks To | Action/Params | Callback |
|---------|-------------|---------------|----------|
| `crontab` | `find` + `cat` | Find/read `/etc/crontab`, `/var/spool/cron/*`, `/etc/cron.d/*` | Parse cron format |
| `etw-control` | `event-log` | `action: "etw-list"` or `action: "etw-disable"` | Pass-through |
| `rdp-config` | `reg` | RDP registry keys (`fDenyTSConnections`, `PortNumber`, etc.) | Format status |
| `com-objects` | `reg` | Enumerate `HKCR\CLSID\*` registry keys | Format table |
| `proxy-config` | `env` + `reg` | Subtask group: check env vars + Windows proxy registry | Merge results |

### macOS Wrappers (via JXA)

| Command | Subtasks To | JXA Script | Callback |
|---------|-------------|-----------|----------|
| `spotlight` | `jxa` | `$.NSMetadataQuery` search | Format results |
| `defaults-read` | `jxa` | `$.NSUserDefaults` read domain | Format prefs |
| `osascript` | `jxa` | AppleScript execution via bridge | Pass-through |
| `login-items` | `jxa` | `LSSharedFileList` enumeration | Format items |
| `security-tool` | `keychain` | `action: "certs"` or other security ops | Format output |
| `tcc-check` | `cat` | Read TCC.db at known paths | Parse permissions |
| `launchd-enum` | `find` + `cat` | Find plists in LaunchDaemons/LaunchAgents dirs | Parse plists |
| `airport` | `cat` | Read airport framework data files | Format scan results |

---

## Testing Strategy

### Depth-Where-It-Matters

**Thorough tests** (happy path, error paths, edge cases, boundary conditions):
- File operations: `cat`, `cp`, `cd`, `find`, `file-utils`, `hash`, `ls`, `mkdir`, `mv`, `rm`, `tail`, `download`, `upload`
- Network: `ssh`, `sftp`, `dns`, `http-request`, `ping`, `test-port`
- Credentials: `credentials`, `token`, `keylogger`
- Crypto: AES (already exists)
- Execution: `shell`, `exec`, `execute-assembly`, `coff`
- Core: checkin, tasking, assembly handling

**Smoke tests** (happy path + one error case):
- Simple system info: `env`, `hostname`, `whoami`, `uptime`, `drives`, `pwd`, `sysinfo`
- Simple utilities: `echo`, `kill`, `jobs`, `jobkill`, `config`
- Network recon: `arp`, `netstat`, `nslookup`
- All remaining plugins

### Tests Per Phase

| Phase | Thorough Tests | Smoke Tests |
|-------|---------------|-------------|
| 0 | Infrastructure only | — |
| 1 | `find`, `file-utils`, `hash`, `cat`, `cp`, `cd`, `mkdir`, `mv`, `rm`, `tail` | — |
| 2 | `sysinfo` | `env`, `hostname`, `whoami`, `uptime`, `drives`, `ps` |
| 3 | `ping`, `dns`, `http-request`, `ssh`, `sftp` | `nslookup`, `test-port`, `arp`, `netstat` |
| 4 | `credentials`, `token`, `keylogger`, `proc-enum`, `kerberos` | `get-clipboard`, `farmer`, `crop`, `clipboard-monitor` |
| 5 | `shell`, `exec`, `execute-assembly`, `coff`, `inject-shellcode` | `kill`, `jobs`, `jobkill`, `shellcode` |
| 6 | `wmi`, `event-log` | `reg`, `config`, `timestomp`, `screenshot`, `bits-transfer` |
| 7 | `jxa`, `keychain` | All macOS wrappers |
| 8 | — | `ds`, `smb`, `socks`, `rportfwd`, `http-server`, `cursed`, `lnk`, `zip-dl`, `zip-inspect` |

---

## Phase Breakdown

### Phase 0: Foundation

**Plugin dependency system refactor:**
- Create `athena_utils/plugin_registry.py`
- Add `depends_on` and `plugin_libraries` attributes to all existing commands
- Refactor `load.py` to use discovery
- Refactor `builder.py` to use discovery
- Remove hardcoded arrays from `plugin_utilities.py`
- **Verification gate:** Build payload with all default commands, build with `ds` selected (verify sub-commands auto-include), load `coff` at runtime (verify BOF commands register), load `nidhogg` (verify sub-commands register), run full test suite

**Testing infrastructure:**
- Create `PluginTestBase` base class
- Create `JobBuilder` fluent helper
- Extend `Utilities.cs` with new helpers
- Add `coverlet.collector` to test project
- Add test category attributes
- Verify CI runs tests on all platforms

### Phase 1: File Operations

**New DLLs (3):** `find`, `file-utils`, `hash`
**Mythic wrappers (11):** `grep`, `suid-find`, `writable-paths`, `stat`, `wc`, `diff`, `touch`, `link`, `chmod`, `chown`, `base64`
**Tests:** Thorough for all 3 new DLLs + existing `cat`, `cp`, `cd`, `mkdir`, `mv`, `rm`, `tail`

### Phase 2: System Info & Enumeration

**New DLLs (1):** `sysinfo`
**Mythic wrappers (5):** `id`, `container-detect`, `mount`, `package-list`, `dotnet-versions`
**Tests:** Thorough for `sysinfo`, smoke for `env`, `hostname`, `whoami`, `uptime`, `drives`, `ps`

### Phase 3: Network Operations

**New DLLs (3):** `ping`, `dns`, `http-request`
**Mythic wrappers (2):** `traceroute`, `wget`
**Tests:** Thorough for all 3 new DLLs + `ssh`, `sftp`. Smoke for `nslookup`, `test-port`, `arp`, `netstat`

### Phase 4: Credentials & Collection

**New DLLs (5):** `credentials`, `proc-enum`, `kerberos`, `clipboard-monitor`, `ssh-recon`
**Mythic wrappers (10):** `dpapi`, `vault-enum`, `wifi-profiles`, `dns-cache`, `shadow-read`, `lsass-dump`, `sam-dump`, `named-pipes`, `ssh-keys`, `authorized-keys`
**Tests:** Thorough for `credentials`, `token`, `keylogger`, `proc-enum`, `kerberos`, `ssh-recon`. Smoke for rest

### Phase 5: Execution & Process

**New DLLs (0):** None — testing only
**Tests:** Thorough for `shell`, `exec`, `execute-assembly`, `coff`, `inject-shellcode`. Smoke for `kill`, `jobs`, `jobkill`, `shellcode`

### Phase 6: Recon, Persistence & Evasion

**New DLLs (3):** `wmi`, `event-log`, `bits-transfer`
**Mythic wrappers (17):** `installed-software`, `defender-status`, `startup-items`, `etw-control`, `crontab`, `sudo-check`, `ld-preload`, `capabilities`, `selinux-status`, `pam-enum`, `iptables-enum`, `firewall-enum`, `uac-check`, `amsi-status`, `rdp-config`, `com-objects`, `proxy-config`
**Tests:** Thorough for `wmi`, `event-log`. Smoke for `reg`, `config`, `timestomp`, `screenshot`, `bits-transfer`

### Phase 7: macOS Platform

**New DLLs (3):** `keychain` (revived), `jxa` (revived), `inject-shellcode-macos`
**Mythic wrappers (8):** `tcc-check`, `launchd-enum`, `spotlight`, `defaults-read`, `osascript`, `login-items`, `security-tool`, `airport`
**Tests:** Thorough for `jxa`, `keychain`. Smoke for all wrappers

### Phase 8: Windows Revived & Remaining Tests

**New DLLs (1):** `lnk` (revived)
**Tests:** Smoke for `ds`, `smb`, `socks`, `rportfwd`, `http-server`, `cursed`, `lnk`, `zip-dl`, `zip-inspect`

---

## Totals

| Metric | Count |
|--------|-------|
| New plugin DLLs | 19 |
| Mythic-only wrappers | 53 |
| Total new Mythic commands | 72 |
| Existing plugins gaining tests | ~40 |
| External NuGet dependencies | 2 (~300KB total) |
| Phases | 9 (0-8) |

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| P/Invoke signatures may be wrong on first attempt | Test on each target platform in CI. Start with well-documented APIs |
| `find` with recursive search could be slow on large filesystems | Max depth default, async enumeration with cancellation token, timeout |
| `credentials` actions (lsass-dump, sam-dump) are high-detection-risk | Document OPSEC considerations. These are opt-in actions, not automatic |
| Plugin registry scan at startup could be slow with many commands | Cache the scan result. Only re-scan on load command |
| macOS P/Invoke (JavaScriptCore, Security.framework) may break across OS versions | Test on minimum supported macOS version. Use stable public API symbols only |
