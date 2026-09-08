using Agent.Utilities;
using System.Collections.Concurrent;

namespace sftp
{
    internal sealed class ResourceLease<T> : IDisposable where T : class
    {
        private Action? release;
        internal T Value { get; }

        internal ResourceLease(T value, Action release)
        {
            Value = value;
            this.release = release;
        }

        public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
    }

    internal sealed class SftpTaskResourceRegistry<TSession, TTransfer>
        where TSession : class
        where TTransfer : class
    {
        private readonly ConcurrentDictionary<string, Entry> entries = new();

        internal int Count => entries.Count;
        internal int TransferCount => entries.Values.Count(entry => entry.HasTransfer);

        internal bool TryAddTask(string taskId, TSession session, IDisposable ownership)
        {
            var entry = new Entry(session, ownership);
            if (entries.TryAdd(taskId, entry)) return true;
            DisposeIgnoringErrors(ownership);
            return false;
        }

        internal bool TryGetTask(string taskId, out TSession session)
        {
            if (entries.TryGetValue(taskId, out Entry? entry)) return entry.TryGetSession(out session);
            session = null!;
            return false;
        }

        internal bool TryAcquireTask(string taskId, out ResourceLease<TSession> lease)
        {
            if (entries.TryGetValue(taskId, out Entry? entry)) return entry.TryAcquireSession(out lease);
            lease = null!;
            return false;
        }

        internal bool TryAddTransfer(string taskId, TTransfer transfer, IDisposable ownership)
        {
            if (entries.TryGetValue(taskId, out Entry? entry) && entry.TryAddTransfer(transfer, ownership)) return true;
            DisposeIgnoringErrors(ownership);
            return false;
        }

        internal bool TryGetTransfer(string taskId, out TTransfer transfer)
        {
            if (entries.TryGetValue(taskId, out Entry? entry)) return entry.TryGetTransfer(out transfer);
            transfer = null!;
            return false;
        }

        internal bool TryAcquireTransfer(string taskId, out ResourceLease<TTransfer> lease)
        {
            if (entries.TryGetValue(taskId, out Entry? entry)) return entry.TryAcquireTransfer(out lease);
            lease = null!;
            return false;
        }

        internal void RetireTransfer(string taskId)
        {
            if (entries.TryGetValue(taskId, out Entry? entry)) entry.RetireTransfer();
        }

        internal void RetireTask(string taskId)
        {
            if (entries.TryRemove(taskId, out Entry? entry)) entry.Retire();
        }

        private static void DisposeIgnoringErrors(IDisposable? resource)
        {
            try { resource?.Dispose(); }
            catch { }
        }

        private sealed class Entry
        {
            private readonly object gate = new();
            private readonly SemaphoreSlim transferUse = new(1, 1);
            private readonly TSession session;
            private IDisposable? sessionOwnership;
            private TTransfer? transfer;
            private IDisposable? transferOwnership;
            private IDisposable? retiredSessionOwnership;
            private readonly List<IDisposable> retiredTransferOwnerships = new();
            private int sessionLeases;
            private int transferLeases;
            private bool retired;

            internal Entry(TSession session, IDisposable sessionOwnership)
            {
                this.session = session;
                this.sessionOwnership = sessionOwnership;
            }

            internal bool HasTransfer { get { lock (gate) return !retired && transfer is not null; } }

            internal bool TryGetSession(out TSession value)
            {
                lock (gate)
                {
                    value = retired ? null! : session;
                    return value is not null;
                }
            }

            internal bool TryAcquireSession(out ResourceLease<TSession> lease)
            {
                lock (gate)
                {
                    if (retired) { lease = null!; return false; }
                    sessionLeases++;
                    lease = new ResourceLease<TSession>(session, ReleaseSession);
                    return true;
                }
            }

            internal bool TryAddTransfer(TTransfer value, IDisposable ownership)
            {
                lock (gate)
                {
                    if (retired || transfer is not null) return false;
                    transfer = value;
                    transferOwnership = ownership;
                    return true;
                }
            }

            internal bool TryGetTransfer(out TTransfer value)
            {
                lock (gate)
                {
                    if (retired || transfer is null) { value = null!; return false; }
                    value = transfer;
                    return true;
                }
            }

            internal bool TryAcquireTransfer(out ResourceLease<TTransfer> lease)
            {
                transferUse.Wait();
                lock (gate)
                {
                    if (retired || transfer is null)
                    {
                        transferUse.Release();
                        lease = null!;
                        return false;
                    }
                    transferLeases++;
                    lease = new ResourceLease<TTransfer>(transfer, ReleaseTransfer);
                    return true;
                }
            }

            internal void RetireTransfer()
            {
                IDisposable? ownership;
                lock (gate)
                {
                    transfer = null;
                    ownership = transferOwnership;
                    transferOwnership = null;
                    if (transferLeases != 0)
                    {
                        if (ownership is not null) retiredTransferOwnerships.Add(ownership);
                        ownership = null;
                    }
                }
                DisposeIgnoringErrors(ownership);
            }

            internal void Retire()
            {
                IDisposable? transferToDispose;
                IDisposable? sessionToDispose;
                lock (gate)
                {
                    retired = true;
                    transfer = null;
                    transferToDispose = transferOwnership;
                    transferOwnership = null;
                    sessionToDispose = sessionOwnership;
                    sessionOwnership = null;
                    if (transferLeases != 0)
                    {
                        if (transferToDispose is not null) retiredTransferOwnerships.Add(transferToDispose);
                        transferToDispose = null;
                    }
                    if (sessionLeases != 0)
                    {
                        retiredSessionOwnership = sessionToDispose;
                        sessionToDispose = null;
                    }
                }
                DisposeIgnoringErrors(transferToDispose);
                DisposeIgnoringErrors(sessionToDispose);
            }

            private void ReleaseSession()
            {
                IDisposable? ownership = null;
                lock (gate)
                {
                    sessionLeases--;
                    if (sessionLeases == 0)
                    {
                        ownership = retiredSessionOwnership;
                        retiredSessionOwnership = null;
                    }
                }
                DisposeIgnoringErrors(ownership);
            }

            private void ReleaseTransfer()
            {
                List<IDisposable>? ownerships = null;
                lock (gate)
                {
                    transferLeases--;
                    if (transferLeases == 0 && retiredTransferOwnerships.Count != 0)
                    {
                        ownerships = new List<IDisposable>(retiredTransferOwnerships);
                        retiredTransferOwnerships.Clear();
                    }
                }
                transferUse.Release();
                if (ownerships is not null)
                {
                    foreach (IDisposable ownership in ownerships) DisposeIgnoringErrors(ownership);
                }
            }
        }
    }

    internal static class SftpSessionAdmission
    {
        internal static bool TryAdmit<TSession, TTransfer>(
            SftpTaskResourceRegistry<TSession, TTransfer> registry,
            string taskId,
            TSession session,
            IDisposable ownership,
            Action<Action> registerCancellation,
            Action cancellationCallback,
            out Exception? error)
            where TSession : class
            where TTransfer : class
        {
            error = null;
            if (!registry.TryAddTask(taskId, session, ownership)) return false;

            int cancellationObserved = 0;
            void OnCancellation()
            {
                Interlocked.Exchange(ref cancellationObserved, 1);
                cancellationCallback();
            }

            try
            {
                registerCancellation(OnCancellation);
                if (Volatile.Read(ref cancellationObserved) != 0)
                {
                    registry.RetireTask(taskId);
                    return false;
                }

                if (!registry.TryAcquireTask(taskId, out ResourceLease<TSession> lease)) return false;
                using (lease)
                {
                    return ReferenceEquals(lease.Value, session)
                        && Volatile.Read(ref cancellationObserved) == 0;
                }
            }
            catch (Exception exception)
            {
                registry.RetireTask(taskId);
                error = exception;
                return false;
            }
        }
    }

    internal static class SftpInteractiveCommand
    {
        internal static string? GetVerb(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            string[] parts = Misc.SplitCommandLine(command);
            return parts.Length == 0 ? null : parts[0].ToLowerInvariant();
        }
    }

    internal static class SftpUploadProtocol
    {
        internal const int MaxTotalChunks = 1_000_000;

        internal static int MaximumEncodedLength(int maximumDecodedLength)
        {
            if (maximumDecodedLength < 0) throw new ArgumentOutOfRangeException(nameof(maximumDecodedLength));
            long length = 4L * ((maximumDecodedLength + 2L) / 3L);
            if (length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maximumDecodedLength));
            return (int)length;
        }

        internal static byte[] DecodeChunk(string encodedData, int chunkNumber, int totalChunks,
            int expectedChunkNumber, int expectedTotalChunks, int maximumDecodedLength)
        {
            if (maximumDecodedLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDecodedLength));
            if (totalChunks <= 0 || totalChunks > MaxTotalChunks) throw new ArgumentException("Invalid total chunk count.", nameof(totalChunks));
            if (chunkNumber != expectedChunkNumber || chunkNumber <= 0 || chunkNumber > totalChunks) throw new ArgumentException("Unexpected upload chunk number.", nameof(chunkNumber));
            if (expectedTotalChunks != 0 && totalChunks != expectedTotalChunks) throw new ArgumentException("Upload total chunk count changed.", nameof(totalChunks));
            if (string.IsNullOrEmpty(encodedData)) throw new ArgumentException("No chunk data received from server.", nameof(encodedData));
            if (encodedData.Length > MaximumEncodedLength(maximumDecodedLength)) throw new ArgumentException("Encoded upload chunk exceeds the configured size limit.", nameof(encodedData));

            byte[] decoded;
            try { decoded = Convert.FromBase64String(encodedData); }
            catch (FormatException exception) { throw new ArgumentException("Upload chunk is not valid base64.", nameof(encodedData), exception); }
            if (decoded.Length > maximumDecodedLength) throw new ArgumentException("Decoded upload chunk exceeds the configured size limit.", nameof(encodedData));
            return decoded;
        }
    }
}
