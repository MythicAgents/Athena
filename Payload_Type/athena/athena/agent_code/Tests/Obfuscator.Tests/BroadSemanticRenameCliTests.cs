using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Config;

namespace Obfuscator.Tests;

[TestClass]
public sealed class BroadSemanticRenameCliTests
{
    private const string Uuid = "37eb846a-12b9-45d5-a49c-8e10754cc0ba";

    [TestMethod]
    public void RewriteSource_BroadModeOff_RemainsBackwardCompatible()
    {
        using var fixture = new CliFixture();
        var result = Run(fixture.Root);

        Assert.AreEqual(0, result.ExitCode, result.Diagnostics);
    }

    [TestMethod]
    [DataRow("--project-root")]
    [DataRow("--configuration")]
    [DataRow("--handler-os")]
    [DataRow("--crypto-provider")]
    public void RewriteSource_BroadModeOn_RequiresEveryGraphOption(string omitted)
    {
        using var fixture = new CliFixture();
        var options = BroadOptions()
            .Where(pair => pair[0] != omitted)
            .SelectMany(pair => pair)
            .ToArray();

        var result = Run(fixture.Root, ["--broad-semantic-rename", .. options]);

        Assert.AreNotEqual(0, result.ExitCode, result.Diagnostics);
        StringAssert.Contains(result.Diagnostics, omitted);
    }

    [TestMethod]
    [DataRow("--configuration", "Optimized")]
    [DataRow("--handler-os", "freebsd")]
    [DataRow("--crypto-provider", "ChaCha20")]
    public void RewriteSource_BroadModeOn_RejectsInvalidGraphProperty(
        string option, string invalidValue)
    {
        using var fixture = new CliFixture();
        var options = BroadOptions();
        options.Single(pair => pair[0] == option)[1] = invalidValue;

        var result = Run(fixture.Root,
            ["--broad-semantic-rename", .. options.SelectMany(pair => pair)]);

        Assert.AreNotEqual(0, result.ExitCode, result.Diagnostics);
        StringAssert.Contains(result.Diagnostics, "Invalid project property");
        StringAssert.Contains(result.Diagnostics, option switch
        {
            "--configuration" => "Configuration",
            "--handler-os" => "HandlerOS",
            "--crypto-provider" => "CryptoProvider",
            _ => throw new AssertFailedException($"Unexpected option: {option}"),
        });
    }

    [TestMethod]
    public void RewriteSource_BroadModeOn_RejectsProjectRootOutsideWorkspace()
    {
        using var fixture = new CliFixture();
        var outsideProject = Path.Combine(Path.GetDirectoryName(fixture.Root)!,
            $"outside_{Guid.NewGuid():N}.csproj");
        File.WriteAllText(outsideProject, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        try
        {
            var options = BroadOptions();
            options.Single(pair => pair[0] == "--project-root")[1] = outsideProject;

            var result = Run(fixture.Root,
                ["--broad-semantic-rename", .. options.SelectMany(pair => pair)]);

            Assert.AreNotEqual(0, result.ExitCode, result.Diagnostics);
            StringAssert.Contains(result.Diagnostics, "inside the workspace");
        }
        finally
        {
            File.Delete(outsideProject);
        }
    }

    private static List<string[]> BroadOptions() =>
    [
        ["--project-root", "AthenaCore/AthenaCore.csproj"],
        ["--configuration", "Release"],
        ["--handler-os", "linux"],
        ["--crypto-provider", "Aes"],
    ];

    private static CliResult Run(string workspace, params string[] additionalArguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
        {
            typeof(UuidRenameMap).Assembly.Location,
            "rewrite-source", "--seed", "1", "--uuid", Uuid,
            "--input", workspace, "--output", workspace,
        }.Concat(additionalArguments))
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(TimeSpan.FromSeconds(30)),
            "Obfuscator CLI did not exit.");
        return new CliResult(process.ExitCode,
            stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private sealed record CliResult(int ExitCode, string Diagnostics);

    private sealed class CliFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(), $"obfuscator_broad_cli_{Guid.NewGuid():N}");

        public CliFixture()
        {
            var projectDirectory = Path.Combine(Root, "AthenaCore");
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, "AthenaCore.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(Path.Combine(projectDirectory, "Sample.cs"),
                "public sealed class Sample { }");
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}