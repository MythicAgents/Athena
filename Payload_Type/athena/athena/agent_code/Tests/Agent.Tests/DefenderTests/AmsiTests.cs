using Agent.Tests.Defender.Checker.Checkers;
using Agent.Tests.Defender.Checker.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.Defender
{
    [TestClass]
    public class AmsiTests
    {
        [TestMethod]
        public void ScanFiles()
        {
            bool malicious = false;
            string parent_dir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName; //How deep does the rabbit hole go?
            foreach (string file in Directory.EnumerateFiles(parent_dir, "*.dll*", SearchOption.AllDirectories))
            {
                if (file.Contains("Agent.Tests") || file.Contains("\\obj\\")) //Windows only so this should be fine
                {
                    continue;
                }

                byte[] b = File.ReadAllBytes(file);

                using (var amsi = new AmsiScanner())
                {
                    if (!amsi.RealTimeProtectionEnabled)
                    {
                        Console.WriteLine("Ensure real-time protection is enabled");
                        malicious = true;
                    }

                    amsi.AnalyzeBytes(b);

                    if (amsi.isMalicious())
                    {
                        malicious = true;
                        Console.WriteLine("Malicious File: " + file);
                        Console.WriteLine(amsi.badBytes);
                    }
                }
            }

            Assert.IsFalse(malicious);
        }
    }
}
