
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
        public void Log(string message)
        {
            Debug.WriteLine($"[{DateTime.Now}] {message}");
            Console.WriteLine($"[{DateTime.Now}] {message}");
        }
    }
}
