using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class ApiCallHidingTests
{
    private string ApplyTransform(string source, int seed = 42)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var transform = new ApiCallHidingTransform(
            "_Caller", "_Invoke", "_Ns", seed);
        var result = transform.Rewrite(tree);
        return result.GetRoot().ToFullString();
    }

    [TestMethod]
    public void ProcessStart_IsReplaced()
    {
        var source = """
            class C {
                void M() { System.Diagnostics.Process.Start("cmd"); }
            }
            """;
        var result = ApplyTransform(source);
        Assert.IsFalse(result.Contains("Process.Start("));
        Assert.IsTrue(result.Contains("_Invoke"));
    }

    [TestMethod]
    public void ConsoleWriteLine_IsNotReplaced()
    {
        var source = """
            class C {
                void M() { Console.WriteLine("hello"); }
            }
            """;
        var result = ApplyTransform(source);
        Assert.IsTrue(result.Contains("Console.WriteLine("));
        Assert.IsFalse(result.Contains("_Invoke"));
    }

    [TestMethod]
    public void FileReadAllBytes_IsReplaced()
    {
        var source = """
            class C {
                void M(string path) { var b = File.ReadAllBytes(path); }
            }
            """;
        var result = ApplyTransform(source);
        Assert.IsFalse(result.Contains("File.ReadAllBytes("));
        Assert.IsTrue(result.Contains("_Invoke"));
    }

    [TestMethod]
    public void AssemblyLoad_IsReplaced()
    {
        var source = """
            class C {
                void M(byte[] raw) { Assembly.Load(raw); }
            }
            """;
        var result = ApplyTransform(source);
        Assert.IsFalse(result.Contains("Assembly.Load("));
        Assert.IsTrue(result.Contains("_Invoke"));
    }

    [TestMethod]
    public void DynamicDependencyAttribute_IsEmitted()
    {
        var source = """
            class C {
                void M() { System.Diagnostics.Process.Start("cmd"); }
            }
            """;
        var result = ApplyTransform(source);
        Assert.IsTrue(result.Contains("DynamicDependency"));
    }

    [TestMethod]
    public void InstanceMethodCall_PassesReceiver()
    {
        // Use type-name receiver to match how
        // ExtractTypeName works (syntax-based matching)
        var source = """
            class C {
                void M() {
                    Socket.Connect("127.0.0.1", 80);
                }
            }
            """;
        var result = ApplyTransform(source);

        Assert.IsFalse(
            result.Contains("Socket.Connect("),
            "Instance call should be replaced");
        Assert.IsTrue(
            result.Contains("_Invoke"),
            "Should use indirect invocation");

        // For non-static APIs, the receiver expression
        // (Socket) should be passed as the instance arg,
        // not null
        Assert.IsTrue(
            result.Contains("Socket,"),
            "Instance receiver 'Socket' should be passed "
            + "as third argument to the indirect caller");
    }

    [TestMethod]
    public void StaticMethodCall_PassesNullInstance()
    {
        var source = """
            class C {
                void M() {
                    System.Diagnostics.Process.Start("cmd");
                }
            }
            """;
        var result = ApplyTransform(source);

        Assert.IsTrue(
            result.Contains("null,"),
            "Static calls should pass null as instance");
        Assert.IsTrue(
            result.Contains("_Invoke"),
            "Should use indirect invocation");
    }

    [TestMethod]
    public void DifferentSeeds_ProduceDifferentHelperNames()
    {
        var source = """
            class C {
                void M() { System.Diagnostics.Process.Start("cmd"); }
            }
            """;
        var t1 = new ApiCallHidingTransform("_Caller", "_Invoke1", "_Ns", 1);
        var t2 = new ApiCallHidingTransform("_Caller", "_Invoke2", "_Ns", 2);
        var tree = CSharpSyntaxTree.ParseText(source);
        var r1 = t1.Rewrite(tree).GetRoot().ToFullString();
        var r2 = t2.Rewrite(tree).GetRoot().ToFullString();
        Assert.AreNotEqual(r1, r2);
    }
}
