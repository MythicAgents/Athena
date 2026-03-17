using Mono.Cecil;

namespace Obfuscator.IL.Transforms;

public sealed class CrossReferenceTransform
{
    public byte[] PatchReferences(
        byte[] assemblyBytes,
        Dictionary<string, Dictionary<string, string>>
            perAssemblyMaps,
        string? searchDir)
    {
        using var input = new MemoryStream(assemblyBytes);
        var resolver = new DefaultAssemblyResolver();
        if (searchDir is not null)
            resolver.AddSearchDirectory(searchDir);
        var readerParams = new ReaderParameters
        {
            ReadingMode = ReadingMode.Immediate,
            ReadSymbols = false,
            AssemblyResolver = resolver,
        };
        using var asm = AssemblyDefinition.ReadAssembly(
            input, readerParams);

        var module = asm.MainModule;

        // Collect all patches before applying to avoid
        // FullName key invalidation during iteration
        var typePatch = new List<(
            TypeReference Ref,
            string? NewNs,
            string? NewName)>();

        foreach (var typeRef in module.GetTypeReferences())
        {
            if (typeRef.Scope
                is not AssemblyNameReference anr)
                continue;
            if (!perAssemblyMaps.TryGetValue(
                anr.Name, out var map))
                continue;

            string? newNs = null;
            string? newName = null;

            // Build original FullName for type lookup
            var origFull =
                string.IsNullOrEmpty(typeRef.Namespace)
                    ? typeRef.Name
                    : typeRef.Namespace + "." + typeRef.Name;

            if (map.TryGetValue(origFull, out var renamed))
                newName = renamed;

            if (!string.IsNullOrEmpty(typeRef.Namespace)
                && map.TryGetValue(
                    typeRef.Namespace, out var renamedNs))
                newNs = renamedNs;

            if (newNs is not null || newName is not null)
                typePatch.Add((typeRef, newNs, newName));
        }

        foreach (var (typeRef, newNs, newName) in typePatch)
        {
            if (newNs is not null)
                typeRef.Namespace = newNs;
            if (newName is not null)
                typeRef.Name = newName;
        }

        // Patch member references
        foreach (var memberRef
            in module.GetMemberReferences())
        {
            if (memberRef.DeclaringType?.Scope
                is not AssemblyNameReference anr)
                continue;
            if (!perAssemblyMaps.TryGetValue(
                anr.Name, out var map))
                continue;
            if (map.TryGetValue(
                memberRef.Name, out var newMemberName))
                memberRef.Name = newMemberName;
        }

        using var output = new MemoryStream();
        asm.Write(output);
        return output.ToArray();
    }
}
