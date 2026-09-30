using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Tests;

[TestClass]
[DoNotParallelize]
public class ApiCallHidingTests
{
    private static readonly MetadataReference[] PlatformReferences =
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();

    private string ApplyTransform(string source, int seed = 42)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "ApiCallHidingSymbols",
            [tree],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var transform = new ApiCallHidingTransform(
            "_Caller", "_Invoke", "_Ns", seed);
        var result = transform.Rewrite(tree, compilation.GetSemanticModel(tree));
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
    public void LocalTypesNamedLikeSensitiveBclTypes_AreNotRewrittenAndExecute()
    {
        var source = """
            public static class Process
            {
                public static string Start(string value) => "local-process:" + value;
            }

            public static class File
            {
                public static byte[] ReadAllBytes(string value) => [
                    (byte)value.Length
                ];
            }

            public static class Subject
            {
                public static string Run() =>
                    Process.Start("ok") + ":" + File.ReadAllBytes("abc")[0];
            }
            """;

        var rewritten = ApplyTransform(source);

        Assert.IsFalse(rewritten.Contains("_Invoke"));
        Assert.AreEqual("local-process:ok:3", CompileAndRun(rewritten, "Subject"));
    }

    [TestMethod]
    public void FileReadAllBytes_IsReplaced()
    {
        var source = """
            class C {
                void M(string path) { var b = System.IO.File.ReadAllBytes(path); }
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
                void M(byte[] raw) { System.Reflection.Assembly.Load(raw); }
            }
            """;
        var result = ApplyTransform(source);
        Assert.IsFalse(result.Contains("Assembly.Load("));
        Assert.IsTrue(result.Contains("_Invoke"));
    }

    [TestMethod]
    public void TypedNullAssemblyLoadOverloads_AreSelectedInBothCallOrders()
    {
        var source = """
            public static class Subject
            {
                private static string Capture(System.Action action)
                {
                    try
                    {
                        action();
                        return "no-error";
                    }
                    catch (System.Reflection.TargetInvocationException ex)
                    {
                        return ((System.ArgumentNullException)ex.InnerException!).ParamName!;
                    }
                }

                public static string Run() =>
                    Capture(() => { System.Reflection.Assembly.Load((byte[])null!); })
                    + "," +
                    Capture(() => { System.Reflection.Assembly.Load((string)null!); })
                    + "|" +
                    Capture(() => { System.Reflection.Assembly.Load((string)null!); })
                    + "," +
                    Capture(() => { System.Reflection.Assembly.Load((byte[])null!); });
            }
            """;

        var rewritten = RuntimeCallerSource() + ApplyTransform(source);

        Assert.AreEqual(
            "rawAssembly,assemblyName|assemblyName,rawAssembly",
            CompileAndRun(rewritten, "Subject"));
    }

    [TestMethod]
    public void ByReferenceMappedCall_RemainsDirectAndWritesBack()
    {
        const string source = """
            public static class SensitiveFixture
            {
                public static void Mutate(ref int value) => value = 42;
            }

            public static class Subject
            {
                public static int Run()
                {
                    var value = 1;
                    SensitiveFixture.Mutate(ref value);
                    return value;
                }
            }
            """;

        var rewritten = ApplyTransformWithMappedFixture(
            source, "SensitiveFixture", "Mutate");
        var root = CSharpSyntaxTree.ParseText(rewritten).GetRoot();
        var directCall = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .Single(invocation => invocation.Expression is
                Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax access
                && access.Name.Identifier.ValueText == "Mutate");

        Assert.AreEqual(
            SyntaxKind.RefKeyword,
            directCall.ArgumentList.Arguments.Single().RefKindKeyword.Kind());
        Assert.IsFalse(root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .Any(invocation => invocation.Expression is
                Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax access
                && access.Name.Identifier.ValueText == "_Invoke"));
        Assert.AreEqual(42, CompileAndRun(rewritten, "Subject"));
    }

    [TestMethod]
    public void ExpandedParams_PreserveEveryCallShapeAcrossCachedOverloads()
    {
        const string source = """
            public static class ParamsFixture
            {
                public static string Describe(params string?[] values) =>
                    values is null
                        ? "array:null"
                        : values.Length + ":" + string.Join(",",
                            System.Array.ConvertAll(values,
                                value => value ?? "<null>"));

                public static string Describe(int tag, string? value) =>
                    "tag:" + tag + ":" + (value ?? "<null>");
            }

            public static class Subject
            {
                public static string Run() => string.Join("|",
                    ParamsFixture.Describe((string?[]?)null),
                    ParamsFixture.Describe(new string?[] { "array" }),
                    ParamsFixture.Describe(),
                    ParamsFixture.Describe((string?)null),
                    ParamsFixture.Describe("a", "b"),
                    ParamsFixture.Describe(7, (string?)null),
                    ParamsFixture.Describe((string?)null));
            }
            """;

        var sourceTree = CSharpSyntaxTree.ParseText(source);
        var sourceCompilation = CSharpCompilation.Create(
            "ParamsFixtureSymbols", [sourceTree], PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var sourceErrors = sourceCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.AreEqual(0, sourceErrors.Length,
            string.Join(Environment.NewLine, sourceErrors.Select(error => error.ToString())));
        var fixtureCalls = sourceTree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression is
                Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax access
                && access.Name.Identifier.ValueText == "Describe")
            .ToArray();
        var fixtureModel = sourceCompilation.GetSemanticModel(sourceTree);
        Assert.AreEqual(7, fixtureCalls.Count(call =>
            fixtureModel.GetSymbolInfo(call).Symbol is IMethodSymbol));

        var transformedSource = ApplyTransformWithMappedFixture(
            source, "ParamsFixture", "Describe");
        var transformedRoot = CSharpSyntaxTree.ParseText(transformedSource).GetRoot();
        var indirectCalls = transformedRoot.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression is
                Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax access
                && access.Name.Identifier.ValueText == "_Invoke")
            .ToArray();
        Assert.AreEqual(7, indirectCalls.Length, transformedSource);
        Assert.IsTrue(
            indirectCalls.All(call => call.ArgumentList.Arguments.Count == 8),
            "Every indirect call must carry expanded-argument metadata.");
        var rewritten = RuntimeCallerSource() + transformedSource;

        Assert.AreEqual(
            "array:null|1:array|0:|1:<null>|2:a,b|tag:7:<null>|1:<null>",
            CompileAndRun(rewritten, "Subject"));
    }

    [TestMethod]
    public void RuntimeCaller_DoesNotUseDynamicTypeGetType()
    {
        var runtimeSource = RuntimeCallerSource();

        Assert.IsFalse(runtimeSource.Contains("Type.GetType("));
    }

    [TestMethod]
    public void RuntimeCaller_ResolvesTypesFromLoadedFrameworkAssemblies()
    {
        Assembly.Load("System.Diagnostics.Process");
        var source = RuntimeCallerSource() + """
            public static class Subject
            {
                public static string Run()
                {
                    var value = _Ns._Caller._Invoke(
                        "System.Diagnostics.Process",
                        "GetCurrentProcess",
                        null,
                        System.Array.Empty<System.Type>(),
                        System.Array.Empty<object?>());
                    var typeName = value!.GetType().FullName!;
                    ((System.IDisposable)value).Dispose();
                    return typeName;
                }
            }
            """;

        Assert.AreEqual(
            "System.Diagnostics.Process",
            CompileAndRun(source, "Subject"));
    }

    [TestMethod]
    public void RuntimeCaller_FailsClearlyWhenLoadedTypeIsMissing()
    {
        var source = RuntimeCallerSource() + """
            public static class Subject
            {
                public static object? Run() => _Ns._Caller._Invoke(
                    "Missing.Namespace.Type",
                    "Call",
                    null,
                    System.Array.Empty<System.Type>(),
                    System.Array.Empty<object?>());
            }
            """;

        var error = Assert.ThrowsExactly<TargetInvocationException>(() =>
            CompileAndRun(source, "Subject"));
        Assert.IsInstanceOfType<TypeLoadException>(error.InnerException);
        StringAssert.Contains(
            error.InnerException.Message,
            "Could not resolve loaded type 'Missing.Namespace.Type'.");
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
        var source = """
            class C {
                void M(System.Net.Sockets.Socket socket) {
                    socket.Connect("127.0.0.1", 80);
                }
            }
            """;
        var result = ApplyTransform(source);

        Assert.IsFalse(
            result.Contains("socket.Connect("),
            "Instance call should be replaced");
        Assert.IsTrue(
            result.Contains("_Invoke"),
            "Should use indirect invocation");

        // For non-static APIs, the receiver expression
        // should be passed as the instance arg,
        // not null
        Assert.IsTrue(
            result.Contains("socket,"),
            "Instance receiver 'socket' should be passed "
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
        var compilation = CSharpCompilation.Create(
            "ApiCallHidingSeeds", [tree], PlatformReferences);
        var semanticModel = compilation.GetSemanticModel(tree);
        var r1 = t1.Rewrite(tree, semanticModel).GetRoot().ToFullString();
        var r2 = t2.Rewrite(tree, semanticModel).GetRoot().ToFullString();
        Assert.AreNotEqual(r1, r2);
    }

    private static object? CompileAndRun(string source, string typeName)
    {
        var compilation = CSharpCompilation.Create(
            "ApiCallHidingRegression_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.IsTrue(result.Success,
            string.Join(Environment.NewLine, result.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        return assembly.GetType(typeName)!
            .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null);
    }

    private string ApplyTransformWithMappedFixture(
        string source, string typeName, string methodName)
    {
        var field = typeof(ApiCallHidingTransform).GetField(
            "SensitiveApis", BindingFlags.NonPublic | BindingFlags.Static)!;
        var mappedApis = (HashSet<(string Type, string Method)>)field.GetValue(null)!;
        var mapping = (typeName, methodName);
        mappedApis.Add(mapping);
        try
        {
            return ApplyTransform(source);
        }
        finally
        {
            mappedApis.Remove(mapping);
        }
    }

    private static string RuntimeCallerSource()
    {
        var assembly = typeof(ApiCallHidingTransform).Assembly;
        using var stream = assembly.GetManifestResourceStream("IndirectCaller.cs")!;
        using var reader = new StreamReader(stream);
        return "using System;\nusing System.Linq;\n" + reader.ReadToEnd()
            .Replace("__OBFS_NS__", "_Ns")
            .Replace("__OBFS_CALLER_CLASS__", "_Caller")
            .Replace("__OBFS_INVOKE_METHOD__", "_Invoke");
    }
}
