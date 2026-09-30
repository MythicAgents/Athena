using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.IL;

namespace Obfuscator.Tests;

[TestClass]
public class DepsJsonPatcherTests
{
    [TestMethod]
    public void Render_ReturnsPatchedBytesWithoutMutatingSourceBytes()
    {
        var original = """
        { "targets": { "t": {
            "Alpha/1.0.0": { "runtime": { "Alpha.dll": {} } }
        } }, "libraries": { "Alpha/1.0.0": {} } }
        """u8.ToArray();

        var rendered = DepsJsonPatcher.Render(original, new Dictionary<string, string>
        {
            ["Alpha"] = "Renamed",
        });

        CollectionAssert.AreEqual("""
        { "targets": { "t": {
            "Alpha/1.0.0": { "runtime": { "Alpha.dll": {} } }
        } }, "libraries": { "Alpha/1.0.0": {} } }
        """u8.ToArray(), original);
        var root = JsonNode.Parse(rendered)!.AsObject();
        Assert.IsNotNull(root["targets"]!["t"]!["Renamed/1.0.0"]);
        Assert.IsNotNull(root["libraries"]!["Renamed/1.0.0"]);
    }

    [TestMethod]
    public void Patch_RewritesManagedGraphAndPreservesUnrelatedData()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "Entry.deps.json");
            File.WriteAllText(path, """
            {
              "runtimeTarget": { "name": ".NETCoreApp,Version=v10.0/linux-x64", "signature": "keep" },
              "compilationOptions": { "custom": 17 },
              "targets": {
                ".NETCoreApp,Version=v10.0/linux-x64": {
                  "Entry/1.0.0": {
                    "dependencies": { "FirstParty": "1.0.0", "Renci.SshNet": "2024.0.0" },
                    "runtime": {
                      "Entry.dll": { "assemblyVersion": "1.0.0.0" },
                      "runtimes/linux/lib/net10.0/FirstParty.dll": { "fileVersion": "2.0.0.0" },
                      "Renci.SshNet.dll": {}
                    },
                    "runtimeTargets": {
                      "runtimes/linux-x64/lib/net10.0/FirstParty.dll": { "rid": "linux-x64", "assetType": "runtime", "custom": true },
                      "runtimes/linux-x64/native/FirstParty.dll": { "rid": "linux-x64", "assetType": "native" },
                      "fr/FirstParty.resources.dll": { "rid": "linux-x64", "assetType": "resources" }
                    },
                    "native": { "FirstParty.dll": { "keep": true } },
                    "resources": { "fr/FirstParty.resources.dll": { "locale": "fr" } },
                    "unknown": { "FirstParty.dll": "untouched" }
                  },
                  "FirstParty/1.0.0": {
                    "dependencies": { "FirstParty": "1.0.0" },
                    "runtime": { "FirstParty.dll": { "assemblyVersion": "1.0.0.0" } },
                    "compile": { "ref/net10.0/FirstParty.dll": {} },
                    "custom": "keep"
                  },
                  "Renci.SshNet/2024.0.0": { "runtime": { "Renci.SshNet.dll": {} } }
                }
              },
              "libraries": {
                "Entry/1.0.0": { "type": "project", "serviceable": false, "sha512": "entry-hash" },
                "FirstParty/1.0.0": { "type": "project", "serviceable": true, "sha512": "first-hash", "path": "firstparty/1.0.0", "hashPath": "firstparty.1.0.0.nupkg.sha512", "custom": 42 },
                "Renci.SshNet/2024.0.0": { "type": "package", "serviceable": true, "sha512": "third-party-hash", "path": "renci.sshnet/2024.0.0", "hashPath": "renci.sshnet.2024.0.0.nupkg.sha512" }
              },
              "unknownTopLevel": { "FirstParty": "untouched" }
            }
            """);

            DepsJsonPatcher.Patch(path, new Dictionary<string, string>
            {
                ["FirstParty"] = "_Renamed",
            });

            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.AreEqual(".NETCoreApp,Version=v10.0/linux-x64", root["runtimeTarget"]!["name"]!.GetValue<string>());
            Assert.AreEqual("keep", root["runtimeTarget"]!["signature"]!.GetValue<string>());
            Assert.AreEqual(17, root["compilationOptions"]!["custom"]!.GetValue<int>());

            var target = root["targets"]![".NETCoreApp,Version=v10.0/linux-x64"]!.AsObject();
            Assert.IsFalse(target.ContainsKey("FirstParty/1.0.0"));
            Assert.IsTrue(target.ContainsKey("_Renamed/1.0.0"));
            Assert.AreEqual("keep", target["_Renamed/1.0.0"]!["custom"]!.GetValue<string>());
            Assert.AreEqual("1.0.0", target["Entry/1.0.0"]!["dependencies"]!["_Renamed"]!.GetValue<string>());
            Assert.IsNull(target["Entry/1.0.0"]!["dependencies"]!["FirstParty"]);
            Assert.AreEqual("1.0.0", target["_Renamed/1.0.0"]!["dependencies"]!["_Renamed"]!.GetValue<string>());

            var entry = target["Entry/1.0.0"]!;
            Assert.IsNotNull(entry["runtime"]!["runtimes/linux/lib/net10.0/_Renamed.dll"]);
            Assert.IsNotNull(entry["runtime"]!["Entry.dll"]);
            Assert.IsNotNull(entry["runtime"]!["Renci.SshNet.dll"]);
            Assert.IsNotNull(entry["runtimeTargets"]!["runtimes/linux-x64/lib/net10.0/_Renamed.dll"]);
            Assert.IsNotNull(entry["runtimeTargets"]!["runtimes/linux-x64/native/FirstParty.dll"]);
            Assert.IsNotNull(entry["runtimeTargets"]!["fr/FirstParty.resources.dll"]);
            Assert.IsNotNull(entry["native"]!["FirstParty.dll"]);
            Assert.IsNotNull(entry["resources"]!["fr/FirstParty.resources.dll"]);
            Assert.AreEqual("untouched", entry["unknown"]!["FirstParty.dll"]!.GetValue<string>());
            Assert.IsNotNull(target["_Renamed/1.0.0"]!["compile"]!["ref/net10.0/FirstParty.dll"]);

            var libraries = root["libraries"]!.AsObject();
            Assert.IsFalse(libraries.ContainsKey("FirstParty/1.0.0"));
            var library = libraries["_Renamed/1.0.0"]!;
            Assert.AreEqual(true, library["serviceable"]!.GetValue<bool>());
            Assert.AreEqual("first-hash", library["sha512"]!.GetValue<string>());
            Assert.AreEqual("firstparty/1.0.0", library["path"]!.GetValue<string>());
            Assert.AreEqual("firstparty.1.0.0.nupkg.sha512", library["hashPath"]!.GetValue<string>());
            Assert.AreEqual(42, library["custom"]!.GetValue<int>());
            Assert.IsNotNull(libraries["Renci.SshNet/2024.0.0"]);
            Assert.AreEqual("untouched", root["unknownTopLevel"]!["FirstParty"]!.GetValue<string>());
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [TestMethod]
    public void Patch_RejectsDuplicateDestinationsWithoutChangingFile()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "Entry.deps.json");
            var original = """
            { "targets": { "t": {
                "Alpha/1.0.0": { "runtime": { "Alpha.dll": {} } },
                "Beta/1.0.0": { "runtime": { "Beta.dll": {} } }
            } }, "libraries": {
                "Alpha/1.0.0": { "type": "project" },
                "Beta/1.0.0": { "type": "project" }
            } }
            """;
            File.WriteAllText(path, original);
            var originalBytes = File.ReadAllBytes(path);

            Assert.ThrowsExactly<InvalidDataException>(() =>
                DepsJsonPatcher.Patch(path, new Dictionary<string, string>
                {
                    ["Alpha"] = "Same",
                    ["Beta"] = "Same",
                }));

            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(path));
            CollectionAssert.AreEquivalent(new[] { "Entry.deps.json" },
                Directory.GetFiles(dir).Select(Path.GetFileName).ToArray());
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [TestMethod]
    public void Patch_RejectsExistingPropertyCollisionWithoutChangingFile()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "Entry.deps.json");
            File.WriteAllText(path, """
            { "targets": { "t": {
                "Alpha/1.0.0": { "runtime": { "Alpha.dll": {}, "Taken.dll": {} } },
                "Taken/1.0.0": { "runtime": { "Taken.dll": {} } }
            } }, "libraries": {
                "Alpha/1.0.0": {}, "Taken/1.0.0": {}
            } }
            """);
            var originalBytes = File.ReadAllBytes(path);

            Assert.ThrowsExactly<InvalidDataException>(() =>
                DepsJsonPatcher.Patch(path, new Dictionary<string, string>
                {
                    ["Alpha"] = "Taken",
                }));

            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(path));
            CollectionAssert.AreEquivalent(new[] { "Entry.deps.json" },
                Directory.GetFiles(dir).Select(Path.GetFileName).ToArray());
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [TestMethod]
    public void Patch_MalformedJsonLeavesOriginalBytesAndNoTempFile()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "Entry.deps.json");
            var originalBytes = "{ malformed"u8.ToArray();
            File.WriteAllBytes(path, originalBytes);

            Assert.Throws<System.Text.Json.JsonException>(() =>
                DepsJsonPatcher.Patch(path, new Dictionary<string, string>
                {
                    ["Alpha"] = "Renamed",
                }));

            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(path));
            CollectionAssert.AreEquivalent(new[] { "Entry.deps.json" },
                Directory.GetFiles(dir).Select(Path.GetFileName).ToArray());
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "depspatch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDir(string path)
    {
        try { Directory.Delete(path, true); } catch { }
    }
}
