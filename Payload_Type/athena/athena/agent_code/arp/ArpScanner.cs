using System.Collections.Concurrent;
using System.Net;
using System.Runtime.InteropServices;

namespace arp;

public interface IArpResolver
{
    string Resolve(string address);
}

public sealed class ArpScanner
{
    private readonly IArpResolver resolver;
    private readonly SemaphoreSlim resolverSlots;

    public ArpScanner(IArpResolver resolver, int maximumConcurrency = 16)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumConcurrency, 1);
        this.resolver = resolver;
        resolverSlots = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
    }

    public async Task<IReadOnlyList<string>> ScanAsync(
        IEnumerable<string> addresses,
        TimeSpan deadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(deadline, TimeSpan.Zero);
        var results = new ConcurrentQueue<string>();
        using var deadlineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCancellation.CancelAfter(deadline);

        try
        {
            Task[] scans = addresses.Select(address => ResolveAsync(address, results, deadlineCancellation.Token)).ToArray();
            await Task.WhenAll(scans).WaitAsync(deadlineCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (deadlineCancellation.IsCancellationRequested)
        {
        }

        return results.ToArray();
    }

    private async Task ResolveAsync(string address, ConcurrentQueue<string> results, CancellationToken token)
    {
        await resolverSlots.WaitAsync(token).ConfigureAwait(false);
        try
        {
            string result = await Task.Run(() => resolver.Resolve(address), CancellationToken.None).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result) && !token.IsCancellationRequested) results.Enqueue(result);
        }
        finally { resolverSlots.Release(); }
    }
}

public sealed class NativeArpResolver : IArpResolver
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(int destinationIp, int sourceIp, byte[] macAddress, ref uint physicalAddressLength);

    public string Resolve(string address)
    {
        try
        {
            var ipAddress = IPAddress.Parse(address);
            var macAddress = new byte[6];
            uint macAddressLength = (uint)macAddress.Length;
            SendARP(
                BitConverter.ToInt32(ipAddress.GetAddressBytes(), 0),
                0,
                macAddress,
                ref macAddressLength);
            var mac = BitConverter.ToString(macAddress).ToUpperInvariant();
            return mac == "00-00-00-00-00-00" ? string.Empty : $"{address} - {mac} - Alive";
        }
        catch
        {
            return $"{address} - Invalid";
        }
    }
}
