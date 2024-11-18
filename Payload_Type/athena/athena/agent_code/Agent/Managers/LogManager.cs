
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Interfaces;

namespace Agent.Managers
{
    public class LogManager : ILogger
    {
        private bool isDebugEnabled = false;

        public void SetDebug(bool debug)
        {
            this.isDebugEnabled = debug;
        }
        public void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now}] {message}");
            Console.WriteLine($"[{DateTime.Now}] {message}");
        }
        public void Debug(string message)
        {
            if (this.isDebugEnabled)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG][{DateTime.Now}] {message}");
                Console.WriteLine($"[DEBUG][{DateTime.Now}] {message}");
            }

        }
    }
}
