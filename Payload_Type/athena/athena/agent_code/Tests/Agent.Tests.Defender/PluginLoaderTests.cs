using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Agent.Tests.Defender
{
    [TestClass]
    public class PluginLoaderTests
    {
        [TestMethod]
        public void GetPluginPathFindsNet10DebugOutput()
        {
            string path = PluginLoader.GetPluginPath("cat");

            Assert.IsFalse(string.IsNullOrEmpty(path));
            StringAssert.Contains(path, Path.Combine("Debug", "net10.0", "cat.dll"));
        }
    }
}
