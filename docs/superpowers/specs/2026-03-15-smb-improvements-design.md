# SMB Profile & SmbLink Improvements — Design Spec

**Date:** 2026-03-15
**Goal:** Fix all critical bugs, eliminate code duplication, and simplify the messaging protocol across SmbProfile, SmbLink, and the smb Plugin.
**Tech Stack:** C# / .NET, H.Pipes, MSTest

---

## Problem Statement

The SMB channel implementation has 6 critical bugs, 6 medium issues, and several pieces of dead code. The most impactful problems:

1. **Unlink is completely broken** — links are stored by random GUID but looked up by `task.id`, so unlink never finds the target. The indexer also throws `KeyNotFoundException` with no guard.
2. **ListConnections crashes at runtime** — attempts to serialize `SmbLink` objects containing non-serializable pipe handles and wait handles.
3. **Protocol ambiguity** — a single `AutoResetEvent` is shared between connection handshake and chunk acknowledgments, causing race conditions where a late handshake ack is mistaken for a chunk ack.
4. **Disconnect race** — `OnMessageReceive` takes a lock in two separate blocks with an unsynchronized gap between them, allowing `OnClientDisconnect` to clear state mid-processing.
5. **CTS lifecycle bug** — `StartBeacon()` disposes the original `CancellationTokenSource` and creates a new one, but the server pipe was started with the original token and won't respond to the new one.
6. **Duplicated chunk reassembly** — near-identical logic in both `SmbProfile` and `SmbLink`.

---

## Simplified Message Protocol

The current protocol sends per-chunk acks using a `Success` message type that is also reused for connection handshake. This is the root cause of the `messageSuccess` collision bug.

Named pipes are a reliable, ordered transport. Per-chunk acks add complexity without benefit.

### New message types

| Message Type | Constant | Direction | Purpose |
|---|---|---|---|
| `connection_ready` | `SmbMessageType.ConnectionReady` | Server to Client | Sent once when client connects. Carries `agent_guid` |
| `chunked_message` | `SmbMessageType.Chunked` | Both | Data chunk (unchanged) |
| `message_complete` | `SmbMessageType.MessageComplete` | Receiver to Sender | Sent once after final chunk received and full message assembled |
| `error` | `SmbMessageType.Error` | Either | Sent on failure (out-of-order chunk, assembly error) |

### Protocol flow

**Connection:**
1. Client connects to server pipe
2. Server sends `connection_ready` with its `agent_guid`
3. Client learns `linked_agent_id` from this message

**Sending data (either direction):**
1. Sender splits encrypted payload into chunks
2. Sender sends all chunks sequentially (no waiting between chunks)
3. Receiver reassembles using `ChunkedMessageAssembler`
4. Receiver sends one `message_complete` after full message assembled
5. On out-of-order chunk, receiver sends `error` and discards the message

---

## ChunkedMessageAssembler

Shared class that replaces the duplicated `partialMessages` + `expectedSequence` + lock logic in both SmbProfile and SmbLink.

**Location:** `Workflow.Models/Comms/SMB/ChunkedMessageAssembler.cs`

### Interface

```csharp
public class ChunkedMessageAssembler
{
    public enum Result { Accepted, Complete, OutOfOrder }

    public Result AddChunk(
        string guid, int sequence, string content,
        bool isFinal, out string? fullMessage);

    public void Clear();
}
```

### Behavior

- Tracks partial `StringBuilder` and expected sequence number per message GUID
- Single internal lock covers the entire add-through-complete path (fixes the disconnect race)
- On out-of-order: discards the entire message for that GUID, returns `OutOfOrder`
- On complete: removes tracking state, returns assembled string via `out` parameter
- `Clear()` wipes all partial state (called on disconnect)

---

## SmbProfile Changes

### Fields removed

| Field | Reason |
|---|---|
| `partialMessages`, `expectedSequence`, `disconnectLock` | Replaced by `ChunkedMessageAssembler` |
| `volatile bool connected` | Redundant with `clientConnected.IsSet` |
| `ManualResetEventSlim checkinAvailable` | Replaced by `TaskCompletionSource<CheckinResponse>` |
| `CheckinResponse cir` | Folded into the TCS |
| `SendUpdate()` method | Identical to `SendSuccess()` |

### Fields added

| Field | Purpose |
|---|---|
| `ChunkedMessageAssembler assembler` | Shared chunk reassembly |
| `TaskCompletionSource<CheckinResponse> checkinTcs` | One-shot signal for checkin response |

### Method changes

| Method | Change |
|---|---|
| `Send()` | Return type `Task` instead of `Task<string>` (return value was always `String.Empty`) |
| `Checkin()` | Await `checkinTcs.Task.WaitAsync(cts.Token)` instead of `ManualResetEventSlim.Wait` |
| `OnClientConnection()` | Send `connection_ready` instead of `Success` |
| `OnClientDisconnect()` | Call `assembler.Clear()`, return `Task.CompletedTask` |
| `OnMessageReceive()` | Ignore `connection_ready` and `message_complete`. For `chunked_message`: use `assembler.AddChunk()`. On `Complete` call `OnMessageReceiveComplete()` then send `message_complete`. On `OutOfOrder` send `error` |
| `OnMessageReceiveComplete()` | Use `checkinTcs.TrySetResult()` instead of setting field + signaling event |
| `StartBeacon()` | Don't recreate CTS — use the existing one from the constructor |
| `DisposeAsync()` | Remove `checkinAvailable.Dispose()` |

---

## SmbLink Changes

### Fields removed

| Field | Reason |
|---|---|
| `partialMessages`, `expectedSequence` | Replaced by `ChunkedMessageAssembler` |
| `AutoResetEvent messageSuccess` | Split into purpose-specific signals |

### Fields added

| Field | Purpose |
|---|---|
| `ChunkedMessageAssembler assembler` | Shared chunk reassembly |
| `TaskCompletionSource<string> connectionTcs` | One-shot signal for connection handshake, resolves with `agent_guid` |
| `ManualResetEventSlim messageAck` | Signaled when `message_complete` arrives |

### Method changes

| Method | Change |
|---|---|
| `Link()` | Await `connectionTcs.Task` with timeout. Extract `linked_agent_id` from result |
| `OnMessageReceive()` | Route by message type: `connection_ready` sets `connectionTcs`. `message_complete` sets `messageAck`. `chunked_message` uses `assembler.AddChunk()`, on `Complete` adds delegate message. `error` logs |
| `ForwardDelegateMessage()` | Send all chunks in tight loop (no per-chunk waiting). After last chunk, wait once on `messageAck` for `message_complete` |
| `Unlink()` | Also call `assembler.Clear()` |
| `IDisposable` to `IAsyncDisposable` | Dispose `messageAck` and `clientPipe` properly |

---

## smb Plugin (smb.cs) Bug Fixes

### 1. Unlink key mismatch (CRITICAL)

Store links by `linked_agent_id` instead of random GUID. After `link.Link()` succeeds, call `forwarders.TryAdd(link.linked_agent_id, link)`.

### 2. UnlinkForwarder rewrite (CRITICAL)

Replace with `UnlinkAgent(SmbLinkArgs args, string task_id)`. Search forwarders by `linked_agent_id`. Use `FirstOrDefault` + null check instead of direct indexer.

### 3. Successful unlink status (MEDIUM)

Remove `status = "error"` from the success path. Let it default to success.

### 4. ListConnections serialization crash (CRITICAL)

Create a DTO with `linked_agent_id`, `connected`, `pipename`, `hostname`. Project forwarders into it before serializing.

### 5. Silent no-op on unknown action (MEDIUM)

Add `default` case returning error with `"Unknown action: {args.action}"`.

### 6. ForwardDelegate double iteration (LOW)

Replace `.Any()` + `.Where().First()` with single `.FirstOrDefault()` + null check.

### 7. Redundant re-access after TryAdd (LOW)

Use local `link` variable directly instead of `forwarders[linkId]`.

---

## Cleanup

| Item | Action |
|---|---|
| `MessageReceivedArgs.cs` | Delete — completely unused |
| `SmbMessageType` constants | Replace `Success`/`Chunked` with `ConnectionReady`/`Chunked`/`MessageComplete`/`Error` |
| `EncryptedExchangeCheck` | Leave as-is — unused across all profiles, not SMB-specific |

---

## File Change Summary

| File | Action |
|---|---|
| `Workflow.Models/Comms/SMB/SmbMessage.cs` | Modify — update SmbMessageType constants |
| `Workflow.Models/Comms/SMB/ChunkedMessageAssembler.cs` | Create |
| `Workflow.Channels.Smb/SmbProfile.cs` | Modify |
| `Workflow.Channels.Smb/MessageReceivedArgs.cs` | Delete |
| `smb/SmbLink.cs` | Modify |
| `smb/smb.cs` | Modify |
| `Tests/Workflow.Tests/PluginTests/SmbPluginTests.cs` | Modify — add tests |

---

## Testing Strategy

- Unit tests for smb plugin: invalid params, unlink missing key, unlink success status, list connections serialization, unknown action error
- ChunkedMessageAssembler: in-order assembly, out-of-order rejection, multi-message interleaving, Clear() during assembly
- Integration tests for protocol changes require real pipes (existing test infrastructure if available)
