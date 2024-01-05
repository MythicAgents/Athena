using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Agent.Interfaces;
using Agent.Utilities;

public class ConsoleWriterEventArgs : EventArgs
{
    public string Value { get; private set; }
    public ConsoleWriterEventArgs(string value)
    {
        Value = value;
    }
}

public class ConsoleWriter : TextWriter, IDisposable
{
    public override Encoding Encoding { get { return Encoding.UTF8; } }
    private readonly StringWriter stringWriter;
    private readonly TextWriter originalOutput;
    public ConsoleWriter()
    {
        stringWriter = new StringWriter();
        originalOutput = Console.Out;
        Console.SetOut(stringWriter);
    }

    public override void Write(string value)
    {
        if (WriteEvent != null) WriteEvent(this, new ConsoleWriterEventArgs(value));
        base.Write(value);
    }

    public override void WriteLine(string value)
    {
        if (WriteLineEvent != null) WriteLineEvent(this, new ConsoleWriterEventArgs(value));
        base.WriteLine(value);
    }

    public event EventHandler<ConsoleWriterEventArgs> WriteEvent;
    public event EventHandler<ConsoleWriterEventArgs> WriteLineEvent;
    public void Dispose()
    {
        Console.SetOut(originalOutput);
        stringWriter.Dispose();
    }
}

public class ConsoleApplicationExecutor
{
    private AssemblyLoadContext alc = new AssemblyLoadContext(Misc.RandomString(10));
    private readonly byte[] asmBytes;
    private readonly string[] args;
    private readonly string task_id;
    private readonly IMessageManager messageManager;
    private bool running = false;
    public ConsoleApplicationExecutor(byte[] asmBytes, string[] args, string task_id, IMessageManager messageManager)
    {
        this.messageManager = messageManager;
        this.asmBytes = asmBytes;
        this.args = args;
        this.task_id = task_id;
    }
    public ConsoleApplicationExecutor()
    {

    }
    public void Execute()
    {
        using (var redirector = new ConsoleWriter())
        {
            redirector.WriteEvent += consoleWriter_WriteEvent;
            redirector.WriteLineEvent += consoleWriter_WriteLineEvent;
            running = true;
            // Load the assembly
            Assembly assembly = alc.LoadFromStream(new MemoryStream(this.asmBytes));

            // Find the entry point (Main method)
            MethodInfo entryPoint = assembly.EntryPoint;

            // Create an instance of the class containing the Main method
            object instance = assembly.CreateInstance(entryPoint.DeclaringType.FullName);

            // Invoke the Main method
            entryPoint.Invoke(instance, new object[] { this.args });
            running = false;
        }
    }

    private void consoleWriter_WriteLineEvent(object sender, ConsoleWriterEventArgs e)
    {
        messageManager.WriteLine(e.Value, this.task_id, false);
    }

    private void consoleWriter_WriteEvent(object sender, ConsoleWriterEventArgs e)
    {
        messageManager.Write(e.Value, this.task_id, false);
    }

    public bool IsRunning()
    {
        return running;
    }
}