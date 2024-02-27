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
        public void ScanAgent()
        {
            string path = PluginLoader.GetPluginPath("Agent");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanAgentModels()
        {
            string path = PluginLoader.GetPluginPath("Agent.Models");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanAgentManagersWindows()
        {
            string path = PluginLoader.GetPluginPath("Agent.Managers.Windows");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanAgentManagersReflection()
        {
            string path = PluginLoader.GetPluginPath("Agent.Managers.Reflection");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanAgentProfilesHttp()
        {
            string path = PluginLoader.GetPluginPath("Agent.Profiles.Http");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanAgentProfilesSmb()
        {
            string path = PluginLoader.GetPluginPath("Agent.Profiles.Smb");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanAgentProfilesWebsocket()
        {
            string path = PluginLoader.GetPluginPath("Agent.Profiles.Websocket");
            Assert.IsFalse(ScanPath(path));
        }

        [TestMethod]
        public void ScanPluginArp()
        {
            string path = PluginLoader.GetPluginPath("arp");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginCaffeinate()
        {
            string path = PluginLoader.GetPluginPath("caffeinate");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginCat()
        {
            string path = PluginLoader.GetPluginPath("cat");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginCd()
        {
            string path = PluginLoader.GetPluginPath("cd");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginCoff()
        {
            string path = PluginLoader.GetPluginPath("coff");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginConfig()
        {
            string path = PluginLoader.GetPluginPath("config");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginCp()
        {
            string path = PluginLoader.GetPluginPath("cp");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginCursed()
        {
            string path = PluginLoader.GetPluginPath("cursed");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginDownload()
        {
            string path = PluginLoader.GetPluginPath("download");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginDrives()
        {
            string path = PluginLoader.GetPluginPath("drives");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginDs()
        {
            string path = PluginLoader.GetPluginPath("ds");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginEcho()
        {
            string path = PluginLoader.GetPluginPath("echo");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginEntitelments()
        {
            string path = PluginLoader.GetPluginPath("entitlements");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginEnv()
        {
            string path = PluginLoader.GetPluginPath("env");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginExec()
        {
            string path = PluginLoader.GetPluginPath("exec");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginExecuteAssembly()
        {
            string path = PluginLoader.GetPluginPath("execute-assembly");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginExit()
        {
            string path = PluginLoader.GetPluginPath("exit");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginFarmer()
        {
            string path = PluginLoader.GetPluginPath("farmer");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginGetClipboard()
        {
            string path = PluginLoader.GetPluginPath("get-clipboard");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginGetLocalGroup()
        {
            string path = PluginLoader.GetPluginPath("get-localgroup");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginGetSessions()
        {
            string path = PluginLoader.GetPluginPath("get-sessions");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginGetShares()
        {
            string path = PluginLoader.GetPluginPath("get-shares");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginHostname()
        {
            string path = PluginLoader.GetPluginPath("hostname");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginHttpServer()
        {
            string path = PluginLoader.GetPluginPath("http-server");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginIfconfig()
        {
            string path = PluginLoader.GetPluginPath("ifconfig");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginInjectShellcode()
        {
            string path = PluginLoader.GetPluginPath("inject-shellcode");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginInjectShellcodeLinux()
        {
            string path = PluginLoader.GetPluginPath("inject-shellcode-linux");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginJobKill()
        {
            string path = PluginLoader.GetPluginPath("jobkill");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginJobs()
        {
            string path = PluginLoader.GetPluginPath("jobs");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginJxa()
        {
            string path = PluginLoader.GetPluginPath("jxa");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginKeychain()
        {
            string path = PluginLoader.GetPluginPath("keychain");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginKeylogger()
        {
            string path = PluginLoader.GetPluginPath("keylogger");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginKill()
        {
            string path = PluginLoader.GetPluginPath("kill");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginLnk()
        {
            string path = PluginLoader.GetPluginPath("lnk");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginLs()
        {
            string path = PluginLoader.GetPluginPath("ls");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginMkDir()
        {
            string path = PluginLoader.GetPluginPath("mkdir");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginMv()
        {
            string path = PluginLoader.GetPluginPath("mv");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginNetstat()
        {
            string path = PluginLoader.GetPluginPath("netstat");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginNsLookup()
        {
            string path = PluginLoader.GetPluginPath("nslookup");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginPortBender()
        {
            string path = PluginLoader.GetPluginPath("port-bender");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginPs()
        {
            string path = PluginLoader.GetPluginPath("ps");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginPwd()
        {
            string path = PluginLoader.GetPluginPath("pwd");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginReg()
        {
            string path = PluginLoader.GetPluginPath("reg");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginRm()
        {
            string path = PluginLoader.GetPluginPath("rm");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginRportFwd()
        {
            string path = PluginLoader.GetPluginPath("rportfwd");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginScreenshot()
        {
            string path = PluginLoader.GetPluginPath("screenshot");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginSftp()
        {
            string path = PluginLoader.GetPluginPath("sftp");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginShell()
        {
            string path = PluginLoader.GetPluginPath("shell");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginShellcode()
        {
            string path = PluginLoader.GetPluginPath("shellcode");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginSmb()
        {
            string path = PluginLoader.GetPluginPath("smb");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginSocks()
        {
            string path = PluginLoader.GetPluginPath("socks");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginSsh()
        {
            string path = PluginLoader.GetPluginPath("ssh");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginTail()
        {
            string path = PluginLoader.GetPluginPath("tail");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginTestPort()
        {
            string path = PluginLoader.GetPluginPath("test-port");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginTimestomp()
        {
            string path = PluginLoader.GetPluginPath("timestomp");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginToken()
        {
            string path = PluginLoader.GetPluginPath("token");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginUpload()
        {
            string path = PluginLoader.GetPluginPath("upload");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginUptime()
        {
            string path = PluginLoader.GetPluginPath("uptime");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginWget()
        {
            string path = PluginLoader.GetPluginPath("wget");
            Assert.IsFalse(ScanPath(path));
        }
        [TestMethod]
        public void ScanPluginWhoami()
        {
            string path = PluginLoader.GetPluginPath("whoami");
            Assert.IsFalse(ScanPath(path));
        }
        private bool ScanPath(string path)
        {
            Assert.IsTrue(Path.Exists(path));
            byte[] b = File.ReadAllBytes(path);

            var defender = new DefenderScanner(b);
            defender.Analyze();
            if (defender.isMalicious())
            {
                Console.WriteLine(defender.badBytes);
            }

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
