using System.Collections.Concurrent;
using Agent.Utilities;

namespace Agent
{
    internal sealed class SshSessionLease<TStream> : IDisposable where TStream : class
    {
        private Action? release;
        internal TStream Stream { get; }

        internal SshSessionLease(TStream stream, Action release)
        {
            Stream = stream;
            this.release = release;
        }

        public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
    }

    internal static class SshSessionAdmission
    {
        internal static bool TryCreate<TStream>(SshSessionRegistry<TStream> registry, string taskId,
            IDisposable owner, Func<TStream> createStream, Action<TStream> configureStream, out Exception? error)
            where TStream : class, IDisposable
        {
            IDisposable ownership = owner;
            try
            {
                TStream stream = createStream();
                ownership = new OwnedConnection(owner, stream);
                configureStream(stream);
                error = null;
                return registry.TryAdmit(taskId, stream, ownership);
            }
            catch (Exception exception)
            {
                try { ownership.Dispose(); } catch { }
                error = exception;
                return false;
            }
        }
    }

    internal sealed class SshSessionRegistry<TStream> where TStream : class
    {
        private readonly ConcurrentDictionary<string, Session> sessions = new();
        internal int Count => sessions.Count;

        internal bool TryAdmit(string taskId, TStream stream, IDisposable ownership)
        {
            if (sessions.TryAdd(taskId, new Session(stream, ownership))) return true;
            DisposeIgnoringErrors(ownership);
            return false;
        }

        internal bool TryGet(string taskId, out TStream stream)
        {
            if (sessions.TryGetValue(taskId, out Session? session))
            {
                stream = session.Stream;
                return true;
            }
            stream = null!;
            return false;
        }

        internal bool TryAcquire(string taskId, out SshSessionLease<TStream> lease)
        {
            if (sessions.TryGetValue(taskId, out Session? session)) return session.TryAcquire(out lease);
            lease = null!;
            return false;
        }

        internal void Retire(string taskId)
        {
            if (sessions.TryRemove(taskId, out Session? session)) session.Retire();
        }

        private static void DisposeIgnoringErrors(IDisposable resource)
        {
            try { resource.Dispose(); } catch { }
        }

        private sealed class Session
        {
            private readonly object gate = new();
            private int leases;
            private bool retired;
            internal TStream Stream { get; }
            internal IDisposable Ownership { get; }

            internal Session(TStream stream, IDisposable ownership)
            {
                Stream = stream;
                Ownership = ownership;
            }

            internal bool TryAcquire(out SshSessionLease<TStream> lease)
            {
                lock (gate)
                {
                    if (retired) { lease = null!; return false; }
                    leases++;
                    lease = new SshSessionLease<TStream>(Stream, Release);
                    return true;
                }
            }

            internal void Retire()
            {
                bool dispose;
                lock (gate)
                {
                    retired = true;
                    dispose = leases == 0;
                }
                if (dispose) DisposeIgnoringErrors(Ownership);
            }

            private void Release()
            {
                bool dispose;
                lock (gate)
                {
                    leases--;
                    dispose = retired && leases == 0;
                }
                if (dispose) DisposeIgnoringErrors(Ownership);
            }
        }
    }
}
