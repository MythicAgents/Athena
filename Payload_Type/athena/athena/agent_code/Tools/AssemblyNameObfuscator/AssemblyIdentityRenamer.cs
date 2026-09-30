using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

namespace AssemblyNameObfuscator;

public sealed class AssemblyIdentityRenamer
{
    // 62-character alphabet: lowercase + digits + uppercase
    private const string Chars =
        "abcdefghijklmnopqrstuvwxyz0123456789"
        + "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static readonly string[] DefaultSkipPrefixes =
    [
        "System.", "Microsoft.", "runtime.",
        "Autofac", "IronPython", "BouncyCastle",
        "H.", "Renci", "Mono.", "NamedPipe"
    ];

    private readonly int _seed;
    private readonly string[] _skipPrefixes;

    public AssemblyIdentityRenamer(
        int seed,
        string[]? skipPrefixes = null)
    {
        _seed = seed;
        _skipPrefixes = skipPrefixes
            ?? DefaultSkipPrefixes;
    }

    public static string Rewrite(string assemblyPath, int seed)
    {
        var bytes = File.ReadAllBytes(assemblyPath);
        using var input = new MemoryStream(bytes);
        using var assembly = AssemblyDefinition.ReadAssembly(input);
        var newName = GenerateName(seed, assembly.Name.Name);
        assembly.Name.Name = newName;
        assembly.MainModule.Name = newName + ".dll";
        var renamer = new AssemblyIdentityRenamer(seed);
        foreach (var reference in assembly.MainModule.AssemblyReferences)
        {
            if (!renamer.ShouldSkip(reference.Name, null))
                reference.Name = GenerateName(seed, reference.Name);
        }
        using var output = new MemoryStream();
        assembly.Write(output);
        File.WriteAllBytes(assemblyPath, output.ToArray());
        return newName;
    }

    public Dictionary<string, string> RenameAll(
        string directory,
        bool skipFileRename = false,
        IEnumerable<string>? extraSkipNames = null)
    {
        var extraSkipSet = extraSkipNames is null
            ? null
            : new HashSet<string>(extraSkipNames, StringComparer.OrdinalIgnoreCase);

        var dllFiles = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);
        Array.Sort(dllFiles, StringComparer.Ordinal);

        var renameMap = BuildRenameMap(dllFiles, extraSkipSet);
        foreach (var dllPath in dllFiles)
            RewriteAssemblyReferences(dllPath, renameMap);

        if (!skipFileRename)
            RenamePhysicalFiles(dllFiles, renameMap);

        return renameMap;
    }

    private Dictionary<string, string> BuildRenameMap(
        string[] dllFiles,
        IReadOnlySet<string>? extraSkipSet)
    {
        var renameMap = new Dictionary<string, string>();
        foreach (var dllPath in dllFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(dllPath);
            if (ShouldSkip(fileName, extraSkipSet)
                || !TryReadAssemblyName(dllPath, out var originalName)
                || ShouldSkip(originalName, extraSkipSet))
                continue;

            renameMap[originalName] = GenerateName(_seed, originalName);
        }
        return renameMap;
    }

    private static bool TryReadAssemblyName(string dllPath, out string originalName)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(dllPath));
        try
        {
            using var asm = AssemblyDefinition.ReadAssembly(stream);
            originalName = asm.Name.Name;
            return true;
        }
        catch (BadImageFormatException)
        {
            originalName = string.Empty;
            return false;
        }
    }

    private static void RewriteAssemblyReferences(
        string dllPath,
        IReadOnlyDictionary<string, string> renameMap)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(dllPath));
        AssemblyDefinition asm;
        try
        {
            asm = AssemblyDefinition.ReadAssembly(
                stream,
                new ReaderParameters
                {
                    ReadingMode = ReadingMode.Deferred,
                    ReadSymbols = false,
                });
        }
        catch (BadImageFormatException)
        {
            return;
        }

        using (asm)
        {
            if (!ApplyRenames(asm, renameMap))
                return;

            using var output = new MemoryStream();
            asm.Write(output);
            File.WriteAllBytes(dllPath, output.ToArray());
        }
    }

    private static bool ApplyRenames(
        AssemblyDefinition asm,
        IReadOnlyDictionary<string, string> renameMap)
    {
        var changed = false;
        if (renameMap.TryGetValue(asm.Name.Name, out var newIdentity))
        {
            asm.Name.Name = newIdentity;
            asm.MainModule.Name = newIdentity + ".dll";
            changed = true;
        }

        foreach (var asmRef in asm.MainModule.AssemblyReferences)
        {
            if (!renameMap.TryGetValue(asmRef.Name, out var newRefName))
                continue;
            asmRef.Name = newRefName;
            changed = true;
        }

        return changed;
    }

    private static void RenamePhysicalFiles(
        string[] dllFiles,
        IReadOnlyDictionary<string, string> renameMap)
    {
        foreach (var dllPath in dllFiles)
        {
            var original = Path.GetFileNameWithoutExtension(dllPath);
            if (!renameMap.TryGetValue(original, out var newName))
                continue;
            var parent = Path.GetDirectoryName(dllPath)!;
            File.Move(dllPath, Path.Combine(parent, newName + ".dll"));
        }
    }

    private bool ShouldSkip(
        string name,
        IReadOnlySet<string>? extraSkipNames) =>
        extraSkipNames?.Contains(name) == true
        || _skipPrefixes.Any(prefix =>
            name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Derives a new assembly name purely from (seed, originalName).
    /// No shared state — identical result regardless of batch membership.
    /// Uses SHA256(UTF8("{seed}:{name}")) → 5-char base62 with _ prefix.
    /// 62^5 = 916M possibilities; P(collision | 50 assemblies) less than 0.001%.
    /// </summary>
    public static string GenerateName(
        int seed, string originalName)
    {
        var input = Encoding.UTF8.GetBytes(
            $"{seed}:{originalName}");
        var hash = SHA256.HashData(input);

        var sb = new StringBuilder("_");
        for (var i = 0; i < 5; i++)
            sb.Append(Chars[hash[i] % Chars.Length]);
        return sb.ToString();
    }
}
