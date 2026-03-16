# Command Expansion & Consolidation Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 14 new commands, consolidate 29 standalone plugins into multi-action parents (-24 net DLLs), reorganize script_only wrappers, and update load/builder UX.

**Architecture:** Standalone C# plugins are absorbed into multi-action parent modules dispatched by an `action` field. Thin `script_only` Python wrappers create subtasks routed to the parent. New commands slot into existing or new parent modules.

**Tech Stack:** .NET 10 (C#), Python (Mythic container), MSBuild, P/Invoke

**Spec:** `docs/superpowers/specs/2026-03-16-command-expansion-design.md`

---

## Reference Patterns

These patterns are used throughout. Read once, apply everywhere.

### C# Parent Module Pattern (e.g., sysinfo.cs)

```csharp
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "parent-name";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<parentname.ParentArgs>(
                job.task.parameters);

            if (args is null)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "Failed to deserialize arguments.",
                    task_id = job.task.id,
                    status = "error"
                });
                return;
            }

            try
            {
                string result = args.action switch
                {
                    "action-one" => DoActionOne(),
                    "action-two" => DoActionTwo(args),
                    _ => throw new ArgumentException(
                        $"Unknown action: {args.action}")
                };

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = result,
                    task_id = job.task.id,
                });
            }
            catch (Exception e)
            {
                messageManager.Write(
                    e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
```

### C# Args Pattern (e.g., SysinfoArgs.cs)

```csharp
namespace parentname
{
    public class ParentArgs
    {
        public string action { get; set; } = "default-action";
    }
}
```

### script_only Python Wrapper Pattern (lives in script_only/ subfolder)

**Important:** Files in `script_only/` are one level deeper than `agent_functions/`, so imports use `..` (parent package):

```python
from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class MyCommandArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class MyCommandCommand(CommandBase):
    cmd = "my-command"
    needs_admin = False
    script_only = True
    depends_on = "parent-name"
    plugin_libraries = []
    help_cmd = "my-command"
    description = "Description here"
    version = 1
    author = "@checkymander"
    argument_class = MyCommandArguments
    attackmapping = ["TXXXX"]
    attributes = CommandAttributes()
    completion_functions = {
        "command_callback": default_completion_callback
    }

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="parent-name",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"action": "my-command"})
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
```

### Parent Python Command Pattern (e.g., credentials.py)

```python
class ParentCommand(CommandBase):
    cmd = "parent-name"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    # ... standard fields ...
    # Args include ChooseOne with all action names
    # create_go_tasking sets DisplayParams, returns response
```

---

## Chunk 1: Infrastructure — Folder Setup & Load/Builder UX

### Task 1.1: Create script_only/ subfolder

**Files:**
- Create: `Payload_Type/athena/athena/mythic/agent_functions/script_only/__init__.py`

- [ ] **Step 1:** Create the directory and empty `__init__.py`

```python
# Empty file — Mythic auto-discovers commands recursively
```

- [ ] **Step 2:** Verify Mythic/plugin_registry discovery works from subfolders

Read `athena_utils/plugin_registry.py` to confirm how it discovers `CommandBase` subclasses. Mythic's `MythicCommandBase` auto-discovers via Python package imports — subfolders with `__init__.py` are included automatically. Verify by checking that the BOF subfolders (`outflank_bofs/`, `trusted_sec_bofs/`, etc.) already work this way.

**Critical check:** If `plugin_registry.py` scans only the root `agent_functions/` directory explicitly (not using Mythic's auto-discovery), it will need to be updated to also scan `script_only/`. Read the init code to confirm.

- [ ] **Step 3:** Commit

```bash
git add Payload_Type/athena/athena/mythic/agent_functions/script_only/__init__.py
git commit -m "chore: create script_only subfolder for wrapper commands"
```

### Task 1.2: Update load.py — Smart Alias Resolution

**Files:**
- Modify: `Payload_Type/athena/athena/mythic/agent_functions/load.py:104-114`

- [ ] **Step 1:** Read the current `create_go_tasking` method in load.py

- [ ] **Step 2:** Replace the parent rejection block (lines 104-114) with auto-translation logic

Replace this:
```python
command = taskData.args.get_arg('command')

parent = plugin_registry.get_parent(command)
if parent:
    await message_utilities.send_agent_message(
        f"Please load {parent} to enable this command",
        taskData.Task
    )
    raise Exception(
        f"Please load {parent} to enable this command"
    )
```

With this:
```python
command = taskData.args.get_arg('command')

parent = plugin_registry.get_parent(command)
if parent:
    command = parent
```

**Flow after this change:** When `command` is a subcommand (e.g., "cat"), it's reassigned to the parent ("file-utils"). Execution then falls through to the existing compile-and-load logic (lines 116+), which:
1. Looks up `command_libraries` and `subcommands` for the resolved parent name
2. Compiles the parent plugin
3. Registers all subcommands via `SendMythicRPCCallbackAddCommand`

This means `load cat` transparently compiles and loads `file-utils`. No special "already loaded" check is needed in this block — if the DLL is already loaded on the agent, the agent handles the duplicate gracefully. The operator sees the parent was loaded with all its subcommands.

**For the "already loaded" UX improvement** (optional, can defer): Track loaded parents in a module-level `set()` in load.py. Before compiling, check if `command` is in the set. If so, return early with an informational message. This avoids recompilation but is not strictly necessary for correctness.

- [ ] **Step 3:** Also update the `command_libraries` and `subcommands` lookups (lines 116-171) to use the resolved `command` variable (already correct if we reassigned `command = parent` above)

- [ ] **Step 4:** Update the success message to report what was loaded

After the existing subcommands registration block (~line 161-171), add:
```python
sub_list = ", ".join(subcommands) if subcommands else "none"
await message_utilities.send_agent_message(
    f"Loaded {command} (provides: {sub_list})",
    taskData.Task
)
```

- [ ] **Step 5:** Commit

```bash
git add Payload_Type/athena/athena/mythic/agent_functions/load.py
git commit -m "feat: load.py auto-translates subcommands to parent module"
```

### Task 1.3: Update builder.py — Build-Time Alias Resolution

**Files:**
- Modify: `Payload_Type/athena/athena/mythic/agent_functions/builder.py` (loop starts ~line 501, `unloadable_commands` at ~line 496)

- [ ] **Step 1:** Read the current command iteration loop in builder.py

- [ ] **Step 2:** Add a parent-resolution pass before the `.targets` generation loop

Before the `for cmd in list(self.commands.get_commands()):` loop, add:
```python
# Resolve subcommands to their parent modules
resolved_parents = set()
for cmd in list(self.commands.get_commands()):
    parent = plugin_registry.get_parent(cmd)
    if parent:
        resolved_parents.add(parent)

# Add resolved parents and their subcommands
for parent in resolved_parents:
    self.commands.add_command(parent)
    for sub in plugin_registry.get_subcommands(parent):
        self.commands.add_command(sub)
```

- [ ] **Step 3:** Verify the existing loop's `unloadable_commands` skip still works (subcommands are skipped for .csproj references but registered on the callback)

- [ ] **Step 4:** Commit

```bash
git add Payload_Type/athena/athena/mythic/agent_functions/builder.py
git commit -m "feat: builder.py resolves subcommand selections to parent modules"
```

### Task 1.4: Move existing script_only wrappers to script_only/ folder

All pre-existing `script_only = True` wrappers currently in `agent_functions/` root need to move to `agent_functions/script_only/` BEFORE new wrappers are created, to avoid an inconsistent state.

- [ ] **Step 1:** Find all existing script_only wrappers

```bash
grep -rl "script_only = True" Payload_Type/athena/athena/mythic/agent_functions/*.py
```

- [ ] **Step 2:** Move each file to `script_only/` folder. Use `git mv` to preserve history.

- [ ] **Step 3:** Update the import in EACH moved file

Change `from .athena_utils` to `from ..athena_utils` since they're now one level deeper. This applies to both:
- `from .athena_utils.plugin_utilities import default_completion_callback`
- Any other `from .athena_utils` imports

- [ ] **Step 4:** Verify no duplicate files remain in root. Verify Mythic container still discovers the moved commands (if possible, run the container).

- [ ] **Step 5:** Commit

```bash
git add -A
git commit -m "refactor: move all existing script_only wrappers to script_only/ subfolder"
```

---

## Chunk 2: Consolidation — sysinfo & file-utils Families

These are the two largest consolidations (5 + 7 commands).

### Task 2.1: Absorb whoami, hostname, uptime, env, drives into sysinfo

**Files:**
- Modify: `Payload_Type/athena/athena/agent_code/sysinfo/sysinfo.cs`
- Modify: `Payload_Type/athena/athena/agent_code/sysinfo/SysinfoArgs.cs` (if additional args needed)
- Modify: `Payload_Type/athena/athena/mythic/agent_functions/sysinfo.py` (update action choices)
- Create: `Payload_Type/athena/athena/mythic/agent_functions/script_only/whoami.py`
- Create: `Payload_Type/athena/athena/mythic/agent_functions/script_only/hostname.py`
- Create: `Payload_Type/athena/athena/mythic/agent_functions/script_only/uptime.py`
- Create: `Payload_Type/athena/athena/mythic/agent_functions/script_only/env.py`
- Create: `Payload_Type/athena/athena/mythic/agent_functions/script_only/drives.py`
- Delete: `Payload_Type/athena/athena/agent_code/whoami/` (entire folder)
- Delete: `Payload_Type/athena/athena/agent_code/hostname/` (entire folder)
- Delete: `Payload_Type/athena/athena/agent_code/uptime/` (entire folder)
- Delete: `Payload_Type/athena/athena/agent_code/env/` (entire folder)
- Delete: `Payload_Type/athena/athena/agent_code/drives/` (entire folder)
- Delete: `Payload_Type/athena/athena/mythic/agent_functions/whoami.py` (old standalone)
- Delete: `Payload_Type/athena/athena/mythic/agent_functions/hostname.py`
- Delete: `Payload_Type/athena/athena/mythic/agent_functions/uptime.py`
- Delete: `Payload_Type/athena/athena/mythic/agent_functions/env.py`
- Delete: `Payload_Type/athena/athena/mythic/agent_functions/drives.py`

- [ ] **Step 1:** Read each standalone C# module to extract the logic:
  - `agent_code/whoami/Whoami.cs` — returns `$"{Environment.UserDomainName}\\{Environment.UserName}"`. **Note:** whoami.cs injects `ICredentialProvider` (tokenManager) via PluginContext. If sysinfo.cs doesn't already inject this, add it — token impersonation may affect the whoami result.
  - `agent_code/hostname/hostname.cs` — returns hostname
  - `agent_code/uptime/uptime.cs` — returns system uptime
  - `agent_code/env/env.cs` — returns environment variables
  - `agent_code/drives/drives.cs` — returns drive info

- [ ] **Step 2:** Read each standalone Python wrapper to audit parameters:
  - `agent_functions/whoami.py`, `hostname.py`, `uptime.py`, `env.py`, `drives.py`
  - Check for any complex `create_go_tasking` logic, parameter groups, or file handling

- [ ] **Step 3:** Add new actions to sysinfo.cs switch expression

Read `sysinfo.cs` first, then add cases. The existing switch has: sysinfo, id, container-detect, mount, package-list, dotnet-versions. Add:

```csharp
"whoami" => GetWhoami(),
"hostname" => GetHostname(),
"uptime" => GetUptime(),
"env" => GetEnv(),
"drives" => GetDrives(),
```

Each method copies the logic from the standalone module's `Execute` method, returning a string.

- [ ] **Step 4:** Update sysinfo.py to include new action choices

Add `"whoami"`, `"hostname"`, `"uptime"`, `"env"`, `"drives"` to the `choices` list in the `action` parameter.

- [ ] **Step 5:** Create script_only wrappers in `script_only/` folder

Create `script_only/whoami.py` following the wrapper pattern:
- `cmd = "whoami"`, `depends_on = "sysinfo"`, `script_only = True`
- `Params=json.dumps({"action": "whoami"})`
- `attackmapping` — copy from the original standalone wrapper
- `description` — copy from the original standalone wrapper

Repeat for hostname, uptime, env, drives.

- [ ] **Step 6:** Delete old standalone C# folders and Python wrappers

```bash
npx trash-cli Payload_Type/athena/athena/agent_code/whoami
npx trash-cli Payload_Type/athena/athena/agent_code/hostname
npx trash-cli Payload_Type/athena/athena/agent_code/uptime
npx trash-cli Payload_Type/athena/athena/agent_code/env
npx trash-cli Payload_Type/athena/athena/agent_code/drives
npx trash-cli Payload_Type/athena/athena/mythic/agent_functions/whoami.py
npx trash-cli Payload_Type/athena/athena/mythic/agent_functions/hostname.py
npx trash-cli Payload_Type/athena/athena/mythic/agent_functions/uptime.py
npx trash-cli Payload_Type/athena/athena/mythic/agent_functions/env.py
npx trash-cli Payload_Type/athena/athena/mythic/agent_functions/drives.py
```

- [ ] **Step 7:** Verify `dotnet build` succeeds for sysinfo project

```bash
cd Payload_Type/athena/athena/agent_code/sysinfo && dotnet build -c Release
```

- [ ] **Step 8:** Commit

```bash
git add -A
git commit -m "refactor: absorb whoami, hostname, uptime, env, drives into sysinfo"
```

### Task 2.2: Absorb cat, cp, mv, rm, mkdir, tail, timestomp into file-utils

**Files:** Same pattern as Task 2.1 but for file-utils.
- Modify: `Payload_Type/athena/athena/agent_code/file-utils/file-utils.cs`
- Modify: `Payload_Type/athena/athena/mythic/agent_functions/file-utils.py`
- Create: 7 wrappers in `script_only/` (cat.py, cp.py, mv.py, rm.py, mkdir.py, tail.py, timestomp.py)
- Delete: 7 C# folders + 7 Python wrappers from root

- [ ] **Step 1:** Read each standalone C# module to extract logic:
  - `agent_code/cat/cat.cs`, `agent_code/cp/cp.cs`, `agent_code/mv/mv.cs`
  - `agent_code/rm/rm.cs`, `agent_code/mkdir/mkdir.cs`, `agent_code/tail/tail.cs`
  - `agent_code/timestomp/timestomp.cs`

- [ ] **Step 2:** Read each Python wrapper to audit parameters

**Critical:** Some file commands likely have parameters (path, source, destination, etc.). These must be preserved. For commands with parameters:
- The script_only wrapper keeps the `CommandParameter` definitions
- Parameters are passed through to the subtask `Params` JSON alongside `action`
- Example: `Params=json.dumps({"action": "cat", "path": taskData.args.get_arg("path")})`

- [ ] **Step 3:** Add new actions to file-utils.cs switch expression

Read `file-utils.cs` first. Existing actions: head, touch, wc, diff, link, chmod, chown. Add:

```csharp
"cat" => Cat(args),
"cp" => Copy(args),
"mv" => Move(args),
"rm" => Remove(args),
"mkdir" => MakeDir(args),
"tail" => Tail(args),
"timestomp" => Timestomp(args),
```

- [ ] **Step 4:** Update the file-utils args class to include any new fields needed by absorbed commands

- [ ] **Step 5:** Update file-utils.py action choices

- [ ] **Step 6:** Create 7 script_only wrappers, preserving parameter definitions

- [ ] **Step 7:** Delete old standalone C# folders and Python wrappers

- [ ] **Step 8:** Verify `dotnet build` succeeds for file-utils project

- [ ] **Step 9:** Commit

```bash
git add -A
git commit -m "refactor: absorb cat, cp, mv, rm, mkdir, tail, timestomp into file-utils"
```

---

## Chunk 3: Consolidation — Remaining Groups

### Task 3.1: Absorb ps into proc-enum

**Files:**
- Modify: `agent_code/proc-enum/proc-enum.cs` (add "ps" action)
- Modify: `agent_functions/proc-enum.py` (add "ps" to choices)
- Create: `agent_functions/script_only/ps.py`
- Delete: `agent_code/ps/` folder, `agent_functions/ps.py`

- [ ] **Step 1:** Read `agent_code/ps/ps.cs` and `agent_functions/ps.py` — audit logic and parameters
- [ ] **Step 2:** Read `agent_code/proc-enum/proc-enum.cs` — understand existing switch
- [ ] **Step 3:** Add "ps" action to proc-enum.cs, copy process listing logic
- [ ] **Step 4:** Update proc-enum.py choices, create `script_only/ps.py` wrapper
- [ ] **Step 5:** Delete old ps/ folder and agent_functions/ps.py
- [ ] **Step 6:** Build verify, commit

```bash
git commit -m "refactor: absorb ps into proc-enum"
```

### Task 3.2: Refactor jobs + absorb jobkill

**Files:**
- Modify: `agent_code/jobs/jobs.cs` (add args deserialization + switch)
- Create: `agent_code/jobs/JobsArgs.cs`
- Modify: `agent_functions/jobs.py` (add action parameter)
- Create: `agent_functions/script_only/jobkill.py`
- Delete: `agent_code/jobkill/` folder, `agent_functions/jobkill.py`

- [ ] **Step 1:** Read `agent_code/jobs/jobs.cs` — currently no args, no switch
- [ ] **Step 2:** Read `agent_code/jobkill/jobkill.cs` and `agent_functions/jobkill.py` — audit kill logic and parameters (likely takes a task_id param)
- [ ] **Step 3:** Create `JobsArgs.cs` with `action` and `task_id` fields
- [ ] **Step 4:** Refactor jobs.cs to deserialize args, add switch: "list" (default, existing logic), "kill" (from jobkill)
- [ ] **Step 5:** Update jobs.py with action parameter, create `script_only/jobkill.py`
- [ ] **Step 6:** Delete old jobkill/ folder and agent_functions/jobkill.py
- [ ] **Step 7:** Build verify, commit

```bash
git commit -m "refactor: absorb jobkill into jobs with action dispatch"
```

### Task 3.3: Absorb zip-dl, zip-inspect into zip

**Files:**
- Modify: `agent_code/zip/zip.cs` (implement both IModule and IFileModule, add actions)
- Modify: `agent_functions/zip.py`
- Create: `agent_functions/script_only/zip-dl.py`, `agent_functions/script_only/zip-inspect.py`
- Delete: `agent_code/zip-dl/`, `agent_code/zip-inspect/`, corresponding Python files

**Special:** zip must implement both `IModule` and `IFileModule` since zip-dl uses chunked downloads.

- [ ] **Step 1:** Read `agent_code/zip/zip.cs`, `agent_code/zip-dl/zip-dl.cs`, `agent_code/zip-inspect/zip-inspect.cs`
- [ ] **Step 2:** Read all three Python wrappers for parameter audit
- [ ] **Step 2.5:** Read the TaskManager dispatch code to verify how `IFileModule` is resolved. Search for `IFileModule` usage in the Agent code (likely `TaskManager.cs` or similar). Confirm whether it dispatches by task command name or by `Plugin.Name`. This determines whether the consolidated zip module correctly receives `HandleNextMessage` calls.
- [ ] **Step 3:** Refactor zip.cs to implement `IModule, IFileModule`, add switch for: "compress" (existing), "download" (from zip-dl), "inspect" (from zip-inspect)
- [ ] **Step 4:** Copy `HandleNextMessage`, `TryHandleNextChunk`, `CompleteDownloadJob`, `GetJob` from zip-dl.cs into zip.cs (these support the download action)
- [ ] **Step 5:** Update zip.py, create script_only wrappers (preserve zip-dl's parameter groups)
- [ ] **Step 6:** Delete old folders
- [ ] **Step 7:** Build verify, commit

```bash
git commit -m "refactor: absorb zip-dl, zip-inspect into zip (IModule + IFileModule)"
```

### Task 3.4: Rename get-clipboard to clipboard, absorb clipboard-monitor

**Files:**
- Rename: `agent_code/get-clipboard/` → `agent_code/clipboard/`
- Modify: clipboard.cs (rename, add switch, "get" + "monitor" actions)
- Modify/Create: `agent_functions/clipboard.py` (rename from get-clipboard.py)
- Create: `agent_functions/script_only/get-clipboard.py` (wrapper for "get" action)
- Create: `agent_functions/script_only/clipboard-monitor.py` (wrapper for "monitor" action)
- Delete: `agent_code/clipboard-monitor/`, `agent_functions/clipboard-monitor.py`, `agent_functions/get-clipboard.py`

- [ ] **Step 1:** Read get-clipboard C# and clipboard-monitor C# modules
- [ ] **Step 2:** Create new `agent_code/clipboard/` with renamed/refactored code
- [ ] **Step 3:** Add switch: "get" (existing get-clipboard logic), "monitor" (from clipboard-monitor)
- [ ] **Step 4:** Create parent `clipboard.py` and both script_only wrappers
- [ ] **Step 5:** Delete old folders and files
- [ ] **Step 6:** Build verify, commit

```bash
git commit -m "refactor: rename get-clipboard to clipboard, absorb clipboard-monitor"
```

### Task 3.5: Absorb nslookup into dns

**Files:**
- Modify: `agent_code/dns/dns.cs` (add "bulk" action from nslookup logic)
- Modify: `agent_functions/dns.py` (add action choices)
- Create: `agent_functions/script_only/nslookup.py`
- Delete: `agent_code/nslookup/`, `agent_functions/nslookup.py`

**Special:** nslookup.py has two parameter groups (Default + TargetList with file upload). The script_only wrapper must preserve these parameter groups and file-handling logic.

- [ ] **Step 1:** Read `agent_code/nslookup/nslookup.cs` and `agent_functions/nslookup.py` carefully
- [ ] **Step 2:** Read `agent_code/dns/dns.cs` and `agent_functions/dns.py`
- [ ] **Step 3:** Add "bulk" action to dns.cs with nslookup's multi-host resolution and file-based target list support
- [ ] **Step 4:** Create `script_only/nslookup.py` preserving both parameter groups. In `create_go_tasking`, handle file upload (read file content via `SendMythicRPCFileGetContent`), then pass targets to subtask
- [ ] **Step 5:** Delete old nslookup/ folder and standalone nslookup.py
- [ ] **Step 6:** Build verify, commit

```bash
git commit -m "refactor: absorb nslookup into dns as bulk action"
```

### Task 3.6: Create net-enum parent (ping, ifconfig, netstat, arp, test-port)

**Files:**
- Create: `agent_code/net-enum/net-enum.cs`
- Create: `agent_code/net-enum/net-enum.csproj`
- Create: `agent_code/net-enum/NetEnumArgs.cs`
- Create: `agent_functions/net-enum.py`
- Create: `agent_functions/script_only/ping.py`
- Create: `agent_functions/script_only/ifconfig.py`
- Create: `agent_functions/script_only/netstat.py`
- Create: `agent_functions/script_only/arp.py`
- Create: `agent_functions/script_only/test-port.py`
- Delete: `agent_code/ping/`, `agent_code/ifconfig/`, `agent_code/netstat/`, `agent_code/arp/`, `agent_code/test-port/`
- Delete: `agent_functions/ping.py`, `agent_functions/ifconfig.py`, `agent_functions/netstat.py`, `agent_functions/arp.py`, `agent_functions/test-port.py`

- [ ] **Step 1:** Read all 5 standalone C# modules and Python wrappers
- [ ] **Step 2:** Create `net-enum.csproj` (copy from a simple plugin like whoami.csproj, adjust name)
- [ ] **Step 3:** Create `NetEnumArgs.cs` with `action` field plus union of all params needed by the 5 commands
- [ ] **Step 4:** Create `net-enum.cs` with switch dispatching to all 5 actions, copying logic from each standalone
- [ ] **Step 5:** Create parent `net-enum.py` with action choices
- [ ] **Step 6:** Create 5 script_only wrappers (preserve each command's parameters)
- [ ] **Step 7:** Delete old standalone folders and Python files
- [ ] **Step 8:** Build verify, commit

```bash
git commit -m "refactor: create net-enum parent, absorb ping/ifconfig/netstat/arp/test-port"
```

### Task 3.7: Create enum-windows parent (get-localgroup, get-sessions, get-shares)

**Files:** Same pattern as 3.6 but for Windows-specific NetAPI32 enumeration.
- Create: `agent_code/enum-windows/enum-windows.cs`, `.csproj`, `EnumWindowsArgs.cs`
- Create: `agent_functions/enum-windows.py`
- Create: 3 script_only wrappers
- Delete: 3 standalone C# folders + Python wrappers

- [ ] **Step 1-7:** Follow same pattern as Task 3.6
- [ ] **Step 8:** Commit

```bash
git commit -m "refactor: create enum-windows parent, absorb get-localgroup/sessions/shares"
```

### Task 3.8: Consolidate inject-shellcode platform variants

**Files:**
- Modify: `agent_code/inject-shellcode/inject-shellcode.cs` (add Linux + macOS paths via RuntimeInformation)
- Delete: `agent_code/inject-shellcode-linux/`, `agent_code/inject-shellcode-macos/`

- [ ] **Step 1:** Read all three inject-shellcode variants (Windows, Linux, macOS)
- [ ] **Step 2:** Merge Linux and macOS logic into the main inject-shellcode.cs using `RuntimeInformation.IsOSPlatform()` dispatch
- [ ] **Step 3:** Delete the platform-specific folders
- [ ] **Step 4:** Build verify, commit

```bash
git commit -m "refactor: merge inject-shellcode platform variants into single module"
```

### Task 3.9: Delete http-request

**Files:**
- Delete: `agent_code/http-request/` (entire folder)
- Delete: `agent_functions/http-request.py`

- [ ] **Step 1:** Verify wget covers the same functionality by reading both modules
- [ ] **Step 2:** Delete http-request C# folder and Python wrapper
- [ ] **Step 3:** Commit

```bash
git commit -m "chore: remove http-request (wget covers same functionality)"
```

---

## Chunk 4: Stub Completions & New Commands — Existing Parents

### Task 4.1: Implement kerberos purge (stub completion)

**Files:**
- Modify: `agent_code/kerberos/kerberos.cs` (implement `PurgeTickets()` method body)

- [ ] **Step 1:** Read `kerberos.cs` — find the stub `PurgeTickets()` method and existing P/Invoke declarations
- [ ] **Step 2:** Implement `PurgeTickets()` using `LsaCallAuthenticationPackage` with `KERB_PURGE_TKT_CACHE_REQUEST`

```csharp
private string PurgeTickets()
{
    // Use existing LsaConnectUntrusted to get handle
    // Build KERB_PURGE_TKT_CACHE_REQUEST structure
    // Call LsaCallAuthenticationPackage
    // Return success/failure message
}
```

- [ ] **Step 3:** Build verify, commit

```bash
git commit -m "feat: implement kerberos purge ticket cache"
```

### Task 4.2: Implement credentials dpapi (stub completion)

**Files:**
- Modify: `agent_code/credentials/credentials.cs` (implement dpapi method body)
- Create: `agent_code/credentials/CredentialsNative.cs` (P/Invoke declarations if not already present)

- [ ] **Step 1:** Read credentials.cs — find the dpapi stub
- [ ] **Step 2:** Implement DPAPI extraction using `CryptUnprotectData` P/Invoke
- [ ] **Step 3:** Build verify, commit

```bash
git commit -m "feat: implement credentials dpapi extraction"
```

### Task 4.3: Implement credentials vault-enum (stub completion)

**Files:**
- Modify: `agent_code/credentials/credentials.cs`
- Modify: `agent_code/credentials/CredentialsNative.cs` (add Vault P/Invoke)

- [ ] **Step 1:** Implement vault enumeration using `VaultOpenVault`, `VaultEnumerateVaults`, `VaultEnumerateItems`
- [ ] **Step 2:** Build verify, commit

```bash
git commit -m "feat: implement credentials vault-enum"
```

### Task 4.4: Add wmi hotfixes, wmi-exec, av-enum actions

**Files:**
- Modify: `agent_code/wmi/wmi.cs` (add 3 new switch cases)
- Modify: `agent_code/wmi/WmiArgs.cs` (add host, command, username, password fields for wmi-exec)
- Modify: `agent_functions/wmi.py` (add action choices)
- Create: `agent_functions/script_only/hotfixes.py`
- Create: `agent_functions/script_only/wmi-exec.py`
- Create: `agent_functions/script_only/av-enum.py`

- [ ] **Step 1:** Read `wmi.cs` and `WmiArgs.cs` — understand existing pattern
- [ ] **Step 2:** Add switch cases:

```csharp
"hotfixes" => ExecuteQuery(
    "SELECT HotFixID, InstalledOn, Description FROM Win32_QuickFixEngineering",
    args.ns),
"av-enum" => EnumAvProducts(),
"wmi-exec" => ExecuteRemoteProcess(args),
```

- [ ] **Step 3:** Implement `EnumAvProducts()` — query BOTH `AntiVirusProduct` and `AntiSpywareProduct` from `root\SecurityCenter2`, combine results
- [ ] **Step 4 (renumbered):** Implement `ExecuteRemoteProcess` for wmi-exec using `Win32_Process.Create` with optional credentials
- [ ] **Step 4:** Update WmiArgs with host/command/username/password fields
- [ ] **Step 5:** Update wmi.py choices, create 3 script_only wrappers (wmi-exec needs host+command params)
- [ ] **Step 6:** Build verify, commit

```bash
git commit -m "feat: add hotfixes, wmi-exec, av-enum actions to wmi"
```

### Task 4.5: Add ds acl action + fix GetLdapDirectoryIdentifier bug

**Files:**
- Modify: `agent_code/ds/ds.cs` (add "acl" switch case, fix bug at lines 122-132)
- Modify: `agent_functions/ds.py` (add "acl" to action choices)
- Create: `agent_functions/script_only/acl.py`

- [ ] **Step 1:** Read `ds.cs` — understand existing query infrastructure
- [ ] **Step 2:** Fix the inverted `GetLdapDirectoryIdentifier` logic at lines 122-132

Replace the condition so empty `args.server` falls back to `domain`, not vice versa.

- [ ] **Step 3:** Add "acl" action that queries `nTSecurityDescriptor` and parses ACEs via `RawSecurityDescriptor`
- [ ] **Step 4:** Update ds.py, create `script_only/acl.py`
- [ ] **Step 5:** Build verify, commit

```bash
git commit -m "feat: add ds acl action, fix GetLdapDirectoryIdentifier bug"
```

### Task 4.6: Add ds-query preset parameter group

**Files:**
- Modify: `agent_functions/ds-query.py` (or `script_only/ds-query.py` if already moved)

- [ ] **Step 1:** Read current ds-query.py — understand existing "Default" parameter group

- [ ] **Step 2:** Add "Preset" parameter group with a `ChooseOne` dropdown

```python
CommandParameter(
    name="preset", cli_name="preset",
    display_name="Preset Query",
    type=ParameterType.ChooseOne,
    choices=[
        "Kerberoastable Users",
        "AS-REP Roastable",
        "Unconstrained Delegation",
        "Constrained Delegation",
        "RBCD Targets",
        "Domain Admins",
        "LAPS Passwords",
        "GPO List",
        "Disabled Accounts",
        "AdminSDHolder Protected",
    ],
    description="Select a preset LDAP query",
    parameter_group_info=[
        ParameterGroupInfo(required=True, group_name="Preset")
    ]
),
CommandParameter(
    name="searchbase", cli_name="searchbase",
    display_name="Search Base",
    type=ParameterType.String,
    default_value="",
    description="LDAP search base (defaults to domain root)",
    parameter_group_info=[
        ParameterGroupInfo(required=False, group_name="Preset")
    ]
),
```

- [ ] **Step 3:** Add preset→filter mapping dict and update `create_go_tasking` to resolve presets

```python
PRESET_QUERIES = {
    "Kerberoastable Users": {
        "ldapfilter": "(&(objectClass=user)(servicePrincipalName=*)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))",
        "properties": "sAMAccountName,servicePrincipalName,memberOf",
    },
    # ... (all 10 presets from spec)
}
```

In `create_go_tasking`, detect "Preset" group and map to filter+properties before creating subtask.

- [ ] **Step 4:** Commit

```bash
git commit -m "feat: add ds-query preset parameter group with 10 canned LDAP queries"
```

---

## Chunk 5: New Parent Modules — recon, privesc, adws

### Task 5.1: Create recon module (dns-cache, autologon, rdp-check, always-install-elevated)

**Dependency:** This task modifies `credentials.cs` and `credentials.py` (removing dns-cache). Do NOT run in parallel with any other task that touches credentials.

**Files:**
- Create: `agent_code/recon/recon.cs`
- Create: `agent_code/recon/recon.csproj`
- Create: `agent_code/recon/ReconArgs.cs`
- Create: `agent_code/recon/ReconNative.cs` (P/Invoke for DnsGetCacheDataTable)
- Create: `agent_functions/recon.py`
- Create: `agent_functions/script_only/autologon.py`
- Create: `agent_functions/script_only/rdp-check.py`
- Create: `agent_functions/script_only/always-install-elevated.py`
- Modify: `agent_functions/script_only/dns-cache.py` (change depends_on from "credentials" to "recon")
- Modify: `agent_code/credentials/credentials.cs` (remove "dns-cache" case from switch)
- Modify: `agent_functions/credentials.py` (remove "dns-cache" from choices)

- [ ] **Step 1:** Create `recon.csproj` (standard plugin template — net10.0, ProjectReference to Workflow.Models)

- [ ] **Step 2:** Create `ReconArgs.cs`

```csharp
namespace recon
{
    public class ReconArgs
    {
        public string action { get; set; } = "dns-cache";
    }
}
```

- [ ] **Step 3:** Create `ReconNative.cs` with `DnsGetCacheDataTable` P/Invoke

```csharp
using System.Runtime.InteropServices;

namespace recon
{
    internal static class ReconNative
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsGetCacheDataTable")]
        internal static extern bool DnsGetCacheDataTable(
            out IntPtr ppEntry);

        [StructLayout(LayoutKind.Sequential)]
        internal struct DNS_CACHE_ENTRY
        {
            public IntPtr pNext;
            public IntPtr pszName;
            public ushort wType;
            public ushort wDataLength;
            public uint dwFlags;
        }
    }
}
```

- [ ] **Step 4:** Create `recon.cs` with all 4 actions

```csharp
string result = args.action switch
{
    "dns-cache" => GetDnsCache(),
    "autologon" => CheckAutologon(),
    "rdp-check" => CheckRdp(),
    "always-install-elevated" => CheckAlwaysInstallElevated(),
    _ => throw new ArgumentException($"Unknown action: {args.action}")
};
```

Implement each method:
- `GetDnsCache()` — P/Invoke DnsGetCacheDataTable, walk linked list. Copy existing logic from credentials.cs dns-cache implementation.
- `CheckAutologon()` — Read registry `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon` keys
- `CheckRdp()` — Read registry Terminal Server keys + check "Remote Desktop Users" group
- `CheckAlwaysInstallElevated()` — Read both HKLM and HKCU `AlwaysInstallElevated` values

- [ ] **Step 5:** Remove dns-cache from credentials module

In `credentials.cs`, remove the `"dns-cache" => GetDnsCache()` case and the `GetDnsCache()` method.
In `credentials.py`, remove `"dns-cache"` from the action choices list.

- [ ] **Step 6:** Update `script_only/dns-cache.py`: change `depends_on = "credentials"` to `depends_on = "recon"` and `CommandName="credentials"` to `CommandName="recon"`

- [ ] **Step 7:** Create parent `recon.py` and 3 new script_only wrappers (autologon, rdp-check, always-install-elevated)

- [ ] **Step 8:** Build verify both recon and credentials modules

```bash
cd agent_code/recon && dotnet build -c Release
cd agent_code/credentials && dotnet build -c Release
```

- [ ] **Step 9:** Commit

```bash
git commit -m "feat: create recon module (dns-cache, autologon, rdp-check, always-install-elevated)"
```

### Task 5.2: Create privesc module (privcheck, service-enum)

**Files:**
- Create: `agent_code/privesc/privesc.cs`
- Create: `agent_code/privesc/privesc.csproj`
- Create: `agent_code/privesc/PrivescArgs.cs`
- Create: `agent_code/privesc/PrivescNative.cs`
- Create: `agent_functions/privesc.py`
- Create: `agent_functions/script_only/privcheck.py`
- Create: `agent_functions/script_only/service-enum.py`

- [ ] **Step 1:** Create `privesc.csproj` (standard plugin template)

- [ ] **Step 2:** Create `PrivescNative.cs` with P/Invoke declarations:
  - `OpenProcessToken`, `GetTokenInformation` from advapi32.dll (for privcheck)
  - `OpenSCManager`, `EnumServicesStatusEx`, `QueryServiceConfig`, `QueryServiceObjectSecurity` from advapi32.dll (for service-enum)

- [ ] **Step 3:** Create `PrivescArgs.cs`

- [ ] **Step 4:** Create `privesc.cs` with switch for "privcheck" and "service-enum"

- [ ] **Step 5:** Implement `CheckPrivileges()`:
  - Open current process token
  - Enumerate privileges, flag escalation-relevant ones
  - Check integrity level, admin status

- [ ] **Step 6:** Implement `EnumServices()`:
  - Open SCManager, enumerate all services
  - For each: check unquoted paths, writable binaries, modifiable configs
  - Output structured results

- [ ] **Step 7:** Create `privesc.py` parent and 2 script_only wrappers

- [ ] **Step 8:** Build verify, commit

```bash
git commit -m "feat: create privesc module (privcheck, service-enum)"
```

### Task 5.3: Create adws module (connect, query, disconnect)

**Files:**
- Create: `agent_code/adws/adws.cs`
- Create: `agent_code/adws/adws.csproj` (with System.ServiceModel.NetTcp NuGet reference)
- Create: `agent_code/adws/AdwsArgs.cs`
- Create: `agent_functions/adws.py`
- Create: `agent_functions/script_only/adws-connect.py`
- Create: `agent_functions/script_only/adws-query.py`
- Create: `agent_functions/script_only/adws-disconnect.py`

**Note:** This is the most complex new module. It requires WCF net.tcp transport.

- [ ] **Step 1:** Create `adws.csproj` with NuGet reference:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.ServiceModel.NetTcp" Version="8.1.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Workflow.Models\Workflow.Models.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2:** Create `AdwsArgs.cs` with action, server, filter, properties, searchbase fields

- [ ] **Step 3:** Create `adws.cs` with connection state management (similar to ds.cs pattern)

```csharp
string result = args.action switch
{
    "connect" => Connect(args),
    "query" => Query(args),
    "disconnect" => Disconnect(),
    _ => throw new ArgumentException($"Unknown action: {args.action}")
};
```

- [ ] **Step 4:** Implement ADWS SOAP client

**This is the most complex implementation in the plan.** Key details:

**Connection state:** Store the WCF `ChannelFactory` and channel in private fields (same pattern as `ds.cs` storing `LdapConnection`).

**Authentication:** NetTcpBinding uses Windows/Negotiate authentication by default. Configure:
```csharp
var binding = new NetTcpBinding(SecurityMode.Transport);
binding.Security.Transport.ClientCredentialType =
    TcpClientCredentialType.Windows;
```

**ADWS uses three SOAP protocols:**
- WS-Transfer: `Get` for reading single objects
- WS-Enumeration: `Enumerate` + `Pull` for queries (this is what we need)
- WS-Transfer: `Put` for modifications (not needed initially)

**Enumerate request SOAP envelope structure:**
```xml
<s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
            xmlns:wsen="http://schemas.xmlsoap.org/ws/2004/09/enumeration"
            xmlns:ad="http://schemas.microsoft.com/2008/1/ActiveDirectory">
  <s:Header>
    <wsa:Action>http://schemas.xmlsoap.org/ws/2004/09/enumeration/Enumerate</wsa:Action>
    <wsa:To>net.tcp://{dc}:9389/ActiveDirectoryWebServices/Windows</wsa:To>
  </s:Header>
  <s:Body>
    <wsen:Enumerate>
      <wsen:Filter Dialect="http://schemas.microsoft.com/2008/1/ActiveDirectory/Dialect/LdapQuery">
        <ad:LdapQuery>
          <ad:Filter>{ldapFilter}</ad:Filter>
          <ad:BaseObject>{searchBase}</ad:BaseObject>
          <ad:Scope>Subtree</ad:Scope>
        </ad:LdapQuery>
      </wsen:Filter>
    </wsen:Enumerate>
  </s:Body>
</s:Envelope>
```

**Reference implementations to study:**
- SOAPHound (C#): https://github.com/FalconForceTeam/SOAPHound — full ADWS client
- ADWSLib: search for .NET ADWS enumeration examples

**Methods:**
  - `Connect(args)` — create `ChannelFactory<IRequestChannel>` with NetTcpBinding, open channel
  - `Query(args)` — build Enumerate SOAP, send via channel, parse Pull responses into results
  - `Disconnect()` — close channel and factory

- [ ] **Step 5:** Create parent `adws.py` and 3 script_only wrappers

- [ ] **Step 6:** Build verify, commit

```bash
git commit -m "feat: create adws module (ADWS SOAP enumeration over net.tcp)"
```

---

## Execution Order

1. **Chunk 1** — Infrastructure (folder setup, move existing wrappers, load.py, builder.py)
2. **Chunk 2** — Big consolidations (sysinfo, file-utils)
3. **Chunk 3** — Remaining consolidations + http-request deletion
4. **Chunk 4** — Stub completions + new actions on existing parents
5. **Chunk 5** — New parent modules (recon, privesc, adws)

**Chunk 1 must go first** — it sets up the `script_only/` folder and moves existing wrappers.

**Parallelization within Chunks 2-5:** Tasks touching different parent modules can run in parallel. Exception: Task 5.1 (recon) modifies `credentials.cs/py` — do NOT run it in parallel with Tasks 4.2/4.3 (credentials dpapi/vault-enum).

**Within each task:** Steps are sequential. Read first, then implement, then delete, then verify, then commit.
