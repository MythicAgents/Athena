using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugin;
using PluginBase;

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
            ResponseResult response = sftp.Execute(data);
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
            ResponseResult response = sftp.Execute(data);
            Assert.IsTrue(String.IsNullOrEmpty(response.status));

            data.Add("path", "/rt/slack/C2_Profiles");
            data["action"] = "ls";
            response = sftp.Execute(data);
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
            ResponseResult response = sftp.Execute(data);
            Assert.IsTrue(String.IsNullOrEmpty(response.status));

            data["action"] = "cd";
            data["path"] = "rt/slack";
            response = sftp.Execute(data);

            data["path"] = "C2_Profiles";
            data["action"] = "ls";
            response = sftp.Execute(data);
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
            ResponseResult response = sftp.Execute(data);
            Assert.IsTrue(String.IsNullOrEmpty(response.status));

            data["action"] = "cd";
            data["path"] = "rt/slack";
            response = sftp.Execute(data);

            data["path"] = "../Athena";
            data["action"] = "ls";
            response = sftp.Execute(data);
            Console.WriteLine(response.user_output);
            Assert.IsTrue(String.IsNullOrEmpty(response.status));
        }
    }
}
