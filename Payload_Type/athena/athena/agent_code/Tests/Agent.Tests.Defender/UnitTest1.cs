using Agent.Tests.Defender.Checker.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Agent.Tests.Defender.Checker.Checkers;
using System.IO;
using System;

namespace Agent.Tests.Defender
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestDllsWithDefender()
        {
            string parent_dir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
            foreach (string file in Directory.EnumerateFiles(parent_dir, "*.dll*", SearchOption.AllDirectories))
            {
                if(file.)
                Console.WriteLine(file);
            }



            Console.WriteLine(parent_dir);

            //var file = System.IO.File.ReadAllBytes(@"C:\Users\user\source\repos\Agent\Agent.Tests.Defender\bin\Debug\netcoreapp3.1\Agent.Tests.Defender.dll");
            //var result = ScanWithDefender(file);
            //Assert.IsFalse(result);

            Assert.IsTrue(false);
        }
        private bool ScanWithDefender(byte[] file)
        {
            var defender = new DefenderScanner(file);
            defender.Analyze();

            return defender.isMalicious();
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
