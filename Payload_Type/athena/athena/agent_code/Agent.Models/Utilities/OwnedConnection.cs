using System.Threading;

namespace Agent.Utilities
{
    public sealed class OwnedConnection : IDisposable
    {
        private IDisposable? owner;
        private IDisposable? dependent;

        public OwnedConnection(IDisposable owner, IDisposable? dependent = null)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.dependent = dependent;
        }

        public void Dispose()
        {
            IDisposable? ownerToDispose = Interlocked.Exchange(ref owner, null);
            if (ownerToDispose is null) return;

            IDisposable? dependentToDispose = Interlocked.Exchange(ref dependent, null);
            try
            {
                dependentToDispose?.Dispose();
            }
            finally
            {
                ownerToDispose.Dispose();
            }
        }
    }
}
