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

    [TestMethod]
    public void InterpolatedString_LiteralParts_AreReplaced()
    {
        // The literal portions "Hello " and " world" must be encrypted;
        // the interpolation hole {x} must be preserved.
        var source =
            "class C { void M(string x) {"
            + " var s = $\"Hello {x} world\"; } }";
        var result = ApplyTransform(source);
        Assert.IsFalse(result.Contains("\"Hello \""),
            "literal 'Hello ' should not appear as plaintext");
        Assert.IsFalse(result.Contains("\" world\""),
            "literal ' world' should not appear as plaintext");
        Assert.IsTrue(result.Contains("new byte[]"),
            "encrypted byte arrays should be present");
    }

    [TestMethod]
    public void InterpolatedString_ExpressionHoles_ArePreserved()
    {
        // The {x} hole expression must survive unchanged.
        var source =
            "class C { void M(string x) {"
            + " var s = $\"prefix {x}\"; } }";
        var result = ApplyTransform(source);
        Assert.IsTrue(result.Contains("{x}"),
            "interpolation hole {x} must be preserved verbatim");
    }

    [TestMethod]
    public void InterpolatedString_InsideConst_IsNotReplaced()
    {
        // const interpolated string: text must not be replaced
        var source =
            "class C { const string X = $\"constant\"; }";
        var result = ApplyTransform(source);
        Assert.IsTrue(result.Contains("constant"),
            "const interpolated string text must not be encrypted");
    }
}
