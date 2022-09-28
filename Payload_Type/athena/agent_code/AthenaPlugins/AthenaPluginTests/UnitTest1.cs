using Microsoft.VisualStudio.TestTools.UnitTesting;
using Plugin;
using System.Collections.Generic;
using PluginBase;
using System.IO;
using Newtonsoft.Json;
using System.Net;
using System;
using System.Diagnostics;
using System.Linq;

namespace PluginPluginTests
{
    [TestClass]
    public class UnitTest1
    {
        //[TestMethod]
        //public void TestCD()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>
        //    {
        //        { "path", Path.GetTempPath() },
        //        { "task-id", "1" }
        //    };
        //    cd.Execute(dict);
        //    Assert.IsTrue(Directory.GetCurrentDirectory() + "\\" == Path.GetTempPath());
        //}
        //[TestMethod]
        //public void TestCDInvalid()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("path", $"C:\\idontexist\\");
        //    dict.Add("task-id", "1");
        //    cd.Execute(dict);
        //    ResponseResult rr = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(rr.user_output);

        //    Assert.IsTrue(rr.status == "error");
        //}
        //[TestMethod]
        //public void TestCat()
        //{
        //    File.WriteAllText($"{Path.GetTempPath()}testcat.txt", "Hello World!");
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", $"{Path.GetTempPath()}testcat.txt");
        //    cat.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();


        //    Assert.IsTrue(result.user_output == "Hello World!");
        //    Console.WriteLine(Path.GetTempPath());
        //    File.Delete($"{Path.GetTempPath()}testcat.txt");
        //}
        //[TestMethod]
        //public void TestCatInvalid()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", $"C:\\idontexist\\testfile.txt");
        //    cat.Execute(dict);
        //    ResponseResult rr = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();

        //    Assert.IsTrue(rr.status == "error");
        //    Console.WriteLine(Path.GetTempPath());
        //}
        //[TestMethod]
        //public void TestWget()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("url", "https://www.whatsmyua.info/api/v1/ua");
        //    wget.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(result.user_output.Contains("platform.js"));
        //}
        //[TestMethod]
        //public void TestWgetNoUrl()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    wget.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(result.status == "error");
        //}
        //[TestMethod]
        //public void TestDrives()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    drives.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(result.user_output.Contains("C:\\"));
        //}
        //[TestMethod]
        //public void TestWhoami()
        //{
        //    //Dictionary<string, object> dict = new Dictionary<string, object>();
        //    //dict.Add("task-id", "1");
        //    //whoami.Execute(dict);
        //    //ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    //Assert.IsTrue(result.user_output.Contains(Environment.UserName));
        //}
        //[TestMethod]
        //public void TestHostname()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    hostname.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(result.user_output == Dns.GetHostName());
        //}
        //[TestMethod]
        //public void TestEnv()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    Plugins.Plugin p = new Env.Plugins.Plugin();

        //    Plugin.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(JsonConvert.SerializeObject(result).Contains(Dns.GetHostName()));

        //}
        //[TestMethod]
        //public void TestKill()
        //{
        //    Process p = Process.Start("notepad.exe");
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("id", p.Id);
        //    kill.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(p.HasExited);
        //}
        //[TestMethod]
        //public void TestKillInvalid()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    kill.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(result.status == "error");
        //}
        //[TestMethod]
        //public void TestLs()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", "C:\\Users");
        //    ls.Execute(dict);
        //    FileBrowserResponseResult result = (FileBrowserResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(JsonConvert.SerializeObject(result));
        //    Assert.IsTrue(result.file_browser.files.Count > 3);
        //}
        //[TestMethod]
        //public void TestLsRemote()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", "C$\\Users");
        //    dict.Add("host", "127.0.0.1");
            
        //    ls.Execute(dict);
        //    FileBrowserResponseResult result = (FileBrowserResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(JsonConvert.SerializeObject(result));
        //    Assert.IsTrue(result.file_browser.files.Count > 3);
        //}

        //[TestMethod]
        //public void TestLsQuotedPath()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", "\"C:\\Program Files\\\"");
        //    ls.Execute(dict);
        //    FileBrowserResponseResult result = (FileBrowserResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(result.user_output);
        //    Assert.IsTrue(result.file_browser.files.Count > 3);
        //}
        //[TestMethod]
        //public void TestLsSpacePath()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", "C:\\Program Files\\");
        //    ls.Execute(dict);
        //    FileBrowserResponseResult result = (FileBrowserResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(result.user_output);
        //    Assert.IsTrue(result.file_browser.files.Count > 3);
        //}
        //[TestMethod]
        //public void TestLsInvalid()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", "C:\\Idontexist");
        //    ls.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(result.user_output);
        //    Assert.IsTrue(result.status == "error");
        //}
        //[TestMethod]
        //public void TestNsLookupInvalid()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("hosts", "www.thiswebsiteisntreal.com");
        //    nslookup.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(result.user_output.Contains("NOTFOUND"));
        //}
        //[TestMethod]
        //public void TestNsLookup()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("hosts", "google.com");
        //    nslookup.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(!result.user_output.Contains("NOTFOUND"));
        //}

        ////[TestMethod]
        ////public void TestSSH()
        ////{
        ////    Dictionary<string, object> dict = new Dictionary<string, object>();
        ////    dict.Add("task-id", "1");
        ////    dict.Add("hosts", "google.com,reddit.com,twitter.com");
        ////    nslookup.Execute(dict);
        ////    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        ////    Assert.IsTrue(!result.user_output.Contains("NOTFOUND"));
        ////}

        ////[TestMethod]
        ////public void TestSSHWithPort()
        ////{
        ////    Dictionary<string, object> dict = new Dictionary<string, object>();
        ////    dict.Add("task-id", "1");
        ////    dict.Add("hosts", "google.com,reddit.com,twitter.com");
        ////    nslookup.Execute(dict);
        ////    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        ////    Assert.IsTrue(!result.user_output.Contains("NOTFOUND"));
        ////}


        //[TestMethod]
        //public void TestNsLookupMultiple()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("hosts", "google.com,reddit.com,twitter.com");
        //    nslookup.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(!result.user_output.Contains("NOTFOUND"));
        //}
        //[TestMethod]
        //public void TestMkdir()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", $"{Path.GetTempPath()}testdir");
        //    mkdir.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(Directory.Exists((string)dict["path"]));
        //    Directory.Delete((string)dict["path"]);
        //}
        //[TestMethod]
        //public void TestMkdirAccessDenied()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", $"C:\\Windows\\testdir");
        //    mkdir.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(result.status == "error");
        //}
        //[TestMethod]
        //public void TestMkdirInvalidPath()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", $"C:\\Windows\\idontexist\\testdir");
        //    mkdir.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(!Directory.Exists($"C:\\Windows\\idontexist\\testdir"));
        //}
        //[TestMethod]
        //public void TestMv()
        //{
        //    File.WriteAllText($"{Path.GetTempPath()}testfile.txt", "hello world");
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("source", $"{Path.GetTempPath()}testfile.txt");
        //    dict.Add("destination", $"{Path.GetTempPath()}testfile2.txt");
        //    mv.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(File.Exists($"{Path.GetTempPath()}testfile2.txt"));
        //    File.Delete($"{Path.GetTempPath()}testfile2.txt");
        //}
        //[TestMethod]
        //public void TestMvInvalid()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("source", $"C:\\idontexist\\testfile.txt");
        //    dict.Add("destination", $"C:\\idontexist\\testfile.txt");
        //    mv.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(!File.Exists($"C:\\idontexist\\testfile.txt"));
        //}
        //[TestMethod]
        //public void TestPs()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    Plugin.ps.Execute(dict);
        //    ProcessResponseResult result = (ProcessResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(result.processes.Count > 3);
        //}
        //[TestMethod]
        //public void TestPwd()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    pwd.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(result.user_output == Directory.GetCurrentDirectory());
        //}
        //[TestMethod]
        //public void TestRM()
        //{
        //    Directory.CreateDirectory($"{Path.GetTempPath()}testrmdir");
        //    Assert.IsTrue(Directory.Exists($"{Path.GetTempPath()}testrmdir"));
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", $"{Path.GetTempPath()}testrmdir");
        //    rm.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsFalse(Directory.Exists($"{Path.GetTempPath()}testrmdir"));
        //}
        //[TestMethod]
        //public void TestRMInvalid()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", "C:\\Windows\\idontexist");
        //    rm.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(result.status == "error");
        //}
        //[TestMethod]
        //public void TestRMInvalid2()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("path", $"{Path.GetTempPath()}\testrmdir");
        //    rm.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsTrue(result.status == "error");
        //}
        //[TestMethod]
        //public void testUptime()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    uptime.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Assert.IsFalse(result.status == "error");
        //}
        //[TestMethod]
        //public void testLocalGroups()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("hostname", "");
        //    dict.Add("group", "");
        //    getlocalgroup.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(result.user_output);
        //    Assert.IsFalse(result.status == "error");
        //}
        //[TestMethod]
        //public void testRemoteGroups()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("hostname", "localhost");
        //    dict.Add("group", "");
        //    getlocalgroup.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(result.user_output);
        //    Assert.IsFalse(result.status == "error");
        //}
        //[TestMethod]
        //public void testRemoteGroupMembers()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("hostname", "localhost");
        //    dict.Add("group", "Administrators");
        //    getlocalgroup.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(result.user_output);
        //    Assert.IsFalse(result.status == "error");
        //}
        //[TestMethod]
        //public void testLocalGroupMembers()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("hostname", "");
        //    dict.Add("group", "Administrators");
        //    getlocalgroup.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(result.user_output);
        //    Assert.IsFalse(result.status == "error");
        //}
        //[TestMethod]
        //public void testWinEnumResources()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    winenumresources.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(result.user_output);
        //    Assert.IsFalse(result.status == "error");
        //}
        //[TestMethod]
        //public void testArp()
        //{
        //    Dictionary<string, object> dict = new Dictionary<string, object>();
        //    dict.Add("task-id", "1");
        //    dict.Add("cidr", "192.168.86.0/24");
        //    dict.Add("timeout", "5");
        //    arp.Execute(dict);
        //    ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
        //    Console.WriteLine(result.user_output);
        //    Assert.IsFalse(result.status == "error");
        //}
    }
}