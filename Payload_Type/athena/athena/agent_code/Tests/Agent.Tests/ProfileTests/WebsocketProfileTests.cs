using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Agent.Tests.ProfileTests
{
    //https://echo.free.beeceptor.com/
    [TestClass]
    public class WebsocketProfileTests
    {
        [TestMethod]
        public void TestProfileReplaceAndBuild()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "Agent.Profiles.Websocket");
            var proj_path = Path.Combine(path, "Agent.Profiles.Websocket.csproj");
            string[] oldContents = File.ReadAllLines(Path.Combine(path, "WebsocketProfile.cs"));
            string[] replaceContents = File.ReadAllLines(Path.Combine(path, "Base.txt"));

            File.WriteAllLines(Path.Combine(path, "WebsocketProfile.cs"), replaceContents);

            string[] newContents = File.ReadAllLines(Path.Combine(path, "WebsocketProfile.cs"));

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
            }
            p.WaitForExit();
            Assert.IsTrue(p.ExitCode == 0);

            Console.WriteLine("Returning old values.");
            File.WriteAllLines(Path.Combine(path, "WebsocketProfile.cs"), oldContents);
        }
    }
}
