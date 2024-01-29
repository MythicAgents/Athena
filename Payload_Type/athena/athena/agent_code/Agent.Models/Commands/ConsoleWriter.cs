using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Models
{
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
        private readonly TextWriter originalOutput;
        public ConsoleWriter()
        {
            originalOutput = Console.Out;
            Console.SetOut(this);
            Console.SetError(this);
        }

        public override void Write(string value)
        {
            if (WriteEvent != null) WriteEvent(this, new ConsoleWriterEventArgs(value));
        }

        public override void WriteLine(string value)
        {
            if (WriteLineEvent != null) WriteLineEvent(this, new ConsoleWriterEventArgs(value));
        }

        public event EventHandler<ConsoleWriterEventArgs> WriteEvent;
        public event EventHandler<ConsoleWriterEventArgs> WriteLineEvent;
        public void Dispose()
        {
            Console.SetOut(originalOutput);
        }
    }
}
