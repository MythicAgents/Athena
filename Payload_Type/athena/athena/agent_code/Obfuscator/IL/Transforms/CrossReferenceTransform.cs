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
        using var resolver = new DefaultAssemblyResolver();
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

            // Patch namespace first — MetadataManglingTransform stores
            // type-rename keys using the ALREADY-RENAMED namespace
            // (e.g. "_k7.ContainerBuilder", not "Autofac.ContainerBuilder"),
            // so we must resolve the namespace rename before building
            // the type-name lookup key.
            if (!string.IsNullOrEmpty(typeRef.Namespace)
                && map.TryGetValue(
                    typeRef.Namespace, out var renamedNs))
                newNs = renamedNs;

            // Build FullName using the post-rename namespace so it matches
            // the key format MetadataManglingTransform wrote into the map.
            var effectiveNs = newNs ?? typeRef.Namespace;
            var origFull =
                string.IsNullOrEmpty(effectiveNs)
                    ? typeRef.Name
                    : effectiveNs + "." + typeRef.Name;

            if (map.TryGetValue(origFull, out var renamed))
                newName = renamed;

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

        // Patch member references.
        // Method renames use qualified keys ("TypeFullName::MethodName")
        // to avoid false renames when two types share a method name but
        // only one was renamed (e.g. one is an interface impl, the other
        // is a hiding method).  Field and other member renames continue
        // to use unqualified name keys.
        foreach (var memberRef
            in module.GetMemberReferences())
        {
            if (memberRef.DeclaringType?.Scope
                is not AssemblyNameReference anr)
                continue;
            if (!perAssemblyMaps.TryGetValue(
                anr.Name, out var map))
                continue;

            if (memberRef is MethodReference)
            {
                // Independently reconstruct the renamed declaring-type
                // FullName from the map rather than relying on the TypeRef
                // instance having already been mutated by the loop above.
                // Cecil does not always share the TypeRef instance between
                // GetTypeReferences() and MemberRef.DeclaringType (e.g. for
                // generic method MethodSpec references), so the TypeRef patch
                // above may not have updated this instance.
                var origNs = memberRef.DeclaringType.Namespace;
                var origType = memberRef.DeclaringType.Name;

                // Step 1: resolve namespace rename
                var keyNs = !string.IsNullOrEmpty(origNs)
                    && map.TryGetValue(origNs, out var rNs)
                    ? rNs : origNs;

                // Step 2: resolve type rename (key uses renamed namespace)
                var typeKey = string.IsNullOrEmpty(keyNs)
                    ? origType
                    : $"{keyNs}.{origType}";
                var keyType = map.TryGetValue(typeKey, out var rType)
                    ? rType : origType;

                // Step 3: qualified method key
                var declaringFull = string.IsNullOrEmpty(keyNs)
                    ? keyType
                    : $"{keyNs}.{keyType}";
                var qualifiedKey =
                    $"{declaringFull}::{memberRef.Name}";
                if (map.TryGetValue(
                    qualifiedKey, out var newMethodName))
                    memberRef.Name = newMethodName;
            }
            else
            {
                // Fields and other members use unqualified name keys.
                if (map.TryGetValue(
                    memberRef.Name, out var newMemberName))
                    memberRef.Name = newMemberName;
            }
        }

        // Patch method body instruction operands for GenericInstanceMethod.
        // Cecil may not share the MethodReference instance between
        // GetMemberReferences() and GenericInstanceMethod.ElementMethod,
        // so patching GetMemberReferences() entries above is insufficient
        // for generic method instantiations (MethodSpecs). Iterating
        // instruction operands directly ensures the object Cecil uses
        // during serialization is patched.
        foreach (var type in EnumerateAllTypes(module))
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;
                foreach (var instr in method.Body.Instructions)
                {
                    MethodReference? target = instr.Operand switch
                    {
                        GenericInstanceMethod gim => gim.ElementMethod,
                        MethodReference mr => mr,
                        _ => null
                    };
                    if (target is null) continue;
                    if (target.DeclaringType?.Scope
                        is not AssemblyNameReference anr2) continue;
                    if (!perAssemblyMaps.TryGetValue(
                        anr2.Name, out var map2)) continue;

                    var ns2 = target.DeclaringType.Namespace;
                    var typeName2 = target.DeclaringType.Name;
                    var keyNs2 = !string.IsNullOrEmpty(ns2)
                        && map2.TryGetValue(ns2, out var rNs2)
                        ? rNs2 : ns2;
                    var typeKey2 = string.IsNullOrEmpty(keyNs2)
                        ? typeName2 : $"{keyNs2}.{typeName2}";
                    var keyType2 = map2.TryGetValue(typeKey2, out var rType2)
                        ? rType2 : typeName2;
                    var df2 = string.IsNullOrEmpty(keyNs2)
                        ? keyType2 : $"{keyNs2}.{keyType2}";
                    var qk2 = $"{df2}::{target.Name}";
                    if (map2.TryGetValue(qk2, out var newName2))
                        target.Name = newName2;
                }
            }
        }

        using var output = new MemoryStream();
        asm.Write(output);
        return output.ToArray();
    }

    private static IEnumerable<TypeDefinition> EnumerateAllTypes(
        ModuleDefinition module)
    {
        foreach (var type in module.Types)
        {
            yield return type;
            foreach (var nested in EnumerateNestedTypes(type))
                yield return nested;
        }
    }

    private static IEnumerable<TypeDefinition> EnumerateNestedTypes(
        TypeDefinition type)
    {
        foreach (var nested in type.NestedTypes)
        {
            yield return nested;
            foreach (var deepNested in EnumerateNestedTypes(nested))
                yield return deepNested;
        }
    }
}
