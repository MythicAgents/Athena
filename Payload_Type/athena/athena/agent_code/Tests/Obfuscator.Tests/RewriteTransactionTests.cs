using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Obfuscator.Config;
using Obfuscator.IL;
using Obfuscator.IL.Transforms;
using Obfuscator.Source;

namespace Obfuscator.Tests;

[TestClass]
public class RewriteTransactionTests
{
    [TestMethod]
    public void SourceRewrite_PartialHelperInjectionFailureRestoresOriginalHelper()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "Fixture.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            var decryptor = Path.Combine(dir, "_generated_decryptor.cs");
            var caller = Path.Combine(dir, "_generated_caller.cs");
            var originalDecryptor = new byte[] { 0, 1, 2, 3, 255 };
            File.WriteAllBytes(decryptor, originalDecryptor);
            Directory.CreateDirectory(caller);

            Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
                new SourceRewriter().Rewrite(new ObfuscationConfig(
                    42, null, dir, dir, null)));

            CollectionAssert.AreEqual(originalDecryptor, File.ReadAllBytes(decryptor));
            Assert.IsTrue(Directory.Exists(caller));
            Assert.HasCount(0, Directory.GetFileSystemEntries(caller));
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RenameAll_GeneratedNameCollisionLeavesDirectoryExact()
    {
        var dir = CreateTempDir();
        try
        {
            WriteAssembly(dir, "one.dll", "Collision42306");
            WriteAssembly(dir, "two.dll", "Collision55157");
            var before = Snapshot(dir);

            var error = Assert.ThrowsExactly<InvalidDataException>(() =>
                new AssemblyRenameTransform(42).RenameAll(
                    dir, ["Collision42306", "Collision55157"], []));

            StringAssert.Contains(error.Message, "_1llbh");
            AssertSnapshot(before, dir);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RenameAll_ExistingDestinationLeavesDirectoryExact()
    {
        var dir = CreateTempDir();
        try
        {
            WriteAssembly(dir, "source.dll", "Alpha");
            File.WriteAllBytes(
                Path.Combine(dir, "_kUaQr.dll"),
                BatchRewriteTests.CreateMinimalNativePe());
            var before = Snapshot(dir);

            var error = Assert.ThrowsExactly<IOException>(() =>
                new AssemblyRenameTransform(42).RenameAll(dir, ["Alpha"], []));

            StringAssert.Contains(error.Message, "_kUaQr.dll");
            AssertSnapshot(before, dir);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RewriteBatch_DepsCollisionLeavesDirectoryExact()
    {
        var dir = CreateTempDir();
        try
        {
            WriteAssembly(dir, "Entry.dll", "Entry");
            WriteAssembly(dir, "Alpha.dll", "Alpha");
            File.WriteAllText(Path.Combine(dir, "Entry.runtimeconfig.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "Entry.deps.json"), """
            { "targets": { "t": {
                "Entry/1.0.0": { "dependencies": { "Alpha": "1.0.0", "_kUaQr": "1.0.0" } },
                "Alpha/1.0.0": { "runtime": { "Alpha.dll": {} } },
                "_kUaQr/1.0.0": { "runtime": { "_kUaQr.dll": {} } }
            } }, "libraries": {
                "Entry/1.0.0": {}, "Alpha/1.0.0": {}, "_kUaQr/1.0.0": {}
            } }
            """);
            var before = Snapshot(dir);

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new ILRewriter().RewriteBatch(dir, 42, null, ["Entry", "Alpha"]));

            AssertSnapshot(before, dir);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void Prepare_EmitResolutionFailurePropagatesWithFilenameAndLeavesDirectoryExact()
    {
        var dir = CreateTempDir();
        try
        {
            WriteAssembly(dir, "odd-file-name.dll", "Alpha");
            var before = Snapshot(dir);
            var transform = new AssemblyRenameTransform(
                42,
                _ => throw new AssemblyResolutionException(
                    new AssemblyNameReference("Missing.Dependency", new Version(1, 0))));

            var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
                transform.RenameAll(dir, ["Alpha"], []));

            StringAssert.Contains(error.Message, "odd-file-name.dll");
            Assert.IsInstanceOfType<AssemblyResolutionException>(error.InnerException);
            AssertSnapshot(before, dir);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void Commit_CaseDistinctLinuxDestinationsAreWrittenIndependently()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Inconclusive("Case-distinct path identity is Linux-specific.");

        var dir = CreateTempDir();
        try
        {
            var upper = Path.Combine(dir, "A.dll");
            var lower = Path.Combine(dir, "a.dll");

            FileRewriteTransaction.Commit(
            [
                new FileRewrite(null, upper, [1]),
                new FileRewrite(null, lower, [2]),
            ]);

            CollectionAssert.AreEqual(new byte[] { 1 }, File.ReadAllBytes(upper));
            CollectionAssert.AreEqual(new byte[] { 2 }, File.ReadAllBytes(lower));
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void Commit_CaseDistinctWindowsDestinationsAreRejectedAsDuplicates()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Case-insensitive path identity is Windows-specific.");

        var dir = CreateTempDir();
        try
        {
            Assert.ThrowsExactly<InvalidDataException>(() =>
                FileRewriteTransaction.Commit(
                [
                    new FileRewrite(null, Path.Combine(dir, "A.dll"), [1]),
                    new FileRewrite(null, Path.Combine(dir, "a.dll"), [2]),
                ]));

            Assert.HasCount(0, Directory.GetFiles(dir));
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void Commit_InstallFailureRollsBackEveryOriginalAndRemovesStages()
    {
        var dir = CreateTempDir();
        try
        {
            var first = Path.Combine(dir, "first.bin");
            var second = Path.Combine(dir, "second.bin");
            var occupied = Path.Combine(dir, "occupied.bin");
            File.WriteAllBytes(first, [1]);
            File.WriteAllBytes(second, [2]);
            File.WriteAllBytes(occupied, [3]);
            var before = Snapshot(dir);

            Assert.ThrowsExactly<IOException>(() =>
                FileRewriteTransaction.Commit(
                [
                    new FileRewrite(first, Path.Combine(dir, "first-new.bin"), [11]),
                    new FileRewrite(second, occupied, [22]),
                ]));

            AssertSnapshot(before, dir);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void Commit_InstalledDestinationDeleteFailureStillRestoresEveryOriginal()
    {
        var dir = CreateTempDir();
        try
        {
            var first = Path.Combine(dir, "first.bin");
            var second = Path.Combine(dir, "second.bin");
            var firstNew = Path.Combine(dir, "first-new.bin");
            var occupied = Path.Combine(dir, "occupied.bin");
            File.WriteAllBytes(first, [1]);
            File.WriteAllBytes(second, [2]);
            File.WriteAllBytes(occupied, [3]);

            var error = Assert.ThrowsExactly<AggregateException>(() =>
                FileRewriteTransaction.Commit(
                [
                    new FileRewrite(first, firstNew, [11]),
                    new FileRewrite(second, occupied, [22]),
                ],
                path =>
                {
                    if (path == firstNew)
                        throw new IOException("injected installed-file deletion failure");
                    File.Delete(path);
                }));

            StringAssert.Contains(error.ToString(), "injected installed-file deletion failure");
            CollectionAssert.AreEqual(new byte[] { 1 }, File.ReadAllBytes(first));
            CollectionAssert.AreEqual(new byte[] { 2 }, File.ReadAllBytes(second));
            CollectionAssert.AreEqual(new byte[] { 3 }, File.ReadAllBytes(occupied));
            CollectionAssert.AreEqual(new byte[] { 11 }, File.ReadAllBytes(firstNew));
            Assert.IsFalse(Directory.GetFiles(dir).Any(path =>
                path.EndsWith(".stage", StringComparison.Ordinal) ||
                path.EndsWith(".backup", StringComparison.Ordinal)));
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void Commit_BackupDeleteFailuresDoNotFailCommittedTransactionAndPreserveBackups()
    {
        var dir = CreateTempDir();
        try
        {
            var first = Path.Combine(dir, "first.bin");
            var second = Path.Combine(dir, "second.bin");
            var firstNew = Path.Combine(dir, "first-new.bin");
            var secondNew = Path.Combine(dir, "second-new.bin");
            File.WriteAllBytes(first, [1]);
            File.WriteAllBytes(second, [2]);
            var failedDeletes = new List<string>();

            FileRewriteTransaction.Commit(
            [
                new FileRewrite(first, firstNew, [11]),
                new FileRewrite(second, secondNew, [22]),
            ],
            path =>
            {
                if (path.EndsWith(".backup", StringComparison.Ordinal))
                {
                    failedDeletes.Add(path);
                    throw new IOException("injected backup deletion failure");
                }
                File.Delete(path);
            });

            CollectionAssert.AreEqual(new byte[] { 11 }, File.ReadAllBytes(firstNew));
            CollectionAssert.AreEqual(new byte[] { 22 }, File.ReadAllBytes(secondNew));
            Assert.IsFalse(File.Exists(first));
            Assert.IsFalse(File.Exists(second));
            Assert.HasCount(2, failedDeletes);
            CollectionAssert.AreEquivalent(
                new byte[] { 1, 2 },
                failedDeletes.Select(path => File.ReadAllBytes(path)[0]).ToArray());
            Assert.IsFalse(Directory.GetFiles(dir).Any(path =>
                path.EndsWith(".stage", StringComparison.Ordinal)));
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void Prepare_DuplicateLogicalIdentityLeavesDirectoryExact()
    {
        var dir = CreateTempDir();
        try
        {
            WriteAssembly(dir, "first.dll", "Same.Identity");
            WriteAssembly(dir, "second.dll", "same.identity");
            var before = Snapshot(dir);

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new AssemblyRenameTransform(42).RenameAll(dir, ["Same.Identity"], []));

            AssertSnapshot(before, dir);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RewriteBatch_MalformedPeFailsWithFilenameAndLeavesDirectoryExact()
    {
        var dir = CreateTempDir();
        try
        {
            WriteAssembly(dir, "Alpha.dll", "Alpha");
            File.WriteAllBytes(Path.Combine(dir, "broken-native.dll"),
                [0x4d, 0x5a, 0x01, 0x02, 0x03]);
            var before = Snapshot(dir);

            var error = Assert.ThrowsExactly<InvalidDataException>(() =>
                new ILRewriter().RewriteBatch(dir, 42, null, ["Alpha"]));

            StringAssert.Contains(error.Message, "broken-native.dll");
            AssertSnapshot(before, dir);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RewriteBatch_MalformedManagedMetadataFailsAndLeavesDirectoryExact()
    {
        var dir = CreateTempDir();
        try
        {
            WriteAssembly(dir, "Alpha.dll", "Alpha");
            File.WriteAllBytes(Path.Combine(dir, "broken-managed.dll"),
                CorruptMetadata(CompileToDll("public class Broken {}", "Broken")));
            var before = Snapshot(dir);

            var error = Assert.ThrowsExactly<InvalidDataException>(() =>
                new ILRewriter().RewriteBatch(dir, 42, null, ["Alpha"]));

            StringAssert.Contains(error.Message, "broken-managed.dll");
            AssertSnapshot(before, dir);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RenameAll_MalformedPeFailsWithFilenameAndLeavesDirectoryExact()
    {
        var dir = CreateTempDir();
        try
        {
            WriteAssembly(dir, "Alpha.dll", "Alpha");
            File.WriteAllBytes(Path.Combine(dir, "bad-image.dll"), [0x4d, 0x5a]);
            var before = Snapshot(dir);

            var error = Assert.ThrowsExactly<InvalidDataException>(() =>
                new AssemblyRenameTransform(42).RenameAll(dir, ["Alpha"], []));

            StringAssert.Contains(error.Message, "bad-image.dll");
            AssertSnapshot(before, dir);
        }
        finally { TryDeleteDir(dir); }
    }

    [TestMethod]
    public void RenameAll_MalformedManagedMetadataFailsAndLeavesDirectoryExact()
    {
        var dir = CreateTempDir();
        try
        {
            WriteAssembly(dir, "Alpha.dll", "Alpha");
            File.WriteAllBytes(Path.Combine(dir, "bad-metadata.dll"),
                CorruptMetadata(CompileToDll("public class Broken {}", "Broken")));
            var before = Snapshot(dir);

            var error = Assert.ThrowsExactly<InvalidDataException>(() =>
                new AssemblyRenameTransform(42).RenameAll(dir, ["Alpha"], []));

            StringAssert.Contains(error.Message, "bad-metadata.dll");
            AssertSnapshot(before, dir);
        }
        finally { TryDeleteDir(dir); }
    }

    private static byte[] CorruptMetadata(byte[] assembly)
    {
        var signature = new byte[] { (byte)'B', (byte)'S', (byte)'J', (byte)'B' };
        var offset = assembly.AsSpan().IndexOf(signature);
        Assert.IsGreaterThanOrEqualTo(0, offset);
        assembly[offset] = 0;
        return assembly;
    }

    private static void WriteAssembly(string dir, string fileName, string identity) =>
        File.WriteAllBytes(Path.Combine(dir, fileName), CompileToDll("public class Value {}", identity));

    private static Dictionary<string, byte[]> Snapshot(string dir) =>
        Directory.GetFiles(dir)
            .ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes, StringComparer.Ordinal);

    private static void AssertSnapshot(Dictionary<string, byte[]> expected, string dir)
    {
        var actual = Snapshot(dir);
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(), actual.Keys.ToArray());
        foreach (var (name, bytes) in expected)
            CollectionAssert.AreEqual(bytes, actual[name], name);
    }

    private static byte[] CompileToDll(string source, string assemblyName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var trustedDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Path.Combine(trustedDir, "System.Collections.dll")),
        };
        var compilation = CSharpCompilation.Create(
            assemblyName, [syntaxTree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        Assert.IsTrue(result.Success, string.Join("\n", result.Diagnostics));
        return output.ToArray();
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rewritetxn_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDir(string path)
    {
        try { Directory.Delete(path, true); } catch { }
    }
}