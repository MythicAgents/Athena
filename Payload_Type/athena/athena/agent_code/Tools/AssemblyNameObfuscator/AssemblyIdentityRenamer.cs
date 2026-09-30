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
            : new HashSet<string>(
                extraSkipNames,
                StringComparer.OrdinalIgnoreCase);

        var renameMap = new Dictionary<string, string>();

        var dllFiles =
            Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);
        Array.Sort(dllFiles, StringComparer.Ordinal);

        // Phase 1: Build rename map
        foreach (var dllPath in dllFiles)
        {
            var fileName =
                Path.GetFileNameWithoutExtension(dllPath);
            if (ShouldSkip(fileName, extraSkipSet))
                continue;

            using var stream = new MemoryStream(
                File.ReadAllBytes(dllPath));
            try
            {
                using var asm =
                    AssemblyDefinition.ReadAssembly(stream);
                var originalName = asm.Name.Name;
                if (ShouldSkip(originalName, extraSkipSet))
                    continue;

                renameMap[originalName] =
                    GenerateName(_seed, originalName);
            }
            catch (BadImageFormatException)
            {
                continue;
            }
        }

        // Phase 2: Rewrite identities and refs
        foreach (var dllPath in dllFiles)
        {
            var bytes = File.ReadAllBytes(dllPath);
            using var stream = new MemoryStream(bytes);

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
                continue;
            }

            using (asm)
            {
                var changed = false;

                if (renameMap.TryGetValue(
                    asm.Name.Name, out var newIdentity))
                {
                    asm.Name.Name = newIdentity;
                    asm.MainModule.Name = newIdentity + ".dll";
                    changed = true;
                }

                foreach (var asmRef in
                    asm.MainModule.AssemblyReferences)
                {
                    if (renameMap.TryGetValue(
                        asmRef.Name, out var newRefName))
                    {
                        asmRef.Name = newRefName;
                        changed = true;
                    }
                }

                if (changed)
                {
                    using var output = new MemoryStream();
                    asm.Write(output);
                    File.WriteAllBytes(dllPath, output.ToArray());
                }
            }
        }

        // Phase 3: Rename physical files
        if (!skipFileRename)
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

        return renameMap;
    }

    private bool ShouldSkip(
        string name,
        IReadOnlySet<string>? extraSkipNames)
    {
        if (extraSkipNames?.Contains(name) == true)
            return true;
        foreach (var prefix in _skipPrefixes)
        {
            if (name.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

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
