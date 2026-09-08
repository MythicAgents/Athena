using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Config;
using Obfuscator.Source;

namespace Obfuscator.Tests;

[TestClass]
public sealed class UuidCliValidationTests
{
    [TestMethod]
    [DataRow("550E8400-E29B-41D4-A716-446655440000")]
    [DataRow("{550e8400-e29b-41d4-a716-446655440000}")]
    [DataRow(" 550e8400-e29b-41d4-a716-446655440000 ")]
    public void AcceptedUuidFormats_NormalizeDeterministically(string uuid)
    {
        Assert.IsTrue(UuidRenameMap.TryNormalizeUuid(
            uuid, out var normalizedUuid));
        Assert.AreEqual(
            "550e8400-e29b-41d4-a716-446655440000", normalizedUuid);
    }

    [TestMethod]
    public void SourceRewriter_MalformedUuid_RejectsBeforeMutation()
    {
        var root = Path.Combine(
            Path.GetTempPath(), $"obfuscator_uuid_api_{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(input);
        var sourcePath = Path.Combine(input, "Sample.cs");
        const string originalSource = "public sealed class Sample { }";
        File.WriteAllText(sourcePath, originalSource);
        File.WriteAllText(Path.Combine(input, "Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        try
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                new SourceRewriter().Rewrite(new ObfuscationConfig(
                    Seed: 1,
                    Uuid: "not-a-uuid",
                    InputPath: input,
                    OutputPath: output,
                    MapPath: null)));

            Assert.IsFalse(Directory.Exists(output),
                "Malformed UUID must be rejected before output creation.");
            Assert.AreEqual(originalSource, File.ReadAllText(sourcePath),
                "Malformed UUID must be rejected before input mutation.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void RewriteSource_MalformedUuid_RejectsBeforeCreatingOutput()
    {
        var root = Path.Combine(
            Path.GetTempPath(), $"obfuscator_uuid_cli_{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(input);
        File.WriteAllText(Path.Combine(input, "Sample.cs"),
            "public sealed class Sample { }");

        try
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(typeof(UuidRenameMap).Assembly.Location);
            startInfo.ArgumentList.Add("rewrite-source");
            startInfo.ArgumentList.Add("--seed");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("--uuid");
            startInfo.ArgumentList.Add("not-a-uuid");
            startInfo.ArgumentList.Add("--input");
            startInfo.ArgumentList.Add(input);
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(output);

            using var process = Process.Start(startInfo)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            Assert.IsTrue(process.WaitForExit(TimeSpan.FromSeconds(30)),
                "Obfuscator CLI did not exit.");
            var diagnostics = stdout.GetAwaiter().GetResult()
                + stderr.GetAwaiter().GetResult();

            Assert.AreNotEqual(0, process.ExitCode, diagnostics);
            Assert.IsFalse(Directory.Exists(output),
                "Malformed UUID must be rejected before output mutation.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
