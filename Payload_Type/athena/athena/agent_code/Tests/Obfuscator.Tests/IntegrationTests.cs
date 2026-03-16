using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Config;
using Obfuscator.IL.Transforms;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class IntegrationTests
{
    private const string CalculatorSource = """
        public class Calculator
        {
            public static int Add(int a, int b)
            {
                string msg = "adding values";
                int result = 0;
                if (a > 0)
                    result = a + b;
                else if (a == 0)
                    result = b;
                else
                    result = a + b;
                for (int i = 0; i < 1; i++)
                    result += 0;
                return result;
            }
        }
        """;

    [TestMethod]
    public void FullPipeline_TransformedCode_StillExecutes()
    {
        const int seed = 42;
        var (decNs, decClass, decMethod) =
            ("DecNs", "DecClass", "DecMethod");

        // 1. Apply string encryption source transform
        var tree = CSharpSyntaxTree.ParseText(CalculatorSource);
        var strTransform = new StringEncryptionTransform(
            decClass, decMethod, decNs, seed);
        tree = strTransform.Rewrite(tree);

        // 2. Add a matching decryptor implementation
        var decryptorSource = BuildDecryptorSource(
            decNs, decClass, decMethod);
        var decryptorTree = CSharpSyntaxTree.ParseText(
            decryptorSource);

        // 3. Compile both trees
        var dllBytes = CompileToBytes(
            [tree, decryptorTree], "IntegCalc");

        // 4. Apply IL transforms
        var mmt = new MetadataManglingTransform(seed);
        dllBytes = mmt.Transform(dllBytes);

        // 5. Load and invoke via reflection
        var result = InvokeStaticBySignature<int>(
            dllBytes, [typeof(int), typeof(int)],
            [3, 4]);
        Assert.AreEqual(7, result);
    }

    [TestMethod]
    public void FullPipeline_MultipleInputs_AllCorrect()
    {
        const int seed = 99;
        var (decNs, decClass, decMethod) =
            ("DN", "DC", "DM");

        var tree = CSharpSyntaxTree.ParseText(CalculatorSource);
        var strTransform = new StringEncryptionTransform(
            decClass, decMethod, decNs, seed);
        tree = strTransform.Rewrite(tree);

        var decryptorTree = CSharpSyntaxTree.ParseText(
            BuildDecryptorSource(decNs, decClass, decMethod));
        var dllBytes = CompileToBytes(
            [tree, decryptorTree], "IntegMulti");

        var mmt = new MetadataManglingTransform(seed);
        dllBytes = mmt.Transform(dllBytes);

        int[][] cases =
        [
            [3, 4, 7],
            [0, 5, 5],
            [-2, 3, 1],
            [10, 20, 30],
            [-5, -3, -8],
        ];

        foreach (var c in cases)
        {
            var result = InvokeStaticBySignature<int>(
                dllBytes, [typeof(int), typeof(int)],
                [c[0], c[1]]);
            Assert.AreEqual(
                c[2], result,
                $"Add({c[0]}, {c[1]}) expected {c[2]}");
        }
    }

    [TestMethod]
    public void DifferentSeeds_ProduceDifferentOutput()
    {
        var bytes1 = BuildFullPipeline(CalculatorSource, seed: 1);
        var bytes2 = BuildFullPipeline(CalculatorSource, seed: 2);

        // Both must produce correct results
        var r1 = InvokeStaticBySignature<int>(
            bytes1, [typeof(int), typeof(int)], [3, 4]);
        var r2 = InvokeStaticBySignature<int>(
            bytes2, [typeof(int), typeof(int)], [3, 4]);
        Assert.AreEqual(7, r1);
        Assert.AreEqual(7, r2);

        // But the bytes must differ
        Assert.IsFalse(
            bytes1.AsSpan().SequenceEqual(bytes2),
            "Different seeds should produce different bytes");
    }

    [TestMethod]
    public void SameUuid_ProducesSameInterfaceNames()
    {
        var map1 = UuidRenameMap.Derive("test-uuid-1");
        var map2 = UuidRenameMap.Derive("test-uuid-1");

        Assert.AreEqual(
            map1.GetRenamed("IModule"),
            map2.GetRenamed("IModule"));
        Assert.AreEqual(
            map1.GetRenamed("IChannel"),
            map2.GetRenamed("IChannel"));
        Assert.AreEqual(
            map1.GetRenamed("Execute"),
            map2.GetRenamed("Execute"));
    }

    [TestMethod]
    public void DifferentUuid_ProducesDifferentInterfaceNames()
    {
        var map1 = UuidRenameMap.Derive("uuid-agent-1");
        var map2 = UuidRenameMap.Derive("uuid-agent-2");

        Assert.AreNotEqual(
            map1.GetRenamed("IModule"),
            map2.GetRenamed("IModule"));
        Assert.AreNotEqual(
            map1.GetRenamed("IChannel"),
            map2.GetRenamed("IChannel"));
    }

    [TestMethod]
    public void SameUuid_DifferentSeeds_BothWork()
    {
        const string uuid = "shared-agent-uuid";
        var uuidMap = UuidRenameMap.Derive(uuid);

        // Source with a fake interface reference that will be
        // renamed by UuidRenameTransform. We use a simple class
        // to verify the pipeline still produces working code
        // even when UUID rename is part of the chain.
        var bytes1 = BuildFullPipelineWithUuid(
            CalculatorSource, seed: 10, uuid: uuid);
        var bytes2 = BuildFullPipelineWithUuid(
            CalculatorSource, seed: 20, uuid: uuid);

        var r1 = InvokeStaticBySignature<int>(
            bytes1, [typeof(int), typeof(int)], [5, 6]);
        var r2 = InvokeStaticBySignature<int>(
            bytes2, [typeof(int), typeof(int)], [5, 6]);

        Assert.AreEqual(11, r1);
        Assert.AreEqual(11, r2);

        // Bytes should differ (different seeds)
        Assert.IsFalse(
            bytes1.AsSpan().SequenceEqual(bytes2),
            "Same UUID but different seeds should produce "
            + "different bytes");
    }

    [TestMethod]
    public void MetadataMangling_RenamesTypes()
    {
        var dllBytes = CompileToBytes(
            [CSharpSyntaxTree.ParseText(CalculatorSource)],
            "MetaTest");

        var mmt = new MetadataManglingTransform(42);
        var transformed = mmt.Transform(dllBytes);

        var alc = new AssemblyLoadContext(
            $"Test_{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(
                new MemoryStream(transformed));
            var types = asm.GetTypes()
                .Where(t => t.Name != "<Module>")
                .ToList();

            // Original type name "Calculator" should not exist
            Assert.IsFalse(
                types.Any(t => t.Name == "Calculator"),
                "Type should be renamed after metadata mangling");

            // But we should still be able to invoke by signature
            var result = InvokeStaticBySignature<int>(
                transformed,
                [typeof(int), typeof(int)],
                [3, 4]);
            Assert.AreEqual(7, result);
        }
        finally
        {
            alc.Unload();
        }
    }

    // --- Helpers ---

    private static byte[] BuildFullPipeline(
        string source, int seed)
    {
        var (decNs, decClass, decMethod) =
            ($"N{seed}", $"C{seed}", $"M{seed}");

        var tree = CSharpSyntaxTree.ParseText(source);
        var strTransform = new StringEncryptionTransform(
            decClass, decMethod, decNs, seed);
        tree = strTransform.Rewrite(tree);

        var decryptorTree = CSharpSyntaxTree.ParseText(
            BuildDecryptorSource(decNs, decClass, decMethod));
        var dllBytes = CompileToBytes(
            [tree, decryptorTree], $"Asm{seed}");

        var mmt = new MetadataManglingTransform(seed);
        dllBytes = mmt.Transform(dllBytes);

        return dllBytes;
    }

    private static byte[] BuildFullPipelineWithUuid(
        string source, int seed, string uuid)
    {
        var (decNs, decClass, decMethod) =
            ($"N{seed}", $"C{seed}", $"M{seed}");
        var uuidMap = UuidRenameMap.Derive(uuid);

        var tree = CSharpSyntaxTree.ParseText(source);

        var uuidTransform = new UuidRenameTransform(uuidMap);
        tree = uuidTransform.Rewrite(tree);

        var strTransform = new StringEncryptionTransform(
            decClass, decMethod, decNs, seed);
        tree = strTransform.Rewrite(tree);

        var decryptorTree = CSharpSyntaxTree.ParseText(
            BuildDecryptorSource(decNs, decClass, decMethod));
        var dllBytes = CompileToBytes(
            [tree, decryptorTree], $"Uuid{seed}");

        var mmt = new MetadataManglingTransform(seed);
        dllBytes = mmt.Transform(dllBytes);

        return dllBytes;
    }

    private static string BuildDecryptorSource(
        string ns, string className, string methodName)
    {
        return $$"""
            namespace {{ns}}
            {
                internal static class {{className}}
                {
                    internal static string {{methodName}}(
                        byte[] data, byte key)
                    {
                        byte[] r = new byte[data.Length];
                        for (int i = 0; i < data.Length; i++)
                            r[i] = (byte)(data[i] ^ key);
                        return System.Text.Encoding.UTF8.GetString(r);
                    }
                }
            }
            """;
    }

    private static T InvokeStaticBySignature<T>(
        byte[] asmBytes,
        Type[] paramTypes,
        object[] args)
    {
        var alc = new AssemblyLoadContext(
            $"Test_{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(
                new MemoryStream(asmBytes));

            // Find a method matching the parameter signature,
            // since names are mangled after metadata transforms.
            foreach (var type in asm.GetTypes())
            {
                foreach (var method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static
                    | BindingFlags.NonPublic))
                {
                    var ps = method.GetParameters();
                    if (ps.Length != paramTypes.Length)
                        continue;
                    var match = true;
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (ps[i].ParameterType != paramTypes[i])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (!match)
                        continue;
                    if (method.ReturnType != typeof(T))
                        continue;

                    var result = method.Invoke(null, args);
                    return (T)result!;
                }
            }

            throw new InvalidOperationException(
                "No method found matching the given signature");
        }
        finally
        {
            alc.Unload();
        }
    }

    private static byte[] CompileToBytes(
        SyntaxTree[] trees, string assemblyName = "TestAsm")
    {
        var trustedDir = Path.GetDirectoryName(
            typeof(object).Assembly.Location)!;

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(
                typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(
                Path.Combine(trustedDir,
                    "System.Collections.dll")),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName,
            trees,
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
