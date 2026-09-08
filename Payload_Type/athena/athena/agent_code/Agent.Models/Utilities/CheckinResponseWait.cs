namespace Agent.Utilities
{
    public static class CheckinResponseWait
    {
        public static Task<bool> WaitAsync(
            ManualResetEventSlim signal,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(signal);
            if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            return Task.Run(() => signal.Wait(timeout, cancellationToken), CancellationToken.None);
        }
    }
}
