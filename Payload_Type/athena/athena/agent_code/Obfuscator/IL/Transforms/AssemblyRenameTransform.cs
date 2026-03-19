using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

namespace Obfuscator.IL.Transforms;

public sealed class AssemblyRenameTransform
{
    // 62-character alphabet: lowercase + digits + uppercase
    private const string Chars =
        "abcdefghijklmnopqrstuvwxyz0123456789"
        + "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private readonly int _seed;
    private readonly string[] _skipPrefixes;

    public AssemblyRenameTransform(
        int seed,
        string[]? skipPrefixes = null)
    {
        _seed = seed;
        _skipPrefixes = skipPrefixes
            ?? ObfuscatorConstants.SkipPrefixes;
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
            Directory.GetFiles(directory, "*.dll");
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
                    GenerateAssemblyName(_seed, originalName);
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
                    try
                    {
                        using var output = new MemoryStream();
                        asm.Write(output);
                        File.WriteAllBytes(
                            dllPath, output.ToArray());
                    }
                    catch (AssemblyResolutionException)
                    {
                        // A dependency not present in the directory
                        // is required to emit constant type metadata.
                        // Skip; refs remain unmodified.
                    }
                }
            }
        }

        // Phase 3: Rename physical files
        if (!skipFileRename)
        {
            foreach (var (original, newName) in renameMap)
            {
                var oldPath = Path.Combine(
                    directory, original + ".dll");
                var newPath = Path.Combine(
                    directory, newName + ".dll");
                if (File.Exists(oldPath))
                    File.Move(oldPath, newPath);
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
    internal static string GenerateAssemblyName(
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
