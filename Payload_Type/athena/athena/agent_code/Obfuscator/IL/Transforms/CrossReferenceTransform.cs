using Mono.Cecil;

namespace Obfuscator.IL.Transforms;

public sealed class CrossReferenceTransform
{
    public byte[] PatchReferences(
        byte[] assemblyBytes,
        Dictionary<string, Dictionary<string, string>> perAssemblyMaps,
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
        using var asm = AssemblyDefinition.ReadAssembly(input, readerParams);

        var module = asm.MainModule;
        var originalMethodSignatures = CaptureOriginalMethodSignatures(module);
        PatchTypeReferences(module, perAssemblyMaps);
        PatchMemberReferences(module, perAssemblyMaps, originalMethodSignatures);
        PatchInstructionMethodReferences(module, perAssemblyMaps, originalMethodSignatures);

        using var output = new MemoryStream();
        asm.Write(output);
        return output.ToArray();
    }

    private static Dictionary<MethodReference, string> CaptureOriginalMethodSignatures(
        ModuleDefinition module)
    {
        var signatures = new Dictionary<MethodReference, string>();
        var allMethodRefs = module.GetMemberReferences()
            .OfType<MethodReference>()
            .Concat(EnumerateInstructionMethodReferences(module));
        foreach (var methodRef in allMethodRefs)
            signatures.TryAdd(methodRef, MethodSignature(methodRef));
        return signatures;
    }

    private static void PatchTypeReferences(
        ModuleDefinition module,
        Dictionary<string, Dictionary<string, string>> perAssemblyMaps)
    {
        var typePatch = new List<(TypeReference Ref, string? NewNs, string? NewName)>();
        foreach (var typeRef in module.GetTypeReferences())
        {
            if (!TryGetTargetAssemblyMap(typeRef.Scope, perAssemblyMaps, out var map))
                continue;

            var identity = ResolveTypeIdentity(typeRef, map);
            var newNs = typeRef.DeclaringType is null && identity.Namespace != typeRef.Namespace
                ? identity.Namespace : null;
            var newName = identity.Name != typeRef.Name ? identity.Name : null;

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
    }

    private static void PatchMemberReferences(
        ModuleDefinition module,
        Dictionary<string, Dictionary<string, string>> perAssemblyMaps,
        IReadOnlyDictionary<MethodReference, string> originalMethodSignatures)
    {
        foreach (var memberRef in module.GetMemberReferences())
        {
            if (!TryGetTargetAssemblyMap(memberRef.DeclaringType?.Scope, perAssemblyMaps, out var map))
                continue;

            if (memberRef is MethodReference methodRef)
            {
                PatchMethodReference(methodRef, map, originalMethodSignatures);
            }
            else if (memberRef is FieldReference)
            {
                var declaringFull = ResolveTypeIdentity(memberRef.DeclaringType!, map).FullName;
                if (map.TryGetValue($"{declaringFull}::{memberRef.Name}", out var newMemberName))
                    memberRef.Name = newMemberName;
                else if (map.TryGetValue(memberRef.Name, out var legacyFieldName))
                    memberRef.Name = legacyFieldName;
            }
            else if (map.TryGetValue(memberRef.Name, out var newMemberName))
            {
                memberRef.Name = newMemberName;
            }
        }
    }

    private static void PatchInstructionMethodReferences(
        ModuleDefinition module,
        Dictionary<string, Dictionary<string, string>> perAssemblyMaps,
        IReadOnlyDictionary<MethodReference, string> originalMethodSignatures)
    {
        foreach (var target in EnumerateInstructionMethodReferences(module))
        {
            if (TryGetTargetAssemblyMap(target.DeclaringType?.Scope, perAssemblyMaps, out var map))
                PatchMethodReference(target, map, originalMethodSignatures);
        }
    }

    private static void PatchMethodReference(
        MethodReference methodRef,
        Dictionary<string, string> map,
        IReadOnlyDictionary<MethodReference, string> originalMethodSignatures)
    {
        var declaringFull = ResolveTypeIdentity(methodRef.DeclaringType, map).FullName;
        var signature = originalMethodSignatures.TryGetValue(methodRef, out var originalSignature)
            ? originalSignature : MethodSignature(methodRef);
        if (map.TryGetValue($"{declaringFull}::{signature}", out var newMemberName))
            methodRef.Name = newMemberName;
        else if (map.TryGetValue($"{declaringFull}::{methodRef.Name}", out var legacyMethodName))
            methodRef.Name = legacyMethodName;
    }

    private static IEnumerable<MethodReference> EnumerateInstructionMethodReferences(
        ModuleDefinition module) =>
        EnumerateAllTypes(module)
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
            .Select(instruction => instruction.Operand switch
            {
                GenericInstanceMethod generic => generic.ElementMethod,
                MethodReference reference => reference,
                _ => null,
            })
            .OfType<MethodReference>();

    private static bool TryGetTargetAssemblyMap(
        IMetadataScope? scope,
        Dictionary<string, Dictionary<string, string>> perAssemblyMaps,
        out Dictionary<string, string> map)
    {
        if (scope is AssemblyNameReference anr
            && perAssemblyMaps.TryGetValue(anr.Name, out map!))
            return true;

        map = null!;
        return false;
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
        if (type is TypeSpecification spec)
            type = spec.ElementType;

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
