using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Agent.Interfaces;
using Agent.Managers;
using Agent.Models;
using Agent.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.Config;
using Obfuscator.Source;

namespace PluginContract.Tests;

[TestClass]
public sealed class PluginContractLoadingTests
{
    private const string PayloadUuid = "37eb846a-12b9-45d5-a49c-8e10754cc0ba";
    private const string OtherUuid = "8a11ffb7-d012-4bca-b55d-d84a67e32110";

    [TestMethod]
    public void Fingerprint_NormalizesUuidBeforeHashing()
    {
        Assert.AreEqual(
            "6f1002bf3deabf006a9caff07d53d12a8ebcd92dfcf60adb8ba0b0ac844e627b",
            PluginContractFingerprint.Derive("{37EB846A-12B9-45D5-A49C-8E10754CC0BA}"));
    }

    [TestMethod]
    public void LoadPlugin_MatchingContractFingerprint_LoadsPlugin()
    {
        byte[] plugin = CompilePlugin(PayloadUuid);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);
        int initialCount = manager.LoadContextAssemblyCount;

        bool loaded = manager.LoadPluginAsync("matching", "contract-fixture", plugin);

        Assert.IsTrue(loaded);
        Assert.AreEqual(initialCount + 1, manager.LoadContextAssemblyCount);
        Assert.IsTrue(manager.TryGetPlugin<IPlugin>("contract-fixture", out _));
    }

    [TestMethod]
    public void LoadPlugin_DifferentContractFingerprint_RejectsBeforeTypeActivation()
    {
        string marker = Path.Combine(Path.GetTempPath(), $"athena-contract-{Guid.NewGuid():N}");
        byte[] plugin = CompilePlugin(OtherUuid, marker);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);
        int initialAssemblyCount = manager.LoadContextAssemblyCount;

        for (int attempt = 0; attempt < 3; attempt++)
            Assert.IsFalse(manager.LoadPluginAsync($"mismatch-{attempt}", $"contract-fixture-{attempt}", plugin));

        Assert.AreEqual(initialAssemblyCount, manager.LoadContextAssemblyCount);
        Assert.IsFalse(File.Exists(marker), "plugin constructor must not run on mismatch");
        Assert.IsTrue(((RecordingMessageProxy)(object)messages).Responses.All(
            response => response.user_output.StartsWith("Plugin contract mismatch", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void LoadPlugin_MissingFingerprint_IsAllowedWhenPayloadObfuscationIsOff()
    {
        byte[] plugin = CompilePlugin(null);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: false);

        Assert.IsTrue(manager.LoadPluginAsync("legacy", "contract-fixture", plugin));
    }

    [TestMethod]
    public void LoadPlugin_MismatchedFingerprint_IsRejectedWhenFingerprintIsOptional()
    {
        byte[] plugin = CompilePlugin(OtherUuid);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: false);
        int initialCount = manager.LoadContextAssemblyCount;

        Assert.IsFalse(manager.LoadPluginAsync("legacy-mismatch", "contract-fixture", plugin));

        Assert.AreEqual(initialCount, manager.LoadContextAssemblyCount);
        StringAssert.StartsWith(
            ((RecordingMessageProxy)(object)messages).Responses.Single().user_output,
            "Plugin contract mismatch");
    }

    [TestMethod]
    public void LoadPlugin_MissingFingerprint_IsRejectedWhenPayloadObfuscationIsOn()
    {
        byte[] plugin = CompilePlugin(null);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);

        Assert.IsFalse(manager.LoadPluginAsync("missing", "contract-fixture", plugin));
        StringAssert.Contains(
            ((RecordingMessageProxy)(object)messages).Responses.Single().user_output,
            "contract mismatch");
    }

    [TestMethod]
    public void LoadPlugin_DuplicateName_IsRejectedBeforeInputIsLoaded()
    {
        byte[] plugin = CompilePlugin(PayloadUuid);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);

        Assert.IsTrue(manager.LoadPluginAsync("first", "contract-fixture", plugin));
        int countAfterFirstLoad = manager.LoadContextAssemblyCount;
        for (int attempt = 0; attempt < 3; attempt++)
            Assert.IsFalse(manager.LoadPluginAsync(
                $"duplicate-{attempt}", "contract-fixture", [0x00]));

        Assert.AreEqual(countAfterFirstLoad, manager.LoadContextAssemblyCount);
        Assert.IsTrue(((RecordingMessageProxy)(object)messages).Responses.Skip(1).All(
            response => response.user_output == "Plugin already loaded."));
    }

    [TestMethod]
    public void LoadPlugin_AliasesWithSameEmbeddedName_RejectsSecondBeforePersistentLoad()
    {
        byte[] first = CompilePlugin(PayloadUuid, assemblyName: "AliasFixtureOne");
        byte[] second = CompilePlugin(PayloadUuid, assemblyName: "AliasFixtureTwo");
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);

        Assert.IsTrue(manager.LoadPluginAsync("first-alias", "alias-a", first));
        int countAfterFirstLoad = manager.LoadContextAssemblyCount;

        Assert.IsFalse(manager.LoadPluginAsync("second-alias", "alias-b", second));

        Assert.AreEqual(countAfterFirstLoad, manager.LoadContextAssemblyCount);
        Assert.IsTrue(manager.TryGetPlugin<IPlugin>("contract-fixture", out _));
        Assert.IsFalse(manager.TryGetPlugin<IPlugin>("alias-a", out _));
        Assert.AreEqual(
            "Plugin already loaded.",
            ((RecordingMessageProxy)(object)messages).Responses.Last().user_output);
    }

    [TestMethod]
    public void LoadPlugin_MatchingFingerprintWithoutPlugin_RejectsBeforePersistentLoad()
    {
        byte[] assembly = CompilePlugin(PayloadUuid, includePlugin: false);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);
        int initialCount = manager.LoadContextAssemblyCount;

        Assert.IsFalse(manager.LoadPluginAsync("no-plugin", "caller-alias", assembly));

        Assert.AreEqual(initialCount, manager.LoadContextAssemblyCount);
        StringAssert.StartsWith(
            ((RecordingMessageProxy)(object)messages).Responses.Single().user_output,
            "Plugin contract mismatch");
    }

    [TestMethod]
    public void LoadPlugin_OversizeAndBoundaryInputs_AreBoundedBeforeLoading()
    {
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);
        int initialCount = manager.LoadContextAssemblyCount;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.IsFalse(manager.LoadPluginAsync(
                $"oversize-{attempt}", $"oversize-{attempt}",
                new byte[AssemblyManager.MaxPluginAssemblyBytes + 1]));
            StringAssert.Contains(
                ((RecordingMessageProxy)(object)messages).Responses.Last().user_output,
                "maximum size");
        }
        Assert.IsFalse(manager.LoadPluginAsync(
            "boundary", "boundary", new byte[AssemblyManager.MaxPluginAssemblyBytes]));

        Assert.AreEqual(initialCount, manager.LoadContextAssemblyCount);
        Assert.IsFalse(((RecordingMessageProxy)(object)messages).Responses.Last().user_output
            .Contains("maximum size", StringComparison.Ordinal));
    }

    [TestMethod]
    public void LoadPlugin_MissingMalformedAndDuplicateMetadata_NeverReachLoadContext()
    {
        byte[] missing = CompilePlugin(null);
        byte[] malformed = CorruptMetadataProlog(CompilePlugin(PayloadUuid));
        byte[] duplicate = CompilePlugin(PayloadUuid, metadataCount: 2);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);
        int initialCount = manager.LoadContextAssemblyCount;

        foreach ((string name, byte[] bytes) in new[]
        {
            ("missing-repeat", missing),
            ("malformed-metadata-repeat", malformed),
            ("malformed-pe-repeat", new byte[] { 0x4d, 0x5a }),
            ("duplicate-repeat", duplicate),
        })
        {
            for (int attempt = 0; attempt < 3; attempt++)
                Assert.IsFalse(
                    manager.LoadPluginAsync($"{name}-{attempt}", $"{name}-{attempt}", bytes),
                    $"{name} attempt {attempt} unexpectedly loaded");
        }

        Assert.AreEqual(initialCount, manager.LoadContextAssemblyCount);
        Assert.IsTrue(((RecordingMessageProxy)(object)messages).Responses.All(
            response => response.user_output.StartsWith("Plugin contract mismatch", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void LoadPlugin_ConcurrentSameName_LoadsExactlyOneAssembly()
    {
        byte[] plugin = CompilePlugin(PayloadUuid);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);
        using var ready = new CountdownEvent(8);
        using var start = new ManualResetEventSlim();
        Task<bool>[] attempts = Enumerable.Range(0, 8).Select(index => Task.Factory.StartNew(() =>
        {
            ready.Signal();
            start.Wait();
            return manager.LoadPluginAsync($"race-{index}", "contract-fixture", plugin);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        ready.Wait();
        int initialCount = manager.LoadContextAssemblyCount;

        start.Set();
        Task.WaitAll(attempts);

        Assert.AreEqual(1, attempts.Count(attempt => attempt.Result));
        Assert.AreEqual(initialCount + 1, manager.LoadContextAssemblyCount);
    }

    [TestMethod]
    public void LoadPlugin_DecoyPublicNameWithDifferentExplicitName_RejectsBeforePersistentLoad()
    {
        byte[] plugin = CompilePlugin(PayloadUuid, pluginSource: """
            public sealed class ContractFixturePlugin : IPlugin
            {
                public string Name => "decoy-name";
                string IPlugin.Name => "actual-name";
                public ContractFixturePlugin(IMessageManager m, IAgentConfig c, ILogger l, ITokenManager t, ISpawner s, IPythonManager p) { }
                public Task Execute(ServerJob job) => Task.CompletedTask;
            }
            """);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);
        int initialCount = manager.LoadContextAssemblyCount;

        Assert.IsFalse(manager.LoadPluginAsync("explicit-decoy", "decoy-name", plugin));

        Assert.AreEqual(initialCount, manager.LoadContextAssemblyCount);
        StringAssert.StartsWith(
            ((RecordingMessageProxy)(object)messages).Responses.Single().user_output,
            "Plugin contract mismatch");
    }

    [TestMethod]
    public void LoadPlugin_ConcretePluginInheritingImplementation_LoadsSuccessfully()
    {
        byte[] plugin = CompilePlugin(PayloadUuid, pluginSource: """
            public abstract class ContractPluginBase : IPlugin
            {
                public string Name => "inherited-plugin";
                public Task Execute(ServerJob job) => Task.CompletedTask;
            }
            public sealed class InheritedPlugin : ContractPluginBase
            {
                public InheritedPlugin(IMessageManager m, IAgentConfig c, ILogger l, ITokenManager t, ISpawner s, IPythonManager p) { }
            }
            """);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);

        Assert.IsTrue(manager.LoadPluginAsync("inherited", "inherited-plugin", plugin));
        Assert.IsTrue(manager.TryGetPlugin<IPlugin>("inherited-plugin", out _));
    }

    [TestMethod]
    public void LoadPlugin_ExplicitNameImplementationAlone_LoadsSuccessfully()
    {
        byte[] plugin = CompilePlugin(PayloadUuid, pluginSource: """
            public sealed class ExplicitPlugin : IPlugin
            {
                string IPlugin.Name => "explicit-plugin";
                public ExplicitPlugin(IMessageManager m, IAgentConfig c, ILogger l, ITokenManager t, ISpawner s, IPythonManager p) { }
                public Task Execute(ServerJob job) => Task.CompletedTask;
            }
            """);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);

        Assert.IsTrue(manager.LoadPluginAsync("explicit", "explicit-plugin", plugin));
        Assert.IsTrue(manager.TryGetPlugin<IPlugin>("explicit-plugin", out _));
    }

    [DataTestMethod]
    [DataRow(false, "source-normal")]
    [DataRow(true, "source-explicit")]
    public void LoadPlugin_SourceObfuscatedCanonicalPlugin_LoadsThroughPreflightAndPersistentContext(
        bool explicitImplementation,
        string pluginName)
    {
        string nameProperty = explicitImplementation
            ? $"string IPlugin.Name => \"{pluginName}\";"
            : $"public string Name => \"{pluginName}\";";
        byte[] plugin = CompilePlugin(PayloadUuid,
            assemblyName: $"SourceObfuscated{(explicitImplementation ? "Explicit" : "Normal")}",
            sourceObfuscate: true,
            pluginSource: $$"""
                public sealed class SourceObfuscatedPlugin : IPlugin
                {
                    {{nameProperty}}
                    public string Other => "encrypt-source-plugin";
                    public SourceObfuscatedPlugin(IMessageManager m, IAgentConfig c, ILogger l, ITokenManager t, ISpawner s, IPythonManager p) { }
                    public Task Execute(ServerJob job) => Task.CompletedTask;
                }
                """);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);
        int initialCount = manager.LoadContextAssemblyCount;

        Assert.IsTrue(manager.LoadPluginAsync("source-obfuscated", pluginName, plugin));

        Assert.AreEqual(initialCount + 1, manager.LoadContextAssemblyCount);
        Assert.IsTrue(manager.TryGetPlugin<IPlugin>(pluginName, out _));
    }

    [TestMethod]
    public void LoadPlugin_MultipleConcreteImplementations_FailsClosedBeforePersistentLoad()
    {
        byte[] plugin = CompilePlugin(PayloadUuid, pluginSource: """
            public abstract class PluginBase : IPlugin
            {
                public abstract string Name { get; }
                public Task Execute(ServerJob job) => Task.CompletedTask;
            }
            public sealed class FirstPlugin : PluginBase
            {
                public override string Name => "first-plugin";
                public FirstPlugin(IMessageManager m, IAgentConfig c, ILogger l, ITokenManager t, ISpawner s, IPythonManager p) { }
            }
            public sealed class SecondPlugin : PluginBase
            {
                public override string Name => "second-plugin";
                public SecondPlugin(IMessageManager m, IAgentConfig c, ILogger l, ITokenManager t, ISpawner s, IPythonManager p) { }
            }
            """);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);
        int initialCount = manager.LoadContextAssemblyCount;

        Assert.IsFalse(manager.LoadPluginAsync("multiple", "first-plugin", plugin));

        Assert.AreEqual(initialCount, manager.LoadContextAssemblyCount);
        StringAssert.StartsWith(
            ((RecordingMessageProxy)(object)messages).Responses.Single().user_output,
            "Plugin contract mismatch");
    }

    [TestMethod]
    public async Task LoadPlugin_RepeatedConstructorFailures_DoNotRetainContextsOrAssemblies()
    {
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);
        int initialAssemblyCount = manager.LoadContextAssemblyCount;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            string pluginName = $"throwing-plugin-{attempt}";
            byte[] plugin = CompilePlugin(PayloadUuid,
                assemblyName: $"ThrowingFixture{attempt}",
                pluginSource: $$"""
                    public sealed class ThrowingPlugin : IPlugin
                    {
                        public string Name => "{{pluginName}}";
                        public ThrowingPlugin(IMessageManager m, IAgentConfig c, ILogger l, ITokenManager t, ISpawner s, IPythonManager p)
                            => throw new InvalidOperationException("constructor-failure-{{attempt}}");
                        public Task Execute(ServerJob job) => Task.CompletedTask;
                    }
                    """);

            Assert.IsFalse(manager.LoadPluginAsync($"throw-{attempt}", pluginName, plugin));
            StringAssert.Contains(
                ((RecordingMessageProxy)(object)messages).Responses.Last().user_output,
                $"constructor-failure-{attempt}");
            Assert.AreEqual(initialAssemblyCount, manager.LoadContextAssemblyCount);
            Assert.AreEqual(0, manager.PluginLoadContextCount);
        }

        string marker = Path.Combine(Path.GetTempPath(), $"athena-success-{Guid.NewGuid():N}");
        try
        {
            byte[] success = CompilePlugin(PayloadUuid,
                assemblyName: "SuccessAfterFailures",
                pluginSource: $$"""
                    public sealed class SuccessPlugin : IPlugin
                    {
                        public string Name => "success-after-failures";
                        public SuccessPlugin(IMessageManager m, IAgentConfig c, ILogger l, ITokenManager t, ISpawner s, IPythonManager p) { }
                        public Task Execute(ServerJob job)
                        {
                            File.WriteAllText(@"{{marker}}", "called");
                            return Task.CompletedTask;
                        }
                    }
                    """);

            Assert.IsTrue(manager.LoadPluginAsync("success", "success-after-failures", success));
            Assert.AreEqual(initialAssemblyCount + 1, manager.LoadContextAssemblyCount);
            Assert.AreEqual(1, manager.PluginLoadContextCount);
            Assert.IsTrue(manager.TryGetPlugin<IPlugin>("success-after-failures", out IPlugin? loaded));
            await loaded!.Execute(null!);
            Assert.AreEqual("called", File.ReadAllText(marker));
        }
        finally
        {
            File.Delete(marker);
        }
    }

    [TestMethod]
    public void LoadPlugin_UnresolvedDependency_DoesNotRetainContextOrAssembly()
    {
        byte[] plugin = CompilePlugin(PayloadUuid,
            assemblyName: "MissingTypeFixture",
            dependencySource: "public class MissingBase { public static void Touch() { } }",
            pluginSource: """
                public sealed class ContractFixturePlugin : IPlugin
                {
                    public string Name => "missing-type-plugin";
                    public ContractFixturePlugin(IMessageManager m, IAgentConfig c, ILogger l, ITokenManager t, ISpawner s, IPythonManager p)
                        => MissingBase.Touch();
                    public Task Execute(ServerJob job) => Task.CompletedTask;
                }
                public sealed class UnloadableType : MissingBase { }
                """);
        var messages = DispatchProxy.Create<IMessageManager, RecordingMessageProxy>();
        var manager = CreateManager(messages, PayloadUuid, requireFingerprint: true);
        int initialAssemblyCount = manager.LoadContextAssemblyCount;

        Assert.IsFalse(manager.LoadPluginAsync("missing-type", "missing-type-plugin", plugin));

        Assert.AreEqual(initialAssemblyCount, manager.LoadContextAssemblyCount);
        Assert.AreEqual(0, manager.PluginLoadContextCount);
        StringAssert.Contains(
            ((RecordingMessageProxy)(object)messages).Responses.Single().user_output,
            "MissingFixtureDependency");
    }

    private static AssemblyManager CreateManager(
        IMessageManager messages, string uuid, bool requireFingerprint)
    {
        return new AssemblyManager(
            messages,
            DispatchProxy.Create<ILogger, EmptyProxy>(),
            new ContractAgentConfig(uuid, requireFingerprint),
            DispatchProxy.Create<ITokenManager, EmptyProxy>(),
            DispatchProxy.Create<ISpawner, EmptyProxy>(),
            DispatchProxy.Create<IPythonManager, EmptyProxy>());
    }

    private static byte[] CompilePlugin(
        string? contractUuid,
        string? marker = null,
        int metadataCount = 1,
        string assemblyName = "fixture",
        bool includePlugin = true,
        string? pluginSource = null,
        bool sourceObfuscate = false,
        string? dependencySource = null)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"athena-plugin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string models = typeof(IPlugin).Assembly.Location;
            string dependencyReference = dependencySource is null
                ? ""
                : "<ProjectReference Include=\"Dependency/Dependency.csproj\" />";
            File.WriteAllText(Path.Combine(directory, "fixture.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <AssemblyName>{{assemblyName}}</AssemblyName>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include="Agent.Models"><HintPath>{{models}}</HintPath></Reference>
                    {{dependencyReference}}
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Remove="Agent.Models/**/*.cs" />
                    <Compile Remove="Dependency/**/*.cs" />
                  </ItemGroup>
                </Project>
                """);
            if (dependencySource is not null)
            {
                string dependencyDirectory = Path.Combine(directory, "Dependency");
                Directory.CreateDirectory(dependencyDirectory);
                File.WriteAllText(Path.Combine(dependencyDirectory, "Dependency.csproj"), """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net10.0</TargetFramework>
                        <AssemblyName>MissingFixtureDependency</AssemblyName>
                      </PropertyGroup>
                    </Project>
                    """);
                File.WriteAllText(Path.Combine(dependencyDirectory, "Dependency.cs"), dependencySource);
            }
            string metadata = contractUuid is null
                ? ""
                : string.Join(
                    Environment.NewLine,
                    Enumerable.Range(0, metadataCount).Select(index =>
                        $"[assembly: AssemblyMetadata(PluginContractFingerprint.MetadataKey, \"{PluginContractFingerprint.Derive(index == 0 ? contractUuid : OtherUuid)}\")]"));
            string constructorEffect = marker is null
                ? ""
                : $"File.WriteAllText(@\"{marker}\", \"activated\");";
            string pluginType = pluginSource ?? (includePlugin ? $$"""
                public sealed class ContractFixturePlugin : IPlugin
                {
                    public string Name => "contract-fixture";
                    public ContractFixturePlugin(IMessageManager m, IAgentConfig c, ILogger l, ITokenManager t, ISpawner s, IPythonManager p)
                    {
                        {{constructorEffect}}
                    }
                    public Task Execute(ServerJob job) => Task.CompletedTask;
                }
                """ : "public sealed class NotAPlugin { }");
            File.WriteAllText(Path.Combine(directory, "Plugin.cs"), $$"""
                using System.Reflection;
                using Agent.Interfaces;
                using Agent.Models;
                using Agent.Utilities;
                {{metadata}}
                {{pluginType}}
                """);
            if (sourceObfuscate)
            {
                string contractDirectory = Path.Combine(directory, "Agent.Models");
                Directory.CreateDirectory(contractDirectory);
                File.WriteAllText(Path.Combine(contractDirectory, "IPlugin.cs"), """
                    using System.Threading.Tasks;
                    namespace Agent.Models { public sealed class ServerJob { } }
                    namespace Agent.Interfaces
                    {
                        using Agent.Models;
                        public interface IPlugin
                        {
                            string Name { get; }
                            Task Execute(ServerJob job);
                        }
                    }
                    """);
                new SourceRewriter().Rewrite(new ObfuscationConfig(
                    Seed: 42,
                    Uuid: null,
                    InputPath: directory,
                    OutputPath: directory,
                    MapPath: null));
                string rewrittenSource = File.ReadAllText(Path.Combine(directory, "Plugin.cs"));
                Assert.IsFalse(
                    rewrittenSource.Contains("\"encrypt-source-plugin\"", StringComparison.Ordinal),
                    rewrittenSource);
            }
            var start = new ProcessStartInfo("dotnet", "build -c Release --nologo")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(start)!;
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, stdout + stderr);
            return File.ReadAllBytes(Path.Combine(directory, "bin", "Release", "net10.0", $"{assemblyName}.dll"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] CorruptMetadataProlog(byte[] assembly)
    {
        byte[] result = assembly.ToArray();
        byte[] key = System.Text.Encoding.UTF8.GetBytes(PluginContractFingerprint.MetadataKey);
        int keyOffset = result.AsSpan().IndexOf(key);
        Assert.IsTrue(keyOffset >= 3, "Metadata key was not found in fixture assembly.");
        result[keyOffset - 3] = 0xff;
        return result;
    }

    private sealed class ContractAgentConfig(string uuidValue, bool requireFingerprint) : IAgentConfig
    {
        public int chunk_size { get; set; }
        public string? uuid { get; set; } = uuidValue;
        public string build_uuid { get; } = uuidValue;
        public bool require_plugin_contract_fingerprint { get; } = requireFingerprint;
        public int sleep { get; set; }
        public int jitter { get; set; }
        public string? psk { get; set; }
        public bool prettyOutput { get; set; }
        public bool debug { get; set; }
        public int inject { get; set; }
        public DateTime killDate { get; set; }
        public event EventHandler? SetAgentConfigUpdated { add { } remove { } }
    }

    private class EmptyProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType == typeof(Task) ? Task.CompletedTask :
            targetMethod?.ReturnType.IsValueType == true ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }

    private class RecordingMessageProxy : DispatchProxy
    {
        public List<ITaskResponse> Responses { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IMessageManager.AddTaskResponse)
                && args is [{ } response, ..]
                && response is ITaskResponse typed)
                Responses.Add(typed);
            if (targetMethod?.ReturnType == typeof(void))
                return null;
            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
