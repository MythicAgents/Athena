using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Plugin;
using PluginBase;
using System.Linq;

namespace AthenaPluginTests
{
    [TestClass]
    public class SftpTests
    {
        [TestMethod]
        public void TestSftpConnect()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data.Add("username", "rt");
            data.Add("hostname", "192.168.4.201");
            data.Add("password", "RedT3amR0cks!");
            data.Add("action", "connect");
            data.Add("task-id", "0");
            sftp.Execute(data);
            ResponseResult response = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
            Console.WriteLine(response.user_output);
            Assert.IsTrue(String.IsNullOrEmpty(response.status));
        }

        [TestMethod]
        public void TestSftpLsWithFullPath()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data.Add("username", "rt");
            data.Add("hostname", "192.168.4.201");
            data.Add("password", "RedT3amR0cks!");
            data.Add("action", "connect");
            data.Add("task-id", "0");
            sftp.Execute(data);
            ResponseResult response = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
            Assert.IsTrue(String.IsNullOrEmpty(response.status));

            data.Add("path", "/rt/slack/C2_Profiles");
            data["action"] = "ls";
            sftp.Execute(data);
            response = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
            Console.WriteLine(response.user_output);
            Assert.IsTrue(String.IsNullOrEmpty(response.status));
        }
        [TestMethod]
        public void TestSftpLsWithPartialPath()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data.Add("username", "rt");
            data.Add("hostname", "192.168.4.201");
            data.Add("password", "RedT3amR0cks!");
            data.Add("action", "connect");
            data.Add("task-id", "0");
            ResponseResult response = response = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
            sftp.Execute(data);
            ResponseResult result = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
            Assert.IsTrue(String.IsNullOrEmpty(response.status));

            data["action"] = "cd";
            data["path"] = "rt/slack";
            response = response = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
            sftp.Execute(data);

            data["path"] = "C2_Profiles";
            data["action"] = "ls";
            response = response = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
            sftp.Execute(data);
            Console.WriteLine(response.user_output);
            Assert.IsTrue(String.IsNullOrEmpty(response.status));
        }
        [TestMethod]
        public void TestSftpLsWitRelativePath()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data.Add("username", "rt");
            data.Add("hostname", "192.168.4.201");
            data.Add("password", "RedT3amR0cks!");
            data.Add("action", "connect");
            data.Add("task-id", "0");
            ResponseResult response = response = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
            sftp.Execute(data);
            Assert.IsTrue(String.IsNullOrEmpty(response.status));

            data["action"] = "cd";
            data["path"] = "rt/slack";
            response = response = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
            sftp.Execute(data);

            data["path"] = "../Athena";
            data["action"] = "ls";
            response = response = (ResponseResult)PluginHandler.GetResponses().Result.FirstOrDefault();
            sftp.Execute(data);
            Console.WriteLine(response.user_output);
            Assert.IsTrue(String.IsNullOrEmpty(response.status));
        }
    }
}
