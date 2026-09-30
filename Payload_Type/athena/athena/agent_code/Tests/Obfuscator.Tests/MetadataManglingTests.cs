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
                if (type.IsEnum
                    || type.IsSerializable
                    || field.Name.StartsWith("<"))
                    continue;

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
    public void ConstructedInheritedInterface_MatchesGenericSignature()
    {
        const string source = """
            public interface IBase<T> { string Format(T value); }
            public interface IDerived<T> : IBase<T> { }
            public sealed class Formatter : IDerived<int>
            {
                public string Format(int value) => "contract:" + value;
                public string Format(double value) => "unrelated:" + value;
                public static string Verify(int ignored, string marker)
                {
                    IBase<int> formatter = new Formatter();
                    return formatter.Format(7);
                }
            }
            """;
        var dll = CompileToDll(source, "ConstructedInheritedInterface");
        var transformed = new MetadataManglingTransform(seed: 42).Transform(dll);

        Assert.AreEqual("contract:7",
            InvokeMethod<string>(transformed, 0, string.Empty));
        using var stream = new MemoryStream(transformed);
        using var assembly = AssemblyDefinition.ReadAssembly(stream);
        var formatter = assembly.MainModule.Types.Single(type =>
            !type.IsInterface
            && type.Methods.Any(method => method.Name == "Format"));
        Assert.IsTrue(formatter.Methods.Any(method => method.Name == "Format"
            && method.Parameters.Single().ParameterType.MetadataType
                == MetadataType.Int32));
        Assert.IsFalse(formatter.Methods.Any(method => method.Name == "Format"
            && method.Parameters.Single().ParameterType.MetadataType
                == MetadataType.Double));
    }

    [TestMethod]
    public void InheritedAndExplicitInterfaceMethods_PreserveOnlyMatchingSignatures()
    {
        const string source = """
            public interface IBase
            {
                string Run(int value);
                string Clash(int value);
            }
            public interface IDerived : IBase { }
            public sealed class Plugin : IDerived
            {
                string IBase.Run(int value) => "explicit:" + value;
                public string Clash(int value) => "contract:" + value;
                public string Clash(string value) => "unrelated:" + value;
                public static string Verify(int ignored, string marker)
                {
                    IBase plugin = new Plugin();
                    return plugin.Run(3) + "|" + plugin.Clash(4);
                }
            }
            """;
        var dll = CompileToDll(source, "InheritedExplicitInterfaces");
        var transformed = new MetadataManglingTransform(seed: 42).Transform(dll);

        Assert.AreEqual("explicit:3|contract:4",
            InvokeMethod<string>(transformed, 0, string.Empty));

        using var stream = new MemoryStream(transformed);
        using var assembly = AssemblyDefinition.ReadAssembly(stream);
        var plugin = assembly.MainModule.Types.Single(type =>
            type.Methods.Any(method => method.Name.Contains("IBase.Run")));
        Assert.IsTrue(plugin.Methods.Any(method =>
            method.Name == "Clash"
            && method.Parameters.Single().ParameterType.MetadataType
                == MetadataType.Int32));
        Assert.IsFalse(plugin.Methods.Any(method =>
            method.Name == "Clash"
            && method.Parameters.Single().ParameterType.MetadataType
                == MetadataType.String),
            "Same-named non-contract overload should still be mangled.");
    }

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

    private const string EnumSource = """
        public enum Color
        {
            Red,
            Green,
            Blue
        }
        public class EnumUser
        {
            public static string GetName(Color c)
            {
                return System.Enum.GetName(typeof(Color), c);
            }
        }
        """;

    [TestMethod]
    public void EnumFields_ArePreserved()
    {
        var dll = CompileToDll(EnumSource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        var expectedNames = new HashSet<string>
            { "value__", "Red", "Green", "Blue" };

        foreach (var type in asm.MainModule.Types)
        {
            if (!type.IsEnum)
                continue;
            foreach (var field in type.Fields)
            {
                Assert.IsTrue(
                    expectedNames.Contains(field.Name),
                    $"Enum field '{field.Name}' must retain "
                    + "its original name (expected one of: "
                    + string.Join(", ", expectedNames) + ")");
            }
        }
    }

    [TestMethod]
    public void EnumWithGetName_StillWorks()
    {
        var dll = CompileToDll(EnumSource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        var alc = new AssemblyLoadContext(
            $"EnumTest_{Guid.NewGuid():N}",
            isCollectible: true);
        try
        {
            var asm = alc.LoadFromStream(
                new MemoryStream(transformed));

            var enumType = asm.GetTypes()
                .First(t => t.IsEnum);
            var userType = asm.GetTypes()
                .First(t => !t.IsEnum
                    && t.Name != "<Module>"
                    && t.FullName
                        != "System.Runtime.CompilerServices"
                        + ".RefSafetyRulesAttribute");

            var method = userType.GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .First(m => m.ReturnType == typeof(string));

            var enumVal = Enum.ToObject(enumType, 1);
            var result = method.Invoke(
                null, new[] { enumVal });
            Assert.AreEqual(
                "Green", result,
                "Enum.GetName should return 'Green' "
                + "for value 1");
        }
        finally
        {
            alc.Unload();
        }
    }

    private const string SerializableSource = """
        [System.Serializable]
        public class Config
        {
            public string Host = "localhost";
            public int Port = 8080;
        }
        """;

    [TestMethod]
    public void SerializableTypeFields_ArePreserved()
    {
        var dll = CompileToDll(SerializableSource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        foreach (var type in asm.MainModule.Types)
        {
            if (type.Name == "<Module>")
                continue;
            if (!type.IsSerializable)
                continue;
            foreach (var field in type.Fields)
            {
                Assert.IsFalse(
                    field.Name.StartsWith("_"),
                    $"[Serializable] field '{field.Name}' "
                    + "should not be renamed");
            }
        }
    }

    private const string SafePropertySource = """
        public class PropertyOwner
        {
            private int Secret { get; set; }
            internal int InternalValue { get; set; }

            public static int Run(string ignored)
            {
                var owner = new PropertyOwner();
                owner.Secret = 17;
                owner.InternalValue = 25;
                return owner.Secret + owner.InternalValue;
            }
        }
        """;

    private const string ReflectionMemberSource = """
        using System;
        using System.Reflection;

        public class ReflectionTarget
        {
            private int Value = 17;
            private int UnusedField = 19;
            private int MemberValue = 23;
            private int DeclaredValue = 29;
            private event EventHandler? Changed;
            private event EventHandler? DeclaredChanged;

            private string Compute() => "method";
            private string Compute(int value) => "overload:" + value;
            private string UnusedMethod() => "unused";
            private string DeclaredCompute() => "declared-method";

            public static string Run(string lookup)
            {
                var target = new ReflectionTarget();
                const BindingFlags flags = BindingFlags.NonPublic
                    | BindingFlags.Instance;
                return lookup switch
                {
                    "GetField" => typeof(ReflectionTarget)
                        .GetField("Value", flags)!.GetValue(target)!.ToString()!,
                    "GetMethod" => (string)typeof(ReflectionTarget)
                        .GetMethod("Compute", flags, null, Type.EmptyTypes, null)!
                        .Invoke(target, null)!,
                    "GetEvent" => typeof(ReflectionTarget)
                        .GetEvent("Changed", flags)!.Name,
                    "GetMember" => typeof(ReflectionTarget)
                        .GetMember("MemberValue", MemberTypes.Field, flags)[0].Name,
                    "GetDeclaredField" => typeof(ReflectionTarget).GetTypeInfo()
                        .GetDeclaredField("DeclaredValue")!.GetValue(target)!
                        .ToString()!,
                    "GetDeclaredMethod" => (string)typeof(ReflectionTarget)
                        .GetTypeInfo().GetDeclaredMethod("DeclaredCompute")!
                        .Invoke(target, null)!,
                    "GetDeclaredEvent" => typeof(ReflectionTarget).GetTypeInfo()
                        .GetDeclaredEvent("DeclaredChanged")!.Name,
                    _ => "unknown",
                };
            }
        }
        """;

    [TestMethod]
    public void DynamicReflectionName_PreservesLookupKindConservatively()
    {
        const string source = """
            using System.Reflection;
            public sealed class DynamicTarget
            {
                private int First = 17;
                private int Second = 23;
                private int UnrelatedMethod() => 31;
                public static int Read(string name)
                {
                    var target = new DynamicTarget();
                    return (int)typeof(DynamicTarget).GetField(
                        name, BindingFlags.NonPublic | BindingFlags.Instance)!
                        .GetValue(target)!;
                }
            }
            """;
        var dll = CompileToDll(source, "DynamicReflection");
        var transformed = new MetadataManglingTransform(seed: 42).Transform(dll);

        Assert.AreEqual(17, InvokeMethod<int>(transformed, "First"));
        Assert.AreEqual(23, InvokeMethod<int>(transformed, "Second"));

        using var stream = new MemoryStream(transformed);
        using var assembly = AssemblyDefinition.ReadAssembly(stream);
        var names = assembly.MainModule.Types.SelectMany(type => type.Fields)
            .Select(field => field.Name).ToArray();
        CollectionAssert.Contains(names, "First");
        CollectionAssert.Contains(names, "Second");
        Assert.IsFalse(assembly.MainModule.Types.SelectMany(type => type.Methods)
            .Any(method => method.Name == "UnrelatedMethod"),
            "Dynamic field lookup should not blanket-preserve methods.");
    }

    [TestMethod]
    public void BranchedLocalWithRuntimeDefinition_PreservesFieldKindConservatively()
    {
        const string source = """
            using System.Reflection;
            public sealed class DynamicTarget
            {
                private int First = 17;
                private int Second = 23;
                private int UnrelatedMethod() => 31;
                public static int Read(string name, bool useRuntime)
                {
                    string selected;
                    if (useRuntime)
                        selected = name;
                    else
                        selected = "First";
                    var target = new DynamicTarget();
                    return (int)typeof(DynamicTarget).GetField(
                        selected, BindingFlags.NonPublic | BindingFlags.Instance)!
                        .GetValue(target)!;
                }
            }
            """;
        var dll = CompileToDll(source, "BranchedReflection");
        var transformed = new MetadataManglingTransform(seed: 42).Transform(dll);

        var loadContext = new AssemblyLoadContext(
            $"branched-reflection-{Guid.NewGuid():N}", isCollectible: true);
        using var input = new MemoryStream(transformed);
        var assembly = loadContext.LoadFromStream(input);
        var read = assembly.GetTypes().SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Single(method => method.GetParameters().Length == 2);
        Assert.AreEqual(23, read.Invoke(null, ["Second", true]));

        using var stream = new MemoryStream(transformed);
        using var definition = AssemblyDefinition.ReadAssembly(stream);
        Assert.IsFalse(definition.MainModule.Types.SelectMany(type => type.Methods)
            .Any(method => method.Name == "UnrelatedMethod"),
            "Dynamic field lookup should not blanket-preserve methods.");
        loadContext.Unload();
    }

    [TestMethod]
    [DataRow("parameter")]
    [DataRow("reassignment")]
    [DataRow("byref")]
    [DataRow("unknown-call")]
    public void DynamicLocalDefinitions_PreserveFieldKindConservatively(
        string scenario)
    {
        var assignment = scenario switch
        {
            "parameter" => "string selected = name;",
            "reassignment" => "string selected = \"First\"; selected = name;",
            "byref" => "string selected = \"First\"; Replace(ref selected, name);",
            "unknown-call" => "string selected = Choose(name);",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        var source = $$"""
            using System.Reflection;
            public sealed class DynamicTarget
            {
                private int First = 17;
                private int Second = 23;
                private int UnrelatedMethod() => 31;
                private static string Choose(string value) => value;
                private static void Replace(ref string target, string value)
                    => target = value;
                public static int Read(string name)
                {
                    {{assignment}}
                    var target = new DynamicTarget();
                    return (int)typeof(DynamicTarget).GetField(
                        selected, BindingFlags.NonPublic | BindingFlags.Instance)!
                        .GetValue(target)!;
                }
            }
            """;
        var transformed = new MetadataManglingTransform(seed: 42)
            .Transform(CompileToDll(source, "DynamicLocal" + scenario));

        Assert.AreEqual(23, InvokeMethod<int>(transformed, "Second"));
        using var stream = new MemoryStream(transformed);
        using var definition = AssemblyDefinition.ReadAssembly(stream);
        Assert.IsFalse(definition.MainModule.Types.SelectMany(type => type.Methods)
            .Any(method => method.Name == "UnrelatedMethod"),
            "Dynamic field lookup should preserve fields only.");
    }

    [TestMethod]
    public void DynamicTypeGetType_InDictionaryBearingUserType_PreservesIdentity()
    {
        const string source = """
            using System;
            using System.Collections.Concurrent;
            using System.Reflection;
            namespace Lookup
            {
                public sealed class Spoof
                {
                    private static readonly ConcurrentDictionary<string, MethodInfo>
                        Cache = new();

                    public static string Read(string runtimeName) =>
                        Type.GetType(runtimeName, throwOnError: true)!.FullName!;
                }
                public sealed class Payload { }
            }
            """;
        var transformed = new MetadataManglingTransform(seed: 42)
            .Transform(CompileToDll(source, "SpoofReflection"));

        Assert.AreEqual("Lookup.Payload", InvokeMethod<string>(
            transformed, "Lookup.Payload, SpoofReflection"));
    }

    [TestMethod]
    public void TypeGetType_PreservesLocalGenericArgumentsRecursively()
    {
        const string source = """
            using System;
            public sealed class LocalGeneric { }
            namespace Lookup
            {
                public sealed class Envelope<T> { }
                public sealed class Payload { }
                public sealed class UnusedType { }
                public static class Entry
                {
                    public static string Read(string ignored)
                    {
                        var type = Type.GetType(
                            "Lookup.Envelope`1[[Lookup.Payload, LocalGeneric]], LocalGeneric",
                            throwOnError: true)!;
                        return type.GetGenericArguments()[0].FullName!;
                    }
                }
            }
            """;
        var transformed = new MetadataManglingTransform(seed: 42)
            .Transform(CompileToDll(source, "LocalGeneric"));

        Assert.AreEqual("Lookup.Payload",
            InvokeMethod<string>(transformed, string.Empty));
        using var stream = new MemoryStream(transformed);
        using var definition = AssemblyDefinition.ReadAssembly(stream);
        var names = definition.MainModule.Types.Select(type => type.Name).ToArray();
        CollectionAssert.Contains(names, "Envelope`1");
        CollectionAssert.Contains(names, "Payload");
        CollectionAssert.DoesNotContain(names, "LocalGeneric");
        CollectionAssert.DoesNotContain(names, "UnusedType");
    }

    [TestMethod]
    public void TypeGetType_PreservesNestedLocalGenericArgumentIdentities()
    {
        const string source = """
            using System;
            namespace Lookup
            {
                public sealed class Envelope<T> { }
                public sealed class Box<T> { }
                public sealed class Outer
                {
                    public sealed class Payload { }
                }
                public sealed class UnusedType { }
                public static class Entry
                {
                    public static string Read(string ignored)
                    {
                        var type = Type.GetType(
                            "Lookup.Envelope`1[[Lookup.Box`1[[Lookup.Outer+Payload[], NestedGeneric]], NestedGeneric]], NestedGeneric",
                            throwOnError: true)!;
                        return type.GetGenericArguments()[0]
                            .GetGenericArguments()[0].GetElementType()!.FullName!;
                    }
                }
            }
            """;
        var transformed = new MetadataManglingTransform(seed: 42)
            .Transform(CompileToDll(source, "NestedGeneric"));

        Assert.AreEqual("Lookup.Outer+Payload",
            InvokeMethod<string>(transformed, string.Empty));
        using var stream = new MemoryStream(transformed);
        using var definition = AssemblyDefinition.ReadAssembly(stream);
        var names = definition.MainModule.Types
            .SelectMany(type => new[] { type }.Concat(type.NestedTypes))
            .Select(type => type.Name).ToArray();
        CollectionAssert.Contains(names, "Envelope`1");
        CollectionAssert.Contains(names, "Box`1");
        CollectionAssert.Contains(names, "Outer");
        CollectionAssert.Contains(names, "Payload");
        CollectionAssert.DoesNotContain(names, "UnusedType");
    }

    [TestMethod]
    public void TypeGetType_PreservesNestedGenericAndAssemblyQualifiedNames()
    {
        const string source = """
            using System;
            namespace Lookup
            {
                public sealed class Container<T>
                {
                    public sealed class Nested { }
                }
                public sealed class UnusedType { }
                public static class Entry
                {
                    public static string Read(string ignored)
                    {
                        var type = Type.GetType(
                            "Lookup.Container`1+Nested[[System.String, System.Private.CoreLib]], TypeReflection",
                            throwOnError: true)!;
                        return type.FullName!;
                    }
                }
            }
            """;
        var transformed = new MetadataManglingTransform(seed: 42)
            .Transform(CompileToDll(source, "TypeReflection"));

        StringAssert.Contains(InvokeMethod<string>(transformed, string.Empty),
            "Lookup.Container`1+Nested");
        using var stream = new MemoryStream(transformed);
        using var definition = AssemblyDefinition.ReadAssembly(stream);
        var names = definition.MainModule.Types
            .SelectMany(type => new[] { type }.Concat(type.NestedTypes))
            .Select(type => type.Name).ToArray();
        CollectionAssert.Contains(names, "Container`1");
        CollectionAssert.Contains(names, "Nested");
        CollectionAssert.DoesNotContain(names, "UnusedType");
    }

    [TestMethod]
    public void RuntimeReflectionExtensions_UsesActualStringParameter()
    {
        const string source = """
            using System;
            using System.Reflection;
            public sealed class RuntimeTarget
            {
                public int Value = 17;
                public int Unused = 23;
                public static int Read(string ignored)
                {
                    var target = new RuntimeTarget();
                    return (int)RuntimeReflectionExtensions
                        .GetRuntimeField(typeof(RuntimeTarget), "Value")!
                        .GetValue(target)!;
                }
            }
            """;
        var transformed = new MetadataManglingTransform(seed: 42)
            .Transform(CompileToDll(source, "RuntimeReflection"));

        Assert.AreEqual(17, InvokeMethod<int>(transformed, string.Empty));
        using var stream = new MemoryStream(transformed);
        using var definition = AssemblyDefinition.ReadAssembly(stream);
        var fields = definition.MainModule.Types.SelectMany(type => type.Fields)
            .Select(field => field.Name).ToArray();
        CollectionAssert.Contains(fields, "Value");
        CollectionAssert.DoesNotContain(fields, "Unused");
    }

    [TestMethod]
    public void ConditionalReflectionArgument_DoesNotSelectOneLexicalProducer()
    {
        const string source = """
            using System.Reflection;
            public sealed class BranchTarget
            {
                private int First = 17;
                private int Second = 23;
                private int UnrelatedMethod() => 31;
                public static int Read(string name)
                {
                    var target = new BranchTarget();
                    return (int)typeof(BranchTarget).GetField(
                        name == "First" ? "First" : "Second",
                        BindingFlags.NonPublic | BindingFlags.Instance)!
                        .GetValue(target)!;
                }
            }
            """;
        var transformed = new MetadataManglingTransform(seed: 42)
            .Transform(CompileToDll(source, "ConditionalReflection"));

        Assert.AreEqual(17, InvokeMethod<int>(transformed, "First"));
        Assert.AreEqual(23, InvokeMethod<int>(transformed, "Second"));
        using var stream = new MemoryStream(transformed);
        using var definition = AssemblyDefinition.ReadAssembly(stream);
        Assert.IsFalse(definition.MainModule.Types.SelectMany(type => type.Methods)
            .Any(method => method.Name == "UnrelatedMethod"));
    }

    [TestMethod]
    public void ExactReflectionLookup_DoesNotPreserveCaseVariant()
    {
        const string source = """
            using System.Reflection;
            public sealed class CaseTarget
            {
                private int Value = 17;
                private int value = 23;
                public static int Read(string ignored)
                {
                    var target = new CaseTarget();
                    return (int)typeof(CaseTarget).GetField(
                        "Value", BindingFlags.NonPublic | BindingFlags.Instance)!
                        .GetValue(target)!;
                }
            }
            """;
        var transformed = new MetadataManglingTransform(seed: 42)
            .Transform(CompileToDll(source, "ExactCaseReflection"));

        Assert.AreEqual(17, InvokeMethod<int>(transformed, string.Empty));
        using var stream = new MemoryStream(transformed);
        using var definition = AssemblyDefinition.ReadAssembly(stream);
        var fields = definition.MainModule.Types.SelectMany(type => type.Fields)
            .Select(field => field.Name).ToArray();
        CollectionAssert.Contains(fields, "Value");
        CollectionAssert.DoesNotContain(fields, "value");
    }

    [TestMethod]
    public void IgnoreCaseReflectionLookup_PreservesResolvableMetadataName()
    {
        const string source = """
            using System.Reflection;
            public sealed class CaseTarget
            {
                private int Value = 17;
                private int UnusedField = 23;
                public static int Read(string ignored)
                {
                    var target = new CaseTarget();
                    return (int)typeof(CaseTarget).GetField(
                        "value", BindingFlags.NonPublic | BindingFlags.Instance
                            | BindingFlags.IgnoreCase)!.GetValue(target)!;
                }
            }
            """;
        var transformed = new MetadataManglingTransform(seed: 42)
            .Transform(CompileToDll(source, "IgnoreCaseReflection"));

        Assert.AreEqual(17, InvokeMethod<int>(transformed, string.Empty));
        using var stream = new MemoryStream(transformed);
        using var definition = AssemblyDefinition.ReadAssembly(stream);
        var fields = definition.MainModule.Types.SelectMany(type => type.Fields)
            .Select(field => field.Name).ToArray();
        CollectionAssert.Contains(fields, "Value");
        CollectionAssert.DoesNotContain(fields, "UnusedField");
    }

    [TestMethod]
    public void LiteralGetField_PreservesOnlyReferencedField()
    {
        var dll = CompileToDll(ReflectionMemberSource, "ReflectionMembers");
        var transformed = new MetadataManglingTransform(seed: 42).Transform(dll);

        Assert.AreEqual("17", InvokeMethod<string>(transformed, "GetField"));

        using var stream = new MemoryStream(transformed);
        using var assembly = AssemblyDefinition.ReadAssembly(stream);
        var fields = assembly.MainModule.Types
            .SelectMany(type => type.Fields)
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);
        CollectionAssert.Contains(fields.ToArray(), "Value");
        CollectionAssert.DoesNotContain(fields.ToArray(), "UnusedField");
    }

    [TestMethod]
    [DataRow("GetMethod", "method")]
    [DataRow("GetEvent", "Changed")]
    [DataRow("GetMember", "MemberValue")]
    [DataRow("GetDeclaredField", "29")]
    [DataRow("GetDeclaredMethod", "declared-method")]
    [DataRow("GetDeclaredEvent", "DeclaredChanged")]
    public void LiteralNameBasedMemberLookup_PreservesReferencedMember(
        string lookup, string expected)
    {
        var dll = CompileToDll(ReflectionMemberSource, "ReflectionMembers");
        var transformed = new MetadataManglingTransform(seed: 42).Transform(dll);

        Assert.AreEqual(expected, InvokeMethod<string>(transformed, lookup));
    }

    [TestMethod]
    public void LiteralGetMethod_PreservesAllSameNamedOverloadsOnly()
    {
        var dll = CompileToDll(ReflectionMemberSource, "ReflectionMembers");
        var transformed = new MetadataManglingTransform(seed: 42).Transform(dll);

        using var stream = new MemoryStream(transformed);
        using var assembly = AssemblyDefinition.ReadAssembly(stream);
        var methods = assembly.MainModule.Types
            .SelectMany(type => type.Methods)
            .Select(method => method.Name)
            .ToList();
        Assert.HasCount(2, methods.Where(name => name == "Compute"));
        CollectionAssert.DoesNotContain(methods, "UnusedMethod");
    }

    [TestMethod]
    public void InternalPropertyFamilies_AreRenamedAndStillExecute()
    {
        var dll = CompileToDll(SafePropertySource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        using (var stream = new MemoryStream(transformed))
        using (var assembly = AssemblyDefinition.ReadAssembly(stream))
        {
            var properties = assembly.MainModule.Types
                .Where(type => type.Name != "<Module>")
                .SelectMany(type => type.Properties)
                .ToList();

            Assert.HasCount(2, properties);
            foreach (var property in properties)
            {
                Assert.IsTrue(property.Name.StartsWith("_"),
                    $"Safe property '{property.Name}' should be renamed");
                Assert.AreEqual("get_" + property.Name,
                    property.GetMethod?.Name);
                Assert.AreEqual("set_" + property.Name,
                    property.SetMethod?.Name);
            }
        }

        Assert.AreEqual(42, InvokeMethod<int>(transformed, "ignored"));
    }

    private const string AutoPropSource = """
        public class AutoProps
        {
            public string Name { get; set; }
            public int Value { get; set; }
        }
        """;

    [TestMethod]
    public void PublicPropertyAccessors_ArePreserved()
    {
        var dll = CompileToDll(AutoPropSource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        foreach (var type in asm.MainModule.Types)
        {
            if (type.Name == "<Module>")
                continue;
            foreach (var method in type.Methods)
            {
                if (method.IsConstructor)
                    continue;
                if (method.IsGetter || method.IsSetter)
                {
                    Assert.IsTrue(
                        method.Name.StartsWith("get_")
                        || method.Name.StartsWith("set_"),
                        $"Accessor '{method.Name}' should "
                        + "retain its original name");
                }
            }
        }
    }

    [TestMethod]
    public void CompilerGeneratedBackingFields_ArePreserved()
    {
        var dll = CompileToDll(AutoPropSource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        foreach (var type in asm.MainModule.Types)
        {
            if (type.Name == "<Module>")
                continue;
            foreach (var field in type.Fields)
            {
                if (field.Name.StartsWith("<"))
                {
                    Assert.IsTrue(
                        field.Name.Contains(
                            "k__BackingField"),
                        $"Backing field '{field.Name}' "
                        + "should be preserved");
                }
            }
        }
    }

    private const string PropertyContractsSource = """
        using System;
        using System.Reflection;
        using System.Runtime.Serialization;
        using System.Text.Json.Serialization;

        public interface IContract
        {
            int InterfaceValue { get; }
        }

        public class BaseContract
        {
            protected virtual int OverriddenValue => 3;
        }

        public class Contract : BaseContract, IContract
        {
            protected override int OverriddenValue => 5;
            int IContract.InterfaceValue => 7;

            [DataMember]
            internal string WireValue { get; set; } = "wire";

            [JsonPropertyName("json_value")]
            internal string JsonValue { get; set; } = "json";

            public int ReflectedValue => 11;
            private int ReflectedPrivate => 13;

            public static string Reflect(string ignored)
            {
                return typeof(Contract).GetProperty(
                    "ReflectedValue", BindingFlags.Public
                    | BindingFlags.Instance)!.Name;
            }

            public static int ReflectPrivate(string ignored)
            {
                return (int)typeof(Contract).GetProperty(
                    "ReflectedPrivate", BindingFlags.NonPublic
                    | BindingFlags.Instance)!.GetValue(new Contract())!;
            }
        }
        """;

    [TestMethod]
    public void ExternallyObservedPropertyContracts_ArePreserved()
    {
        var trustedDir = Path.GetDirectoryName(
            typeof(object).Assembly.Location)!;
        var references = JsonRefs.Concat(
        [
            MetadataReference.CreateFromFile(Path.Combine(
                trustedDir,
                "System.Runtime.Serialization.Primitives.dll")),
        ]).ToArray();
        var dll = CompileToDll(
            PropertyContractsSource, "TestAsm", references);
        var transformed = new MetadataManglingTransform(seed: 42)
            .Transform(dll);

        using (var stream = new MemoryStream(transformed))
        using (var assembly = AssemblyDefinition.ReadAssembly(stream))
        {
            var names = assembly.MainModule.Types
                .SelectMany(type => type.Properties)
                .Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal);

            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "InterfaceValue", "OverriddenValue",
                    "IContract.InterfaceValue", "WireValue",
                    "JsonValue", "ReflectedValue", "ReflectedPrivate",
                },
                names.ToArray());
        }

        Assert.AreEqual("ReflectedValue",
            InvokeMethod<string>(transformed, "ignored"));
        Assert.AreEqual(13,
            InvokeMethod<int>(transformed, "ignored"));
    }

    private const string DataContractSource = """
        using System.Runtime.Serialization;
        [DataContract]
        public class Message
        {
            [DataMember]
            public string Content = "hello";
            [DataMember]
            public int Id = 1;
        }
        """;

    [TestMethod]
    public void DataContractTypeFields_ArePreserved()
    {
        var trustedDir = Path.GetDirectoryName(
            typeof(object).Assembly.Location)!;
        var extraRefs = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(
                Path.Combine(trustedDir,
                    "System.Runtime.Serialization"
                    + ".Primitives.dll")),
        };
        var dll = CompileToDll(
            DataContractSource, "TestAsm", extraRefs);
        var transform = new MetadataManglingTransform(
            seed: 42);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        foreach (var type in asm.MainModule.Types)
        {
            if (type.Name == "<Module>")
                continue;
            var hasDataContract = type.CustomAttributes
                .Any(a => a.AttributeType.Name
                    == "DataContractAttribute");
            if (!hasDataContract)
                continue;
            foreach (var field in type.Fields)
            {
                Assert.IsFalse(
                    field.Name.StartsWith("_"),
                    $"[DataContract] field '{field.Name}' "
                    + "should not be renamed");
            }
        }
    }

    private const string VirtualChainSource = """
        public class Animal
        {
            public virtual string Speak()
            {
                return "...";
            }
            public int NonVirtual() { return 1; }
        }
        public class Dog : Animal
        {
            public override string Speak()
            {
                return "Woof";
            }
            public int AnotherNonVirtual() { return 2; }
        }
        """;

    private static readonly HashSet<string> PreservedNames =
        ["ToString", "GetHashCode", "Equals",
         "Dispose", "GetEnumerator", "MoveNext",
         "get_Current"];

    [TestMethod]
    public void VirtualOverrideChain_GetsSameName()
    {
        var dll = CompileToDll(VirtualChainSource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        var types = asm.MainModule.Types
            .Where(t => t.Name != "<Module>")
            .ToList();

        var baseSpeakName = types[0].Methods
            .First(m => m.IsVirtual
                && !m.IsConstructor
                && !PreservedNames.Contains(m.Name))
            .Name;
        var derivedSpeakName = types[1].Methods
            .First(m => m.IsVirtual
                && !m.IsConstructor
                && !PreservedNames.Contains(m.Name))
            .Name;

        Assert.AreEqual(
            baseSpeakName, derivedSpeakName,
            "Virtual method and its override must have "
            + "the same renamed name");
        Assert.IsTrue(
            baseSpeakName.StartsWith("_"),
            "Virtual method should be renamed");
    }

    [TestMethod]
    public void NonVirtualMethods_RenamedIndependently()
    {
        var dll = CompileToDll(VirtualChainSource);
        var transform = new MetadataManglingTransform(seed: 42);
        var transformed = transform.Transform(dll);

        using var ms = new MemoryStream(transformed);
        var asm = AssemblyDefinition.ReadAssembly(ms);

        var types = asm.MainModule.Types
            .Where(t => t.Name != "<Module>")
            .ToList();

        var baseNonVirtual = types[0].Methods
            .Where(m => !m.IsVirtual
                && !m.IsConstructor
                && !PreservedNames.Contains(m.Name))
            .Select(m => m.Name)
            .ToHashSet();

        var derivedNonVirtual = types[1].Methods
            .Where(m => !m.IsVirtual
                && !m.IsConstructor
                && !PreservedNames.Contains(m.Name))
            .Select(m => m.Name)
            .ToHashSet();

        Assert.IsFalse(
            baseNonVirtual.Overlaps(derivedNonVirtual),
            "Non-virtual methods in different types "
            + "should get different renamed names");
    }

    [TestMethod]
    public void VirtualFamilyName_NeverCollidesWithSameSignatureMethod()
    {
        const string source = """
            public class Base
            {
                public virtual string Render(int value) => "base:" + value;
            }
            public class Derived : Base
            {
                public override string Render(int value) => "override:" + value;
                public string Format(int value) => "format:" + value;
                public static string Run(string ignored)
                {
                    var value = new Derived();
                    return ((Base)value).Render(3) + "|" + value.Format(4);
                }
            }
            """;
        var dll = CompileToDll(source, "VirtualCollision");

        for (var seed = 0; seed < 2_000; seed++)
        {
            var transformed = new MetadataManglingTransform(seed).Transform(dll);
            using (var stream = new MemoryStream(transformed))
            using (var assembly = AssemblyDefinition.ReadAssembly(stream))
            {
                foreach (var type in assembly.MainModule.Types)
                {
                    var duplicate = type.Methods
                        .Where(method => !method.IsConstructor)
                        .GroupBy(method => method.Name + "|"
                            + string.Join(",", method.Parameters.Select(
                                parameter => parameter.ParameterType.FullName)),
                            StringComparer.Ordinal)
                        .FirstOrDefault(group => group.Count() > 1);
                    Assert.IsNull(duplicate,
                        $"Seed {seed} emitted duplicate method signature '{duplicate?.Key}'.");
                }
            }

            Assert.AreEqual("override:3|format:4",
                InvokeMethod<string>(transformed, string.Empty));
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
            MetadataReference.CreateFromFile(
                Path.Combine(trustedDir, "System.Collections.Concurrent.dll")),
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
