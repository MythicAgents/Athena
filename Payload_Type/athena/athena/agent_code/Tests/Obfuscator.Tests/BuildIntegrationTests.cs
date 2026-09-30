using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Config;
using Obfuscator.Source;

namespace Obfuscator.Tests;

[TestClass]
[DoNotParallelize]
public class BuildIntegrationTests
{
    private static readonly string AgentCodePath =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));

    private static string CopySourceToTemp()
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            $"obfbuild_{Guid.NewGuid():N}");

        CopyDirectory(AgentCodePath, tempDir,
            ["bin", "obj", "Tests", "Obfuscator", ".vs",
             ".git", "AMD", "app", "docs"]);

        return tempDir;
    }

    private static void ObfuscateSource(string sourceDir)
    {
        var config = new ObfuscationConfig(
            Seed: 42,
            Uuid: "991cc952-8c40-5499-8805-2cde316f306a",
            InputPath: sourceDir,
            OutputPath: sourceDir,
            MapPath: null);

        var rewriter = new SourceRewriter();
        rewriter.Rewrite(config);
    }

    private static (int exitCode, string output) DotnetBuild(
        string workingDir,
        string project,
        string? extraArgs = null)
    {
        var args = $"build {project} --nologo --no-restore"
            + (extraArgs ?? "");

        // Restore first (separate step for clearer errors)
        var restoreResult = RunDotnet(
            workingDir, $"restore {project} --nologo");
        if (restoreResult.exitCode != 0)
            return restoreResult;

        return RunDotnet(workingDir, args);
    }

    private static (int exitCode, string output) RunDotnet(
        string workingDir, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = args,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        proc.WaitForExit(TimeSpan.FromMinutes(10));
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        return (proc.ExitCode, stdout + "\n" + stderr);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(300_000)]
    public void ObfuscatedSource_CoreProjects_Build()
    {
        var tempDir = CopySourceToTemp();
        try
        {
            ObfuscateSource(tempDir);

            // Build the core library projects that don't need
            // generated config files. These are the projects
            // where obfuscator bugs have historically caused
            // build failures.
            string[] coreProjects =
            [
                "Agent.Models",
                "Agent.Managers.Reflection",
            ];

            foreach (var project in coreProjects)
            {
                var (exitCode, output) = DotnetBuild(
                    tempDir, project);
                Assert.AreEqual(
                    0, exitCode,
                    $"Build of obfuscated {project} failed:\n"
                    + ExtractErrors(output));
            }
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(2_100_000)]
    public void ObfuscatedSource_AthenaCoreWithPlugins_Builds()
    {
        var tempDir = CopySourceToTemp();
        try
        {
            ObfuscateSource(tempDir);

            var (exitCode, output) = DotnetBuild(
                tempDir,
                "AthenaCore",
                " -p:LinuxTest=True -p:HandlerOS=linux");

            Assert.AreEqual(
                0, exitCode,
                "Build of obfuscated AthenaCore with plugins "
                + "failed:\n" + ExtractErrors(output));
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static string ExtractErrors(string output)
    {
        var lines = output.Split('\n');
        var errors = lines
            .Where(l => l.Contains(": error "))
            .Take(20);
        var summary = string.Join("\n", errors);
        return string.IsNullOrEmpty(summary)
            ? output[..Math.Min(output.Length, 2000)]
            : summary;
    }

    private static void CopyDirectory(
        string source,
        string dest,
        string[] excludeDirs)
    {
        var excludeSet = new HashSet<string>(
            excludeDirs, StringComparer.OrdinalIgnoreCase);

        foreach (var dir in Directory.EnumerateDirectories(
            source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, dir);
            var topDir = relative.Split(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)[0];

            if (excludeSet.Contains(topDir))
                continue;

            Directory.CreateDirectory(
                Path.Combine(dest, relative));
        }

        foreach (var file in Directory.EnumerateFiles(
            source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var topDir = relative.Split(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)[0];

            if (excludeSet.Contains(topDir))
                continue;

            var destFile = Path.Combine(dest, relative);
            Directory.CreateDirectory(
                Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite: true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
