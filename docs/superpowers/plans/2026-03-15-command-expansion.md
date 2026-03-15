# Athena Command Expansion Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 72 new Mythic commands (19 plugin DLLs + 53 wrappers), replace hardcoded plugin dependencies with convention-based discovery, and achieve meaningful test coverage across all plugins.

**Architecture:** Convention-based `depends_on`/`plugin_libraries` attributes on Python CommandBase classes, discovered at startup by `plugin_registry.py`. New C# plugins follow existing `IModule`/`PluginContext` pattern. Wrappers are Mythic-only `.py` files using the `ds-query` subtask pattern.

**Tech Stack:** .NET 10, C#, Python (Mythic container), MSTest, P/Invoke, BCL only (2 small NuGet exceptions: `System.Management` ~200KB, `System.Diagnostics.EventLog` ~100KB)

**Spec:** `docs/superpowers/specs/2026-03-15-command-expansion-design.md`

---

## Key Paths Reference

| Alias | Path |
|-------|------|
| `AGENT` | `Payload_Type/athena/athena/agent_code/` |
| `MYTHIC` | `Payload_Type/athena/athena/mythic/agent_functions/` |
| `UTILS` | `Payload_Type/athena/athena/mythic/agent_functions/athena_utils/` |
| `TESTS` | `Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/` |
| `MODELS` | `Payload_Type/athena/athena/agent_code/Workflow.Models/` |

## Key Patterns Reference

**C# Plugin Structure** (see `AGENT/tail/tail.cs` for reference):
```csharp
// {AGENT}/{name}/{name}.cs
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "command-name";
        private IDataBroker messageManager { get; set; }
        // other services as needed from PluginContext

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<CommandArgs>(job.task.parameters);
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
            // ... implementation ...
            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = result,
                task_id = job.task.id,
            });
        }
    }
}
```

**C# Plugin .csproj** (see `AGENT/tail/tail.csproj`):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Configurations>Debug;Release;LocalDebugGitHub;LocalDebugHttp;LocalDebugWebsocket;LocalDebugSmb;LocalDebugDiscord</Configurations>
  </PropertyGroup>
  <!-- Obfuscation Replacement Placeholder Do Not Remove -->
  <ItemGroup>
    <ProjectReference Include="..\Workflow.Models\Workflow.Models.csproj" />
  </ItemGroup>
</Project>
```

**Mythic Wrapper Pattern** (see `MYTHIC/ds-query.py`):
```python
from .athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class WrapperArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="param1", cli_name="param1",
                display_name="Param 1",
                type=ParameterType.String,
                description="Description",
                parameter_group_info=[ParameterGroupInfo(required=True, group_name="Default")]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class WrapperCommand(CommandBase):
    cmd = "wrapper-name"
    needs_admin = False
    script_only = True
    depends_on = "parent-plugin"          # NEW ATTRIBUTE
    plugin_libraries = []                 # NEW ATTRIBUTE
    help_cmd = "wrapper-name <args>"
    description = "Description"
    version = 1
    author = "@checkymander"
    argument_class = WrapperArguments
    attackmapping = []
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        createSubtaskMessage = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="parent-plugin",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "specific-action",
                "param1": taskData.args.get_arg("param1"),
            })
        )
        subtask = await SendMythicRPCTaskCreateSubtask(createSubtaskMessage)
        return PTTaskCreateTaskingMessageResponse(TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
```

**Test Pattern** (see `TESTS/PluginTests/CatTests.cs`):
```csharp
using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class CommandTests
    {
        IDataBroker _messageManager = new TestDataBroker();
        IModule _plugin;

        public CommandTests()
        {
            _plugin = new PluginLoader(_messageManager).LoadPluginFromDisk("command-name");
        }

        [TestMethod]
        public async Task TestCommand_HappyPath()
        {
            var parameters = new Dictionary<string, object> { { "key", "value" } };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "command-name"
                }
            };
            await _plugin.Execute(job);
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(/* assertion */);
        }
    }
}
```

---

## Chunk 1: Phase 0 — Foundation (Plugin Registry + Test Infrastructure)

### Task 1: Create plugin_registry.py

**Files:**
- Create: `UTILS/plugin_registry.py`

This is the core dependency discovery module. It scans all imported `CommandBase` subclasses after Mythic auto-discovery and builds a cached dependency graph.

- [ ] **Step 1: Create `plugin_registry.py` with registry class**

```python
# {UTILS}/plugin_registry.py
from mythic_container.MythicCommandBase import CommandBase

# Registry populated via __init_subclass__ hook or post-import scan
_command_registry: dict[str, type] = {}
_subcommand_cache: dict[str, list[str]] = {}
_initialized = False


def _ensure_initialized():
    """Build registry from all CommandBase subclasses."""
    global _initialized, _command_registry, _subcommand_cache
    if _initialized:
        return

    def _collect_subclasses(cls):
        for sub in cls.__subclasses__():
            if hasattr(sub, 'cmd'):
                _command_registry[sub.cmd] = sub
            _collect_subclasses(sub)

    _collect_subclasses(CommandBase)

    # Build subcommand cache
    for cmd_name, cmd_cls in _command_registry.items():
        parent = getattr(cmd_cls, 'depends_on', None)
        if parent:
            _subcommand_cache.setdefault(parent, []).append(cmd_name)

    _initialized = True


def get_subcommands(plugin_name: str) -> list[str]:
    """Returns all command names where depends_on == plugin_name."""
    _ensure_initialized()
    return _subcommand_cache.get(plugin_name, [])


def get_libraries(command_name: str) -> list[dict[str, str]]:
    """Returns plugin_libraries for the command as load-assembly dicts."""
    _ensure_initialized()
    cmd_cls = _command_registry.get(command_name)
    if not cmd_cls:
        return []
    libs = getattr(cmd_cls, 'plugin_libraries', [])
    return [{"libraryname": lib, "target": "plugin"} for lib in libs]


def is_subcommand(command_name: str) -> bool:
    """Returns True if depends_on is set."""
    _ensure_initialized()
    cmd_cls = _command_registry.get(command_name)
    if not cmd_cls:
        return False
    return getattr(cmd_cls, 'depends_on', None) is not None


def get_parent(command_name: str) -> str | None:
    """Returns the depends_on value."""
    _ensure_initialized()
    cmd_cls = _command_registry.get(command_name)
    if not cmd_cls:
        return None
    return getattr(cmd_cls, 'depends_on', None)


def get_all_subcommands() -> list[str]:
    """Returns all commands that have depends_on set."""
    _ensure_initialized()
    return [
        name for name, cls in _command_registry.items()
        if getattr(cls, 'depends_on', None) is not None
    ]


def reset():
    """Reset cache — used for testing only."""
    global _initialized, _command_registry, _subcommand_cache
    _initialized = False
    _command_registry = {}
    _subcommand_cache = {}
```

- [ ] **Step 2: Verify file created and has no syntax errors**

Run: `cd Payload_Type/athena/athena/mythic/agent_functions && python -c "import athena_utils.plugin_registry; print('OK')"`
Expected: `OK` (may need to adjust import path depending on container structure — verify)

- [ ] **Step 3: Commit**

```bash
git add Payload_Type/athena/athena/mythic/agent_functions/athena_utils/plugin_registry.py
git commit -m "feat: add plugin_registry.py for convention-based dependency discovery"
```

---

### Task 2: Add depends_on and plugin_libraries to all existing commands

**Files:**
- Modify: `MYTHIC/ds-query.py` — add `depends_on = "ds"`
- Modify: `MYTHIC/ds-connect.py` — add `depends_on = "ds"`
- Modify: `MYTHIC/ds.py` — add `plugin_libraries = ["System.DirectoryServices.Protocols.dll"]`
- Modify: `MYTHIC/inject-assembly.py` — add `depends_on = "inject-shellcode"`
- Modify: `MYTHIC/ssh.py` — add `plugin_libraries = ["Renci.SshNet.dll", "BouncyCastle.Cryptography.dll"]`
- Modify: `MYTHIC/sftp.py` — add `plugin_libraries = ["Renci.SshNet.dll", "BouncyCastle.Cryptography.dll"]`
- Modify: `MYTHIC/screenshot.py` — add `plugin_libraries = ["System.Drawing.Common.dll"]`
- Modify: `MYTHIC/nidhogg.py` — add `depends_on = None` (explicit), keep existing sub-commands
- Modify: All BOF command `.py` files in `MYTHIC/outflank_bofs/`, `MYTHIC/trusted_sec_bofs/`, `MYTHIC/trusted_sec_remote_bofs/`, `MYTHIC/misc_bofs/` — add `depends_on = "coff"`
- Modify: All nidhogg command `.py` files in `MYTHIC/nidhogg_commands/` — add `depends_on = "nidhogg"`

For each file, add the two attributes right after the `cmd = "..."` line on the `CommandBase` subclass:

- [ ] **Step 1: Add attributes to ds-query.py and ds-connect.py**

In `MYTHIC/ds-query.py`, on the `DsQueryCommand` class (line ~88), add after `cmd = "ds-query"`:
```python
    depends_on = "ds"
    plugin_libraries = []
```

In `MYTHIC/ds-connect.py`, on its `CommandBase` subclass, add:
```python
    depends_on = "ds"
    plugin_libraries = []
```

- [ ] **Step 2: Add plugin_libraries to ds.py, ssh.py, sftp.py, screenshot.py**

In `MYTHIC/ds.py` command class, add:
```python
    depends_on = None
    plugin_libraries = ["System.DirectoryServices.Protocols.dll"]
```

In `MYTHIC/ssh.py` command class, add:
```python
    depends_on = None
    plugin_libraries = ["Renci.SshNet.dll", "BouncyCastle.Cryptography.dll"]
```

In `MYTHIC/sftp.py` command class, add:
```python
    depends_on = None
    plugin_libraries = ["Renci.SshNet.dll", "BouncyCastle.Cryptography.dll"]
```

In `MYTHIC/screenshot.py` command class, add:
```python
    depends_on = None
    plugin_libraries = ["System.Drawing.Common.dll"]
```

- [ ] **Step 3: Add depends_on to inject-assembly.py**

```python
    depends_on = "inject-shellcode"
    plugin_libraries = []
```

- [ ] **Step 4: Add depends_on = "coff" to all BOF command files**

Find all BOF `.py` files:
```bash
find Payload_Type/athena/athena/mythic/agent_functions/outflank_bofs/ \
     Payload_Type/athena/athena/mythic/agent_functions/trusted_sec_bofs/ \
     Payload_Type/athena/athena/mythic/agent_functions/trusted_sec_remote_bofs/ \
     Payload_Type/athena/athena/mythic/agent_functions/misc_bofs/ \
     -name "*.py" -not -name "__init__.py"
```

For each file's `CommandBase` subclass, add after `cmd = "..."`:
```python
    depends_on = "coff"
    plugin_libraries = []
```

- [ ] **Step 5: Add depends_on = "nidhogg" to all nidhogg command files**

Find all nidhogg `.py` files in `MYTHIC/nidhogg_commands/`:
```bash
find Payload_Type/athena/athena/mythic/agent_functions/nidhogg_commands/ \
     -name "*.py" -not -name "__init__.py"
```

For each file's `CommandBase` subclass, add:
```python
    depends_on = "nidhogg"
    plugin_libraries = []
```

- [ ] **Step 6: Commit**

```bash
git add -u  # only stage tracked modified files, not unintended new files
git commit -m "feat: add depends_on and plugin_libraries to all existing commands"
```

---

### Task 3: Refactor load.py to use plugin_registry

**Files:**
- Modify: `MYTHIC/load.py`

Replace hardcoded arrays and dicts with `plugin_registry` calls.

- [ ] **Step 1: Add import and replace sub-command checks (lines 84-113)**

At top of `load.py`, add:
```python
from .athena_utils import plugin_registry
```

Replace lines 84-113 (the hardcoded `bof_commands`, `shellcode_commands`, `ds_commands`, `nidhogg_commands` checks and the duplicate `command_checks` dict loop) with:
```python
        parent = plugin_registry.get_parent(command)
        if parent:
            await message_utilities.send_agent_message(
                f"Please load {parent} to enable this command", taskData.Task
            )
            raise Exception(f"Please load {parent} to enable this command")
```

- [ ] **Step 2: Replace command_libraries dict (lines 115-121)**

Replace the hardcoded `command_libraries` dict with:
```python
        command_libraries = plugin_registry.get_libraries(command)
```

And update the library loading loop (lines 162-170) to iterate this list directly:
```python
        if command_libraries:
            for lib in command_libraries:
                print("Kicking off load-assembly for " + json.dumps(lib))
                createSubtaskMessage = MythicRPCTaskCreateSubtaskMessage(
                    taskData.Task.ID,
                    CommandName="load-assembly",
                    Params=json.dumps(lib),
                    ParameterGroupName="InternalLib"
                )
                subtask = await SendMythicRPCTaskCreateSubtask(createSubtaskMessage)
```

- [ ] **Step 3: Replace command_plugins dict (lines 123-128)**

Replace the hardcoded `command_plugins` dict with:
```python
        subcommands = plugin_registry.get_subcommands(command)
        if subcommands:
            resp = await SendMythicRPCCallbackAddCommand(MythicRPCCallbackAddCommandMessage(
                TaskID=taskData.Task.ID,
                Commands=subcommands
            ))
            if not resp.Success:
                raise Exception("Failed to add commands to callback: " + resp.Error)
```

Remove the old `if command in command_plugins:` block (lines 172-178).

- [ ] **Step 4: Verify load.py has no syntax errors**

Run: `python -c "import ast; ast.parse(open('Payload_Type/athena/athena/mythic/agent_functions/load.py').read()); print('OK')"`
Expected: `OK`

- [ ] **Step 5: Commit**

```bash
git add Payload_Type/athena/athena/mythic/agent_functions/load.py
git commit -m "refactor: load.py uses plugin_registry instead of hardcoded arrays"
```

---

### Task 4: Refactor builder.py to use plugin_registry

**Files:**
- Modify: `MYTHIC/builder.py` (lines ~476-501)

- [ ] **Step 1: Add import**

At top of `builder.py`, add:
```python
from .athena_utils import plugin_registry
```

- [ ] **Step 2: Replace hardcoded sub-command expansion (lines 476-501)**

Replace:
```python
            unloadable_commands = plugin_utilities.get_unloadable_commands()
            # ... and the nidhogg/ds/coff/inject-shellcode if blocks
```

With:
```python
            unloadable_commands = plugin_registry.get_all_subcommands() + plugin_utilities.get_builtin_commands()

            rid = self.getRid()

            # Snapshot the command list to avoid modifying collection during iteration
            for cmd in list(self.commands.get_commands()):
                if cmd in unloadable_commands:
                    continue

                # Preserve platform-specific exclusions
                if cmd == "ds" and self.selected_os.lower() == "redhat":
                    continue

                # Auto-include sub-commands for any parent plugin
                for sub in plugin_registry.get_subcommands(cmd):
                    self.commands.add_command(sub)

                try:
                    all_references.append(
                        os.path.join("..", cmd, "{}.csproj".format(cmd))
                    )
                    roots_replace += "<assembly fullname=\"{}\"/>".format(cmd) + '\n'
                except:
                    pass
```

**Important notes:**
- `list(self.commands.get_commands())` snapshots the collection to avoid "collection modified during enumeration" errors when calling `add_command` inside the loop.
- The `ds` + RedHat exclusion is preserved from the original `builder.py` (line 488-490). `System.DirectoryServices.Protocols` is not available on RHEL, so `ds` must be skipped on that platform. If more platform exclusions arise in the future, consider adding a `platform_exclude` attribute to the registry system.

Note: Keep `get_builtin_commands()` in `plugin_utilities.py` since `load` and `load-assembly` don't have `depends_on` — they're special-cased.

- [ ] **Step 3: Remove unused get_*_commands() imports if no other callers remain**

Check if `plugin_utilities.get_coff_commands()`, `get_ds_commands()`, `get_nidhogg_commands()`, `get_inject_shellcode_commands()`, and `get_unloadable_commands()` are called anywhere except `load.py` and `builder.py`. If not, remove them from `plugin_utilities.py`. Keep `get_builtin_commands()`.

Run: `grep -rn "get_coff_commands\|get_ds_commands\|get_nidhogg_commands\|get_inject_shellcode_commands\|get_unloadable_commands" Payload_Type/athena/athena/mythic/agent_functions/ --include="*.py"`

If only `load.py` (now refactored) and `builder.py` (now refactored) reference them, delete the functions from `UTILS/plugin_utilities.py`.

- [ ] **Step 4: Verify builder.py syntax**

Run: `python -c "import ast; ast.parse(open('Payload_Type/athena/athena/mythic/agent_functions/builder.py').read()); print('OK')"`

- [ ] **Step 5: Commit**

```bash
git add Payload_Type/athena/athena/mythic/agent_functions/builder.py \
        Payload_Type/athena/athena/mythic/agent_functions/athena_utils/plugin_utilities.py
git commit -m "refactor: builder.py uses plugin_registry, remove hardcoded arrays from plugin_utilities"
```

---

### Task 5: Create PluginTestBase and JobBuilder test helpers

**Files:**
- Create: `TESTS/PluginTestBase.cs`
- Create: `TESTS/JobBuilder.cs`

- [ ] **Step 1: Create PluginTestBase.cs**

```csharp
// {TESTS}/PluginTestBase.cs
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Tests.TestClasses;
using System.Text.Json;

namespace Workflow.Tests
{
    public abstract class PluginTestBase
    {
        protected IModule _plugin;
        protected TestDataBroker _messageManager;
        protected PluginLoader _pluginLoader;

        protected void LoadPlugin(string moduleName)
        {
            _messageManager = new TestDataBroker();
            _pluginLoader = new PluginLoader(_messageManager);
            _plugin = _pluginLoader.LoadPluginFromDisk(moduleName);
            Assert.IsNotNull(_plugin, $"Failed to load plugin: {moduleName}");
        }

        protected ServerJob CreateJob(
            string command, object parameters, string taskId = "test-1")
        {
            return new ServerJob()
            {
                task = new ServerTask()
                {
                    id = taskId,
                    parameters = JsonSerializer.Serialize(parameters),
                    command = command
                }
            };
        }

        /// Note: hasResponse is an AutoResetEvent that triggers on each Write/AddTaskResponse.
        /// For plugins that emit multiple intermediate responses, call Execute first then
        /// use GetRecentOutput() which returns the last response in the list.
        protected async Task<TaskResponse> ExecuteAndGetResponse(ServerJob job)
        {
            await _plugin.Execute(job);
            _messageManager.hasResponse.WaitOne(TimeSpan.FromSeconds(10));
            string response = _messageManager.GetRecentOutput();
            return JsonSerializer.Deserialize<TaskResponse>(response);
        }

        protected void AssertSuccess(TaskResponse response)
        {
            Assert.IsNotNull(response);
            Assert.AreNotEqual("error", response.status);
        }

        protected void AssertError(TaskResponse response)
        {
            Assert.IsNotNull(response);
            Assert.AreEqual("error", response.status);
        }

        protected void AssertOutputContains(TaskResponse response, string expected)
        {
            Assert.IsNotNull(response);
            Assert.IsTrue(
                response.user_output?.Contains(expected) == true,
                $"Expected output to contain '{expected}', got: '{response.user_output}'"
            );
        }
    }
}
```

- [ ] **Step 2: Create JobBuilder.cs**

```csharp
// {TESTS}/JobBuilder.cs
using Workflow.Models;
using System.Text.Json;

namespace Workflow.Tests
{
    public class JobBuilder
    {
        private string _command;
        private object _parameters;
        private string _taskId = "test-1";

        public JobBuilder(string command)
        {
            _command = command;
        }

        public JobBuilder WithParameters(object parameters)
        {
            _parameters = parameters;
            return this;
        }

        public JobBuilder WithTaskId(string taskId)
        {
            _taskId = taskId;
            return this;
        }

        public ServerJob Build()
        {
            return new ServerJob()
            {
                task = new ServerTask()
                {
                    id = _taskId,
                    parameters = _parameters != null
                        ? JsonSerializer.Serialize(_parameters)
                        : "{}",
                    command = _command
                }
            };
        }
    }
}
```

- [ ] **Step 3: Run existing tests to verify no regressions**

Run: `dotnet test Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/ -v minimal`
Expected: All existing tests pass (new files don't affect existing tests)

- [ ] **Step 4: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/PluginTestBase.cs \
        Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/JobBuilder.cs
git commit -m "feat: add PluginTestBase and JobBuilder test helpers"
```

---

### Task 6: Extend Utilities.cs with new test helpers

**Files:**
- Modify: `TESTS/Utilities.cs`

- [ ] **Step 1: Add new helper methods to Utilities.cs**

Add these methods to the existing `Utilities` class:

```csharp
public static string CreateTempFileWithContent(string content)
{
    string tempFile = Path.GetTempFileName();
    File.WriteAllText(tempFile, content);
    return tempFile;
}

public static string CreateTempDirectoryWithStructure(
    Dictionary<string, string> files)
{
    string tempDir = Path.Combine(
        Path.GetTempPath(),
        "TestDir_" + Guid.NewGuid().ToString());
    Directory.CreateDirectory(tempDir);

    foreach (var (relativePath, content) in files)
    {
        string fullPath = Path.Combine(tempDir, relativePath);
        string dir = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
    }

    return tempDir;
}

public static TcpListener CreateLocalListener(int port)
{
    var listener = new System.Net.Sockets.TcpListener(
        System.Net.IPAddress.Loopback, port);
    listener.Start();
    return listener;
}

/// Creates a simple HTTP listener for testing http-request plugin.
/// Returns the listener and its prefix URL. Caller must Stop() when done.
public static (HttpListener listener, string url) CreateLocalHttpServer(
    string responseBody = "OK", int statusCode = 200)
{
    int port = new Random().Next(49152, 65535);
    string prefix = $"http://localhost:{port}/";
    var listener = new System.Net.HttpListener();
    listener.Prefixes.Add(prefix);
    listener.Start();
    Task.Run(async () =>
    {
        while (listener.IsListening)
        {
            try
            {
                var ctx = await listener.GetContextAsync();
                ctx.Response.StatusCode = statusCode;
                var buffer = System.Text.Encoding.UTF8.GetBytes(responseBody);
                ctx.Response.ContentLength64 = buffer.Length;
                await ctx.Response.OutputStream.WriteAsync(buffer);
                ctx.Response.Close();
            }
            catch (ObjectDisposedException) { break; }
        }
    });
    return (listener, prefix);
}

public static string GetTempPath()
{
    return Path.GetTempPath();
}
```

Add the required `using` directives at the top:
```csharp
using System.Net;
using System.Net.Sockets;
```

- [ ] **Step 2: Run existing tests**

Run: `dotnet test Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/ -v minimal`
Expected: All existing tests pass

- [ ] **Step 3: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/Utilities.cs
git commit -m "feat: extend Utilities.cs with test helpers"
```

---

### Task 7: Phase 0 Verification Gate

- [ ] **Step 1: Verify all Python files parse correctly**

Run:
```bash
find Payload_Type/athena/athena/mythic/agent_functions/ -name "*.py" -exec python -c "
import ast, sys
try:
    ast.parse(open(sys.argv[1]).read())
except SyntaxError as e:
    print(f'FAIL: {sys.argv[1]}: {e}')
    sys.exit(1)
" {} \;
```
Expected: No FAIL output

- [ ] **Step 2: Verify C# test project builds**

Run: `dotnet build Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/ -c Release`
Expected: Build succeeded

- [ ] **Step 3: Run full test suite**

Run: `dotnet test Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/ -v minimal`
Expected: All tests pass

- [ ] **Step 4: Commit any fixes if needed**

```bash
git add -A
git commit -m "fix: phase 0 verification fixes" # only if fixes needed
```

---

## Chunk 2: Phase 1 — File Operations (find, file-utils, hash + wrappers)

### Task 8: Create `find` plugin DLL

**Files:**
- Create: `AGENT/find/find.csproj`
- Create: `AGENT/find/FindArgs.cs`
- Create: `AGENT/find/find.cs`
- Create: `TESTS/PluginTests/FindTests.cs`
- Modify: `TESTS/Workflow.Tests.csproj` — add `<ProjectReference Include="..\..\find\find.csproj" />`

- [ ] **Step 1: Write FindTests.cs (failing tests)**

```csharp
// {TESTS}/PluginTests/FindTests.cs
using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("FileOps")]
    public class FindTests : PluginTestBase
    {
        public FindTests()
        {
            LoadPlugin("find");
        }

        [TestMethod]
        public async Task TestFind_ByPattern()
        {
            string tempDir = Utilities.CreateTempDirectoryWithStructure(
                new Dictionary<string, string>
                {
                    { "test.txt", "hello" },
                    { "test.log", "world" },
                    { "sub/nested.txt", "nested" }
                });

            var job = CreateJob("find", new
            {
                action = "find",
                path = tempDir,
                pattern = "*.txt",
                max_depth = 5
            });

            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("test.txt"));
            Assert.IsTrue(response.user_output.Contains("nested.txt"));
            Assert.IsFalse(response.user_output.Contains("test.log"));

            Directory.Delete(tempDir, true);
        }

        [TestMethod]
        public async Task TestFind_GrepContent()
        {
            string tempDir = Utilities.CreateTempDirectoryWithStructure(
                new Dictionary<string, string>
                {
                    { "match.txt", "password=secret123" },
                    { "nomatch.txt", "nothing here" }
                });

            var job = CreateJob("find", new
            {
                action = "grep",
                path = tempDir,
                content_pattern = "password",
                max_depth = 5
            });

            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("match.txt"));
            Assert.IsFalse(response.user_output.Contains("nomatch.txt"));

            Directory.Delete(tempDir, true);
        }

        [TestMethod]
        public async Task TestFind_InvalidPath()
        {
            var job = CreateJob("find", new
            {
                action = "find",
                path = "/nonexistent/path/abc123",
                pattern = "*"
            });

            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }

        [TestMethod]
        public async Task TestFind_MaxDepth()
        {
            string tempDir = Utilities.CreateTempDirectoryWithStructure(
                new Dictionary<string, string>
                {
                    { "level1.txt", "a" },
                    { "sub1/level2.txt", "b" },
                    { "sub1/sub2/level3.txt", "c" }
                });

            var job = CreateJob("find", new
            {
                action = "find",
                path = tempDir,
                pattern = "*.txt",
                max_depth = 1
            });

            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("level1.txt"));
            Assert.IsFalse(response.user_output.Contains("level3.txt"));

            Directory.Delete(tempDir, true);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they fail (plugin not found)**

Run: `dotnet test Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/ --filter "TestCategory=FileOps&ClassName~FindTests" -v minimal`
Expected: FAIL (plugin "find" not loaded)

- [ ] **Step 3: Create find.csproj**

```xml
<!-- {AGENT}/find/find.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Configurations>Debug;Release;LocalDebugGitHub;LocalDebugHttp;LocalDebugWebsocket;LocalDebugSmb;LocalDebugDiscord</Configurations>
  </PropertyGroup>
  <!-- Obfuscation Replacement Placeholder Do Not Remove -->
  <ItemGroup>
    <ProjectReference Include="..\Workflow.Models\Workflow.Models.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create FindArgs.cs**

```csharp
// {AGENT}/find/FindArgs.cs
namespace find
{
    public class FindArgs
    {
        public string action { get; set; } = "find";
        public string path { get; set; } = ".";
        public string pattern { get; set; } = "*";
        public string content_pattern { get; set; } = "";
        public int max_depth { get; set; } = 10;
        public long min_size { get; set; } = -1;
        public long max_size { get; set; } = -1;
        public string permissions { get; set; } = "";
        public string newer_than { get; set; } = "";
        public string older_than { get; set; } = "";
    }
}
```

- [ ] **Step 5: Create find.cs**

```csharp
// {AGENT}/find/find.cs
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "find";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<find.FindArgs>(
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
                if (!Directory.Exists(args.path))
                {
                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        completed = true,
                        user_output = $"Directory does not exist: {args.path}",
                        task_id = job.task.id,
                        status = "error"
                    });
                    return;
                }

                var results = new List<string>();
                SearchDirectory(
                    args.path, args, results, 0, job.cancellationtokensource?.Token ?? CancellationToken.None);

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = results.Count > 0
                        ? string.Join(Environment.NewLine, results)
                        : "No matching files found.",
                    task_id = job.task.id,
                });
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private void SearchDirectory(
            string dir, find.FindArgs args, List<string> results,
            int currentDepth, CancellationToken token)
        {
            if (currentDepth > args.max_depth || token.IsCancellationRequested)
                return;

            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, args.pattern))
                {
                    if (token.IsCancellationRequested) return;
                    if (MatchesFilters(file, args))
                        results.Add(file);
                }

                foreach (var subDir in Directory.EnumerateDirectories(dir))
                {
                    if (token.IsCancellationRequested) return;
                    SearchDirectory(
                        subDir, args, results, currentDepth + 1, token);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
        }

        private bool MatchesFilters(string filePath, find.FindArgs args)
        {
            var info = new FileInfo(filePath);

            if (args.min_size >= 0 && info.Length < args.min_size) return false;
            if (args.max_size >= 0 && info.Length > args.max_size) return false;

            if (!string.IsNullOrEmpty(args.newer_than))
            {
                if (DateTime.TryParse(args.newer_than, out var newerDate)
                    && info.LastWriteTimeUtc < newerDate)
                    return false;
            }

            if (!string.IsNullOrEmpty(args.older_than))
            {
                if (DateTime.TryParse(args.older_than, out var olderDate)
                    && info.LastWriteTimeUtc > olderDate)
                    return false;
            }

            if (!string.IsNullOrEmpty(args.permissions))
            {
                if (!MatchesPermissions(filePath, args.permissions))
                    return false;
            }

            if (args.action == "grep"
                && !string.IsNullOrEmpty(args.content_pattern))
            {
                try
                {
                    // Read line-by-line to avoid OOM on large files
                    var regex = new Regex(args.content_pattern);
                    foreach (var line in File.ReadLines(filePath))
                    {
                        if (regex.IsMatch(line)) return true;
                    }
                    return false;
                }
                catch { return false; }
            }

            return true;
        }

        private bool MatchesPermissions(string filePath, string permissions)
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return true;

            try
            {
                var mode = File.GetUnixFileMode(filePath);
                return permissions switch
                {
                    "suid" => mode.HasFlag(UnixFileMode.SetUser),
                    "sgid" => mode.HasFlag(UnixFileMode.SetGroup),
                    "world-writable" => mode.HasFlag(UnixFileMode.OtherWrite),
                    _ => true
                };
            }
            catch { return false; }
        }
    }
}
```

- [ ] **Step 6: Add project reference to test .csproj**

In `TESTS/Workflow.Tests.csproj`, add inside the `<ItemGroup>` with other project references:
```xml
    <ProjectReference Include="..\..\find\find.csproj" />
```

- [ ] **Step 7: Run tests — verify they pass**

Run: `dotnet test Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/ --filter "TestCategory=FileOps&ClassName~FindTests" -v minimal`
Expected: All 4 tests PASS

- [ ] **Step 8: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/find/ \
        Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/PluginTests/FindTests.cs \
        Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/Workflow.Tests.csproj
git commit -m "feat: add find plugin with tests"
```

---

### Task 9: Create `find` Mythic command definition

**Files:**
- Create: `MYTHIC/find.py`

- [ ] **Step 1: Create find.py Mythic command**

```python
# {MYTHIC}/find.py
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class FindArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["find", "grep"],
                default_value="find",
                description="Search mode",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="path", cli_name="path",
                display_name="Search Path",
                type=ParameterType.String,
                description="Directory to search",
                default_value=".",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="pattern", cli_name="pattern",
                display_name="File Pattern",
                type=ParameterType.String,
                description="Glob pattern for filename matching (e.g. *.conf)",
                default_value="*",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
            CommandParameter(
                name="content_pattern", cli_name="content_pattern",
                display_name="Content Pattern",
                type=ParameterType.String,
                description="Regex pattern to search file contents (grep mode)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=3)
                ]
            ),
            CommandParameter(
                name="max_depth", cli_name="max_depth",
                display_name="Max Depth",
                type=ParameterType.Number,
                description="Maximum directory depth to search",
                default_value=10,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=4)
                ]
            ),
            CommandParameter(
                name="permissions", cli_name="permissions",
                display_name="Permission Filter",
                type=ParameterType.ChooseOne,
                choices=["", "suid", "sgid", "world-writable"],
                default_value="",
                description="Filter by file permission (Linux/macOS only)",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=5)
                ]
            ),
            CommandParameter(
                name="min_size", cli_name="min_size",
                display_name="Min Size (bytes)",
                type=ParameterType.Number,
                description="Minimum file size in bytes (-1 to disable)",
                default_value=-1,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=6)
                ]
            ),
            CommandParameter(
                name="max_size", cli_name="max_size",
                display_name="Max Size (bytes)",
                type=ParameterType.Number,
                description="Maximum file size in bytes (-1 to disable)",
                default_value=-1,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=7)
                ]
            ),
            CommandParameter(
                name="newer_than", cli_name="newer_than",
                display_name="Newer Than",
                type=ParameterType.String,
                description="Only files modified after this date (YYYY-MM-DD)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=8)
                ]
            ),
            CommandParameter(
                name="older_than", cli_name="older_than",
                display_name="Older Than",
                type=ParameterType.String,
                description="Only files modified before this date (YYYY-MM-DD)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=9)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class FindCommand(CommandBase):
    cmd = "find"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "find -path /etc -pattern *.conf"
    description = "Recursive filesystem search with filters"
    version = 1
    author = "@checkymander"
    argument_class = FindArguments
    attackmapping = ["T1083"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = f"{taskData.args.get_arg('pattern')} in {taskData.args.get_arg('path')}"
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
```

- [ ] **Step 2: Commit**

```bash
git add Payload_Type/athena/athena/mythic/agent_functions/find.py
git commit -m "feat: add find Mythic command definition"
```

---

### Task 10: Create `file-utils` plugin DLL

**Files:**
- Create: `AGENT/file-utils/file-utils.csproj`
- Create: `AGENT/file-utils/FileUtilsArgs.cs`
- Create: `AGENT/file-utils/file-utils.cs`
- Create: `TESTS/PluginTests/FileUtilsTests.cs`
- Modify: `TESTS/Workflow.Tests.csproj` — add project reference

This is a multi-action plugin: `head`, `touch`, `wc`, `diff`, `link`, `chmod`, `chown`.

- [ ] **Step 1: Write FileUtilsTests.cs**

```csharp
// {TESTS}/PluginTests/FileUtilsTests.cs
using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("FileOps")]
    public class FileUtilsTests : PluginTestBase
    {
        public FileUtilsTests()
        {
            LoadPlugin("file-utils");
        }

        [TestMethod]
        public async Task TestHead_FirstNLines()
        {
            string content = "line1\nline2\nline3\nline4\nline5\nline6";
            string tempFile = Utilities.CreateTempFileWithContent(content);

            var response = await ExecuteAndGetResponse(
                CreateJob("file-utils", new
                {
                    action = "head",
                    path = tempFile,
                    lines = 3
                }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("line1"));
            Assert.IsTrue(response.user_output.Contains("line3"));
            Assert.IsFalse(response.user_output.Contains("line4"));

            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task TestHead_FileNotFound()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("file-utils", new
                {
                    action = "head",
                    path = "/tmp/nonexistent_file_xyz.txt"
                }));

            AssertError(response);
        }

        [TestMethod]
        public async Task TestWc_CountLines()
        {
            string content = "line1\nline2\nline3";
            string tempFile = Utilities.CreateTempFileWithContent(content);

            var response = await ExecuteAndGetResponse(
                CreateJob("file-utils", new
                {
                    action = "wc",
                    path = tempFile
                }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("3"));

            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task TestDiff_TwoFiles()
        {
            string file1 = Utilities.CreateTempFileWithContent("line1\nline2\nline3");
            string file2 = Utilities.CreateTempFileWithContent("line1\nmodified\nline3");

            var response = await ExecuteAndGetResponse(
                CreateJob("file-utils", new
                {
                    action = "diff",
                    path = file1,
                    path2 = file2
                }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("line2"));
            Assert.IsTrue(response.user_output.Contains("modified"));

            File.Delete(file1);
            File.Delete(file2);
        }

        [TestMethod]
        public async Task TestTouch_CreateFile()
        {
            string tempFile = Path.Combine(
                Path.GetTempPath(), $"touch_test_{Guid.NewGuid()}.txt");

            var response = await ExecuteAndGetResponse(
                CreateJob("file-utils", new
                {
                    action = "touch",
                    path = tempFile
                }));

            AssertSuccess(response);
            Assert.IsTrue(File.Exists(tempFile));

            File.Delete(tempFile);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

Run: `dotnet test Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/ --filter "ClassName~FileUtilsTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Create file-utils.csproj, FileUtilsArgs.cs, file-utils.cs**

`file-utils.csproj` — same template as find.csproj.

```csharp
// {AGENT}/file-utils/FileUtilsArgs.cs
namespace fileutils
{
    public class FileUtilsArgs
    {
        public string action { get; set; } = "head";
        public string path { get; set; } = "";
        public string path2 { get; set; } = "";
        public int lines { get; set; } = 10;
        public string mode { get; set; } = "";
        public string owner { get; set; } = "";
        public string group { get; set; } = "";
        public string link_type { get; set; } = "symbolic";
    }
}
```

```csharp
// {AGENT}/file-utils/file-utils.cs
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "file-utils";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<fileutils.FileUtilsArgs>(
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
                    "head" => ExecuteHead(args),
                    "touch" => ExecuteTouch(args),
                    "wc" => ExecuteWc(args),
                    "diff" => ExecuteDiff(args),
                    "link" => ExecuteLink(args),
                    "chmod" => ExecuteChmod(args),
                    "chown" => ExecuteChown(args),
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
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private string ExecuteHead(fileutils.FileUtilsArgs args)
        {
            if (!File.Exists(args.path))
                throw new FileNotFoundException(
                    $"File not found: {args.path}");

            var lines = File.ReadLines(args.path).Take(args.lines);
            return string.Join(Environment.NewLine, lines);
        }

        private string ExecuteTouch(fileutils.FileUtilsArgs args)
        {
            if (File.Exists(args.path))
            {
                File.SetLastWriteTimeUtc(args.path, DateTime.UtcNow);
                return $"Updated timestamp: {args.path}";
            }
            File.Create(args.path).Dispose();
            return $"Created: {args.path}";
        }

        private string ExecuteWc(fileutils.FileUtilsArgs args)
        {
            if (!File.Exists(args.path))
                throw new FileNotFoundException(
                    $"File not found: {args.path}");

            int lineCount = 0;
            int wordCount = 0;
            long byteCount = new FileInfo(args.path).Length;

            foreach (var line in File.ReadLines(args.path))
            {
                lineCount++;
                wordCount += line.Split(
                    (char[])null,
                    StringSplitOptions.RemoveEmptyEntries).Length;
            }

            return $"{lineCount} lines, {wordCount} words, {byteCount} bytes\t{args.path}";
        }

        private string ExecuteDiff(fileutils.FileUtilsArgs args)
        {
            if (!File.Exists(args.path))
                throw new FileNotFoundException(
                    $"File not found: {args.path}");
            if (!File.Exists(args.path2))
                throw new FileNotFoundException(
                    $"File not found: {args.path2}");

            var lines1 = File.ReadAllLines(args.path);
            var lines2 = File.ReadAllLines(args.path2);
            var diff = new List<string>();
            int maxLines = Math.Max(lines1.Length, lines2.Length);

            for (int i = 0; i < maxLines; i++)
            {
                string l1 = i < lines1.Length ? lines1[i] : "";
                string l2 = i < lines2.Length ? lines2[i] : "";
                if (l1 != l2)
                {
                    diff.Add($"@@ line {i + 1} @@");
                    if (i < lines1.Length) diff.Add($"- {l1}");
                    if (i < lines2.Length) diff.Add($"+ {l2}");
                }
            }

            return diff.Count > 0
                ? string.Join(Environment.NewLine, diff)
                : "Files are identical.";
        }

        private string ExecuteLink(fileutils.FileUtilsArgs args)
        {
            if (args.link_type == "symbolic")
                File.CreateSymbolicLink(args.path2, args.path);
            else
                throw new NotSupportedException(
                    "Hard links require platform-specific P/Invoke");

            return $"Created {args.link_type} link: {args.path2} -> {args.path}";
        }

        private string ExecuteChmod(fileutils.FileUtilsArgs args)
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return "chmod is only supported on Linux/macOS";

            var mode = Convert.ToInt32(args.mode, 8);
            File.SetUnixFileMode(args.path, (UnixFileMode)mode);
            return $"Changed mode of {args.path} to {args.mode}";
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int chown(string path, int owner, int group);

        private string ExecuteChown(fileutils.FileUtilsArgs args)
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return "chown is only supported on Linux/macOS";

            int uid = int.Parse(args.owner);
            int gid = int.Parse(args.group);
            int result = chown(args.path, uid, gid);

            if (result != 0)
                throw new Exception(
                    $"chown failed with error code: {Marshal.GetLastWin32Error()}");

            return $"Changed owner of {args.path} to {args.owner}:{args.group}";
        }
    }
}
```

- [ ] **Step 4: Add project reference to test .csproj**

Add to `TESTS/Workflow.Tests.csproj`:
```xml
    <ProjectReference Include="..\..\file-utils\file-utils.csproj" />
```

- [ ] **Step 5: Run tests — verify they pass**

Run: `dotnet test Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/ --filter "ClassName~FileUtilsTests" -v minimal`
Expected: All tests PASS

- [ ] **Step 6: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/file-utils/ \
        Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/PluginTests/FileUtilsTests.cs \
        Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/Workflow.Tests.csproj
git commit -m "feat: add file-utils plugin with tests"
```

---

### Task 11: Create `file-utils` Mythic command + wrapper commands

**Files:**
- Create: `MYTHIC/file-utils.py` (primary — loads DLL, `depends_on = None`)
- Create: `MYTHIC/wc.py` (wrapper, `depends_on = "file-utils"`)
- Create: `MYTHIC/diff.py` (wrapper)
- Create: `MYTHIC/touch.py` (wrapper)
- Create: `MYTHIC/link.py` (wrapper)
- Create: `MYTHIC/chmod.py` (wrapper)
- Create: `MYTHIC/chown.py` (wrapper)
- Create: `MYTHIC/stat.py` (wrapper — subtasks to `ls`, `depends_on = None`)

Each wrapper follows the standard pattern from the Key Patterns Reference above. The primary command `file-utils.py` is like `find.py` but its `cmd = "head"` (operators run `load file-utils` to get all of them).

- [ ] **Step 1: Create file-utils.py (primary command, cmd="file-utils")**

**Critical:** The `cmd` must match the DLL directory name (`file-utils`) because `load.py` uses `cmd` to locate the plugin directory at `agent_code/{cmd}/`. If `cmd` were `"head"`, `load head` would look for `agent_code/head/` which doesn't exist. Operators type `load file-utils` and get the `file-utils` command plus all wrapper commands (`wc`, `diff`, `touch`, `link`, `chmod`, `chown`) registered via `depends_on`.

```python
# {MYTHIC}/file-utils.py
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

class FileUtilsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["head", "touch", "wc", "diff", "link", "chmod", "chown"],
                default_value="head",
                description="File utility action",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="path", cli_name="path",
                display_name="Path",
                type=ParameterType.String,
                description="File path",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="path2", cli_name="path2",
                display_name="Second Path",
                type=ParameterType.String,
                description="Second file path (for diff, link)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
            CommandParameter(
                name="lines", cli_name="lines",
                display_name="Lines",
                type=ParameterType.Number,
                description="Number of lines (for head)",
                default_value=10,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=3)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class FileUtilsCommand(CommandBase):
    cmd = "file-utils"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "file-utils -action head -path /etc/passwd -lines 10"
    description = "File utilities: head, touch, wc, diff, link, chmod, chown"
    version = 1
    author = "@checkymander"
    argument_class = FileUtilsArguments
    attackmapping = ["T1005"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
```

- [ ] **Step 2: Create wc.py wrapper**

```python
# {MYTHIC}/wc.py
from .athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class WcArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path", cli_name="path",
                display_name="File Path",
                type=ParameterType.String,
                description="File to count lines/words/bytes",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("path", self.command_line.strip())

class WcCommand(CommandBase):
    cmd = "wc"
    needs_admin = False
    script_only = True
    depends_on = "file-utils"
    plugin_libraries = []
    help_cmd = "wc /path/to/file"
    description = "Count lines, words, and bytes in a file"
    version = 1
    author = "@checkymander"
    argument_class = WcArguments
    attackmapping = ["T1005"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="file-utils",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "wc",
                "path": taskData.args.get_arg("path"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
```

- [ ] **Step 3: Create remaining wrappers (diff, touch, link, chmod, chown)**

Each follows the exact same pattern as `wc.py`, changing:
- `cmd`, class names, `help_cmd`, `description`
- The `action` parameter in the subtask JSON
- Any additional args the wrapper needs to pass

Create `diff.py`, `touch.py`, `link.py`, `chmod.py`, `chown.py` following the wrapper pattern.

- [ ] **Step 4: Create stat.py (wraps `ls`, depends_on = None)**

`stat.py` subtasks to `ls` (a built-in command always available), so `depends_on = None`:

```python
# {MYTHIC}/stat.py
from .athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class StatArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path", cli_name="path",
                display_name="File Path",
                type=ParameterType.String,
                description="File to get stats for",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("path", self.command_line.strip())

class StatCommand(CommandBase):
    cmd = "stat"
    needs_admin = False
    script_only = True
    depends_on = None
    plugin_libraries = []
    help_cmd = "stat /path/to/file"
    description = "Get detailed file metadata"
    version = 1
    author = "@checkymander"
    argument_class = StatArguments
    attackmapping = ["T1083"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="ls",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "path": taskData.args.get_arg("path"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
```

- [ ] **Step 5: Commit**

```bash
git add Payload_Type/athena/athena/mythic/agent_functions/file-utils.py \
        Payload_Type/athena/athena/mythic/agent_functions/wc.py \
        Payload_Type/athena/athena/mythic/agent_functions/diff.py \
        Payload_Type/athena/athena/mythic/agent_functions/touch.py \
        Payload_Type/athena/athena/mythic/agent_functions/link.py \
        Payload_Type/athena/athena/mythic/agent_functions/chmod.py \
        Payload_Type/athena/athena/mythic/agent_functions/chown.py \
        Payload_Type/athena/athena/mythic/agent_functions/stat.py
git commit -m "feat: add file-utils Mythic command + wrappers (wc, diff, touch, link, chmod, chown, stat)"
```

---

### Task 12: Create `hash` plugin DLL + Mythic commands

**Files:**
- Create: `AGENT/hash/hash.csproj`, `AGENT/hash/HashArgs.cs`, `AGENT/hash/hash.cs`
- Create: `TESTS/PluginTests/HashTests.cs`
- Create: `MYTHIC/hash.py`, `MYTHIC/base64.py` (wrapper)
- Modify: `TESTS/Workflow.Tests.csproj`

- [ ] **Step 1: Write HashTests.cs**

```csharp
// {TESTS}/PluginTests/HashTests.cs
using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("FileOps")]
    public class HashTests : PluginTestBase
    {
        public HashTests()
        {
            LoadPlugin("hash");
        }

        [TestMethod]
        public async Task TestHash_Sha256()
        {
            string tempFile = Utilities.CreateTempFileWithContent("test content");

            var response = await ExecuteAndGetResponse(
                CreateJob("hash", new
                {
                    action = "hash",
                    path = tempFile,
                    algorithm = "sha256"
                }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Length == 64);

            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task TestHash_FileNotFound()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("hash", new
                {
                    action = "hash",
                    path = "/nonexistent/file.txt"
                }));

            AssertError(response);
        }

        [TestMethod]
        public async Task TestBase64_Encode()
        {
            string tempFile = Utilities.CreateTempFileWithContent("hello world");

            var response = await ExecuteAndGetResponse(
                CreateJob("hash", new
                {
                    action = "base64",
                    path = tempFile,
                    encode = true
                }));

            AssertSuccess(response);
            Assert.AreEqual("aGVsbG8gd29ybGQ=", response.user_output.Trim());

            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task TestBase64_Decode()
        {
            string tempFile = Utilities.CreateTempFileWithContent("aGVsbG8gd29ybGQ=");

            var response = await ExecuteAndGetResponse(
                CreateJob("hash", new
                {
                    action = "base64",
                    path = tempFile,
                    encode = false
                }));

            AssertSuccess(response);
            Assert.AreEqual("hello world", response.user_output.Trim());

            File.Delete(tempFile);
        }
    }
}
```

- [ ] **Step 2: Create hash.csproj, HashArgs.cs, hash.cs**

Standard plugin structure. `hash.cs` implements:
- `action = "hash"`: Read file, compute `MD5`/`SHA1`/`SHA256` via `System.Security.Cryptography`, return hex string
- `action = "base64"`: Read file, `Convert.ToBase64String` or `Convert.FromBase64String`

- [ ] **Step 3: Add project reference, run tests**

- [ ] **Step 4: Create hash.py and base64.py Mythic commands**

`hash.py`: standard command, `depends_on = None`
`base64.py`: wrapper, `depends_on = "hash"`, subtasks to `hash` with `action: "base64"`

- [ ] **Step 5: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/hash/ \
        Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/PluginTests/HashTests.cs \
        Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/Workflow.Tests.csproj \
        Payload_Type/athena/athena/mythic/agent_functions/hash.py \
        Payload_Type/athena/athena/mythic/agent_functions/base64.py
git commit -m "feat: add hash plugin + base64 wrapper with tests"
```

---

### Task 13: Create `find` wrapper commands (grep, suid-find, writable-paths)

**Files:**
- Create: `MYTHIC/grep.py` (`depends_on = "find"`)
- Create: `MYTHIC/suid-find.py` (`depends_on = "find"`)
- Create: `MYTHIC/writable-paths.py` (`depends_on = "find"`)

Each wrapper subtasks to `find` with pre-configured parameters.

- [ ] **Step 1: Create grep.py**

Subtasks to `find` with `action: "grep"`, passing user's `content_pattern` and `path`.

- [ ] **Step 2: Create suid-find.py**

Subtasks to `find` with `permissions: "suid"`, searching common binary paths (`/usr/bin`, `/usr/sbin`, `/usr/local/bin`).

- [ ] **Step 3: Create writable-paths.py**

Subtasks to `find` with `permissions: "world-writable"`.

- [ ] **Step 4: Commit**

```bash
git add Payload_Type/athena/athena/mythic/agent_functions/grep.py \
        Payload_Type/athena/athena/mythic/agent_functions/suid-find.py \
        Payload_Type/athena/athena/mythic/agent_functions/writable-paths.py
git commit -m "feat: add find wrappers (grep, suid-find, writable-paths)"
```

---

### Task 14: Add thorough tests for existing file-ops plugins

**Files:**
- Create: `TESTS/PluginTests/CpTests.cs`
- Create: `TESTS/PluginTests/CdTests.cs`
- Create: `TESTS/PluginTests/MkdirTests.cs`
- Create: `TESTS/PluginTests/MvTests.cs`
- Create: `TESTS/PluginTests/RmTests.cs`
- Create: `TESTS/PluginTests/TailTests.cs` (note: this file was previously deleted — recreating with updated patterns using `PluginTestBase`)

Each test class extends `PluginTestBase` and covers: happy path, error path (file not found, permission denied), edge cases (empty input, special chars in path).

- [ ] **Step 1: Write test files for cp, cd, mkdir, mv, rm, tail**

Follow the CatTests.cs pattern. Each test class should have 3-5 tests covering the main scenarios for that plugin. Use `PluginTestBase` and `JobBuilder` for consistency.

- [ ] **Step 2: Run all file-ops tests**

Run: `dotnet test Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/ --filter "TestCategory=FileOps" -v minimal`
Expected: All pass

- [ ] **Step 3: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/PluginTests/
git commit -m "test: add thorough tests for existing file-ops plugins (cp, cd, mkdir, mv, rm, tail)"
```

---

## Chunk 3: Phase 2 — System Info & Phase 3 — Network Operations

### Task 15: Create `sysinfo` plugin DLL + wrappers

**Files:**
- Create: `AGENT/sysinfo/sysinfo.csproj`, `AGENT/sysinfo/SysinfoArgs.cs`, `AGENT/sysinfo/sysinfo.cs`
- Create: `TESTS/PluginTests/SysinfoTests.cs`
- Create: `MYTHIC/sysinfo.py`, `MYTHIC/id.py`, `MYTHIC/container-detect.py`, `MYTHIC/mount.py`, `MYTHIC/package-list.py`, `MYTHIC/dotnet-versions.py`
- Modify: `TESTS/Workflow.Tests.csproj`

Multi-action plugin handling: `sysinfo`, `id`, `container-detect`, `mount`, `package-list`, `dotnet-versions`.

- [ ] **Step 1: Write SysinfoTests.cs**

Test the `sysinfo` and `id` actions (these work on all platforms). Test `container-detect` (should return results even if not in container). Test error case for unknown action.

- [ ] **Step 2: Create sysinfo.csproj, SysinfoArgs.cs, sysinfo.cs**

`sysinfo.cs` uses:
- `Environment.OSVersion`, `RuntimeInformation`, `Environment.MachineName`, `Environment.UserName`
- `System.Net.Dns.GetHostAddresses` for IPs
- `DriveInfo.GetDrives()` for drives
- P/Invoke `getuid`/`getgid`/`getgroups` for Linux/macOS `id`
- `WindowsIdentity` for Windows `id`
- File checks for `container-detect` (`/.dockerenv`, cgroup, etc.)
- File parsing for `package-list` and `mount`
- Directory scanning for `dotnet-versions`

All BCL + P/Invoke. No WMI.

- [ ] **Step 3: Add project reference, run tests**
- [ ] **Step 4: Create Mythic commands (sysinfo.py + 5 wrappers)**

`sysinfo.py`: `depends_on = None`, primary command
`id.py`, `container-detect.py`, `mount.py`, `package-list.py`, `dotnet-versions.py`: all `script_only = True`, `depends_on = "sysinfo"`

- [ ] **Step 5: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/sysinfo/ \
        Payload_Type/athena/athena/agent_code/Tests/ \
        Payload_Type/athena/athena/mythic/agent_functions/sysinfo.py \
        Payload_Type/athena/athena/mythic/agent_functions/id.py \
        Payload_Type/athena/athena/mythic/agent_functions/container-detect.py \
        Payload_Type/athena/athena/mythic/agent_functions/mount.py \
        Payload_Type/athena/athena/mythic/agent_functions/package-list.py \
        Payload_Type/athena/athena/mythic/agent_functions/dotnet-versions.py
git commit -m "feat: add sysinfo plugin + wrappers (id, container-detect, mount, package-list, dotnet-versions)"
```

---

### Task 16: Add smoke tests for existing system info plugins

**Files:**
- Create: `TESTS/PluginTests/EnvTests.cs`
- Create: `TESTS/PluginTests/HostnameTests.cs`
- Create: `TESTS/PluginTests/WhoamiTests.cs`
- Create: `TESTS/PluginTests/UptimeTests.cs`
- Create: `TESTS/PluginTests/DrivesTests.cs`
- Create: `TESTS/PluginTests/PsTests.cs`

Each smoke test: one happy path + one error case. Use `PluginTestBase`.

- [ ] **Step 1: Write smoke tests**
- [ ] **Step 2: Run tests, verify pass**
- [ ] **Step 3: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/PluginTests/
git commit -m "test: add smoke tests for env, hostname, whoami, uptime, drives, ps"
```

---

### Task 17: Create `ping` plugin DLL + traceroute wrapper

**Files:**
- Create: `AGENT/ping/ping.csproj`, `AGENT/ping/PingArgs.cs`, `AGENT/ping/ping.cs`
- Create: `TESTS/PluginTests/PingTests.cs`
- Create: `MYTHIC/ping.py`, `MYTHIC/traceroute.py`
- Modify: `TESTS/Workflow.Tests.csproj`

- [ ] **Step 1: Write PingTests.cs**

Test ping to `127.0.0.1` (always reachable). Test ping to unreachable host. Test traceroute action to `127.0.0.1`.

- [ ] **Step 2: Create ping.csproj, PingArgs.cs, ping.cs**

Uses `System.Net.NetworkInformation.Ping.SendPingAsync()`. For traceroute: send pings with TTL 1..30. For CIDR sweep: parse CIDR notation and iterate IPs.

- [ ] **Step 3: Add project reference, run tests**
- [ ] **Step 4: Create Mythic commands**

`ping.py`: `depends_on = None`
`traceroute.py`: `script_only = True`, `depends_on = "ping"`, subtasks to `ping` with `action: "traceroute"`

- [ ] **Step 5: Commit**

```bash
git add Payload_Type/athena/athena/agent_code/ping/ \
        Payload_Type/athena/athena/agent_code/Tests/ \
        Payload_Type/athena/athena/mythic/agent_functions/ping.py \
        Payload_Type/athena/athena/mythic/agent_functions/traceroute.py
git commit -m "feat: add ping plugin + traceroute wrapper with tests"
```

---

### Task 18: Create `dns` plugin DLL

**Files:**
- Create: `AGENT/dns/dns.csproj`, `AGENT/dns/DnsArgs.cs`, `AGENT/dns/dns.cs`
- Create: `TESTS/PluginTests/DnsTests.cs`
- Create: `MYTHIC/dns.py`
- Modify: `TESTS/Workflow.Tests.csproj`

**Note:** This plugin has significant P/Invoke complexity (see spec: `dns` Plugin Complexity). Start with A/AAAA/CNAME records. Add MX/SRV/TXT/SOA/PTR/NS incrementally.

- [ ] **Step 1: Write DnsTests.cs**

Test A record lookup for `localhost`. Test invalid hostname. Test CNAME lookup.

- [ ] **Step 2: Create dns plugin with Windows P/Invoke (`DnsQuery_A` from `dnsapi.dll`)**
- [ ] **Step 3: Create dns plugin with Linux/macOS fallback (use `System.Net.Dns` as simpler starting point, add `res_query` P/Invoke later)**
- [ ] **Step 4: Run tests, commit**

```bash
git commit -m "feat: add dns plugin with A/AAAA/CNAME support and tests"
```

---

### Task 19: Create `http-request` plugin DLL + wget wrapper

**Files:**
- Create: `AGENT/http-request/http-request.csproj`, `AGENT/http-request/HttpRequestArgs.cs`, `AGENT/http-request/http-request.cs`
- Create: `TESTS/PluginTests/HttpRequestTests.cs`
- Create: `MYTHIC/http-request.py`, `MYTHIC/wget.py` (wrapper replacing existing DLL)
- Modify: `TESTS/Workflow.Tests.csproj`
- Delete: `AGENT/wget/` (existing DLL — replaced by wrapper)
- Modify: `TESTS/Workflow.Tests.csproj` — remove `wget.csproj` reference

- [ ] **Step 1: Write HttpRequestTests.cs**

Test GET to a local HTTP server (use `Utilities.CreateLocalHttpServer()` from Task 6). Test POST with body. Test invalid URL error. Test `download` action saves to file.

- [ ] **Step 2: Create http-request plugin**

Uses `System.Net.Http.HttpClient`. Supports GET/POST/PUT/DELETE/PATCH/HEAD/OPTIONS. For `download` action: stream to file.

- [ ] **Step 3: Delete old wget DLL**

```bash
rm -rf Payload_Type/athena/athena/agent_code/wget/
```

Remove `wget.csproj` reference from `TESTS/Workflow.Tests.csproj`:
Delete the line: `<ProjectReference Include="..\..\wget\wget.csproj" />`

- [ ] **Step 4: Create wget.py wrapper**

Create `MYTHIC/wget.py`: `script_only = True`, `depends_on = "http-request"`, subtasks to `http-request` with `action: "download"`.

- [ ] **Step 5: Run tests, commit**

```bash
git add -A
git commit -m "feat: add http-request plugin, replace wget DLL with wrapper"
```

---

### Task 20: Add thorough tests for ssh/sftp and smoke tests for network plugins

**Files:**
- Create: `TESTS/PluginTests/SshTests.cs` (thorough — as required by Phase 3 spec)
- Create: `TESTS/PluginTests/SftpTests.cs` (thorough — as required by Phase 3 spec)
- Create: `TESTS/PluginTests/NslookupTests.cs`
- Create: `TESTS/PluginTests/TestPortTests.cs`
- Create: `TESTS/PluginTests/ArpTests.cs`
- Create: `TESTS/PluginTests/NetstatTests.cs`

Note: ssh/sftp tests need a local SSH server or mock. Test parameter validation and error paths at minimum. Consider using a local SSH server in CI if available.

- [ ] **Step 1: Write smoke tests**
- [ ] **Step 2: Run tests, commit**

```bash
git commit -m "test: add smoke tests for nslookup, test-port, arp, netstat"
```

---

## Chunk 4: Phase 4 — Credentials & Collection

### Task 21: Create `credentials` plugin DLL + wrappers

**Files:**
- Create: `AGENT/credentials/credentials.csproj`, `AGENT/credentials/CredentialArgs.cs`, `AGENT/credentials/credentials.cs`
- Create: `TESTS/PluginTests/CredentialsTests.cs`
- Create: `MYTHIC/credentials.py` + 7 wrapper `.py` files
- Modify: `TESTS/Workflow.Tests.csproj`

Multi-platform credential harvesting with actions: `dpapi`, `vault-enum`, `wifi-profiles`, `dns-cache`, `shadow-read`, `lsass-dump`, `sam-dump`. Many actions are platform-specific — test appropriately.

- [ ] **Step 1: Write CredentialsTests.cs**

Test `dns-cache` action on Windows (should succeed or gracefully report unavailable). Test `shadow-read` with a mock file. Test unknown action error.

- [ ] **Step 2: Create credentials plugin**

Platform-gated actions via `OperatingSystem.IsWindows()` / `OperatingSystem.IsLinux()`. P/Invoke for vault-enum, wifi-profiles, dns-cache, lsass-dump. File parsing for shadow-read.

- [ ] **Step 3: Create Mythic commands**

`credentials.py`: `depends_on = None`, primary command
7 wrappers: `dpapi.py`, `vault-enum.py`, `wifi-profiles.py`, `dns-cache.py`, `shadow-read.py`, `lsass-dump.py`, `sam-dump.py` — all `depends_on = "credentials"`

**OPSEC note:** `lsass-dump.py` must include warning in `description`: "WARNING: MiniDumpWriteDump on lsass.exe is heavily monitored by EDR."

- [ ] **Step 4: Run tests, commit**

```bash
git commit -m "feat: add credentials plugin + wrappers with tests"
```

---

### Task 22: Create `proc-enum` plugin DLL + named-pipes wrapper

**Files:**
- Create: `AGENT/proc-enum/proc-enum.csproj`, `AGENT/proc-enum/ProcEnumArgs.cs`, `AGENT/proc-enum/proc-enum.cs`
- Create: `TESTS/PluginTests/ProcEnumTests.cs`
- Create: `MYTHIC/proc-enum.py`, `MYTHIC/named-pipes.py`
- Modify: `TESTS/Workflow.Tests.csproj`

- [ ] **Step 1: Write ProcEnumTests.cs**

Test `proc-enum` action (should list current process at minimum). Test `named-pipes` action on Windows.

- [ ] **Step 2: Create proc-enum plugin**

Linux: parse `/proc/[pid]/cmdline`, `/proc/[pid]/environ`. Windows `named-pipes`: `Directory.GetFiles(@"\\.\pipe\")` or P/Invoke.

- [ ] **Step 3: Create Mythic commands, commit**

```bash
git commit -m "feat: add proc-enum plugin + named-pipes wrapper with tests"
```

---

### Task 23: Create `kerberos` plugin DLL

**Files:**
- Create: `AGENT/kerberos/kerberos.csproj`, `AGENT/kerberos/KerberosArgs.cs`, `AGENT/kerberos/kerberos.cs`
- Create: `TESTS/PluginTests/KerberosTests.cs`
- Create: `MYTHIC/kerberos.py`
- Modify: `TESTS/Workflow.Tests.csproj`

Windows-only. Uses SSPI P/Invoke (`secur32.dll`, `advapi32.dll`).

- [ ] **Step 1: Write KerberosTests.cs** (platform-gated tests)
- [ ] **Step 2: Create kerberos plugin**
- [ ] **Step 3: Create Mythic command, commit**

```bash
git commit -m "feat: add kerberos plugin with tests"
```

---

### Task 24: Create `clipboard-monitor` plugin DLL

**Files:**
- Create: `AGENT/clipboard-monitor/clipboard-monitor.csproj`, `AGENT/clipboard-monitor/ClipboardMonitorArgs.cs`, `AGENT/clipboard-monitor/clipboard-monitor.cs`
- Create: `TESTS/PluginTests/ClipboardMonitorTests.cs`
- Create: `MYTHIC/clipboard-monitor.py`
- Modify: `TESTS/Workflow.Tests.csproj`

Long-running job. Uses P/Invoke for clipboard access.

- [ ] **Step 1: Write ClipboardMonitorTests.cs** (smoke test: start + cancel)
- [ ] **Step 2: Create clipboard-monitor plugin**
- [ ] **Step 3: Create Mythic command, commit**

```bash
git commit -m "feat: add clipboard-monitor plugin with tests"
```

---

### Task 25: Create `ssh-recon` plugin DLL + wrappers

**Files:**
- Create: `AGENT/ssh-recon/ssh-recon.csproj`, `AGENT/ssh-recon/SshReconArgs.cs`, `AGENT/ssh-recon/ssh-recon.cs`
- Create: `TESTS/PluginTests/SshReconTests.cs`
- Create: `MYTHIC/ssh-recon.py`, `MYTHIC/ssh-keys.py`, `MYTHIC/authorized-keys.py`
- Modify: `TESTS/Workflow.Tests.csproj`

Linux/macOS. All file I/O via BCL.

- [ ] **Step 1: Write SshReconTests.cs**

Test `ssh-keys` action with a temp `.ssh/` directory containing test key files. Test `authorized-keys-read` with a temp authorized_keys file.

- [ ] **Step 2: Create ssh-recon plugin**

Enumerate `~/.ssh/`, `/home/*/.ssh/`, `/etc/ssh/`. Parse key files, report type/fingerprint/permissions. Read/write `authorized_keys`.

- [ ] **Step 3: Create Mythic commands (ssh-recon.py + 2 wrappers), commit**

```bash
git commit -m "feat: add ssh-recon plugin + ssh-keys/authorized-keys wrappers with tests"
```

---

### Task 26: Add tests for existing credential/collection plugins + privesc wrappers

**Files:**
- Create: `TESTS/PluginTests/TokenTests.cs`
- Create: `TESTS/PluginTests/KeyloggerTests.cs`
- Create: smoke tests for `get-clipboard`, `farmer`, `crop`
- Create: `MYTHIC/sudo-check.py`, `MYTHIC/capabilities.py`, `MYTHIC/ld-preload.py`, `MYTHIC/selinux-status.py`, `MYTHIC/pam-enum.py`, `MYTHIC/iptables-enum.py`, `MYTHIC/firewall-enum.py`, `MYTHIC/uac-check.py`, `MYTHIC/amsi-status.py`

The privesc/security wrappers subtask to built-in commands (`cat`, `env`, `reg`, `find`, `proc-enum`) — see spec tables.

- [ ] **Step 1: Write tests for token, keylogger**
- [ ] **Step 2: Create privesc wrapper commands**

All wrappers below. `depends_on` for each:

| Wrapper | Subtasks To | `depends_on` |
|---------|-------------|-------------|
| `sudo-check.py` | `cat` (reads `/etc/sudoers`, `/etc/sudoers.d/*`) | `None` (cat is built-in) |
| `capabilities.py` | subtask group: `find` + `proc-enum` | `"find"` (first non-builtin parent) |
| `ld-preload.py` | subtask group: `cat` + `env` | `None` (both built-in) |
| `selinux-status.py` | `cat` (reads `/sys/fs/selinux/enforce`, `/etc/selinux/config`) | `None` |
| `pam-enum.py` | subtask group: `find` + `cat` | `"find"` (find is dynamically loaded) |
| `iptables-enum.py` | `cat` (reads `/proc/net/ip_tables_names`, `/proc/net/nf_conntrack`) | `None` |
| `firewall-enum.py` | `reg` (Windows) / `cat` (Linux) — platform-aware | `None` (both built-in) |
| `uac-check.py` | `reg` (reads UAC policy keys) | `None` |
| `amsi-status.py` | `reg` (reads AMSI provider keys) | `None` |

**Note:** These wrappers are in the spec's Phase 6 but are placed here because all their dependencies (built-in commands + `find` from Phase 1) are already available. No functional issue.

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add privesc/security wrappers + credential plugin tests"
```

---

## Chunk 5: Phases 5-8 — Execution, Recon, macOS, Windows

### Task 27: Phase 5 — Execution & Process tests (no new DLLs)

**Files:**
- Create: `TESTS/PluginTests/ShellTests.cs`
- Create: `TESTS/PluginTests/ExecTests.cs`
- Create: `TESTS/PluginTests/ExecuteAssemblyTests.cs`
- Create: `TESTS/PluginTests/CoffTests.cs`
- Create: `TESTS/PluginTests/InjectShellcodeTests.cs`
- Create: smoke tests for `kill`, `jobs`, `jobkill`, `shellcode`

- [ ] **Step 1: Write thorough tests for shell, exec, execute-assembly, coff, inject-shellcode**

Shell test: execute `echo hello` and verify output. Exec test: similar.

**Test payload notes:**
- `execute-assembly` tests: Build a minimal "hello world" .NET console app as a test assembly embedded in the test project, or test only parameter validation and error paths (e.g., invalid assembly bytes)
- `coff` tests: Use a minimal test BOF or test parameter validation/error paths only
- `inject-shellcode` tests: Test parameter validation and error handling; actual injection requires elevated privileges and is platform-specific — gate with `[TestCategory("RequiresAdmin")]`
- All assembly/shellcode tests need platform-specific gating via `OperatingSystem.IsWindows()` etc.

- [ ] **Step 2: Write smoke tests for kill, jobs, jobkill, shellcode**
- [ ] **Step 3: Run all tests, commit**

```bash
git commit -m "test: add execution plugin tests (shell, exec, execute-assembly, coff, inject-shellcode)"
```

---

### Task 28: Phase 6 — Create `wmi` plugin DLL + wrappers

**Files:**
- Create: `AGENT/wmi/wmi.csproj`, `AGENT/wmi/WmiArgs.cs`, `AGENT/wmi/wmi.cs`
- Create: `TESTS/PluginTests/WmiTests.cs`
- Create: `MYTHIC/wmi.py`, `MYTHIC/installed-software.py`, `MYTHIC/defender-status.py`, `MYTHIC/startup-items.py`
- Modify: `TESTS/Workflow.Tests.csproj`

Windows-only. Requires `System.Management` NuGet (~200KB).

- [ ] **Step 1: Create wmi.csproj with NuGet reference**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Configurations>Debug;Release;LocalDebugGitHub;LocalDebugHttp;LocalDebugWebsocket;LocalDebugSmb;LocalDebugDiscord</Configurations>
  </PropertyGroup>
  <!-- Obfuscation Replacement Placeholder Do Not Remove -->
  <ItemGroup>
    <PackageReference Include="System.Management" Version="9.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Workflow.Models\Workflow.Models.csproj" />
  </ItemGroup>
</Project>
```

Note: Look up latest stable `System.Management` version at implementation time.

- [ ] **Step 2: Write WmiTests.cs (Windows-gated)**
- [ ] **Step 3: Create wmi.cs — general WMI query executor**
- [ ] **Step 4: Create Mythic commands**

`wmi.py`: `depends_on = None`, `plugin_libraries = ["System.Management.dll"]`
`installed-software.py`, `defender-status.py`, `startup-items.py`: all `depends_on = "wmi"`, subtask to `wmi` with pre-built queries

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add wmi plugin + wrappers (installed-software, defender-status, startup-items)"
```

---

### Task 29: Phase 6 — Create `event-log` plugin DLL + etw-control wrapper

**Files:**
- Create: `AGENT/event-log/event-log.csproj`, `AGENT/event-log/EventLogArgs.cs`, `AGENT/event-log/event-log.cs`
- Create: `TESTS/PluginTests/EventLogTests.cs`
- Create: `MYTHIC/event-log.py`, `MYTHIC/etw-control.py`
- Modify: `TESTS/Workflow.Tests.csproj`

Windows-only. Requires `System.Diagnostics.EventLog` NuGet (~100KB).

- [ ] **Step 1-5: Follow standard plugin creation pattern**
- [ ] **Step 6: Commit**

```bash
git commit -m "feat: add event-log plugin + etw-control wrapper with tests"
```

---

### Task 30: Phase 6 — Create `bits-transfer` plugin DLL

**Files:**
- Create: `AGENT/bits-transfer/bits-transfer.csproj`, etc.
- Create: `MYTHIC/bits-transfer.py`

Windows-only. BITS via COM P/Invoke.

- [ ] **Step 1-4: Follow standard plugin creation pattern**
- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add bits-transfer plugin with tests"
```

---

### Task 31: Phase 6 — Create remaining recon/persistence wrappers

**Files:**
- Create: `MYTHIC/crontab.py` (subtask group: `find` + `cat`)
- Create: `MYTHIC/rdp-config.py` (subtasks to `reg`)
- Create: `MYTHIC/com-objects.py` (subtasks to `reg`)
- Create: `MYTHIC/proxy-config.py` (subtask group: `env` + `reg`)

- [ ] **Step 1: Create crontab.py using multi-subtask pattern**

```python
# Uses SendMythicRPCTaskCreateSubtaskGroup for find + cat
# See spec: Architecture: Multi-Subtask Pattern
```

- [ ] **Step 2: Create rdp-config.py, com-objects.py, proxy-config.py**
- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add recon/persistence wrappers (crontab, rdp-config, com-objects, proxy-config)"
```

---

### Task 32: Phase 6 — Add smoke tests for existing plugins

**Files:**
- Create: smoke tests for `reg`, `config`, `timestomp`, `screenshot`

- [ ] **Step 1: Write smoke tests**
- [ ] **Step 2: Commit**

```bash
git commit -m "test: add smoke tests for reg, config, timestomp, screenshot"
```

---

### Task 33: Phase 7 — Revive `jxa` plugin + macOS wrappers

**IMPORTANT:** The existing JXA code in `AGENT/jxa/` has broken imports (`OSXIntegration.Framework`, `Workflow.Framework`) and a `Mono/` subdirectory with Xamarin-era code. The `Native.cs` file is entirely commented out. This is NOT a simple PluginContext update — the JXA execution engine needs to be reimplemented using JavaScriptCore P/Invoke as described in the spec. The existing `ObjectiveC.cs` P/Invoke patterns in the codebase can serve as reference.

**Files:**
- Delete: `AGENT/jxa/Mono/` (broken Xamarin-era code)
- Delete: `AGENT/jxa/Native.cs` (entirely commented out)
- Rewrite: `AGENT/jxa/jxa.cs` (reimplement with JavaScriptCore P/Invoke)
- Modify: `AGENT/jxa/jxa.csproj` (update to net10.0, remove broken project references)
- Rename: `MYTHIC/jxa.why` -> `MYTHIC/jxa.py` (update to current API)
- Create: `TESTS/PluginTests/JxaTests.cs`
- Create: `MYTHIC/spotlight.py`, `MYTHIC/defaults-read.py`, `MYTHIC/osascript.py`, `MYTHIC/login-items.py`
- Modify: `TESTS/Workflow.Tests.csproj`

- [ ] **Step 1: Audit existing jxa code**

Read `AGENT/jxa/jxa.cs`, `AGENT/jxa/jxa.csproj`, all files in `AGENT/jxa/Mono/`, and `AGENT/jxa/Native.cs`. Identify what is salvageable vs broken.

- [ ] **Step 2: Clean up broken code**

```bash
rm -rf Payload_Type/athena/athena/agent_code/jxa/Mono/
rm Payload_Type/athena/athena/agent_code/jxa/Native.cs
```

Remove any broken project references (like `OSXIntegration.Framework`, `Workflow.Framework`) from `jxa.csproj`. Update `TargetFramework` to `net10.0`.

- [ ] **Step 3: Reimplement jxa.cs with JavaScriptCore P/Invoke**

The new implementation should:
- P/Invoke to `JavaScriptCore.framework` on macOS (`JSGlobalContextCreate`, `JSEvaluateScript`, `JSValueToStringCopy`)
- Accept JXA code as string parameter
- Execute and return output
- Follow the standard Plugin/PluginContext pattern
- Return "JXA is only supported on macOS" on non-macOS platforms

Reference: Check existing `ObjectiveC.cs` in the jxa directory for P/Invoke pattern examples. Use the spec section 15 (`jxa` plugin) for the execution model.

- [ ] **Step 4: Rename .why to .py, update Mythic command to current API**

```bash
mv Payload_Type/athena/athena/mythic/agent_functions/jxa.why \
   Payload_Type/athena/athena/mythic/agent_functions/jxa.py
```

Update the Python file to use current `CommandBase` API with `depends_on = None`, `plugin_libraries = []`.

- [ ] **Step 5: Write JxaTests.cs (macOS-gated)**

Test on macOS only. Test simple JXA expression evaluation. Test error handling for invalid scripts.

- [ ] **Step 6: Create JXA wrapper commands**

`spotlight.py`, `defaults-read.py`, `osascript.py`, `login-items.py`: all `depends_on = "jxa"`, subtask to `jxa` with pre-built JXA scripts.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: revive jxa plugin (reimplemented with JavaScriptCore P/Invoke) + macOS wrappers"
```

---

### Task 34: Phase 7 — Revive `keychain` plugin + macOS wrappers

**Files:**
- Rename: `MYTHIC/keychain.why` -> `MYTHIC/keychain.py`
- Modify: `AGENT/keychain/keychain.cs`, `keychain.csproj`
- Create: `TESTS/PluginTests/KeychainTests.cs`
- Create: `MYTHIC/security-tool.py`, `MYTHIC/tcc-check.py`, `MYTHIC/launchd-enum.py`, `MYTHIC/airport.py`

- [ ] **Step 1: Review/update existing keychain C# code**
- [ ] **Step 2: Rename .why, update Mythic command**
- [ ] **Step 3: Write KeychainTests.cs (macOS-gated)**
- [ ] **Step 4: Create macOS wrapper commands**

`tcc-check.py`: subtasks to `cat`, `depends_on = None`
`launchd-enum.py`: subtask group `find` + `cat`, `depends_on = "find"`
`security-tool.py`: `depends_on = "keychain"`
`airport.py`: subtasks to `cat`, `depends_on = None`

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: revive keychain plugin + add macOS wrappers (security-tool, tcc-check, launchd-enum, airport)"
```

---

### Task 35: Phase 7 — Create `inject-shellcode-macos` plugin DLL

**Files:**
- Create: `AGENT/inject-shellcode-macos/` (full plugin)
- Create: `MYTHIC/inject-shellcode-macos.py`
- Create: `TESTS/PluginTests/InjectShellcodeMacosTests.cs`

macOS-only. Mach API P/Invoke.

- [ ] **Step 1-4: Follow standard plugin creation pattern**
- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add inject-shellcode-macos plugin"
```

---

### Task 36: Phase 8 — Revive `lnk` plugin + remaining smoke tests

**Files:**
- Rename: `MYTHIC/lnk.why` -> `MYTHIC/lnk.py`
- Modify: `AGENT/lnk/lnk.cs`, `lnk.csproj`
- Create: `TESTS/PluginTests/LnkTests.cs`
- Create: smoke tests for `ds`, `smb`, `socks`, `rportfwd`, `http-server`, `cursed`, `zip-dl`, `zip-inspect`

- [ ] **Step 1: Review/update existing lnk C# code**
- [ ] **Step 2: Rename .why, update Mythic command**
- [ ] **Step 3: Write LnkTests.cs (Windows-gated)**
- [ ] **Step 4: Write smoke tests for remaining plugins**
- [ ] **Step 5: Delete `python.py.why`**

The `python.py.why` file is not being revived (IronPython dependency is too heavy). Delete it to clean up:
```bash
rm Payload_Type/athena/athena/mythic/agent_functions/python.py.why
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: revive lnk plugin, delete python.py.why, add remaining smoke tests"
```

---

## Chunk 6: Final Verification

### Task 37: Full test suite verification

- [ ] **Step 1: Run full test suite**

Run: `dotnet test Payload_Type/athena/athena/agent_code/Tests/Workflow.Tests/ -v minimal --collect:"XPlat Code Coverage"`
Expected: All tests pass

- [ ] **Step 2: Verify all Python files parse**

Run: `find Payload_Type/athena/athena/mythic/agent_functions/ -name "*.py" | xargs -I{} python -c "import ast; ast.parse(open('{}').read())"`

- [ ] **Step 3: Verify test .csproj references all new plugins**

Count project references: should include all 19 new DLLs plus existing ones.

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "chore: final verification pass for command expansion"
```

---

## Summary

| Chunk | Tasks | Phase(s) | Key Deliverables |
|-------|-------|----------|------------------|
| 1 | 1-7 | 0 | plugin_registry.py, load.py/builder.py refactor, test infra |
| 2 | 8-14 | 1 | find, file-utils, hash DLLs + 11 wrappers + file-ops tests |
| 3 | 15-20 | 2-3 | sysinfo, ping, dns, http-request DLLs + 7 wrappers + tests |
| 4 | 21-26 | 4 (+6 privesc) | credentials, proc-enum, kerberos, clipboard-monitor, ssh-recon DLLs + 19 wrappers + tests |
| 5 | 27-36 | 5-8 | wmi, event-log, bits-transfer DLLs + jxa/keychain/lnk revival + 16 wrappers + tests |
| 6 | 37 | — | Full verification |
