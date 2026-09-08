using Agent.Managers;

namespace Agent.Tests.AssemblyTests
{
    [TestClass]
    public class AssemblyManagerTests
    {
        [TestMethod]
        public void AssemblyManagerLoadsCommandAssembly()
        {
            IMessageManager messages = new TestMessageManager();
            IAssemblyManager manager = new AssemblyManager(
                messages,
                new TestLogger(),
                new TestAgentConfig(),
                new TestTokenManager(),
                new TestSpawner(),
                null);
            string path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "cat", "bin", "Debug", "net10.0", "cat.dll");
            Assert.IsTrue(File.Exists(path), $"Expected .NET 10 cat output at '{path}'.");

            bool loaded = manager.LoadAssemblyAsync("assembly-smoke", File.ReadAllBytes(path));

            Assert.IsTrue(loaded);
        }
    }
}
