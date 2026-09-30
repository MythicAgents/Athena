namespace Agent;

public interface IProcessHandleNative
{
    int GetProcessId(IntPtr processHandle);
}

public sealed class ProcessHandleNative : IProcessHandleNative
{
    public int GetProcessId(IntPtr processHandle) => Native.GetProcessId(processHandle);
}

public sealed class ProcessHandleResolver
{
    private readonly IProcessHandleNative native;

    public ProcessHandleResolver(IProcessHandleNative native)
    {
        this.native = native;
    }

    public int Resolve(IntPtr processHandle)
    {
        if (processHandle == IntPtr.Zero)
        {
            throw new ArgumentException("A valid process handle is required.", nameof(processHandle));
        }

        int processId = native.GetProcessId(processHandle);
        if (processId <= 0)
        {
            throw new InvalidOperationException("GetProcessId failed for the spawned process handle.");
        }

        return processId;
    }
}
