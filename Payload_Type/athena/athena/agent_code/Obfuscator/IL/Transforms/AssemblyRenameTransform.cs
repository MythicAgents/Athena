using System.Text;
using Mono.Cecil;

namespace Obfuscator.IL.Transforms;

public sealed class AssemblyRenameTransform
{
    private static readonly char[] AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyz0123456789"
            .ToCharArray();

    private static readonly string[] SkipPrefixes =
        ["System.", "Microsoft.", "runtime."];

    private readonly int _seed;

    public AssemblyRenameTransform(int seed)
    {
        _seed = seed;
    }

    public Dictionary<string, string> RenameAll(
        string directory,
        bool skipFileRename = false)
    {
        var rng = new Random(_seed ^ 0x5A5A5A5A);
        var used = new HashSet<string>(
            StringComparer.Ordinal);
        var renameMap = new Dictionary<string, string>();

        var dllFiles =
            Directory.GetFiles(directory, "*.dll");
        Array.Sort(dllFiles, StringComparer.Ordinal);

        // Phase 1: Build rename map
        foreach (var dllPath in dllFiles)
        {
            var fileName =
                Path.GetFileNameWithoutExtension(dllPath);
            if (ShouldSkip(fileName))
                continue;

            using var stream = new MemoryStream(
                File.ReadAllBytes(dllPath));
            try
            {
                using var asm =
                    AssemblyDefinition.ReadAssembly(stream);
                var originalName = asm.Name.Name;
                if (ShouldSkip(originalName))
                    continue;

                var newName =
                    GenerateUniqueName(rng, used);
                renameMap[originalName] = newName;
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
                        ReadingMode =
                            ReadingMode.Deferred,
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
                    asm.MainModule.Name =
                        newIdentity + ".dll";
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
                    File.WriteAllBytes(
                        dllPath, output.ToArray());
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

    private static bool ShouldSkip(string name)
    {
        foreach (var prefix in SkipPrefixes)
        {
            if (name.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string GenerateUniqueName(
        Random rng, HashSet<string> used)
    {
        var length = 2;
        while (true)
        {
            var sb = new StringBuilder(length + 1);
            sb.Append('_');
            for (var i = 0; i < length; i++)
                sb.Append(
                    AlphaNumChars[
                        rng.Next(AlphaNumChars.Length)]);
            var candidate = sb.ToString();
            if (used.Add(candidate))
                return candidate;
            length++;
        }
    }
}
