using System.Runtime.InteropServices;

namespace Agent;

public interface ILinuxPtrace
{
    int Attach(long pid);
    int Wait(long pid);
    int GetRegisters(long pid, out PTrace.UserRegs registers);
    int ReadWord(long pid, long address, out ulong value);
    int WriteWord(long pid, long address, ulong value);
    int SetRegisters(long pid, PTrace.UserRegs registers);
    int Detach(long pid);
}

public sealed class LinuxPtraceNative : ILinuxPtrace
{
    public int Attach(long pid) => PTrace.PtraceAttach(pid);

    public int Wait(long pid) => PTrace.Wait(pid);

    public int GetRegisters(long pid, out PTrace.UserRegs registers) =>
        PTrace.PtraceGetRegs(pid, out registers);

    public int WriteWord(long pid, long address, ulong value) =>
        PTrace.PtracePokeText(pid, address, value);

    public int ReadWord(long pid, long address, out ulong value) =>
        PTrace.PtracePeekText(pid, address, out value);

    public int SetRegisters(long pid, PTrace.UserRegs registers) =>
        PTrace.PtraceSetRegs(pid, registers);

    public int Detach(long pid) => PTrace.PtraceDetach(pid);
}

public sealed class LinuxShellcodeInjector
{
    private const int NativeWordSize = sizeof(ulong);
    private readonly ILinuxPtrace native;

    public LinuxShellcodeInjector(ILinuxPtrace native)
    {
        this.native = native;
    }

    public string? FailedOperation { get; private set; }
    public int LastError { get; private set; }

    public bool Inject(long pid, long address, byte[] shellcode)
    {
        FailedOperation = null;
        LastError = 0;

        if (native.Attach(pid) < 0)
        {
            RecordFailure("PTRACE_ATTACH");
            return false;
        }

        bool injected;
        bool detached;

        try
        {
            injected = InjectWhileAttached(pid, address, shellcode);
        }
        finally
        {
            detached = native.Detach(pid) >= 0;
            if (!detached)
            {
                RecordFailure("PTRACE_DETACH");
            }
        }

        return injected && detached;
    }

    private bool InjectWhileAttached(long pid, long address, byte[] shellcode)
    {
        if (native.Wait(pid) < 0)
        {
            RecordFailure("waitpid");
            return false;
        }

        if (native.GetRegisters(pid, out PTrace.UserRegs registers) < 0)
        {
            RecordFailure("PTRACE_GETREGS");
            return false;
        }

        byte[] wordBytes = new byte[NativeWordSize];
        var originalWords = new List<(long Address, ulong Value)>();
        for (int offset = 0; offset < shellcode.Length; offset += NativeWordSize)
        {
            int bytesToCopy = Math.Min(NativeWordSize, shellcode.Length - offset);
            long wordAddress = address + offset;
            if (native.ReadWord(pid, wordAddress, out ulong existing) < 0)
            {
                RecordFailure("PTRACE_PEEKTEXT");
                RestoreWords(pid, originalWords);
                return false;
            }
            BitConverter.TryWriteBytes(wordBytes, existing);
            shellcode.AsSpan(offset, bytesToCopy).CopyTo(wordBytes.AsSpan());
            ulong word = BitConverter.ToUInt64(wordBytes);

            if (native.WriteWord(pid, wordAddress, word) < 0)
            {
                RecordFailure("PTRACE_POKETEXT");
                RestoreWords(pid, originalWords);
                return false;
            }
            originalWords.Add((wordAddress, existing));
        }

        registers.rip = (ulong)address;
        if (native.SetRegisters(pid, registers) < 0)
        {
            RecordFailure("PTRACE_SETREGS");
            RestoreWords(pid, originalWords);
            return false;
        }

        return true;
    }

    private void RestoreWords(long pid, List<(long Address, ulong Value)> words)
    {
        for (int index = words.Count - 1; index >= 0; index--)
            native.WriteWord(pid, words[index].Address, words[index].Value);
    }

    private void RecordFailure(string operation)
    {
        FailedOperation = operation;
        LastError = Marshal.GetLastWin32Error();
    }
}
