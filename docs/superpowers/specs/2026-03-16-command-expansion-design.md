# Athena Command Expansion & Consistency Refactors

**Date:** 2026-03-16
**Author:** @checkymander + Claude
**Status:** Draft

## Overview

Expand the Athena C2 agent with new commands (11 net-new + 3 stub completions), enhance ds-query with preset LDAP queries, consolidate 29 standalone plugins into multi-action parent modules, reorganize script_only wrappers into a dedicated subfolder, and update the load/build infrastructure for smart alias resolution.

**Net DLL impact:** -29 eliminated, +5 new parents = **-24 DLLs**

## 1. New Commands (11 net-new + 3 stub completions)

### 1.1 Additions to Existing Parent Modules

#### credentials (existing) — dpapi, vault-enum (stub completions)

**dpapi:** Extract DPAPI-protected secrets (browser cookies, saved credentials).
- Action routing and Python wrapper already exist — implement the method body
- P/Invoke `CryptUnprotectData` from crypt32.dll
- Enumerate `%APPDATA%\Microsoft\Protect\` for master keys
- Decrypt Chrome/Edge cookie DBs and credential stores

**vault-enum:** Enumerate Windows Credential Manager vaults.
- Action routing and Python wrapper already exist — implement the method body
- P/Invoke `VaultOpenVault`, `VaultEnumerateVaults`, `VaultEnumerateItems`, `VaultGetItem` from vaultcli.dll
- List stored credentials (web, Windows, generic)

#### kerberos (existing) — purge (stub completion)

**purge:** Purge Kerberos ticket cache.
- Action routing (`"purge" => PurgeTickets()`) and Python wrapper already exist — implement the `PurgeTickets()` method body
- Uses existing P/Invoke `LsaConnectUntrusted`, `LsaCallAuthenticationPackage` from secur32.dll
- Send `KERB_PURGE_TKT_CACHE_REQUEST` message type

#### wmi (existing) — hotfixes, wmi-exec, av-enum

**hotfixes:** List installed hotfixes.
- WMI query: `SELECT HotFixID, InstalledOn, Description FROM Win32_QuickFixEngineering`
- script_only wrapper creates subtask with `{"action": "hotfixes"}`

**wmi-exec:** Remote command execution via WMI.
- `Win32_Process.Create` on remote host
- Parameters: host, command, username (optional), password (optional)
- script_only wrapper creates subtask with `{"action": "wmi-exec"}`

**av-enum:** Enumerate installed AV/EDR products.
- WMI query: `SELECT * FROM AntiVirusProduct` on `root\SecurityCenter2`
- Also query `SELECT * FROM AntiSpywareProduct`
- script_only wrapper creates subtask with `{"action": "av-enum"}`

#### ds (existing) — acl

**acl:** Read and parse AD object ACLs.
- New action in ds module, reads `nTSecurityDescriptor` attribute
- Parse ACEs via `System.Security.AccessControl.RawSecurityDescriptor`
- Output: trustee, access type, permissions, inherited flag
- script_only wrapper creates subtask with `{"action": "acl"}`

### 1.2 New Parent Modules

#### recon (new) — dns-cache, autologon, rdp-check, always-install-elevated

Host-level reconnaissance actions that read local config/state. Pure .NET, no NuGet.

**dns-cache:** Dump the local DNS resolver cache.
- P/Invoke `DnsGetCacheDataTable` from dnsapi.dll
- **Breaking change:** Moved from credentials module. The `"dns-cache"` case must be removed from `credentials.cs`, and `dns-cache.py` updated from `depends_on = "credentials"` to `depends_on = "recon"`. Operators with `credentials` loaded will need to load `recon` separately for dns-cache.
- script_only wrapper creates subtask with `{"action": "dns-cache"}`

**autologon:** Check for autologon credentials in the registry.
- Read `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon`
- Keys: `DefaultUserName`, `DefaultPassword`, `DefaultDomainName`, `AutoAdminLogon`
- script_only wrapper creates subtask with `{"action": "autologon"}`

**rdp-check:** Check RDP configuration and access.
- Registry: `HKLM\SYSTEM\CurrentControlSet\Control\Terminal Server` — `fDenyTSConnections`
- Registry: `HKLM\SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp` — `UserAuthentication` (NLA)
- Check local group membership for "Remote Desktop Users"
- script_only wrapper creates subtask with `{"action": "rdp-check"}`

**always-install-elevated:** Check for AlwaysInstallElevated misconfiguration.
- Registry: `HKLM\SOFTWARE\Policies\Microsoft\Windows\Installer` — `AlwaysInstallElevated`
- Registry: `HKCU\SOFTWARE\Policies\Microsoft\Windows\Installer` — `AlwaysInstallElevated`
- Both must be set to 1 for the vulnerability to exist
- script_only wrapper creates subtask with `{"action": "always-install-elevated"}`

#### privesc (new) — privcheck, service-enum

Privilege escalation checks. Pure .NET, no NuGet.

**privcheck:** Enumerate token privileges and identify escalation opportunities.
- P/Invoke `OpenProcessToken`, `GetTokenInformation` from advapi32.dll
- Check for: SeImpersonatePrivilege, SeAssignPrimaryTokenPrivilege, SeDebugPrivilege, SeBackupPrivilege, SeRestorePrivilege, SeTakeOwnershipPrivilege, SeLoadDriverPrivilege
- Flag enabled vs disabled vs removed
- Also check: UAC level, integrity level, is admin
- script_only wrapper creates subtask with `{"action": "privcheck"}`

**service-enum:** Enumerate services and check for misconfigurations.
- P/Invoke `OpenSCManager`, `EnumServicesStatusEx`, `QueryServiceConfig`, `QueryServiceObjectSecurity` from advapi32.dll
- Check for: unquoted service paths, writable service binaries, modifiable service configs
- Output: service name, display name, state, start type, binary path, flags for misconfigs
- script_only wrapper creates subtask with `{"action": "service-enum"}`

#### adws (new) — connect, query, disconnect

Active Directory Web Services enumeration via SOAP over TCP:9389. Requires `System.ServiceModel.NetTcp` NuGet package for the net.tcp WCF transport binding.

**connect:** Establish ADWS connection to a domain controller.
- SOAP endpoint: `net.tcp://<dc>:9389/ActiveDirectoryWebServices/Windows`
- Uses `System.ServiceModel.NetTcpBinding` for WCF client connection
- Store connection state (similar to ds module pattern)
- script_only wrapper creates subtask with `{"action": "connect"}`

**query:** Execute ADWS SOAP queries.
- Enumerate request using ADWS protocol
- Support for property selection and LDAP filter translation
- script_only wrapper creates subtask with `{"action": "query"}`

**disconnect:** Close ADWS connection.
- script_only wrapper creates subtask with `{"action": "disconnect"}`

## 2. ds-query Preset Parameter Group

Add a "Preset" parameter group to `ds-query.py` alongside the existing "Default" freeform group.

### Mythic UI Behavior

Operator sees two tabs:
- **Default** — existing freeform: ldapfilter, objectcategory, searchbase, properties
- **Preset** — single dropdown + optional searchbase

### Preset Mappings

Resolved in `create_go_tasking` before creating the ds subtask:

| Preset Label | LDAP Filter | Properties |
|---|---|---|
| Kerberoastable Users | `(&(objectClass=user)(servicePrincipalName=*)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))` | sAMAccountName, servicePrincipalName, memberOf |
| AS-REP Roastable | `(&(objectClass=user)(userAccountControl:1.2.840.113556.1.4.803:=4194304))` | sAMAccountName, userAccountControl, memberOf |
| Unconstrained Delegation | `(&(objectClass=computer)(userAccountControl:1.2.840.113556.1.4.803:=524288))` | sAMAccountName, dNSHostName, userAccountControl |
| Constrained Delegation | `(msDS-AllowedToDelegateTo=*)` | sAMAccountName, objectClass, msDS-AllowedToDelegateTo |
| RBCD Targets | `(msDS-AllowedToActOnBehalfOfOtherIdentity=*)` | sAMAccountName, msDS-AllowedToActOnBehalfOfOtherIdentity |
| Domain Admins | `(&(objectClass=group)(cn=Domain Admins))` | member |
| LAPS Passwords | `(ms-Mcs-AdmPwd=*)` | sAMAccountName, ms-Mcs-AdmPwd, ms-Mcs-AdmPwdExpirationTime |
| GPO List | `(objectClass=groupPolicyContainer)` | displayName, gPCFileSysPath, gPCMachineExtensionNames |
| Disabled Accounts | `(&(objectClass=user)(userAccountControl:1.2.840.113556.1.4.803:=2))` | sAMAccountName, userAccountControl, whenChanged |
| AdminSDHolder Protected | `(adminCount=1)` | sAMAccountName, objectClass, memberOf |

The "Preset" group includes an optional `searchbase` parameter (defaults to domain root if empty).

## 3. Consolidation Refactors

### Pattern

For each consolidation:
1. C# code moves into the parent module's switch expression as a new action
2. Old C# project folder + .csproj is deleted
3. A `script_only = True` Python wrapper is created (or updated) in `agent_functions/script_only/`
4. Each wrapper creates a subtask with `{"action": "<command-name>"}` routed to the parent

Operator-facing command names do not change.

**Important:** Most commands being absorbed are currently standalone C# plugins with their own `create_go_tasking` in Python — they are NOT already script_only wrappers. Converting them means:
- The C# logic moves into the parent's switch expression
- The existing Python command file is rewritten as a thin `script_only` wrapper
- **Parameter handling must be audited per-command.** Some commands have complex `create_go_tasking` logic (file uploads, display params, multiple parameter groups) that must be preserved in the new wrapper. Commands with complex parameter handling are flagged in the notes below.

### 3.1 Absorptions into Existing Parents

| Parent Module | Commands Absorbed | New Actions |
|---|---|---|
| `sysinfo` | whoami, hostname, uptime, env, drives | whoami, hostname, uptime, env, drives |
| `file-utils` | cat, cp, mv, rm, mkdir, tail, timestomp | cat, cp, mv, rm, mkdir, tail, timestomp |
| `proc-enum` | ps | ps |
| `jobs` | jobkill | kill |
| `zip` | zip-dl, zip-inspect | download, inspect |
| `clipboard` (rename from `get-clipboard`) | clipboard-monitor | monitor |
| `dns` | nslookup | bulk |

**Note on jobs:** Currently does not use the action-switch pattern (it directly lists jobs with no arg deserialization). Refactor to add args deserialization and switch expression before absorbing jobkill as the `kill` action.

**Note on zip + zip-dl (IFileModule):** `zip.cs` implements `IModule`; `zip-dl.cs` implements `IFileModule` (chunked downloads via `HandleNextMessage`). The consolidated `zip` module must implement both `IModule` and `IFileModule`. The `HandleNextMessage` method only activates for the `download` action. The `TaskManager` resolves `IFileModule` by command name — since the parent is still named `zip`, this works. Actions that don't need chunked transfer simply use the `IModule.Execute` path.

**Note on nslookup:** Has complex parameter handling — two parameter groups ("Default" with comma-separated hosts, "TargetList" with file upload via `get_mythic_file`). The script_only wrapper must preserve this file-handling logic in `create_go_tasking` before dispatching the subtask.

**Note on clipboard:** The current standalone module is named `get-clipboard`. Rename to `clipboard` as the parent, absorb `clipboard-monitor` as the `monitor` action, keep existing clipboard read as the `get` action.

### 3.2 New Parent Modules (from consolidation)

| Parent Module | Commands Absorbed | Actions |
|---|---|---|
| `net-enum` (new) | ping, ifconfig, netstat, arp, test-port | ping, ifconfig, netstat, arp, test-port |
| `enum-windows` (new) | get-localgroup, get-sessions, get-shares | get-localgroup, get-sessions, get-shares |
| `inject-shellcode` (existing, becomes parent) | inject-shellcode-linux, inject-shellcode-macos | Platform-specific injection absorbed into single module with runtime OS detection |

**Note on inject:** `inject-shellcode` is already the Windows implementation and the operator-facing command. `inject-shellcode-linux` and `inject-shellcode-macos` are platform-specific variants dispatched by the existing `inject-shellcode.py` wrapper. Consolidation merges all three C# implementations into a single `inject-shellcode` module that uses `RuntimeInformation.IsOSPlatform()` to select the correct injection technique at runtime. The `-linux` and `-macos` folders are deleted.

### 3.3 Deletion

**http-request** — deleted entirely. `wget` covers the same functionality with HttpClient/WebRequest.

### 3.4 DLL Impact

| Source | DLLs Eliminated |
|---|---|
| Section 3.1: sysinfo(5) + file-utils(7) + proc-enum(1) + jobs(1) + zip(2) + clipboard(1) + dns(1) | 18 |
| Section 3.2: net-enum(5) + enum-windows(3) + inject(2) | 10 |
| Section 3.3: http-request deleted | 1 |
| **Total eliminated** | **29** |
| New parent DLLs (recon, privesc, adws, net-enum, enum-windows) | +5 |
| **Net reduction** | **-24 DLLs** |

## 4. Folder Organization

### script_only/ Subfolder

All `script_only = True` Python wrappers move into `agent_functions/script_only/` with an `__init__.py`, mirroring the BOF subfolder pattern:

```
agent_functions/
    script_only/
        __init__.py
        cat.py              (depends_on: file-utils)
        cp.py               (depends_on: file-utils)
        whoami.py            (depends_on: sysinfo)
        hostname.py          (depends_on: sysinfo)
        dpapi.py             (depends_on: credentials)
        vault-enum.py        (depends_on: credentials)
        ping.py              (depends_on: net-enum)
        privcheck.py         (depends_on: privesc)
        ...
    outflank_bofs/
    trusted_sec_bofs/
    trusted_sec_remote_bofs/
    misc_bofs/
    load.py                 (builtin, stays in root)
    load-assembly.py        (builtin, stays in root)
    ds.py                   (parent module, stays in root)
    credentials.py          (parent module, stays in root)
    wmi.py                  (parent module, stays in root)
    ...
```

**What moves:** All existing and new `script_only = True` wrappers (~100+ commands).

**What stays in root:** Parent module commands (with their own C# plugin), builtins (load, load-assembly, exit), and any command that maps 1:1 to a standalone C# module.

**Discovery:** Mythic auto-discovers commands recursively from `agent_functions/`. The `__init__.py` file is empty (matching the BOF subfolder convention). No explicit imports needed.

## 5. Plugin Registry, Load, and Builder UX Updates

### 5.1 plugin_registry.py

No structural changes needed. The existing API handles all new wrappers:
- `get_parent(command)` — returns `depends_on` value
- `get_subcommands(plugin)` — returns all commands with `depends_on=plugin`
- `get_libraries(command)` — returns plugin NuGet libraries
- `is_subcommand(command)` — identifies wrapped commands

### 5.2 load.py — Smart Alias Resolution

**Current behavior:** Rejects loading subcommands with "Please load {parent} to enable this command."

**New behavior:**

```
1. Operator runs: load cat
2. get_parent("cat") returns "file-utils"
3. Auto-translate: compile and load file-utils
4. Register all sibling subcommands via SendMythicRPCCallbackAddCommand
5. Report: "Loaded file-utils (provides: cat, cp, mv, rm, ...)"

6. Operator later runs: load cp
7. get_parent("cp") returns "file-utils"
8. Detect file-utils already loaded (subcommands already registered)
9. Report: "file-utils is already loaded (provides cp)"
```

**Already-loaded detection:** Use `SendMythicRPCCallbackAddCommand` for the parent's subcommands. If the commands are already registered, Mythic returns success without duplication. To detect and report "already loaded" to the operator, track loaded parents in the task's callback metadata or attempt `SendMythicRPCCallbackAddCommand` and check if the parent command itself is already present. The exact RPC call for querying registered commands should be confirmed during implementation against the Mythic RPC API.

**Direct parent load:** `load file-utils` works as before — compile, load, register subcommands.

### 5.3 builder.py — Build-Time Alias Resolution

**Current behavior:** Iterates selected commands, skips subcommands (in `unloadable_commands`), auto-includes subcommands of selected parents.

**New behavior:** Add a parent-resolution pass before `.targets` generation:

```
1. Operator selects "cat" in payload build UI
2. Resolve: get_parent("cat") = "file-utils"
3. Add file-utils.csproj to MSBuild .targets (deduplicate if already present)
4. Register all sibling subcommands on the callback
```

### 5.4 Flow Summary

| Operator Action | Resolution | Result |
|---|---|---|
| `load cat` | get_parent("cat") = file-utils | Compile & load file-utils, register all subs |
| `load cp` (after cat) | get_parent("cp") = file-utils | "file-utils already loaded" |
| `load file-utils` | No parent (IS the parent) | Compile & load file-utils, register all subs |
| Build with cat selected | Resolve to file-utils.csproj | Include file-utils + register subs |
| Build with file-utils | Already a parent | Include file-utils + register subs |

## 6. Implementation Notes

### Dependencies

All new commands except `adws` are pure .NET 10. NuGet deps:
- `wmi` actions use `System.Management` (already referenced)
- `ds` acl action uses `System.DirectoryServices.Protocols` (already referenced)
- `adws` requires new NuGet: `System.ServiceModel.NetTcp` for WCF net.tcp transport

### P/Invoke Conventions

New P/Invoke declarations follow the existing `Native.cs` convention:
- Separate static class per module (e.g., `ReconNative.cs`, `PrivescNative.cs`)
- Use `LibraryImport` where possible (.NET 7+ source-generated)
- Fall back to `DllImport` for complex marshalling

### Known Bug to Fix

`ds.cs:122-132` `GetLdapDirectoryIdentifier` has inverted logic: when `args.server` is empty, it passes the empty string to the constructor instead of using the `domain` fallback. Fix this during the ds module changes for the `acl` action.

### Testing

Each new action should have basic validation:
- Argument deserialization
- Error handling for missing privileges / unsupported OS
- Graceful failure on non-Windows platforms where applicable (recon, privesc, enum-windows are Windows-only)
