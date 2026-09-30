namespace Agent.Profiles.Smb
{
    internal static class SmbConnectionWait
    {
        internal static void Wait(WaitHandle connected, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(connected);
            cancellationToken.ThrowIfCancellationRequested();
            int signaled = WaitHandle.WaitAny(new[] { connected, cancellationToken.WaitHandle });
            if (signaled == 1)
                throw new OperationCanceledException(cancellationToken);
        }
    }
}
