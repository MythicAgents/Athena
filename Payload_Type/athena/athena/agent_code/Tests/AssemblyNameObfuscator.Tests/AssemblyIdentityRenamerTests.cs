using System.Reflection;
using AssemblyNameObfuscator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssemblyNameObfuscator.Tests;

[TestClass]
public class AssemblyIdentityRenamerTests
{
    [TestMethod]
    public void RewriteChangesAssemblyIdentityWithoutRenamingTheFile()
    {
        string source = typeof(AssemblyIdentityRenamerTests).Assembly.Location;
        var tempDir = Directory.CreateTempSubdirectory("athena-asm-rename-");
        string target = Path.Combine(tempDir.FullName, "visible-command-name.dll");
        File.Copy(source, target);

        try
        {
            string renamed = AssemblyIdentityRenamer.Rewrite(target, 123456);

            Assert.AreEqual("_K76PQ", renamed);
            Assert.AreEqual(renamed, AssemblyName.GetAssemblyName(target).Name);
            Assert.IsTrue(File.Exists(target));
        }
        finally { tempDir.Delete(recursive: true); }
    }
}
