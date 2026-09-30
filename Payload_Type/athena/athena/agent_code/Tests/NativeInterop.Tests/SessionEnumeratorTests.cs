extern alias GetSessions;

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SessionEnumerator = GetSessions::Agent.SessionEnumerator;
using INetSessionNative = GetSessions::Agent.INetSessionNative;

namespace NativeInterop.Tests;

[TestClass]
public class SessionEnumeratorTests
{
    [TestMethod]
    public void SessionEnumerationReturnsNativeRecords()
    {
        var native = new OneSessionNative();
        var enumerator = new SessionEnumerator(native);

        var sessions = enumerator.Enumerate("server");

        Assert.AreEqual(1, sessions.Count);
        Assert.AreEqual("client", sessions[0].ClientName);
        Assert.AreEqual("user", sessions[0].UserName);
        Assert.AreEqual((uint)12, sessions[0].ActiveSeconds);
        Assert.AreEqual((uint)3, sessions[0].IdleSeconds);
        Assert.AreEqual(1, native.FreeCount);
    }

    private sealed class OneSessionNative : INetSessionNative
    {
        private IntPtr client;
        private IntPtr user;
        public int FreeCount { get; private set; }

        public int Enumerate(string serverName, string? clientName, string? userName, int level,
            out IntPtr buffer, int preferredMaximumLength, ref int entriesRead,
            ref int totalEntries, ref int resumeHandle)
        {
            client = Marshal.StringToHGlobalUni("client");
            user = Marshal.StringToHGlobalUni("user");
            buffer = Marshal.AllocHGlobal(IntPtr.Size * 2 + sizeof(uint) * 2);
            Marshal.WriteIntPtr(buffer, 0, client);
            Marshal.WriteIntPtr(buffer, IntPtr.Size, user);
            Marshal.WriteInt32(buffer, IntPtr.Size * 2, 12);
            Marshal.WriteInt32(buffer, IntPtr.Size * 2 + sizeof(uint), 3);
            entriesRead = totalEntries = 1;
            return SessionEnumerator.Success;
        }

        public int Free(IntPtr buffer)
        {
            FreeCount++;
            Marshal.FreeHGlobal(buffer);
            Marshal.FreeHGlobal(client);
            Marshal.FreeHGlobal(user);
            return 0;
        }
    }
}
