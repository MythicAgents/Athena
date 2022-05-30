using Microsoft.VisualStudio.TestTools.UnitTesting;
using Athena;
using System.Collections.Generic;
using PluginBase;
using System.IO;
using Newtonsoft.Json;
using System.Net;
using System;

namespace AthenaPluginTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestCD()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("path", "C:\\");
            dict.Add("task-id", "1");
            cd.Execute(dict);

            Assert.IsTrue(Directory.GetCurrentDirectory() == "C:\\");
        }
        [TestMethod]
        public void TestCat()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("path", @"C:\Windows\System32\drivers\etc\hosts");
            var result = cat.Execute(dict);

            Assert.IsTrue(JsonConvert.SerializeObject(result).Contains("Copyright (c) 1993-2009 Microsoft Corp"));
        }
        [TestMethod]
        public void TestWget()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            dict.Add("url", "https://www.whatsmyua.info/api/v1/ua");
            var result = wget.Execute(dict);
            Assert.IsTrue(JsonConvert.SerializeObject(result).Contains("platform.js"));
        }
        [TestMethod]
        public void TestWgetNoUrl()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            var result = wget.Execute(dict);
            Assert.IsTrue(JsonConvert.SerializeObject(result).Contains("A URL needs to be specified."));
        }
        [TestMethod]
        public void TestDrives()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            var result = drives.Execute(dict);
            Assert.IsTrue(JsonConvert.SerializeObject(result).Contains("C:\\"));
        }
        [TestMethod]
        public void TestWhoami()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            var result = whoami.Execute(dict);
            Assert.IsTrue(JsonConvert.SerializeObject(result).Contains(Environment.UserName));
        }
        [TestMethod]
        public void TestHostname()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("task-id", "1");
            var result = hostname.Execute(dict);
            Assert.IsTrue(JsonConvert.SerializeObject(result).Contains(Dns.GetHostName()));
        }
    }
}