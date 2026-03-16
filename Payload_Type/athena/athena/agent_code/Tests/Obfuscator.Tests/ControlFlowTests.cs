using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.IL.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class ControlFlowTests
{
    private const string ComputeSource = """
        public class TestClass
        {
            public static int Compute(int x)
            {
                int result = 0;
                if (x > 10)
                    result = x * 2;
                else if (x > 5)
                    result = x + 10;
                else if (x > 0)
                    result = x - 1;
                else
                    result = -x;

                for (int i = 0; i < 3; i++)
                    result += i;

                return result;
            }
        }
        """;

    private const string SmallMethodSource = """
        public class SmallClass
        {
            public static int Add(int a, int b)
            {
                return a + b;
            }
        }
        """;

    private const string PropertySource = """
        public class PropClass
        {
            private static int _val = 42;
            public static int Value
            {
                get { return _val; }
                set { _val = value; }
            }

            public static int Compute(int x)
            {
                int result = 0;
                if (x > 10)
                    result = x * 2;
                else if (x > 5)
                    result = x + 10;
                else if (x > 0)
                    result = x - 1;
                else
                    result = -x;

                for (int i = 0; i < 3; i++)
                    result += i;

                return result;
            }
        }
        """;

    [TestMethod]
    public void FlattenedMethod_ProducesSameOutput()
    {
        var dll = CompileToDll(ComputeSource);
        var transform = new ControlFlowTransform(seed: 42);
        var transformed = transform.Transform(dll);

        // Invoke original
        var origResult = InvokeCompute(dll, 15);
        var flatResult = InvokeCompute(transformed, 15);
        Assert.AreEqual(origResult, flatResult);

        // Test multiple inputs
        foreach (var input in new[] { -5, 0, 3, 7, 15, 100 })
        {
            var orig = InvokeCompute(dll, input);
            var flat = InvokeCompute(transformed, input);
            Assert.AreEqual(
                orig, flat,
                $"Mismatch for input {input}");
        }
    }

    [TestMethod]
    public void SmallMethod_IsSkipped()
    {
        var dll = CompileToDll(SmallMethodSource);
        var transform = new ControlFlowTransform(seed: 42);
        var transformed = transform.Transform(dll);

        var result = InvokeMethod<int>(
            transformed, "SmallClass", "Add",
            new object[] { 3, 4 });
        Assert.AreEqual(7, result);
    }

    [TestMethod]
    public void PropertyGetter_IsSkipped()
    {
        var dll = CompileToDll(PropertySource);
        var transform = new ControlFlowTransform(seed: 42);
        var transformed = transform.Transform(dll);

        // Property getter should still work
        var val = InvokeMethod<int>(
            transformed, "PropClass", "get_Value",
            Array.Empty<object>());
        Assert.AreEqual(42, val);
    }

    [TestMethod]
    public void DifferentSeeds_ProduceDifferentIL()
    {
        var dll = CompileToDll(ComputeSource);
        var t1 = new ControlFlowTransform(seed: 1);
        var t2 = new ControlFlowTransform(seed: 2);

        var result1 = t1.Transform(dll);
        var result2 = t2.Transform(dll);

        // Both should produce correct results
        Assert.AreEqual(
            InvokeCompute(result1, 15),
            InvokeCompute(result2, 15));

        // But the bytes should differ (different shuffle order)
        Assert.IsFalse(
            result1.AsSpan().SequenceEqual(result2),
            "Different seeds should produce different IL");
    }

    private static int InvokeCompute(
        byte[] asmBytes, int input)
    {
        return InvokeMethod<int>(
            asmBytes, "TestClass", "Compute",
            new object[] { input });
    }

    private static T InvokeMethod<T>(
        byte[] asmBytes,
        string typeName,
        string methodName,
        object[] args)
    {
        var alc = new AssemblyLoadContext(
            $"Test_{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(
                new MemoryStream(asmBytes));
            var type = asm.GetType(typeName)
                ?? throw new InvalidOperationException(
                    $"Type '{typeName}' not found");
            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static
                    | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    $"Method '{methodName}' not found");
            var result = method.Invoke(null, args);
            return (T)result!;
        }
        finally
        {
            alc.Unload();
        }
    }

    private const string AsyncPluginSource = """
        using System.Threading.Tasks;
        public class Plugin
        {
            public string Name => "whoami";

            public async Task Execute(string input)
            {
                var msg = $"Executing {Name} [{input}]";
                System.Console.WriteLine(msg);
                var result =
                    $"{System.Environment.UserDomainName}"
                    + "\\"
                    + System.Environment.UserName;
                System.Console.WriteLine(
                    $"{Name} completed [{input}]");
            }
        }
        """;

    private const string TryCatchSource = """
        public class TryCatchClass
        {
            public static int Compute(int x)
            {
                int result = 0;
                try
                {
                    if (x > 10)
                        result = x * 2;
                    else if (x > 5)
                        result = x + 10;
                    else if (x > 0)
                        result = x - 1;
                    else
                        result = -x;
                }
                catch (System.Exception)
                {
                    result = -1;
                }
                return result;
            }
        }
        """;

    [TestMethod]
    public void AsyncPlugin_ControlFlowTransform_DoesNotCorruptIL()
    {
        var dll = CompileToDll(AsyncPluginSource, refs: AsyncRefs);
        var transform = new ControlFlowTransform(seed: 42);
        var transformed = transform.Transform(dll);

        var alc = new AssemblyLoadContext(
            $"Test_{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(
                new MemoryStream(transformed));
            var type = asm.GetType("Plugin")!;
            var instance = Activator.CreateInstance(type)!;
            var method = type.GetMethod("Execute")!;
            var task = (Task)method.Invoke(
                instance, new object[] { "test" })!;
            task.Wait();
        }
        finally
        {
            alc.Unload();
        }
    }

    [TestMethod]
    public void TryCatch_ControlFlowTransform_DoesNotCorruptIL()
    {
        var dll = CompileToDll(TryCatchSource);
        var transform = new ControlFlowTransform(seed: 42);
        var transformed = transform.Transform(dll);

        foreach (var input in new[] { -5, 0, 3, 7, 15 })
        {
            var orig = InvokeMethod<int>(
                dll, "TryCatchClass", "Compute",
                new object[] { input });
            var flat = InvokeMethod<int>(
                transformed, "TryCatchClass", "Compute",
                new object[] { input });
            Assert.AreEqual(
                orig, flat,
                $"Mismatch for input {input}");
        }
    }

    private const string TernarySource = """
        public class TernaryClass
        {
            public static int Compute(int x)
            {
                int a = x > 0 ? x * 2 : x + 1;
                int b = a > 10 ? a - 5 : a + 5;
                return a + b;
            }
        }
        """;

    private const string NullCoalesceSource = """
        public class NullCoalesceClass
        {
            public static string Compute(string a, string b)
            {
                string r = a ?? b ?? "default";
                return r + (a ?? "none");
            }
        }
        """;

    [TestMethod]
    public void Ternary_ControlFlowTransform_DoesNotCorruptIL()
    {
        var dll = CompileToDll(TernarySource);
        var transform = new ControlFlowTransform(seed: 42);
        var transformed = transform.Transform(dll);

        foreach (var input in new[] { -5, 0, 3, 7, 15 })
        {
            var orig = InvokeMethod<int>(
                dll, "TernaryClass", "Compute",
                new object[] { input });
            var flat = InvokeMethod<int>(
                transformed, "TernaryClass", "Compute",
                new object[] { input });
            Assert.AreEqual(
                orig, flat,
                $"Mismatch for input {input}");
        }
    }

    [TestMethod]
    public void NullCoalesce_ControlFlowTransform_DoesNotCorruptIL()
    {
        var dll = CompileToDll(NullCoalesceSource);
        var transform = new ControlFlowTransform(seed: 42);
        var transformed = transform.Transform(dll);

        var cases = new[]
        {
            new object[] { "hello", "world" },
            new object[] { null!, "fallback" },
            new object[] { null!, null! },
        };

        foreach (var args in cases)
        {
            var orig = InvokeMethod<string>(
                dll, "NullCoalesceClass", "Compute", args);
            var flat = InvokeMethod<string>(
                transformed, "NullCoalesceClass", "Compute",
                args);
            Assert.AreEqual(
                orig, flat,
                $"Mismatch for args ({args[0]}, {args[1]})");
        }
    }

    private static readonly MetadataReference[] AsyncRefs = BuildAsyncRefs();

    private static MetadataReference[] BuildAsyncRefs()
    {
        var trustedDir = Path.GetDirectoryName(
            typeof(object).Assembly.Location)!;
        return
        [
            MetadataReference.CreateFromFile(
                typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(
                Path.Combine(trustedDir,
                    "System.Collections.dll")),
            MetadataReference.CreateFromFile(
                typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(
                Path.Combine(trustedDir,
                    "System.Threading.dll")),
        ];
    }

    private static byte[] CompileToDll(
        string source,
        string assemblyName = "TestAsm",
        MetadataReference[]? refs = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var trustedDir = Path.GetDirectoryName(
            typeof(object).Assembly.Location)!;

        var references = refs ??
        [
            MetadataReference.CreateFromFile(
                typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(
                Path.Combine(trustedDir,
                    "System.Collections.dll")),
        ];

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity
                    == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            throw new InvalidOperationException(
                "Compilation failed:\n"
                + string.Join("\n", errors));
        }
        return ms.ToArray();
    }
}
