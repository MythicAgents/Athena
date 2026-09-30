using System.Runtime.InteropServices;

namespace Agent;

public interface INetSessionNative
{
    int Enumerate(
        string serverName,
        string? clientName,
        string? userName,
        int level,
        out IntPtr buffer,
        int preferredMaximumLength,
        ref int entriesRead,
        ref int totalEntries,
        ref int resumeHandle);

    int Free(IntPtr buffer);
}

public sealed record SessionRecord(
    string ClientName,
    string UserName,
    uint ActiveSeconds,
    uint IdleSeconds);

public sealed class SessionEnumerator
{
    public const int Success = 0;
    public const int MoreData = 234;

    private readonly INetSessionNative native;

    public SessionEnumerator(INetSessionNative native)
    {
        this.native = native;
    }

    public IReadOnlyList<SessionRecord> Enumerate(string serverName)
    {
        var sessions = new List<SessionRecord>();
        int resumeHandle = 0;
        int result;

        do
        {
            IntPtr buffer = IntPtr.Zero;
            int entriesRead = 0;
            int totalEntries = 0;

            try
            {
                result = native.Enumerate(
                    serverName,
                    null,
                    null,
                    10,
                    out buffer,
                    -1,
                    ref entriesRead,
                    ref totalEntries,
                    ref resumeHandle);

                if (result == Success || result == MoreData)
                {
                    ReadEntries(buffer, entriesRead, sessions);
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    native.Free(buffer);
                }
            }
        }
        while (result == MoreData);

        return sessions;
    }

    private static void ReadEntries(
        IntPtr buffer,
        int entriesRead,
        ICollection<SessionRecord> sessions)
    {
        int entrySize = Marshal.SizeOf<SessionInfo10>();

        for (int index = 0; index < entriesRead; index++)
        {
            IntPtr entryAddress = IntPtr.Add(buffer, index * entrySize);
            SessionInfo10 entry = Marshal.PtrToStructure<SessionInfo10>(entryAddress);
            sessions.Add(new SessionRecord(
                entry.ClientName ?? string.Empty,
                entry.UserName ?? string.Empty,
                entry.ActiveSeconds,
                entry.IdleSeconds));
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SessionInfo10
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ClientName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? UserName;

        public uint ActiveSeconds;
        public uint IdleSeconds;
    }
}

public sealed class NetSessionNative : INetSessionNative
{
    public int Enumerate(
        string serverName,
        string? clientName,
        string? userName,
        int level,
        out IntPtr buffer,
        int preferredMaximumLength,
        ref int entriesRead,
        ref int totalEntries,
        ref int resumeHandle)
    {
        return NetSessionEnum(
            serverName,
            clientName,
            userName,
            level,
            out buffer,
            preferredMaximumLength,
            ref entriesRead,
            ref totalEntries,
            ref resumeHandle);
    }

    public int Free(IntPtr buffer) => NetApiBufferFree(buffer);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int NetSessionEnum(
        string serverName,
        string? clientName,
        string? userName,
        int level,
        out IntPtr buffer,
        int preferredMaximumLength,
        ref int entriesRead,
        ref int totalEntries,
        ref int resumeHandle);

    [DllImport("netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);
}
