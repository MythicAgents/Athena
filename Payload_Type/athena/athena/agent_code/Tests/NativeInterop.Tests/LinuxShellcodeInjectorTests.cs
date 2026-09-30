extern alias LinuxInjection;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ILinuxPtrace = LinuxInjection::Agent.ILinuxPtrace;
using LinuxShellcodeInjector = LinuxInjection::Agent.LinuxShellcodeInjector;
using UserRegs = LinuxInjection::Agent.PTrace.UserRegs;

namespace NativeInterop.Tests;

[TestClass]
public class LinuxShellcodeInjectorTests
{
    [TestMethod]
    public void Inject_WritesTheOperatorProvidedShellcode()
    {
        var native = new RecordingPtrace();
        var subject = new LinuxShellcodeInjector(native);
        byte[] shellcode = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        bool injected = subject.Inject(42, 0x1000, shellcode);

        Assert.IsTrue(injected);
        CollectionAssert.AreEqual(
            new ulong[] { 0x0807060504030201, 0x8877665544330A09 },
            native.WrittenWords);
        Assert.AreEqual(2, native.ReadWordCount);
    }

    private sealed class RecordingPtrace : ILinuxPtrace
    {
        public string? OperationToFail { get; init; }
        public int? FailWriteNumber { get; init; }
        public int DetachCount { get; private set; }
        public int ReadWordCount { get; private set; }
        public List<ulong> WrittenWords { get; } = new();
        public List<long> WriteAddresses { get; } = new();

        public int Attach(long pid) => ResultFor("PTRACE_ATTACH");
        public int Wait(long pid) => ResultFor("waitpid");

        public int GetRegisters(long pid, out UserRegs registers)
        {
            registers = default;
            return ResultFor("PTRACE_GETREGS");
        }

        public int WriteWord(long pid, long address, ulong value)
        {
            WrittenWords.Add(value);
            WriteAddresses.Add(address);
            if (FailWriteNumber == WrittenWords.Count) return -1;
            return ResultFor("PTRACE_POKETEXT");
        }

        public int ReadWord(long pid, long address, out ulong value)
        {
            ReadWordCount++;
            value = 0x8877665544332211;
            return ResultFor("PTRACE_PEEKTEXT");
        }

        public int SetRegisters(long pid, UserRegs registers) => ResultFor("PTRACE_SETREGS");

        public int Detach(long pid)
        {
            DetachCount++;
            return ResultFor("PTRACE_DETACH");
        }

        private int ResultFor(string operation) => OperationToFail == operation ? -1 : 0;
    }
}
