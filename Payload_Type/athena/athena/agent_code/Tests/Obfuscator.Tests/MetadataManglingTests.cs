using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Obfuscator.IL.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class MetadataManglingTests
{
    private const string SimpleClassSource = """
        public class MyClass
        {
            private int _value;

            public MyClass(int value) { _value = value; }

            public static int Compute(int x) { return x * 3; }
        }
        """;

    private const string DllImportSource = """
        using System.Runtime.InteropServices;
        public class NativeHelper
        {
            [DllImport("kernel32")]
            public static extern int GetCurrentProcessId();

            public static int Compute(int x) { return x * 2; }
        }
        """;

    private const string CtorSource = """
        public class MyObject
        {
            private static int _count = 0;

            static MyObject() { _count = 1; }

            public MyObject() { _count++; }

            public static int GetCount() { return _count; }
        }
        """;

    [TestMethod]
    public void TypesAndMethods_AreRenamed()
    {
        var dll = CompileToDll(SimpleClassSource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        foreach (var type in asm.MainModule.Types)
        {
            if (type.Name == "<Module>")
                continue;

            Assert.IsTrue(
                type.Name.StartsWith("_"),
                $"Type '{type.Name}' should start with '_'");

            foreach (var method in type.Methods)
            {
                if (method.IsConstructor)
                    continue;

                Assert.IsTrue(
                    method.Name.StartsWith("_"),
                    $"Method '{method.Name}' should start with '_'");
            }
        }
    }

    [TestMethod]
    public void DllImportMethod_IsPreserved()
    {
        var dll = CompileToDll(DllImportSource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        MethodDefinition? preserved = null;
        MethodDefinition? renamed = null;

        foreach (var type in asm.MainModule.Types)
        {
            foreach (var method in type.Methods)
            {
                if (method.IsPInvokeImpl)
                    preserved = method;
                else if (!method.IsConstructor && method.Name.StartsWith("_"))
                    renamed = method;
            }
        }

        Assert.IsNotNull(preserved, "Should find the P/Invoke method");
        Assert.AreEqual(
            "GetCurrentProcessId", preserved.Name,
            "P/Invoke method name must be preserved");

        Assert.IsNotNull(renamed, "Should find at least one renamed method");
    }

    [TestMethod]
    public void ConstructorNames_ArePreserved()
    {
        var dll = CompileToDll(CtorSource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        bool foundCtor = false;
        bool foundCctor = false;

        foreach (var type in asm.MainModule.Types)
        {
            foreach (var method in type.Methods)
            {
                if (method.Name == ".ctor")
                    foundCtor = true;
                if (method.Name == ".cctor")
                    foundCctor = true;
            }
        }

        Assert.IsTrue(foundCtor, ".ctor must be preserved");
        Assert.IsTrue(foundCctor, ".cctor must be preserved");
    }

    [TestMethod]
    public void TransformedAssembly_StillExecutes()
    {
        var dll = CompileToDll(SimpleClassSource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        var alc = new AssemblyLoadContext(
            $"MetaMangle_{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(new MemoryStream(transformed));

            // Find any non-<Module> type whose name starts with _
            var type = asm.GetTypes()
                .FirstOrDefault(t => t.Name.StartsWith("_"));

            Assert.IsNotNull(type, "Should find a renamed type");

            // Find a static method
            var method = type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static
                    | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name.StartsWith("_"));

            Assert.IsNotNull(method, "Should find a renamed static method");

            var result = method.Invoke(null, new object[] { 5 });
            Assert.AreEqual(15, result, "Compute(5) should return 15");
        }
        finally
        {
            alc.Unload();
        }
    }

    [TestMethod]
    public void DifferentSeeds_ProduceDifferentNames()
    {
        var dll = CompileToDll(SimpleClassSource);

        var t1 = new MetadataManglingTransform(seed: 1);
        var t2 = new MetadataManglingTransform(seed: 2);

        var result1 = t1.Transform(dll);
        var result2 = t2.Transform(dll);

        Assert.IsFalse(
            result1.AsSpan().SequenceEqual(result2),
            "Different seeds must produce different output bytes");
    }

    [TestMethod]
    public void NamesStartWithUnderscore()
    {
        var dll = CompileToDll(SimpleClassSource);
        var transform = new MetadataManglingTransform(seed: 99);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        foreach (var type in asm.MainModule.Types)
        {
            if (type.Name == "<Module>")
                continue;

            Assert.IsTrue(
                type.Name.StartsWith("_"),
                $"Type '{type.Name}' must start with '_'");

            foreach (var method in type.Methods)
            {
                if (method.IsConstructor || method.IsPInvokeImpl)
                    continue;

                Assert.IsTrue(
                    method.Name.StartsWith("_"),
                    $"Method '{method.Name}' must start with '_'");
            }

            foreach (var field in type.Fields)
            {
                Assert.IsTrue(
                    field.Name.StartsWith("_"),
                    $"Field '{field.Name}' must start with '_'");
            }
        }
    }

    [TestMethod]
    public void GetRenameMappings_ReturnsNonEmptyAfterTransform()
    {
        var dll = CompileToDll(SimpleClassSource);
        var transform = new MetadataManglingTransform(seed: 42);
        transform.Transform(dll);

        var mappings = transform.GetRenameMappings();
        Assert.IsTrue(
            mappings.Count > 0,
            "Rename mappings should be populated after Transform");

        foreach (var (original, renamed) in mappings)
        {
            Assert.IsTrue(
                renamed.StartsWith("_"),
                $"Renamed value '{renamed}' for '{original}' should start with '_'");
        }
    }

    private const string JsonArgsSource = """
        using System.Text.Json;
        public class PluginArgs
        {
            public string path { get; set; }
            public int count { get; set; }
            public bool recursive { get; set; }
        }
        public class ArgsConsumer
        {
            public static string Roundtrip(string json)
            {
                var args = JsonSerializer.Deserialize<PluginArgs>(json);
                return JsonSerializer.Serialize(args);
            }
        }
        """;

    [TestMethod]
    public void PropertyNames_ArePreservedForSerialization()
    {
        var dll = CompileToDll(JsonArgsSource, "TestAsm", JsonRefs);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        foreach (var type in asm.MainModule.Types)
        {
            if (type.Name == "<Module>")
                continue;
            foreach (var prop in type.Properties)
            {
                Assert.IsFalse(
                    prop.Name.StartsWith("_"),
                    $"Property '{prop.Name}' should NOT be "
                    + "renamed (breaks JSON serialization)");
            }
        }
    }

    [TestMethod]
    public void JsonDeserialization_WorksAfterTransform()
    {
        var dll = CompileToDll(JsonArgsSource, "TestAsm", JsonRefs);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        var json = "{\"path\":\"/tmp\",\"count\":5,"
            + "\"recursive\":true}";
        var result = InvokeMethod<string>(
            transformed, json);
        Assert.IsTrue(
            result.Contains("\"path\":\"/tmp\""),
            $"Roundtrip should preserve 'path': {result}");
        Assert.IsTrue(
            result.Contains("\"count\":5"),
            $"Roundtrip should preserve 'count': {result}");
    }

    private static T InvokeMethod<T>(
        byte[] asmBytes, params object[] args)
    {
        var paramTypes = args.Select(a => a.GetType()).ToArray();
        var alc = new AssemblyLoadContext(
            $"Test_{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(
                new MemoryStream(asmBytes));

            foreach (var type in asm.GetTypes())
            {
                foreach (var method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance))
                {
                    if (method.ReturnType != typeof(T))
                        continue;
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

    private static readonly MetadataReference[] JsonRefs =
        BuildJsonRefs();

    private static MetadataReference[] BuildJsonRefs()
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
                typeof(System.Text.Json.JsonSerializer)
                    .Assembly.Location),
            MetadataReference.CreateFromFile(
                Path.Combine(trustedDir,
                    "System.Text.Encodings.Web.dll")),
        ];
    }

    private const string ContractsSource = """
        namespace Contracts
        {
            public interface IPlugin
            {
                string Name { get; }
                int Execute(int input);
            }
        }
        """;

    private const string PluginSource = """
        using Contracts;
        namespace MyPlugin
        {
            public class Plugin : IPlugin
            {
                public string Name => "test";

                public int Execute(int input)
                {
                    return input * 2;
                }

                public int InternalHelper(int x)
                {
                    return x + 1;
                }
            }
        }
        """;

    [TestMethod]
    public void ExternalInterfaceMethod_PreservedWithSearchDir()
    {
        var (contractsDll, contractsPath) =
            CompileToDllOnDisk(ContractsSource, "Contracts");
        try
        {
            var pluginDll = CompileToDll(
                PluginSource, "MyPlugin",
                [MetadataReference.CreateFromFile(contractsPath)]);

            var searchDir = Path.GetDirectoryName(contractsPath)!;
            var transform = new MetadataManglingTransform(seed: 42);
            var transformed = transform.Transform(
                pluginDll, searchDir);

            using var ms = new MemoryStream(transformed);
            var asm = AssemblyDefinition.ReadAssembly(ms);

            var methods = asm.MainModule.Types
                .Where(t => t.Name != "<Module>")
                .SelectMany(t => t.Methods)
                .Where(m => !m.IsConstructor)
                .ToList();

            Assert.IsTrue(
                methods.Any(m => m.Name == "Execute"),
                "Interface method 'Execute' must be preserved");
            Assert.IsTrue(
                methods.Any(m => m.Name == "get_Name"),
                "Interface property getter 'get_Name' must be "
                + "preserved");
            Assert.IsTrue(
                methods.Any(m => m.Name.StartsWith("_")),
                "Non-interface method 'InternalHelper' should "
                + "be renamed");
            Assert.IsFalse(
                methods.Any(m => m.Name == "InternalHelper"),
                "Non-interface method should not keep its "
                + "original name");
        }
        finally
        {
            TryDeleteDirectory(
                Path.GetDirectoryName(contractsPath)!);
        }
    }

    [TestMethod]
    public void ExternalInterfaceMethod_PreservedWithoutSearchDir()
    {
        var (contractsDll, contractsPath) =
            CompileToDllOnDisk(ContractsSource, "Contracts");
        try
        {
            var pluginDll = CompileToDll(
                PluginSource, "MyPlugin",
                [MetadataReference.CreateFromFile(contractsPath)]);

            var transform = new MetadataManglingTransform(seed: 42);
            var transformed = transform.Transform(pluginDll);

            using var ms = new MemoryStream(transformed);
            var asm = AssemblyDefinition.ReadAssembly(ms);

            var methods = asm.MainModule.Types
                .Where(t => t.Name != "<Module>")
                .SelectMany(t => t.Methods)
                .Where(m => !m.IsConstructor)
                .ToList();

            Assert.IsTrue(
                methods.Any(m => m.Name == "Execute"),
                "Interface method 'Execute' must be preserved "
                + "even without resolver (fallback path)");
        }
        finally
        {
            TryDeleteDirectory(
                Path.GetDirectoryName(contractsPath)!);
        }
    }

    [TestMethod]
    public void ExternalInterface_TransformDoesNotThrow()
    {
        var (contractsDll, contractsPath) =
            CompileToDllOnDisk(ContractsSource, "Contracts");
        try
        {
            var pluginDll = CompileToDll(
                PluginSource, "MyPlugin",
                [MetadataReference.CreateFromFile(contractsPath)]);

            var transform = new MetadataManglingTransform(seed: 42);
            transform.Transform(pluginDll);
        }
        finally
        {
            TryDeleteDirectory(
                Path.GetDirectoryName(contractsPath)!);
        }
    }

    private static (byte[] bytes, string path) CompileToDllOnDisk(
        string source, string assemblyName)
    {
        var bytes = CompileToDll(source, assemblyName);
        var dir = Path.Combine(
            Path.GetTempPath(),
            $"obftest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{assemblyName}.dll");
        File.WriteAllBytes(path, bytes);
        return (bytes, path);
    }

    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private static byte[] CompileToDll(
        string source,
        string assemblyName,
        MetadataReference[]? extraRefs)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var trustedDir = Path.GetDirectoryName(
            typeof(object).Assembly.Location)!;

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(
                typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(
                Path.Combine(trustedDir, "System.Collections.dll")),
        };

        if (extraRefs is not null)
            references.AddRange(extraRefs);

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
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            throw new InvalidOperationException(
                "Compilation failed:\n" + string.Join("\n", errors));
        }
        return ms.ToArray();
    }

    private static byte[] CompileToDll(
        string source, string assemblyName = "TestAsm")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

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
                Path.Combine(trustedDir, "System.Collections.dll")),
        };

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
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            throw new InvalidOperationException(
                "Compilation failed:\n" + string.Join("\n", errors));
        }
        return ms.ToArray();
    }
}
