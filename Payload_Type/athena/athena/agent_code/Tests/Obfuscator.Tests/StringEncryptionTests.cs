using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class StringEncryptionTests
{
    private string ApplyTransform(string source, int seed = 42)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var transform = new StringEncryptionTransform(
            "_Dec", "_D", "_Ns", seed);
        var result = transform.Rewrite(tree);
        return result.GetRoot().ToFullString();
    }

    [TestMethod]
    public void LiteralString_IsReplaced()
    {
        var source = "class C { string x = \"hello world\"; }";
        var result = ApplyTransform(source);
        Assert.IsFalse(result.Contains("\"hello world\""));
        Assert.IsTrue(result.Contains("new byte[]"));
    }

    [TestMethod]
    public void NameofExpression_IsNotReplaced()
    {
        var source =
            "class C { string x = nameof(System.Console); }";
        var result = ApplyTransform(source);
        Assert.IsTrue(result.Contains("nameof(System.Console)"));
    }

    [TestMethod]
    public void AttributeArgument_IsNotReplaced()
    {
        var source =
            "[System.Runtime.InteropServices.DllImport(\"kernel32\")]"
            + " static class C { }";
        var result = ApplyTransform(source);
        Assert.IsTrue(result.Contains("\"kernel32\""));
    }

    [TestMethod]
    public void ConstString_IsNotReplaced()
    {
        var source = "class C { const string x = \"constant\"; }";
        var result = ApplyTransform(source);
        Assert.IsTrue(result.Contains("\"constant\""));
    }

    [TestMethod]
    public void EmptyString_IsNotReplaced()
    {
        var source = "class C { string x = \"\"; }";
        var result = ApplyTransform(source);
        Assert.IsTrue(result.Contains("\"\""));
    }

    [TestMethod]
    public void DifferentSeeds_ProduceDifferentOutput()
    {
        var source = "class C { string x = \"hello\"; }";
        var result1 = ApplyTransform(source, seed: 1);
        var result2 = ApplyTransform(source, seed: 2);
        Assert.AreNotEqual(result1, result2);
    }

    [TestMethod]
    public void EncryptedString_CanBeDecrypted()
    {
        var original = "test";
        var seed = 42;
        var stringIndex = 0;
        var key = (byte)((seed + stringIndex) & 0xFF);
        if (key == 0) key = 1;

        var bytes = System.Text.Encoding.UTF8.GetBytes(original);
        var encrypted = new byte[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
            encrypted[i] = (byte)(bytes[i] ^ key);

        var decrypted = new byte[encrypted.Length];
        for (int i = 0; i < encrypted.Length; i++)
            decrypted[i] = (byte)(encrypted[i] ^ key);

        Assert.AreEqual(
            original,
            System.Text.Encoding.UTF8.GetString(decrypted));
    }
}
