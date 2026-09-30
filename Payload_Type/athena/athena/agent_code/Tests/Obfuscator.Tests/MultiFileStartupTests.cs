using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.IL;

namespace Obfuscator.Tests;

[TestClass]
public class MultiFileStartupTests
{
    private const string DotNet = "/tmp/dotnet10/dotnet";

    [TestMethod]
    public void RewriteBatch_PhysicalRenameRequiresOneCompleteEntryManifestBeforeMutation()
    {
        var dir = CreateTempDir();
        try
        {
            var dll = Path.Combine(dir, "Entry.dll");
            File.Copy(typeof(MultiFileStartupTests).Assembly.Location, dll);
            File.WriteAllText(Path.Combine(dir, "Entry.runtimeconfig.json"), "{}");
            var original = File.ReadAllBytes(dll);

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new ILRewriter().RewriteBatch(dir, 42, null, ["Entry"]));

            CollectionAssert.AreEqual(original, File.ReadAllBytes(dll));
            Assert.AreEqual(2, Directory.GetFiles(dir).Length);
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [TestMethod]
    public void RewriteBatch_PhysicalRenameRejectsAmbiguousDepsBeforeMutation()
    {
        var dir = CreateTempDir();
        try
        {
            var dll = Path.Combine(dir, "Entry.dll");
            File.Copy(typeof(MultiFileStartupTests).Assembly.Location, dll);
            File.WriteAllText(Path.Combine(dir, "Entry.runtimeconfig.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "Entry.deps.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "Other.deps.json"), "{}");
            var original = File.ReadAllBytes(dll);

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new ILRewriter().RewriteBatch(dir, 42, null, ["Entry"]));

            CollectionAssert.AreEqual(original, File.ReadAllBytes(dll));
            Assert.IsTrue(File.Exists(dll));
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [TestMethod]
    public void RewriteBatch_MultiFileNet10ApphostStartsAfterPhysicalRename()
    {
        Assert.IsTrue(File.Exists(DotNet), $"Required SDK was not found at {DotNet}");
        var dir = CreateTempDir();
        try
        {
            WriteProject(
                Path.Combine(dir, "Renci"),
                "Renci.SshNet",
                null,
                "namespace Renci.SshNet; public static class Marker { public static string Value => \"renci\"; }");
            WriteProject(
                Path.Combine(dir, "FirstParty"),
                "FirstParty",
                "../Renci/Renci.SshNet.csproj",
                "namespace FirstParty; public static class Marker { public static string Value => \"first-party|\" + Renci.SshNet.Marker.Value; }");
            WriteProject(
                Path.Combine(dir, "Entry"),
                "Entry",
                "../FirstParty/FirstParty.csproj",
                "Console.WriteLine(FirstParty.Marker.Value);");

            var build = Run(DotNet, dir,
                "build", "Entry/Entry.csproj", "-c", "Release", "--nologo", "-v:q",
                "-p:UseSharedCompilation=false");
            Assert.AreEqual(0, build.ExitCode, build.Combined);

            var output = Path.Combine(dir, "Entry", "bin", "Release", "net10.0");
            var entryDll = Path.Combine(output, "Entry.dll");
            var entryRuntimeConfig = Path.Combine(output, "Entry.runtimeconfig.json");
            var entryDeps = Path.Combine(output, "Entry.deps.json");
            var apphost = Path.Combine(output, "Entry");
            Assert.IsTrue(File.Exists(entryDll));
            Assert.IsTrue(File.Exists(entryRuntimeConfig));
            Assert.IsTrue(File.Exists(entryDeps));
            Assert.IsTrue(File.Exists(apphost));
            Assert.IsTrue(File.Exists(Path.Combine(output, "FirstParty.dll")));
            Assert.IsTrue(File.Exists(Path.Combine(output, "Renci.SshNet.dll")));
            var renciBytes = File.ReadAllBytes(Path.Combine(output, "Renci.SshNet.dll"));

            new ILRewriter().RewriteBatch(
                output, seed: 42, mapPath: null,
                firstPartyAssemblyNames: ["Entry", "FirstParty"]);

            Assert.IsTrue(File.Exists(entryDll), "Entry DLL filename must be preserved.");
            Assert.IsTrue(File.Exists(entryRuntimeConfig), "Entry runtimeconfig must be preserved.");
            Assert.IsTrue(File.Exists(entryDeps), "Entry deps manifest must be preserved.");
            Assert.IsTrue(File.Exists(apphost), "Entry apphost must be preserved.");
            Assert.IsFalse(File.Exists(Path.Combine(output, "FirstParty.dll")),
                "First-party dependency must be physically renamed.");
            Assert.IsTrue(Directory.GetFiles(output, "_*.dll").Length > 0,
                "A renamed first-party assembly must exist.");
            Assert.IsTrue(File.Exists(Path.Combine(output, "Renci.SshNet.dll")),
                "Skipped third-party assembly must retain its filename.");
            CollectionAssert.AreEqual(
                renciBytes,
                File.ReadAllBytes(Path.Combine(output, "Renci.SshNet.dll")),
                "Third-party assembly bytes must remain unchanged.");

            var launched = Run(apphost, output);
            Assert.AreEqual(0, launched.ExitCode, launched.Combined);
            Assert.AreEqual("first-party|renci\n", launched.StandardOutput.Replace("\r\n", "\n"));
            Assert.AreEqual(string.Empty, launched.StandardError);
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [TestMethod]
    public void Run_DrainsStandardOutputAndStandardErrorConcurrently()
    {
        var result = Run(
            "/bin/sh",
            Path.GetTempPath(),
            "-c",
            "i=0; while [ $i -lt 20000 ]; do printf 'stderr-line-%s\\n' \"$i\"; i=$((i+1)); done >&2; printf 'stdout-complete\\n'");

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        Assert.AreEqual("stdout-complete\n", result.StandardOutput.Replace("\r\n", "\n"));
        StringAssert.Contains(result.StandardError, "stderr-line-19999");
    }

    private static void WriteProject(
        string directory,
        string assemblyName,
        string? projectReference,
        string source)
    {
        Directory.CreateDirectory(directory);
        var reference = projectReference is null
            ? string.Empty
            : $"<ItemGroup><ProjectReference Include=\"{projectReference}\" /></ItemGroup>";
        File.WriteAllText(Path.Combine(directory, assemblyName + ".csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>{(assemblyName == "Entry" ? "Exe" : "Library")}</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>{assemblyName}</AssemblyName>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              {reference}
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Program.cs"), source);
    }

    private static ProcessResult Run(string executable, string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["DOTNET_ROOT"] = "/tmp/dotnet10";
        start.Environment["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1";
        // Build servers can outlive `dotnet build` while retaining inherited
        // redirected pipe handles. Disable reuse so stream-drain completion
        // tracks the process launched by this test rather than a server child.
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = Process.Start(start)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            string termination;
            try
            {
                process.Kill(entireProcessTree: true);
                termination = process.WaitForExit(10_000)
                    ? "Process tree was killed."
                    : "Process tree did not exit within 10 seconds after Kill.";
            }
            catch (Exception ex)
            {
                termination = $"Killing the process tree failed: {ex}";
            }

            var streamsTask = Task.WhenAll(stdoutTask, stderrTask);
            var streamsClosed = Task.WhenAny(streamsTask, Task.Delay(10_000)).GetAwaiter().GetResult() == streamsTask;
            var timeoutStdout = CompletedOutput(stdoutTask, "stdout", streamsClosed);
            var timeoutStderr = CompletedOutput(stderrTask, "stderr", streamsClosed);
            throw new AssertFailedException(
                $"Process timed out after 120 seconds: {executable} {string.Join(' ', arguments)}\n" +
                $"{termination}\nStandard output:\n{timeoutStdout}\nStandard error:\n{timeoutStderr}");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static string CompletedOutput(Task<string> outputTask, string streamName, bool streamsClosed)
    {
        if (outputTask.IsCompletedSuccessfully)
            return outputTask.Result;
        if (outputTask.IsFaulted)
            return $"<{streamName} read failed: {outputTask.Exception!.GetBaseException()}>";
        return streamsClosed
            ? $"<{streamName} read did not complete>"
            : $"<{streamName} did not close within 10 seconds after process termination>";
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "multifile_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDir(string path)
    {
        try { Directory.Delete(path, true); } catch { }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string Combined => StandardOutput + StandardError;
    }
}
