using Microsoft.VisualStudio.TestTools.UnitTesting;
using Plugin;
using System.Collections.Generic;
using PluginBase;
using System.IO;
using Newtonsoft.Json;
using System.Net;
using System;
using System.Diagnostics;

namespace PluginPluginTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestCD()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("path", Path.GetTempPath());
            dict.Add("task-id", "1");
            cd.Execute(dict);
            Assert.IsTrue(Directory.GetCurrentDirectory() + "\\" == Path.GetTempPath());
        }
        [TestMethod]
        public void TestCDInvalid()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("path", $"C:\\idontexist\\");
            dict.Add("task-id", "1");
            ResponseResult rr = cd.Execute(dict);
            Console.WriteLine(rr.user_output);

            Assert.IsTrue(rr.status == "error");
        }
        [TestMethod]
        public void TestCat()
        {
            File.WriteAllText($"{Path.GetTempPath()}testcat.txt", "Hello World!");
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", $"{Path.GetTempPath()}testcat.txt");
            ResponseResult result = cat.Execute(dict);

            Assert.IsTrue(result.user_output == "Hello World!");
            Console.WriteLine(Path.GetTempPath());
            File.Delete($"{Path.GetTempPath()}testcat.txt");
        }
        [TestMethod]
        public void TestCatInvalid()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", $"C:\\idontexist\\testfile.txt");
            ResponseResult result = cat.Execute(dict);
            Assert.IsTrue(result.status == "error");
            Console.WriteLine(Path.GetTempPath());
        }
        [TestMethod]
        public void TestWget()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("url", "https://www.whatsmyua.info/api/v1/ua");
            ResponseResult result = wget.Execute(dict);
            Assert.IsTrue(result.user_output.Contains("platform.js"));
        }
        [TestMethod]
        public void TestWgetNoUrl()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            ResponseResult result = wget.Execute(dict);
            Assert.IsTrue(result.status == "error");
        }
        [TestMethod]
        public void TestDrives()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            ResponseResult result = drives.Execute(dict);
            Assert.IsTrue(result.user_output.Contains("C:\\"));
        }
        [TestMethod]
        public void TestWhoami()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            ResponseResult result = whoami.Execute(dict);
            Assert.IsTrue(result.user_output.Contains(Environment.UserName));
        }
        [TestMethod]
        public void TestHostname()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            ResponseResult result = hostname.Execute(dict);
            Assert.IsTrue(result.user_output == Dns.GetHostName());
        }
        [TestMethod]
        public void TestEnv()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            var result = env.Execute(dict);
            Assert.IsTrue(JsonConvert.SerializeObject(result).Contains(Dns.GetHostName()));

        }
        [TestMethod]
        public void TestKill()
        {
            Process p = Process.Start("notepad.exe");
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("id", p.Id);
            var result = kill.Execute(dict);
            Assert.IsTrue(p.HasExited);
        }
        [TestMethod]
        public void TestKillInvalid()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            ResponseResult result = kill.Execute(dict);
            Assert.IsTrue(result.status == "error");
        }
        [TestMethod]
        public void TestLs()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", "C:\\Windows\\System32\\drivers\\etc\\");
            FileBrowserResponseResult result = ls.Execute(dict);
            Console.WriteLine(result.user_output);
            Assert.IsTrue(result.file_browser.files.Count > 3);
        }
        [TestMethod]
        public void TestLsRemote()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", "C$");
            dict.Add("host", "127.0.0.1");
            
            FileBrowserResponseResult result = ls.Execute(dict);
            Console.WriteLine(JsonConvert.SerializeObject(result));
            Assert.IsTrue(result.file_browser.files.Count > 3);
        }

        [TestMethod]
        public void TestLsQuotedPath()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", "\"C:\\Program Files\\\"");
            FileBrowserResponseResult result = ls.Execute(dict);
            Console.WriteLine(result.user_output);
            Assert.IsTrue(result.file_browser.files.Count > 3);
        }
        [TestMethod]
        public void TestLsSpacePath()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", "C:\\Program Files\\");
            FileBrowserResponseResult result = ls.Execute(dict);
            Console.WriteLine(result.user_output);
            Assert.IsTrue(result.file_browser.files.Count > 3);
        }
        [TestMethod]
        public void TestLsInvalid()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", "C:\\Idontexist");
            FileBrowserResponseResult result = ls.Execute(dict);
            Console.WriteLine(result.user_output);
            Assert.IsTrue(result.status == "error");
        }
        [TestMethod]
        public void TestNsLookupInvalid()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("hosts", "www.thiswebsiteisntreal.com");
            ResponseResult result = nslookup.Execute(dict);
            Assert.IsTrue(result.user_output.Contains("NOTFOUND"));
        }
        [TestMethod]
        public void TestNsLookup()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("hosts", "google.com");
            ResponseResult result = nslookup.Execute(dict);
            Assert.IsTrue(!result.user_output.Contains("NOTFOUND"));
        }
        [TestMethod]
        public void TestNsLookupMultiple()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("hosts", "google.com,reddit.com,twitter.com");
            ResponseResult result = nslookup.Execute(dict);
            Assert.IsTrue(!result.user_output.Contains("NOTFOUND"));
        }
        [TestMethod]
        public void TestMkdir()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", $"{Path.GetTempPath()}testdir");
            ResponseResult result = mkdir.Execute(dict);
            Assert.IsTrue(Directory.Exists((string)dict["path"]));
            Directory.Delete((string)dict["path"]);
        }
        [TestMethod]
        public void TestMkdirAccessDenied()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", $"C:\\Windows\\testdir");
            ResponseResult result = mkdir.Execute(dict);
            Assert.IsTrue(result.status == "error");
        }
        [TestMethod]
        public void TestMkdirInvalidPath()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", $"C:\\Windows\\idontexist\\testdir");
            ResponseResult result = mkdir.Execute(dict);
            Assert.IsTrue(!Directory.Exists($"C:\\Windows\\idontexist\\testdir"));
        }
        [TestMethod]
        public void TestMv()
        {
            File.WriteAllText($"{Path.GetTempPath()}testfile.txt", "hello world");
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("source", $"{Path.GetTempPath()}testfile.txt");
            dict.Add("destination", $"{Path.GetTempPath()}testfile2.txt");
            ResponseResult result = mv.Execute(dict);
            Assert.IsTrue(File.Exists($"{Path.GetTempPath()}testfile2.txt"));
            File.Delete($"{Path.GetTempPath()}testfile2.txt");
        }
        [TestMethod]
        public void TestMvInvalid()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("source", $"C:\\idontexist\\testfile.txt");
            dict.Add("destination", $"C:\\idontexist\\testfile.txt");
            ResponseResult result = mv.Execute(dict);
            Assert.IsTrue(!File.Exists($"C:\\idontexist\\testfile.txt"));
        }
        [TestMethod]
        public void TestPs()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            ProcessResponseResult result = ps.Execute(dict);
            Assert.IsTrue(result.processes.Count > 3);
        }
        [TestMethod]
        public void TestPwd()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            ResponseResult result = pwd.Execute(dict);
            Assert.IsTrue(result.user_output == Directory.GetCurrentDirectory());
        }
        [TestMethod]
        public void TestRM()
        {
            Directory.CreateDirectory($"{Path.GetTempPath()}testrmdir");
            Assert.IsTrue(Directory.Exists($"{Path.GetTempPath()}testrmdir"));
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", $"{Path.GetTempPath()}testrmdir");
            ResponseResult result = rm.Execute(dict);
            Assert.IsFalse(Directory.Exists($"{Path.GetTempPath()}testrmdir"));
        }
        [TestMethod]
        public void TestRMInvalid()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", "C:\\Windows\\idontexist");
            ResponseResult result = rm.Execute(dict);
            Assert.IsTrue(result.status == "error");
        }
        [TestMethod]
        public void TestRMInvalid2()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", $"{Path.GetTempPath()}\testrmdir");
            ResponseResult result = rm.Execute(dict);
            Assert.IsTrue(result.status == "error");
        }
        [TestMethod]
        public void testUptime()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            ResponseResult result = uptime.Execute(dict);
            Assert.IsFalse(result.status == "error");
        }
    }
}