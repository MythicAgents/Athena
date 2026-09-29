using System.IO.Compression;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Obfuscator.IL;

namespace Obfuscator.Tests;

[TestClass]
public class SingleFileBundleRewriteTests
{
    private static readonly byte[] Signature =
    [
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38,
        0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18,
        0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae,
    ];

    [TestMethod]
    public void IsBundle_NonBundleFile_ReturnsFalse()
    {
        // Arrange
        var bytes = new byte[256];

        // Act
        var isBundle = SingleFileBundleFormat.IsBundle(bytes);

        // Assert
        Assert.IsFalse(isBundle);
    }

    [TestMethod]
    public void RewriteBatch_SingleFileBundle_ObfuscatesAssembliesTypesAndDeps()
    {
        // Arrange
        var dir = CreateTempDir();
        try
        {
            var exePath = CreateSampleBundle(dir, useBrotli: false);

            // Act
            new ILRewriter().RewriteBatch(
                dir, seed: 42, mapPath: null,
                firstPartyAssemblyNames: ["Workflow.Models", "ServiceHost"]);

            // Assert
            AssertBundleObfuscated(exePath);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RewriteBatch_SingleFileBundle_SkipAssemblyRename_PreservesPaths()
    {
        // Arrange
        var dir = CreateTempDir();
        try
        {
            var exePath = CreateSampleBundle(dir, useBrotli: true);

            // Act
            new ILRewriter().RewriteBatch(
                dir, seed: 42, mapPath: null,
                firstPartyAssemblyNames: ["Workflow.Models", "ServiceHost"],
                skipFileRename: true, skipAssemblyRename: true);

            // Assert
            AssertBundlePreservedPaths(exePath);
        }
        finally { TryDeleteDir(dir); }
    }

    private static void AssertBundleObfuscated(string exePath)
    {
        var bytes = File.ReadAllBytes(exePath);
        Assert.IsTrue(SingleFileBundleFormat.IsBundle(bytes));
        var bundle = SingleFileBundleFormat.ReadManifest(bytes);
        Assert.IsFalse(bundle.Entries.Any(e => e.RelativePath == "Workflow.Models.dll"));
        Assert.IsTrue(bundle.Entries.Any(e => e.RelativePath == "ServiceHost.dll"));

        var renamedLib = bundle.Entries.Single(
            e => e.RelativePath.StartsWith('_') && e.RelativePath.EndsWith(".dll"));
        var libAsm = ReadEmbeddedAssembly(bytes, renamedLib);
        Assert.IsTrue(libAsm.Name.Name.StartsWith('_'));
        Assert.IsTrue(libAsm.MainModule.Types.Single(t => t.Name != "<Module>").Name.StartsWith('_'));

        var depsEntry = bundle.Entries.Single(e => e.RelativePath == "ServiceHost.deps.json");
        var depsJson = Encoding.UTF8.GetString(SingleFileBundleFormat.ExtractEntryBytes(bytes, depsEntry));
        StringAssert.Contains(depsJson, $"{libAsm.Name.Name}.dll");
    }

    private static void AssertBundlePreservedPaths(string exePath)
    {
        var bytes = File.ReadAllBytes(exePath);
        Assert.IsTrue(SingleFileBundleFormat.IsBundle(bytes));
        var bundle = SingleFileBundleFormat.ReadManifest(bytes);
        var libEntry = bundle.Entries.Single(e => e.RelativePath == "Workflow.Models.dll");
        var hostEntry = bundle.Entries.Single(e => e.RelativePath == "ServiceHost.dll");

        var libAsm = ReadEmbeddedAssembly(bytes, libEntry);
        var hostAsm = ReadEmbeddedAssembly(bytes, hostEntry);
        Assert.AreEqual("Workflow.Models", libAsm.Name.Name);
        Assert.IsTrue(libAsm.MainModule.Types.Single(t => t.Name != "<Module>").Name.StartsWith('_'));
        Assert.IsTrue(hostAsm.MainModule.AssemblyReferences.Any(r => r.Name == "Workflow.Models"));
    }

    private static AssemblyDefinition ReadEmbeddedAssembly(byte[] fileBytes, BundleEntry entry)
    {
        var raw = SingleFileBundleFormat.ExtractEntryBytes(fileBytes, entry);
        return AssemblyDefinition.ReadAssembly(new MemoryStream(raw));
    }

    private static string CreateSampleBundle(string dir, bool useBrotli)
    {
        var libBytes = CompileToDll(
            "namespace Lib { public class Helper { public static int Add(int a, int b) => a + b; } }",
            "Workflow.Models");
        var appBytes = CompileToDll(
            "public class App { public static int Run() => Lib.Helper.Add(3, 4); }",
            "ServiceHost", libBytes);
        var depsBytes = Encoding.UTF8.GetBytes(
            """{"targets":{".NETCoreApp,Version=v10.0":{"Workflow.Models/1.0.0":{"runtime":{"Workflow.Models.dll":{}}}}},"libraries":{"Workflow.Models/1.0.0":{"type":"project"}}}""");

        var entries = new[]
        {
            ("Workflow.Models.dll", (byte)1, libBytes),
            ("ServiceHost.dll", (byte)1, appBytes),
            ("ServiceHost.deps.json", (byte)3, depsBytes),
            ("ServiceHost.runtimeconfig.json", (byte)4, Encoding.UTF8.GetBytes("{}")),
        };
        var exePath = Path.Combine(dir, "ServiceHost.exe");
        File.WriteAllBytes(exePath, BuildSyntheticBundle(entries, useBrotli));
        return exePath;
    }

    private static byte[] BuildSyntheticBundle(
        IReadOnlyList<(string Path, byte Type, byte[] Content)> items, bool useBrotli)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        WriteApphostStub(writer);
        var records = WritePayloads(ms, items, useBrotli);
        var headerOffset = ms.Position;
        WriteManifest(writer, records);
        PatchHeaderOffset(ms, headerOffset);
        return ms.ToArray();
    }

    private static void WriteApphostStub(BinaryWriter writer)
    {
        writer.Write(new byte[64]);
        writer.Write(0L);
        writer.Write(Signature);
        writer.Write(new byte[64]);
    }

    private static List<BundleEntry> WritePayloads(
        MemoryStream ms, IReadOnlyList<(string Path, byte Type, byte[] Content)> items, bool useBrotli)
    {
        var list = new List<BundleEntry>(items.Count);
        foreach (var item in items)
        {
            var offset = ms.Position;
            var payload = CompressPayload(item.Content, useBrotli);
            ms.Write(payload, 0, payload.Length);
            list.Add(new BundleEntry(offset, item.Content.Length, payload.Length, item.Type, item.Path));
        }
        return list;
    }

    private static byte[] CompressPayload(byte[] data, bool useBrotli)
    {
        using var output = new MemoryStream();
        using Stream compressor = useBrotli
            ? new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true)
            : new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true);
        compressor.Write(data, 0, data.Length);
        compressor.Dispose();
        return output.ToArray();
    }

    private static void WriteManifest(BinaryWriter writer, List<BundleEntry> entries)
    {
        writer.Write(6u);
        writer.Write(0u);
        writer.Write(entries.Count);
        writer.Write("test-bundle-id");
        writer.Write(new byte[40]);
        foreach (var entry in entries)
        {
            writer.Write(entry.Offset);
            writer.Write(entry.Size);
            writer.Write(entry.CompressedSize);
            writer.Write(entry.FileType);
            writer.Write(entry.RelativePath);
        }
    }

    private static void PatchHeaderOffset(MemoryStream ms, long headerOffset)
    {
        ms.Position = 64;
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        writer.Write(headerOffset);
    }

    private static byte[] CompileToDll(string source, string assemblyName, byte[]? extraRef = null)
    {
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        };
        if (extraRef is not null)
            refs.Add(MetadataReference.CreateFromImage(extraRef));

        var compilation = CSharpCompilation.Create(
            assemblyName, [CSharpSyntaxTree.ParseText(source)], refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var ms = new MemoryStream();
        Assert.IsTrue(compilation.Emit(ms).Success);
        return ms.ToArray();
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sfbtest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDir(string path)
    {
        try { Directory.Delete(path, true); } catch { }
    }
}
