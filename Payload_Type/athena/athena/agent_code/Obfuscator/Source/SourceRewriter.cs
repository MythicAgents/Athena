using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Obfuscator.Config;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Source;

public sealed class SourceRewriter
{
    private static readonly char[] AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    public void Rewrite(ObfuscationConfig config)
    {
        var outputDir = config.OutputPath;

        if (!config.InputPath.Equals(config.OutputPath, StringComparison.OrdinalIgnoreCase))
        {
            CopyDirectory(config.InputPath, config.OutputPath);
        }

        var (decryptorNs, decryptorClass, decryptorMethod,
             callerNs, callerClass, callerMethod) = GenerateHelperNames(config.Seed);

        var decryptorReplacements = new Dictionary<string, string>
        {
            ["__OBFS_NS__"] = decryptorNs,
            ["__OBFS_CLASS__"] = decryptorClass,
            ["__OBFS_METHOD__"] = decryptorMethod,
        };

        var callerReplacements = new Dictionary<string, string>
        {
            ["__OBFS_NS__"] = callerNs,
            ["__OBFS_CALLER_CLASS__"] = callerClass,
            ["__OBFS_INVOKE_METHOD__"] = callerMethod,
        };

        var projectDirs = Directory.EnumerateFiles(
                outputDir, "*.csproj", SearchOption.AllDirectories)
            .Select(f => Path.GetDirectoryName(f) ?? outputDir)
            .Distinct()
            .ToList();

        var generatedFiles = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var projDir in projectDirs)
        {
            var decPath = Path.Combine(
                projDir, "_generated_decryptor.cs");
            var calPath = Path.Combine(
                projDir, "_generated_caller.cs");

            InjectRuntimeHelper(
                "StringDecryptor.cs", decPath,
                decryptorReplacements);
            InjectRuntimeHelper(
                "IndirectCaller.cs", calPath,
                callerReplacements);

            generatedFiles.Add(decPath);
            generatedFiles.Add(calPath);
        }

        UuidRenameMap? uuidMap = null;
        if (config.Uuid is not null)
        {
            var contractsDir = Path.Combine(
                outputDir, "Workflow.Models");
            var names = Directory.Exists(contractsDir)
                ? ContractScanner.Scan(contractsDir)
                : new ContractNames([], [], [], [], []);
            uuidMap = UuidRenameMap.Derive(config.Uuid, names);
        }

        var excludedPrefixes = new[]
        {
            Path.Combine(outputDir, "Tests"),
            Path.Combine(outputDir, "Obfuscator"),
        };

        foreach (var file in Directory.EnumerateFiles(
            outputDir, "*.cs", SearchOption.AllDirectories))
        {
            if (generatedFiles.Contains(file))
                continue;

            if (excludedPrefixes.Any(p => file.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            ApplyTransforms(
                file,
                config.Seed,
                uuidMap,
                decryptorNs, decryptorClass, decryptorMethod,
                callerNs, callerClass, callerMethod);
        }

        if (config.MapPath is not null)
        {
            WriteDeobfuscationMap(
                config,
                decryptorNs, decryptorClass, decryptorMethod,
                callerNs, callerClass, callerMethod,
                uuidMap);
        }
    }

    private static void ApplyTransforms(
        string filePath,
        int seed,
        UuidRenameMap? uuidMap,
        string decryptorNs, string decryptorClass, string decryptorMethod,
        string callerNs, string callerClass, string callerMethod)
    {
        var source = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(source);

        if (uuidMap is not null)
        {
            var uuidTransform = new UuidRenameTransform(uuidMap);
            tree = uuidTransform.Rewrite(tree);
        }

        var apiTransform = new ApiCallHidingTransform(
            callerClass, callerMethod, callerNs, seed);
        tree = apiTransform.Rewrite(tree);

        var strTransform = new StringEncryptionTransform(
            decryptorClass, decryptorMethod, decryptorNs, seed);
        tree = strTransform.Rewrite(tree);

        File.WriteAllText(filePath, tree.GetRoot().ToFullString(), Encoding.UTF8);
    }

    private static (string decNs, string decClass, string decMethod,
                    string calNs, string calClass, string calMethod)
        GenerateHelperNames(int seed)
    {
        var rng = new Random(seed);
        var used = new HashSet<string>();

        var decNs = GenerateUniqueName(rng, used, 8);
        var decClass = GenerateUniqueName(rng, used, 8);
        var decMethod = GenerateUniqueName(rng, used, 8);
        var calNs = GenerateUniqueName(rng, used, 8);
        var calClass = GenerateUniqueName(rng, used, 8);
        var calMethod = GenerateUniqueName(rng, used, 8);

        return (decNs, decClass, decMethod, calNs, calClass, calMethod);
    }

    private static string GenerateUniqueName(Random rng, HashSet<string> used, int length)
    {
        while (true)
        {
            var candidate = GenerateCandidate(rng, length);
            if (used.Add(candidate))
                return candidate;
        }
    }

    private static string GenerateCandidate(Random rng, int length)
    {
        var sb = new StringBuilder(length + 1);
        sb.Append('_');
        for (var i = 0; i < length; i++)
            sb.Append(AlphaNumChars[rng.Next(AlphaNumChars.Length)]);
        return sb.ToString();
    }

    private static void InjectRuntimeHelper(
        string resourceName,
        string outputPath,
        Dictionary<string, string> replacements)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = reader.ReadToEnd();

        foreach (var (token, value) in replacements)
            content = content.Replace(token, value);

        File.WriteAllText(outputPath, content, Encoding.UTF8);
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    private static void WriteDeobfuscationMap(
        ObfuscationConfig config,
        string decryptorNs, string decryptorClass, string decryptorMethod,
        string callerNs, string callerClass, string callerMethod,
        UuidRenameMap? uuidMap)
    {
        var map = new DeobfuscationMap
        {
            Seed = config.Seed,
            Uuid = config.Uuid,
            StringDecryptor = new DeobfuscationMap.HelperInfo(
                decryptorNs, decryptorClass, decryptorMethod),
            IndirectCaller = new DeobfuscationMap.HelperInfo(
                callerNs, callerClass, callerMethod),
            UuidRenames = uuidMap?.GetAllMappings() ?? new Dictionary<string, string>(),
        };

        map.SaveToFile(config.MapPath!);
    }
}
