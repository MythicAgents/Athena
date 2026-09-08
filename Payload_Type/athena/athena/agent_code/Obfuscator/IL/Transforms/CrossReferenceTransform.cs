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
        CliSignatureSafety.Validate(assemblyBytes, "<memory>");
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
        var originalMethodSignatures = new Dictionary<MethodReference, string>();
        foreach (var methodReference in module.GetMemberReferences()
            .OfType<MethodReference>())
        {
            originalMethodSignatures.TryAdd(
                methodReference, MethodSignature(methodReference));
        }
        foreach (var type in EnumerateAllTypes(module))
        {
            foreach (var method in type.Methods.Where(method => method.HasBody))
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    var target = instruction.Operand switch
                    {
                        GenericInstanceMethod generic => generic.ElementMethod,
                        MethodReference reference => reference,
                        _ => null,
                    };
                    if (target is not null)
                        originalMethodSignatures.TryAdd(
                            target, MethodSignature(target));
                }
            }
        }

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

            var identity = ResolveTypeIdentity(typeRef, map);
            var newNs = typeRef.DeclaringType is null
                && identity.Namespace != typeRef.Namespace
                    ? identity.Namespace : null;
            var newName = identity.Name != typeRef.Name
                ? identity.Name : null;

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
        // Method renames prefer canonical signature-qualified keys and fall
        // back to legacy TypeFullName::MemberName maps. Field keys remain
        // type-qualified so same-named fields on different types stay distinct.
        foreach (var memberRef
            in module.GetMemberReferences())
        {
            if (memberRef.DeclaringType?.Scope
                is not AssemblyNameReference anr)
                continue;
            if (!perAssemblyMaps.TryGetValue(
                anr.Name, out var map))
                continue;

            if (memberRef is MethodReference methodRef)
            {
                var declaringFull = ResolveTypeIdentity(
                    memberRef.DeclaringType, map).FullName;
                var signature = originalMethodSignatures.TryGetValue(
                    methodRef, out var originalSignature)
                        ? originalSignature : MethodSignature(methodRef);
                var qualifiedKey = $"{declaringFull}::{signature}";
                if (map.TryGetValue(
                    qualifiedKey, out var newMemberName))
                    memberRef.Name = newMemberName;
                else if (map.TryGetValue(
                    $"{declaringFull}::{memberRef.Name}",
                    out var legacyMethodName))
                    memberRef.Name = legacyMethodName;
            }
            else if (memberRef is FieldReference)
            {
                var declaringFull = ResolveTypeIdentity(
                    memberRef.DeclaringType, map).FullName;
                var qualifiedKey = $"{declaringFull}::{memberRef.Name}";
                if (map.TryGetValue(qualifiedKey, out var newMemberName))
                    memberRef.Name = newMemberName;
                else if (map.TryGetValue(
                    memberRef.Name, out var legacyFieldName))
                    memberRef.Name = legacyFieldName;
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

                    var df2 = ResolveTypeIdentity(
                        target.DeclaringType, map2).FullName;
                    var signature2 = originalMethodSignatures.TryGetValue(
                        target, out var originalSignature2)
                            ? originalSignature2 : MethodSignature(target);
                    var qk2 = $"{df2}::{signature2}";
                    if (map2.TryGetValue(qk2, out var newName2))
                        target.Name = newName2;
                    else if (map2.TryGetValue(
                        $"{df2}::{target.Name}", out var legacyName2))
                        target.Name = legacyName2;
                }
            }
        }

        using var output = new MemoryStream();
        asm.Write(output);
        return output.ToArray();
    }

    private static string MethodSignature(MethodReference method)
    {
        var definition = method is GenericInstanceMethod generic
            ? generic.ElementMethod
            : method;
        return CanonicalMemberKey.MethodSignature(
            definition.Name,
            definition.GenericParameters.Count,
            definition.Parameters.Select(parameter => parameter.ParameterType));
    }

    private static ResolvedTypeIdentity ResolveTypeIdentity(
        TypeReference type,
        Dictionary<string, string> map)
    {
        if (type.DeclaringType is not null)
        {
            var declaring = ResolveTypeIdentity(type.DeclaringType, map);
            var nestedTypeKey = $"{declaring.FullName}/{type.Name}";
            var nestedName = map.TryGetValue(
                nestedTypeKey, out var renamed)
                    ? renamed : type.Name;
            return new ResolvedTypeIdentity(
                string.Empty,
                nestedName,
                $"{declaring.FullName}/{nestedName}");
        }

        var ns = !string.IsNullOrEmpty(type.Namespace)
            && map.TryGetValue(type.Namespace, out var renamedNs)
                ? renamedNs : type.Namespace;
        var typeKey = string.IsNullOrEmpty(ns)
            ? type.Name : $"{ns}.{type.Name}";
        var name = map.TryGetValue(typeKey, out var renamedType)
            ? renamedType : type.Name;
        var fullName = string.IsNullOrEmpty(ns)
            ? name : $"{ns}.{name}";
        return new ResolvedTypeIdentity(ns, name, fullName);
    }

    private readonly record struct ResolvedTypeIdentity(
        string Namespace, string Name, string FullName);

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
