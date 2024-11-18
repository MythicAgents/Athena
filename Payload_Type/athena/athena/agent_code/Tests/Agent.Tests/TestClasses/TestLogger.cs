using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestClasses
{
    internal class TestLogger : ILogger
    {
        public void SetDebug(bool debug)
        {
        }
        public void Debug(string message)
        {
            Debug.WriteLine(message);
        }
        public void Log(string message)
        {
            Debug.WriteLine(message);
        }    
    }
}
