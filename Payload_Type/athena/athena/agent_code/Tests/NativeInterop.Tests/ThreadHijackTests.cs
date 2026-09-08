extern alias WindowsInjection;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Native = WindowsInjection::Agent.IThreadHijackNative;
using Subject = WindowsInjection::Agent.ThreadHijack;

namespace NativeInterop.Tests;

[TestClass]
public sealed class ThreadHijackTests
{
    [TestMethod]
    public void ThreadHijackWritesPayloadRedirectsExecutionAndResumes()
    {
        var native = new FakeNative();
        var subject = new Subject(native);
        byte[] payload = { 1, 2, 3, 4 };

        bool injected = subject.InjectThread(7, 42, payload);

        Assert.IsTrue(injected);
        Assert.AreEqual((uint)(payload.Length + 12), native.WriteSize);
        CollectionAssert.AreEqual(payload, native.WrittenBytes!.Take(payload.Length).ToArray());
        Assert.AreEqual((ulong)0x5000, native.RedirectedRip);
        Assert.AreEqual(1, native.ResumeCalls);
        CollectionAssert.AreEquivalent(new[] { new IntPtr(11), new IntPtr(22) }, native.Closed);
    }

    private sealed class FakeNative : Native
    {
        public uint WriteSize { get; private set; }
        public byte[]? WrittenBytes { get; private set; }
        public ulong RedirectedRip { get; private set; }
        public int ResumeCalls { get; private set; }
        public List<IntPtr> Closed { get; } = new();
        public IntPtr OpenThread(Subject.ThreadAccess access, bool inherit, uint id) => new(11);
        public uint SuspendThread(IntPtr thread) => 0;
        public bool GetThreadContext(IntPtr thread, ref Subject.CONTEXT64 context) { context.Rip = 0x1234; return true; }
        public IntPtr OpenProcess(int access, bool inherit, int id) => new(22);
        public IntPtr VirtualAllocEx(IntPtr process, IntPtr address, uint size, uint allocation, uint protection) => new(0x5000);
        public bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] bytes, uint size, out UIntPtr written)
        {
            WriteSize = size;
            WrittenBytes = bytes;
            written = new UIntPtr(size);
            return true;
        }
        public bool SetThreadContext(IntPtr thread, ref Subject.CONTEXT64 context) { RedirectedRip = context.Rip; return true; }
        public int ResumeThread(IntPtr thread) { ResumeCalls++; return 1; }
        public bool VirtualFreeEx(IntPtr process, IntPtr address, uint size, uint freeType) => true;
        public bool CloseHandle(IntPtr handle) { Closed.Add(handle); return true; }
    }
}
