using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workflow.Tests.ProfileTests
{
    [TestClass]
    public class SmbProfileTests
    {
        [TestMethod]
        public void TestProfileReplaceAndBuild()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "Workflow.Channels.Smb");
            var proj_path = Path.Combine(path, "Workflow.Channels.Smb.csproj");
            string[] oldContents = File.ReadAllLines(Path.Combine(path, "SmbProfile.cs"));
            string[] replaceContents = File.ReadAllLines(Path.Combine(path, "Base.txt"));

            File.WriteAllLines(Path.Combine(path, "SmbProfile.cs"), replaceContents);

            string[] newContents = File.ReadAllLines(Path.Combine(path, "SmbProfile.cs"));

            Assert.AreEqual(string.Join(Environment.NewLine, newContents), string.Join(Environment.NewLine, replaceContents));


            Process p = Process.Start(new ProcessStartInfo()
            {
                FileName = "dotnet",
                Arguments = $"build {proj_path}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            while (!p.StandardOutput.EndOfStream)
            {
                string line = p.StandardOutput.ReadLine();
                Console.WriteLine(line);
                // do something with line
            }

            p.WaitForExit();
            Assert.IsTrue(p.ExitCode == 0);

            Console.WriteLine("Returning old values.");
            File.WriteAllLines(Path.Combine(path, "SmbProfile.cs"), oldContents);
            //Test to make sure the plugin parses local paths like we expect
        }
    }
}
