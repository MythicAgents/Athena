extern alias WindowsInjection;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using IProcessHandleNative = WindowsInjection::Agent.IProcessHandleNative;
using ProcessHandleResolver = WindowsInjection::Agent.ProcessHandleResolver;

namespace NativeInterop.Tests;

[TestClass]
public class ProcessHandleResolverTests
{
    [TestMethod]
    public void ProcessHandleResolvesToItsNativeProcessId()
    {
        var native = new StubProcessHandleNative(4242);
        var resolver = new ProcessHandleResolver(native);
        var handle = new IntPtr(123);

        int processId = resolver.Resolve(handle);

        Assert.AreEqual(4242, processId);
        Assert.AreEqual(handle, native.ReceivedHandle);
    }

    private sealed class StubProcessHandleNative(int processId) : IProcessHandleNative
    {
        public IntPtr ReceivedHandle { get; private set; }
        public int GetProcessId(IntPtr processHandle)
        {
            ReceivedHandle = processHandle;
            return processId;
        }
    }
}
