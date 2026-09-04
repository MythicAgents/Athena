using System.Reflection;
using AssemblyNameObfuscator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;

namespace AssemblyNameObfuscator.Tests;

[TestClass]
public class AssemblyIdentityRenamerTests
{
    [TestMethod]
    public void Rewrite_HidesOriginalAssemblyIdentityWithoutRenamingFile()
    {
        var source = typeof(AssemblyIdentityRenamerTests).Assembly.Location;
        var tempDir = Directory.CreateTempSubdirectory("athena-asm-rename-");
        var target = Path.Combine(tempDir.FullName, "visible-command-name.dll");
        File.Copy(source, target);

        try
        {
            var renamed = AssemblyIdentityRenamer.Rewrite(target, 123456);

            Assert.AreEqual("_K76PQ", renamed);
            Assert.AreEqual(renamed, AssemblyName.GetAssemblyName(target).Name);
            Assert.IsTrue(File.Exists(target), "The build pipeline still expects the original output path.");
            Assert.AreNotEqual("AssemblyNameObfuscator.Tests", AssemblyName.GetAssemblyName(target).Name);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void GenerateName_IsDeterministicAndDependsOnSeedAndOriginalName()
    {
        var first = AssemblyIdentityRenamer.GenerateName(42, "jobs");

        Assert.AreEqual(first, AssemblyIdentityRenamer.GenerateName(42, "jobs"));
        Assert.AreNotEqual(first, AssemblyIdentityRenamer.GenerateName(43, "jobs"));
        Assert.AreNotEqual(first, AssemblyIdentityRenamer.GenerateName(42, "coff"));
        StringAssert.Matches(first, new System.Text.RegularExpressions.Regex("^_[A-Za-z0-9]{5}$"));
    }

    [TestMethod]
    public void RewritePatchesNonFrameworkAssemblyReferences()
    {
        var source = typeof(AssemblyIdentityRenamerTests).Assembly.Location;
        var target = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll");
        File.Copy(source, target);
        try
        {
            AssemblyIdentityRenamer.Rewrite(target, 123456);
            using var rewritten = AssemblyDefinition.ReadAssembly(target);
            var expected = AssemblyIdentityRenamer.GenerateName(
                123456, "AssemblyNameObfuscator");
            Assert.IsTrue(rewritten.MainModule.AssemblyReferences
                .Any(reference => reference.Name == expected));
        }
        finally
        {
            File.Delete(target);
        }
    }

}
