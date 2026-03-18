# Command Loading Test Plan

**Branch**: `dev-obfuscator-safety-fixes`
**Date**: 2026-03-17
**Goal**: Verify that all commands load and execute correctly in both non-obfuscated (Debug) and obfuscated (Release) builds after the `_ChannelRef` partial-method fix.

---

## Background

Commands are loaded by `AssemblyManager.TryGetModule(name)` which:
1. Scans `AppDomain.CurrentDomain.GetAssemblies()` for any type implementing `IModule`
2. Falls back to `Assembly.Load(new AssemblyName(name))` if not found in the scan

For pre-compiled (bundled) commands, path 1 finds them regardless of obfuscation because:
- `GetAssemblies()` returns all loaded assemblies by object reference (not by name)
- `ParseAssemblyForModule` checks `typeof(IModule).IsAssignableFrom(t)` — works despite type rename
- `plug.Name` is a string constant in source code — the obfuscator preserves string literals

For the `load` command (reflective DLL loading from bytes), assembly bytes are loaded via
`AssemblyLoadContext.LoadFromStream` — no name lookup, so obfuscation is irrelevant.

The `Assembly.Load(new AssemblyName(name))` fallback path is only hit when a command DLL
is NOT pre-compiled into the bundle. In obfuscated builds this path would fail for any
assembly whose name was renamed by the IL obfuscator — but that path shouldn't be hit for
commands built into the payload.

---

## Command Categories

### 1. Built-in (always present, no separate DLL)
| Command | Loaded how |
|---|---|
| `load` | ServiceHost built-in |
| `load-assembly` | ServiceHost built-in |

### 2. Plugin groups (parent DLL + subcommand DLLs loaded together)
| Parent | Subcommands |
|---|---|
| `adws` | adws-connect, adws-query, adws-disconnect |
| `clipboard` | get-clipboard, clipboard-monitor |
| `coff` | nanorubeus, patchit |
| `credentials` | (subcommands within credentials) |
| `dns` | (subcommands) |
| `ds` | ds-connect, ds-query |
| `enum-windows` | (subcommands) |
| `file-utils` | (subcommands) |
| `find` | (subcommands) |
| `hash` | (subcommands) |
| `inject-shellcode` | (subcommands) |
| `jobs` | jobkill |
| `jxa` | (subcommands) |
| `keychain` | (subcommands) |
| `net-enum` | (subcommands) |
| `nidhogg` | nidhogg-hideprocess, nidhogg-hidethread, nidhogg-dumpcreds, etc. |
| `privesc` | (subcommands) |
| `proc-enum` | (subcommands) |
| `recon` | (subcommands) |
| `reg` | (subcommands) |
| `ssh-recon` | (subcommands) |
| `sysinfo` | (subcommands) |
| `wmi` | wmi-exec, wmi-query |
| `zip` | zip-dl, zip-inspect |

### 3. Standalone commands (91 total — each is its own DLL)
Windows-primary: `arp`, `bits-transfer`, `cat`, `cd`, `config`, `cp`, `download`, `drives`,
`echo`, `env`, `event-log`, `exec`, `execute-assembly`, `execute-module`, `exit`, `farmer`,
`get-localgroup`, `get-sessions`, `get-shares`, `hostname`, `http-request`, `http-server`,
`ifconfig`, `inject-shellcode-linux`, `inject-shellcode-macos`, `kill`, `lnk`, `ls`, `mkdir`,
`mv`, `netstat`, `nslookup`, `ping`, `port-bender`, `ps`, `pwd`, `python-exec`, `python-load`,
`rm`, `rportfwd`, `screenshot`, `sftp`, `shell`, `shellcode`, `smb`, `socks`, `ssh`,
`tail`, `test-port`, `timestomp`, `token`, `upload`, `uptime`, `wget`, `whoami`, `wmi`, `zip-dl`

---

## Test Matrix

### Phase 1 — Smoke test: pre-compiled commands in Debug build
**Build**: Debug, no obfuscation, HTTP channel
**Goal**: Confirm basic command execution works at all (channel loading already verified in callback 25).

Build one payload with a representative cross-section of commands. Issue each task and verify
it returns a result (not a "module not found" error).

| Command | Expected output | Priority |
|---|---|---|
| `whoami` | Current user | P0 |
| `shell` cmd=`dir` | Directory listing | P0 |
| `ps` | Process list | P0 |
| `ls` | File listing | P0 |
| `pwd` | Current directory | P0 |
| `hostname` | Machine name | P0 |
| `env` | Environment variables | P1 |
| `uptime` | System uptime | P1 |
| `echo` msg=`hello` | `hello` | P1 |
| `sysinfo` | System information | P1 |
| `netstat` | Network connections | P1 |
| `ifconfig` | Network interfaces | P1 |
| `arp` | ARP table | P1 |
| `drives` | Drive list | P1 |
| `screenshot` | Screenshot bytes | P2 |
| `get-localgroup` | Local groups | P2 |
| `get-shares` | SMB shares | P2 |

### Phase 2 — Plugin group loading in Debug build
Build payload with parent + representative subcommands. Verify subcommands are available.

| Test | Steps | Pass Criteria |
|---|---|---|
| `ds` / `ds-connect` | Issue `ds-connect` | No "module not found" error |
| `wmi` / `wmi-query` | Issue `wmi-query` | Returns output or parameter error (not load error) |
| `zip` / `zip-dl` | Issue `zip-dl` | No "module not found" error |
| `jobs` / `jobkill` | Issue `jobs` | Returns job list |
| `reg` subcommands | Include `reg` | Subcommands appear in Mythic UI |

### Phase 3 — Reflective command loading via `load`
Build payload WITHOUT a specific command pre-compiled in (e.g., exclude `whoami`).
Then use the `load` command to send the `whoami.dll` bytes at runtime.

| Step | Action | Pass Criteria |
|---|---|---|
| 1 | Build payload without `whoami` | Build succeeds |
| 2 | Verify `whoami` command absent | "Failed to fetch command" in Mythic UI |
| 3 | Load `whoami.dll` via `load` command | `load` returns "Loaded" success |
| 4 | Execute `whoami` | Returns current user |

**Repeat for**: `shell`, `ps`, `ls` (representative set)

### Phase 4 — Obfuscated Release: pre-compiled command execution
**Build**: Release, obfuscate=true, HTTP channel, representative command set
**Goal**: Verify the `plug.Name` string-constant approach survives obfuscation.

Repeat Phase 1 test cases on an obfuscated build. Confirm that the `Assembly.Load` fallback
path is NOT needed (scan path finds all pre-compiled commands by type inspection).

Key diagnostic: in the obfuscated Release build's debug log (if debug logging is retained
or added for this test), verify `TryLoadModule: scanning assemblies for <cmd>` finds the
module via `ParseAssemblyForModule` rather than falling back to `Assembly.Load`.

| Command | Obfuscated pass criteria |
|---|---|
| `whoami` | Returns username |
| `shell` | Returns output |
| `ps` | Returns process list |
| `ls` | Returns file listing |
| Any plugin parent | Subcommands are accessible |

### Phase 5 — Obfuscated Release: reflective loading via `load`
Repeat Phase 3 in an obfuscated Release build. The `load` command uses
`AssemblyLoadContext.LoadFromStream` (bytes, not names), so obfuscation should
have no impact — but verify end-to-end.

---

## How to Run

### Build commands
All payload builds go through Mythic. Use `create_payload` with:
```
build_parameters:
  - configuration: Debug (Phase 1-3) / Release (Phase 4-5)
  - trimmed: false
  - obfuscate: false (Phase 1-3) / true (Phase 4-5)
  - output-type: executable
```

### Issuing tasks
Use Mythic UI or MCP `issue_task` for each command. Check response with `get_task_output`.

### Pass/Fail criteria
- **Pass**: Command returns output OR a parameter/permission error (i.e., it loaded but failed due to args/OS)
- **Fail**: "module not found", "IModule not found in assembly", or `ArgumentOutOfRangeException`

---

## Known Pre-existing Issues (not blockers)
- `socks`: `System.Configuration.ConfigurationManager v8.0.0.0` not found — unrelated to loading
- `inject-shellcode-linux`, `inject-shellcode-macos`: Windows-only test machine; expect OS errors, not load errors
- `jxa`, `keychain`, `entitlements`: macOS-only; expect OS errors
- `caffeinate`: macOS-only

---

## Completion Criteria
- [ ] Phase 1: All P0 commands return output in Debug build
- [ ] Phase 2: Plugin group subcommands available (no load errors)
- [ ] Phase 3: `load` command successfully loads and executes 3+ commands
- [ ] Phase 4: All Phase 1 P0 commands pass in obfuscated Release build
- [ ] Phase 5: Reflective loading works in obfuscated Release build
