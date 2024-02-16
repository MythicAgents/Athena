using Agent.Tests.Defender.Checker.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Agent.Tests.Defender.Checker.Checkers;
using System.IO;
using System;
using System.Security.Cryptography;

namespace Agent.Tests.Defender
{
    [TestClass]
    public class DefenderTests
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

                var defender = new DefenderScanner(b);
                defender.Analyze();
                if (defender.isMalicious())
                {
                    malicious = true;
                    Console.WriteLine("Malicious File: " + file);
                    Console.WriteLine(defender.badBytes);
                }
            }

            Assert.IsFalse(malicious);
        }

        private bool ScanWithAmsi(byte[] file)
        {
            using (var amsi = new AmsiScanner())
            {
                if (!amsi.RealTimeProtectionEnabled)
                {
                    CustomConsole.WriteError("Ensure real-time protection is enabled");
                    return true;
                }

                amsi.AnalyzeBytes(file);

                return amsi.isMalicious();
            }

        }
    }
}
