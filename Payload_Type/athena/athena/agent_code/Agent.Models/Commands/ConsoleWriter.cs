using System.Text;

namespace Agent.Models
{
    public class ConsoleWriterEventArgs : EventArgs
    {
        public string Value { get; }

        public ConsoleWriterEventArgs(string value)
        {
            Value = value;
        }
    }

    public sealed class ConsoleWriter : TextWriter
    {
        private static readonly SemaphoreSlim RedirectLock = new(1, 1);
        private readonly TextWriter originalOutput;
        private readonly TextWriter originalError;
        private bool disposed;
        private bool ownsRedirectLock;

        public override Encoding Encoding => Encoding.UTF8;

        public ConsoleWriter()
        {
            RedirectLock.Wait();
            ownsRedirectLock = true;
            try
            {
                originalOutput = Console.Out;
                originalError = Console.Error;
                Console.SetOut(this);
                Console.SetError(this);
            }
            catch
            {
                ownsRedirectLock = false;
                RedirectLock.Release();
                throw;
            }
        }

        public override void Write(string? value)
        {
            if (value is not null)
                WriteEvent?.Invoke(this, new ConsoleWriterEventArgs(value));
        }

        public override void WriteLine(string? value)
        {
            if (value is not null)
                WriteLineEvent?.Invoke(this, new ConsoleWriterEventArgs(value));
        }

        public event EventHandler<ConsoleWriterEventArgs>? WriteEvent;
        public event EventHandler<ConsoleWriterEventArgs>? WriteLineEvent;

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                Console.SetOut(originalOutput);
                Console.SetError(originalError);
                disposed = true;
                if (ownsRedirectLock)
                {
                    ownsRedirectLock = false;
                    RedirectLock.Release();
                }
            }
            base.Dispose(disposing);
        }
    }
}
