using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Obfuscator.IL.Transforms;

/// <summary>
/// Renames types, methods, fields, properties, parameters, events,
/// and generic parameters in a compiled .NET assembly using Mono.Cecil.
/// Strips meaningful metadata names to hinder reverse engineering.
/// </summary>
public sealed class MetadataManglingTransform
{
    private static readonly HashSet<string> PreservedMethodNames = new(
        StringComparer.Ordinal)
    {
        "ToString", "GetHashCode", "Equals", "Dispose",
        "GetEnumerator", "MoveNext", "get_Current",
        // JsonConverter<T> requires Read/Write to be named exactly
        // "Read" and "Write" for the CLR to locate the implementation.
        // GetBaseMethod() via Cecil may not traverse generic-instantiated
        // base types correctly, so we preserve these by name.
        "Read", "Write",
    };

    private static readonly char[] AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private readonly int _seed;
    private Dictionary<string, string> _renameMappings = new();
    private Dictionary<MethodDefinition, MethodDefinition>
        _virtualFamilyRoot = new();
    private Dictionary<MethodDefinition, string>
        _familyNameOverrides = new();
    private Dictionary<MethodDefinition, string>
        _originalMethodSignatures = new();
    private HashSet<string> _reflectionPropertyNames =
        new(StringComparer.Ordinal);
    private HashSet<string> _reflectionFieldNames =
        new(StringComparer.Ordinal);
    private HashSet<string> _reflectionMethodNames =
        new(StringComparer.Ordinal);
    private HashSet<string> _reflectionEventNames =
        new(StringComparer.Ordinal);
    private HashSet<string> _reflectionPropertyNamesIgnoreCase =
        new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _reflectionFieldNamesIgnoreCase =
        new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _reflectionMethodNamesIgnoreCase =
        new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _reflectionEventNamesIgnoreCase =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _preserveAllReflectionProperties;
    private bool _preserveAllReflectionFields;
    private bool _preserveAllReflectionMethods;
    private bool _preserveAllReflectionEvents;
    private HashSet<TypeDefinition> _reflectionTypesToPreserve = new();
    private HashSet<string> _reflectionNamespacesToPreserve =
        new(StringComparer.Ordinal);

    public MetadataManglingTransform(int seed)
    {
        _seed = seed;
    }

    /// <summary>
    /// Transform an assembly in-memory. Returns modified bytes.
    /// </summary>
    /// <param name="assemblyBytes">Raw assembly bytes.</param>
    /// <param name="searchDirectory">
    /// Optional directory to search for referenced assemblies.
    /// Typically the build output folder containing dependency DLLs.
    /// </param>
    public byte[] Transform(
        byte[] assemblyBytes,
        string? searchDirectory = null)
    {
        CliSignatureSafety.Validate(assemblyBytes, "<memory>");
        using var input = new MemoryStream(assemblyBytes);
        using var resolver = new DefaultAssemblyResolver();
        if (searchDirectory is not null)
            resolver.AddSearchDirectory(searchDirectory);
        var readerParams = new ReaderParameters
        {
            ReadingMode = ReadingMode.Immediate,
            ReadSymbols = false,
            AssemblyResolver = resolver,
        };
        using var asm = AssemblyDefinition.ReadAssembly(input, readerParams);

        _renameMappings = new Dictionary<string, string>();
        var rng = new Random(_seed);
        _originalMethodSignatures = EnumerateAllTypes(asm.MainModule)
            .SelectMany(type => type.Methods)
            .ToDictionary(
                method => method,
                method => CanonicalMemberKey.MethodSignature(
                    method.Name,
                    method.GenericParameters.Count,
                    method.Parameters.Select(parameter =>
                        parameter.ParameterType)));
        (_reflectionPropertyNames, _reflectionPropertyNamesIgnoreCase,
            _preserveAllReflectionProperties) =
            FindReflectionMemberNames(
                asm.MainModule, "GetProperty", "GetDeclaredProperty",
                "GetRuntimeProperty", "GetPropertyImpl");
        (_reflectionFieldNames, _reflectionFieldNamesIgnoreCase,
            _preserveAllReflectionFields) =
            FindReflectionMemberNames(
                asm.MainModule, "GetField", "GetDeclaredField",
                "GetRuntimeField");
        (_reflectionMethodNames, _reflectionMethodNamesIgnoreCase,
            _preserveAllReflectionMethods) =
            FindReflectionMemberNames(
                asm.MainModule, "GetMethod", "GetDeclaredMethod",
                "GetRuntimeMethod", "GetMethodImpl");
        var (declaredMethodNames, declaredMethodNamesIgnoreCase,
            preserveAllDeclaredMethods) =
            FindReflectionMemberNames(
                asm.MainModule, typeMethodName: null,
                typeInfoMethodName: "GetDeclaredMethods");
        _reflectionMethodNames.UnionWith(declaredMethodNames);
        _reflectionMethodNamesIgnoreCase.UnionWith(declaredMethodNamesIgnoreCase);
        _preserveAllReflectionMethods |= preserveAllDeclaredMethods;
        (_reflectionEventNames, _reflectionEventNamesIgnoreCase,
            _preserveAllReflectionEvents) =
            FindReflectionMemberNames(
                asm.MainModule, "GetEvent", "GetDeclaredEvent",
                "GetRuntimeEvent");
        var (generalMemberNames, generalMemberNamesIgnoreCase,
            preserveAllGeneralMembers) =
            FindReflectionMemberNames(
                asm.MainModule, "GetMember", typeInfoMethodName: null);
        var (invokeMemberNames, invokeMemberNamesIgnoreCase,
            preserveAllInvokeMembers) =
            FindReflectionMemberNames(
                asm.MainModule, "InvokeMember", typeInfoMethodName: null);
        generalMemberNames.UnionWith(invokeMemberNames);
        generalMemberNamesIgnoreCase.UnionWith(invokeMemberNamesIgnoreCase);
        preserveAllGeneralMembers |= preserveAllInvokeMembers;
        _reflectionPropertyNames.UnionWith(generalMemberNames);
        _reflectionFieldNames.UnionWith(generalMemberNames);
        _reflectionMethodNames.UnionWith(generalMemberNames);
        _reflectionEventNames.UnionWith(generalMemberNames);
        _reflectionPropertyNamesIgnoreCase.UnionWith(generalMemberNamesIgnoreCase);
        _reflectionFieldNamesIgnoreCase.UnionWith(generalMemberNamesIgnoreCase);
        _reflectionMethodNamesIgnoreCase.UnionWith(generalMemberNamesIgnoreCase);
        _reflectionEventNamesIgnoreCase.UnionWith(generalMemberNamesIgnoreCase);
        if (preserveAllGeneralMembers)
        {
            _preserveAllReflectionProperties = true;
            _preserveAllReflectionFields = true;
            _preserveAllReflectionMethods = true;
            _preserveAllReflectionEvents = true;
        }

        ConfigureReflectionTypePreservation(asm.MainModule);

        // Scope-level name sets to avoid collisions per scope
        var usedGlobal = new HashSet<string>(StringComparer.Ordinal);

        _virtualFamilyRoot = BuildVirtualMethodFamilies(
            asm.MainModule);
        _familyNameOverrides =
            new Dictionary<MethodDefinition, string>();

        // First pass: collect and assign renames
        RenameNamespaces(asm.MainModule, rng, usedGlobal);

        // Sort by FullName (after namespace rename) so RNG draws are
        // assigned in the same order regardless of PE-table type ordering.
        // This ensures consistent names when the same DLL is compiled in
        // different contexts (full payload build vs. per-command build).
        foreach (var type in EnumerateAllTypes(asm.MainModule)
            .OrderBy(t => t.FullName, StringComparer.Ordinal))
            RenameType(type, rng, usedGlobal);

        using var output = new MemoryStream();
        asm.Write(output);
        return output.ToArray();
    }

    /// <summary>
    /// Returns the rename mappings from the last Transform call
    /// (original name -> new name).
    /// </summary>
    public Dictionary<string, string> GetRenameMappings()
    {
        return new Dictionary<string, string>(_renameMappings);
    }

    private void RenameNamespaces(
        ModuleDefinition module,
        Random rng,
        HashSet<string> used)
    {
        var nsMap = new Dictionary<string, string>(StringComparer.Ordinal);

        // Assign names to namespaces in sorted order so the RNG draws are
        // consistent regardless of the PE-table type ordering.
        var sortedNs = module.Types
            .Where(t => !string.IsNullOrEmpty(t.Namespace))
            .Select(t => t.Namespace)
            .Distinct(StringComparer.Ordinal)
            .Where(ns => !_reflectionNamespacesToPreserve.Contains(ns))
            .OrderBy(ns => ns, StringComparer.Ordinal);

        foreach (var ns in sortedNs)
        {
            var newNs = GenerateUniqueName(rng, used);
            nsMap[ns] = newNs;
            _renameMappings[ns] = newNs;
        }

        foreach (var type in module.Types)
        {
            if (!string.IsNullOrEmpty(type.Namespace)
                && !_reflectionNamespacesToPreserve.Contains(type.Namespace)
                && nsMap.TryGetValue(type.Namespace, out var newNs))
                type.Namespace = newNs;
        }
    }

    private void RenameType(
        TypeDefinition type,
        Random rng,
        HashSet<string> used)
    {
        // Never rename the Cecil internal <Module> type
        if (type.Name == "<Module>")
            return;

        // Rename the type itself unless a Type/TypeInfo name-based lookup
        // requires its exact metadata name.
        var originalTypeName = type.FullName;
        if (!_reflectionTypesToPreserve.Contains(type))
        {
            var newTypeName = GenerateUniqueName(rng, used);
            _renameMappings[originalTypeName] = newTypeName;
            type.Name = newTypeName;
        }

        // Rename generic parameters on the type
        RenameGenericParameters(type.GenericParameters, rng, used);

        // Rename fields — sorted for deterministic RNG draw order
        var usedFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in type.Fields
            .OrderBy(f => f.Name, StringComparer.Ordinal))
            RenameField(field, rng, usedFields);

        // Rename events — sorted for deterministic RNG draw order
        var usedEvents = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evt in type.Events
            .OrderBy(e => e.Name, StringComparer.Ordinal))
            RenameEvent(evt, rng, usedEvents);

        // Rename safe property/accessor families together so metadata names
        // remain coherent (Property, get_Property, set_Property).
        var usedProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in type.Properties
            .OrderBy(p => p.FullName, StringComparer.Ordinal))
            RenameProperty(property, rng, usedProperties);

        // Reserve inherited virtual-family names before assigning any other
        // method names in this type. Otherwise an unrelated same-signature
        // method can receive the family name and produce invalid metadata.
        var usedMethods = type.Methods
            .Select(method => _familyNameOverrides.TryGetValue(
                method, out var familyName) ? familyName : null)
            .Where(name => name is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        foreach (var method in type.Methods
            .OrderBy(m => m.FullName, StringComparer.Ordinal))
            RenameMethod(method, rng, usedMethods);
    }

    private void RenameField(
        FieldDefinition field,
        Random rng,
        HashSet<string> used)
    {
        if (ShouldPreserveField(field))
            return;

        var original = field.Name;
        var newName = GenerateUniqueName(rng, used);
        _renameMappings[MemberKey(field.DeclaringType, original)] = newName;
        // Retain the legacy simple-name entry for existing map consumers.
        _renameMappings.TryAdd(original, newName);
        field.Name = newName;
    }

    private void RenameEvent(
        EventDefinition evt,
        Random rng,
        HashSet<string> used)
    {
        if (_preserveAllReflectionEvents
            || MatchesReflectionName(_reflectionEventNames,
                _reflectionEventNamesIgnoreCase, evt.Name))
            return;

        var original = evt.Name;
        var newName = GenerateUniqueName(rng, used);
        _renameMappings[original] = newName;
        evt.Name = newName;
    }

    private void RenameProperty(
        PropertyDefinition property,
        Random rng,
        HashSet<string> used)
    {
        if (ShouldPreserveProperty(property))
            return;

        var original = property.Name;
        var newName = GenerateUniqueName(rng, used);
        _renameMappings[MemberKey(property.DeclaringType, original)] = newName;
        property.Name = newName;

        RenameAccessor(property.GetMethod, "get_" + newName);
        RenameAccessor(property.SetMethod, "set_" + newName);
        foreach (var method in property.OtherMethods)
            RenameAccessor(method, newName);
    }

    private void RenameAccessor(MethodDefinition? method, string newName)
    {
        if (method is null)
            return;

        RecordMethodRename(method, method.Name, newName);
        method.Name = newName;
    }

    /// <summary>
    /// Builds a canonical qualified rename-map key for a method. The declaring
    /// type, generic arity, and original parameter signature distinguish both
    /// same-named members on different types and overloads on one type.
    /// </summary>
    private string MethodKey(
        MethodDefinition method, string originalName)
    {
        var signature = _originalMethodSignatures.TryGetValue(
            method, out var originalSignature)
                ? originalSignature
                : CanonicalMemberKey.MethodSignature(
                    originalName,
                    method.GenericParameters.Count,
                    method.Parameters.Select(parameter =>
                        parameter.ParameterType));
        return $"{method.DeclaringType.FullName}::{signature}";
    }

    private void RecordMethodRename(
        MethodDefinition method,
        string originalName,
        string newName)
    {
        _renameMappings[MethodKey(method, originalName)] = newName;
        _renameMappings.TryAdd(
            MemberKey(method.DeclaringType, originalName), newName);
    }

    private static string MemberKey(
        TypeDefinition declaringType, string originalName)
        => $"{declaringType.FullName}::{originalName}";

    private void RenameMethod(
        MethodDefinition method,
        Random rng,
        HashSet<string> used)
    {
        if (ShouldPreserveMethod(method))
        {
            // Even though this method is preserved, propagate its name
            // to any derived methods so they are forced to keep the same
            // name (preventing TypeLoadException when the CLR checks that
            // the override chain is consistent).
            RecordFamilyName(method, method.Name);
            return;
        }

        // If a family member was already renamed and
        // recorded a name for this method, use it
        if (_familyNameOverrides.TryGetValue(
            method, out var familyName))
        {
            RecordMethodRename(method, method.Name, familyName);
            method.Name = familyName;
            RenameGenericParameters(
                method.GenericParameters, rng, used);
            foreach (var param in method.Parameters)
                RenameParameter(param, rng, used);
            return;
        }

        var original = method.Name;
        var newName = GenerateUniqueName(rng, used);
        RecordMethodRename(method, original, newName);
        method.Name = newName;

        // Record the name for all family members
        RecordFamilyName(method, newName);

        RenameGenericParameters(
            method.GenericParameters, rng, used);
        foreach (var param in method.Parameters)
            RenameParameter(param, rng, used);
    }

    private void RecordFamilyName(
        MethodDefinition method, string newName)
    {
        // Case 1: method is a root — record for all
        // derived methods that map to it
        foreach (var (derived, root) in _virtualFamilyRoot)
        {
            if (root == method
                && !_familyNameOverrides
                    .ContainsKey(derived))
            {
                _familyNameOverrides[derived] = newName;
            }
        }

        // Case 2: method is a derived method — record
        // for its root AND all sibling overrides, so
        // processing order doesn't matter
        if (_virtualFamilyRoot.TryGetValue(
            method, out var myRoot))
        {
            if (!_familyNameOverrides.ContainsKey(myRoot))
                _familyNameOverrides[myRoot] = newName;

            foreach (var (sibling, root)
                in _virtualFamilyRoot)
            {
                if (root == myRoot
                    && sibling != method
                    && !_familyNameOverrides
                        .ContainsKey(sibling))
                {
                    _familyNameOverrides[sibling] = newName;
                }
            }
        }
    }

    private void RenameParameter(
        ParameterDefinition param,
        Random rng,
        HashSet<string> used)
    {
        if (string.IsNullOrEmpty(param.Name))
            return;

        var original = param.Name;
        var newName = GenerateUniqueName(rng, used);
        _renameMappings[original] = newName;
        param.Name = newName;
    }

    private void RenameGenericParameters(
        Mono.Collections.Generic.Collection<GenericParameter> genericParams,
        Random rng,
        HashSet<string> used)
    {
        foreach (var gp in genericParams)
        {
            var original = gp.Name;
            var newName = GenerateUniqueName(rng, used);
            _renameMappings[original] = newName;
            gp.Name = newName;
        }
    }

    private static Dictionary<MethodDefinition, MethodDefinition>
        BuildVirtualMethodFamilies(ModuleDefinition module)
    {
        var rootMap = new Dictionary<
            MethodDefinition, MethodDefinition>();

        foreach (var type in EnumerateAllTypes(module))
        {
            foreach (var method in type.Methods)
            {
                if (!method.IsVirtual || !method.IsReuseSlot)
                    continue;

                MethodDefinition baseMethod;
                try
                {
                    baseMethod = method.GetBaseMethod();
                }
                catch (AssemblyResolutionException)
                {
                    continue;
                }

                if (baseMethod == method)
                    continue;
                if (baseMethod.DeclaringType.Scope
                    is AssemblyNameReference)
                    continue;

                rootMap[method] = baseMethod;
            }
        }

        return rootMap;
    }

    private bool ShouldPreserveMethod(MethodDefinition method)
    {
        if (_preserveAllReflectionMethods
            || MatchesReflectionName(_reflectionMethodNames,
                _reflectionMethodNamesIgnoreCase, method.Name))
            return true;

        // Keep all interface method declarations — implementations
        // rely on name-based matching
        if (method.DeclaringType.IsInterface)
            return true;

        // Keep constructors (.ctor, .cctor)
        if (method.IsConstructor)
            return true;

        // Keep P/Invoke extern methods — the OS looks these up by name
        if (method.IsPInvokeImpl)
            return true;

        // Keep the entry point
        if (method.Module.EntryPoint == method)
            return true;

        // Keep known framework method overrides
        if (PreservedMethodNames.Contains(method.Name))
            return true;

        // Keep virtual methods that override a base from an external assembly
        if (method.IsVirtual && method.IsReuseSlot)
        {
            try
            {
                var baseMethod = method.GetBaseMethod();
                if (baseMethod != method
                    && baseMethod.DeclaringType.Scope
                        is AssemblyNameReference)
                    return true;
            }
            catch (AssemblyResolutionException)
            {
                return true;
            }
        }

        // Keep interface method implementations — renaming breaks
        // CLR name-based matching for both external and internal
        // interfaces
        if (IsInterfaceImpl(method))
            return true;

        // Keep delegate methods — the CLR resolves Invoke,
        // BeginInvoke, and EndInvoke by name at runtime
        if (IsDelegate(method.DeclaringType))
            return true;

        // Property accessors are handled as a family by RenameProperty.
        if (method.IsGetter || method.IsSetter)
            return true;

        return false;
    }

    private static bool IsDelegate(TypeDefinition type)
    {
        var baseRef = type.BaseType;
        if (baseRef is null)
            return false;
        return baseRef.FullName == "System.MulticastDelegate"
            || baseRef.FullName == "System.Delegate";
    }

    private static bool IsInterfaceImpl(MethodDefinition method)
    {
        // Explicit MethodImpl records are authoritative even when the body name
        // is qualified or bears no resemblance to the interface member name.
        foreach (var overridden in method.Overrides)
        {
            if (SignaturesMatch(method, overridden, compareReturnType: true))
                return true;
        }

        var unresolvedContract = false;
        foreach (var ifaceMethod in EnumerateInterfaceMethods(
            method.DeclaringType.Interfaces.Select(item => item.InterfaceType),
            new HashSet<string>(StringComparer.Ordinal),
            () => unresolvedContract = true))
        {
            if (method.Name != ifaceMethod.Name
                && !method.Name.EndsWith(
                    "." + ifaceMethod.Name, StringComparison.Ordinal))
                continue;
            if (SignaturesMatch(method, ifaceMethod, compareReturnType: true))
                return true;
        }

        // When an external contract cannot be resolved, C# implicit interface
        // bodies still carry virtual+final metadata. Preserve only those likely
        // dispatch slots rather than every unrelated method on the type.
        return unresolvedContract && method.IsVirtual && method.IsFinal;
    }

    private static IEnumerable<MethodReference> EnumerateInterfaceMethods(
        IEnumerable<TypeReference> interfaces,
        HashSet<string> visited,
        Action onUnresolved)
    {
        foreach (var interfaceType in interfaces)
        {
            if (!visited.Add(interfaceType.FullName))
                continue;

            TypeDefinition? resolved;
            try
            {
                resolved = interfaceType.Resolve();
            }
            catch (AssemblyResolutionException)
            {
                onUnresolved();
                continue;
            }

            if (resolved is null)
            {
                onUnresolved();
                continue;
            }

            var substitutions = BuildTypeSubstitutions(resolved, interfaceType);
            foreach (var candidate in resolved.Methods)
                yield return BindInterfaceMethod(
                    candidate, interfaceType, substitutions);
            foreach (var inheritedType in resolved.Interfaces
                .Select(item => SubstituteType(
                    item.InterfaceType, substitutions)))
            {
                foreach (var inherited in EnumerateInterfaceMethods(
                    [inheritedType], visited, onUnresolved))
                    yield return inherited;
            }
        }
    }

    private static Dictionary<int, TypeReference> BuildTypeSubstitutions(
        TypeDefinition definition,
        TypeReference reference)
    {
        var substitutions = new Dictionary<int, TypeReference>();
        if (reference is not GenericInstanceType instance)
            return substitutions;
        for (var index = 0; index < definition.GenericParameters.Count
            && index < instance.GenericArguments.Count; index++)
            substitutions[index] = instance.GenericArguments[index];
        return substitutions;
    }

    private static MethodReference BindInterfaceMethod(
        MethodDefinition definition,
        TypeReference declaringType,
        IReadOnlyDictionary<int, TypeReference> substitutions)
    {
        var bound = new MethodReference(
            definition.Name,
            SubstituteType(definition.ReturnType, substitutions),
            declaringType)
        {
            HasThis = definition.HasThis,
            ExplicitThis = definition.ExplicitThis,
            CallingConvention = definition.CallingConvention,
        };
        foreach (var parameter in definition.GenericParameters)
            bound.GenericParameters.Add(new GenericParameter(parameter.Name, bound));
        foreach (var parameter in definition.Parameters)
            bound.Parameters.Add(new ParameterDefinition(
                SubstituteType(parameter.ParameterType, substitutions)));
        return bound;
    }

    private static TypeReference SubstituteType(
        TypeReference type,
        IReadOnlyDictionary<int, TypeReference> substitutions)
    {
        if (type is GenericParameter parameter
            && parameter.Type == GenericParameterType.Type
            && substitutions.TryGetValue(parameter.Position, out var replacement))
            return replacement;
        if (type is ByReferenceType byReference)
            return new ByReferenceType(SubstituteType(
                byReference.ElementType, substitutions));
        if (type is PointerType pointer)
            return new PointerType(SubstituteType(
                pointer.ElementType, substitutions));
        if (type is ArrayType array)
            return new ArrayType(SubstituteType(
                array.ElementType, substitutions), array.Rank);
        if (type is GenericInstanceType generic)
        {
            var bound = new GenericInstanceType(generic.ElementType);
            foreach (var argument in generic.GenericArguments)
                bound.GenericArguments.Add(SubstituteType(argument, substitutions));
            return bound;
        }
        return type;
    }

    private static bool SignaturesMatch(
        MethodDefinition implementation,
        MethodReference contract,
        bool compareReturnType)
    {
        if (implementation.GenericParameters.Count
                != contract.GenericParameters.Count
            || implementation.Parameters.Count != contract.Parameters.Count)
            return false;

        for (var index = 0; index < implementation.Parameters.Count; index++)
        {
            if (TypeSignature(implementation.Parameters[index].ParameterType)
                != TypeSignature(contract.Parameters[index].ParameterType))
                return false;
        }

        return !compareReturnType
            || TypeSignature(implementation.ReturnType)
                == TypeSignature(contract.ReturnType);
    }

    private static string TypeSignature(TypeReference type)
    {
        return type switch
        {
            GenericParameter parameter =>
                (parameter.Type == GenericParameterType.Method ? "!!" : "!")
                + parameter.Position,
            ByReferenceType byReference =>
                TypeSignature(byReference.ElementType) + "&",
            PointerType pointer => TypeSignature(pointer.ElementType) + "*",
            ArrayType array => TypeSignature(array.ElementType)
                + "[" + new string(',', array.Rank - 1) + "]",
            GenericInstanceType generic => generic.ElementType.FullName
                + "<" + string.Join(",", generic.GenericArguments
                    .Select(TypeSignature)) + ">",
            _ => type.FullName,
        };
    }

    private bool ShouldPreserveProperty(PropertyDefinition property)
    {
        var accessors = new[] { property.GetMethod, property.SetMethod }
            .Where(method => method is not null)
            .Cast<MethodDefinition>()
            .Concat(property.OtherMethods)
            .ToList();

        if (property.DeclaringType.IsInterface
            || IsDelegate(property.DeclaringType)
            || property.DeclaringType.IsSerializable
            || HasDataContractAttribute(property.DeclaringType)
            || HasReflectionSensitiveAttribute(property.DeclaringType)
            || _preserveAllReflectionProperties
            || MatchesReflectionName(_reflectionPropertyNames,
                _reflectionPropertyNamesIgnoreCase, property.Name))
            return true;

        if (HasSerializationAttribute(property)
            || HasReflectionSensitiveAttribute(property))
            return true;

        foreach (var accessor in accessors)
        {
            if (accessor.IsPublic
                || accessor.IsFamily
                || accessor.IsFamilyOrAssembly
                || accessor.IsFamilyAndAssembly
                || accessor.IsVirtual
                || accessor.IsPInvokeImpl
                || accessor.IsRuntime
                || accessor.IsInternalCall
                || accessor.HasOverrides
                || IsInterfaceImpl(accessor)
                || HasSerializationAttribute(accessor)
                || HasReflectionSensitiveAttribute(accessor))
                return true;
        }

        return false;
    }


    private static bool HasReflectionSensitiveAttribute(
        ICustomAttributeProvider provider)
    {
        if (!provider.HasCustomAttributes)
            return false;

        return provider.CustomAttributes.Any(attribute =>
            attribute.AttributeType.Name is "ObfuscationAttribute"
                or "PreserveAttribute"
                or "DynamicallyAccessedMembersAttribute");
    }

    private static bool HasSerializationAttribute(
        ICustomAttributeProvider provider)
    {
        if (!provider.HasCustomAttributes)
            return false;

        foreach (var attr in provider.CustomAttributes)
        {
            var name = attr.AttributeType.Name;
            if (name == "JsonPropertyNameAttribute"
                || name == "JsonPropertyAttribute"
                || name == "JsonIgnoreAttribute"
                || name == "JsonIncludeAttribute"
                || name == "JsonExtensionDataAttribute"
                || name == "DataMemberAttribute"
                || name == "XmlElementAttribute"
                || name == "XmlAttributeAttribute"
                || name == "XmlArrayAttribute"
                || name == "XmlArrayItemAttribute"
                || name == "XmlTextAttribute"
                || name == "XmlAnyElementAttribute"
                || name == "XmlAnyAttributeAttribute"
                || name == "XmlIgnoreAttribute")
                return true;
        }
        return false;
    }

    private bool ShouldPreserveField(FieldDefinition field)
    {
        if (_preserveAllReflectionFields
            || MatchesReflectionName(_reflectionFieldNames,
                _reflectionFieldNamesIgnoreCase, field.Name))
            return true;

        if (field.DeclaringType.IsEnum)
            return true;

        if (field.DeclaringType.IsSerializable)
            return true;

        if (field.Name.StartsWith("<"))
            return true;

        if (HasDataContractAttribute(field.DeclaringType))
            return true;

        if (HasFieldSerializationAttribute(field))
            return true;

        return false;
    }

    private void ConfigureReflectionTypePreservation(ModuleDefinition module)
    {
        _reflectionTypesToPreserve = new HashSet<TypeDefinition>();
        _reflectionNamespacesToPreserve = new HashSet<string>(
            StringComparer.Ordinal);

        var (typeNames, typeNamesIgnoreCase, preserveAllTypes) =
            FindReflectionTypeNames(module);
        foreach (var type in EnumerateAllTypes(module))
        {
            var fullName = ReflectionFullName(type);
            if (!preserveAllTypes
                && !typeNames.Any(name => TypeNameMatches(
                    name, fullName, StringComparison.Ordinal))
                && !typeNamesIgnoreCase.Any(name => TypeNameMatches(
                    name, fullName, StringComparison.OrdinalIgnoreCase)))
                continue;

            PreserveTypeIdentity(type);
        }

        var (nestedNames, nestedNamesIgnoreCase, preserveAllNested) =
            FindReflectionMemberNames(
            module, "GetNestedType", "GetDeclaredNestedType");
        foreach (var type in EnumerateAllTypes(module).Where(type => type.IsNested))
        {
            if (preserveAllNested
                || MatchesReflectionName(
                    nestedNames, nestedNamesIgnoreCase, type.Name))
                _reflectionTypesToPreserve.Add(type);
        }

        var (interfaceNames, interfaceNamesIgnoreCase,
            preserveAllInterfaces) =
            FindReflectionMemberNames(
                module, "GetInterface", typeInfoMethodName: null);
        foreach (var type in EnumerateAllTypes(module)
            .Where(type => type.IsInterface))
        {
            var reflectionFullName = ReflectionFullName(type);
            var fullNameLookup = MatchesReflectionName(
                interfaceNames, interfaceNamesIgnoreCase, reflectionFullName);
            if (!preserveAllInterfaces
                && !MatchesReflectionName(
                    interfaceNames, interfaceNamesIgnoreCase, type.Name)
                && !fullNameLookup)
                continue;

            _reflectionTypesToPreserve.Add(type);
            if (!preserveAllInterfaces && !fullNameLookup)
                continue;

            // Full-name and dynamic interface lookup both observe namespace
            // and any declaring-type components, not only the leaf name.
            var topLevel = type;
            while (topLevel.DeclaringType is not null)
            {
                topLevel = topLevel.DeclaringType;
                _reflectionTypesToPreserve.Add(topLevel);
            }
            if (!string.IsNullOrEmpty(topLevel.Namespace))
                _reflectionNamespacesToPreserve.Add(topLevel.Namespace);
        }
    }

    private void PreserveTypeIdentity(TypeDefinition type)
    {
        var topLevel = type;
        _reflectionTypesToPreserve.Add(type);
        while (topLevel.DeclaringType is not null)
        {
            topLevel = topLevel.DeclaringType;
            _reflectionTypesToPreserve.Add(topLevel);
        }
        if (!string.IsNullOrEmpty(topLevel.Namespace))
            _reflectionNamespacesToPreserve.Add(topLevel.Namespace);
    }

    private static bool TypeNameMatches(
        string lookupName,
        string metadataFullName,
        StringComparison comparison)
    {
        if (TypeNameMatchesAt(
                lookupName, 0, metadataFullName, comparison))
            return true;

        var brackets = new Stack<TypeNameBracketKind>();
        for (var index = 0; index < lookupName.Length; index++)
        {
            if (lookupName[index] == '[')
            {
                var kind = ClassifyTypeNameBracket(
                    lookupName, index, brackets);
                brackets.Push(kind);
                if ((kind == TypeNameBracketKind.QualifiedArgument
                        || kind == TypeNameBracketKind.GenericArguments)
                    && TypeNameMatchesAt(lookupName, index + 1,
                        metadataFullName, comparison))
                    return true;
            }
            else if (lookupName[index] == ',')
            {
                // A comma introduces another type only in the unqualified
                // generic-argument form. At the top level and inside [[...]],
                // it introduces assembly identity fields instead.
                if (brackets.TryPeek(out var kind)
                    && kind == TypeNameBracketKind.GenericArguments
                    && TypeNameMatchesAt(lookupName, index + 1,
                        metadataFullName, comparison))
                    return true;
            }
            else if (lookupName[index] == ']' && brackets.Count > 0)
            {
                brackets.Pop();
            }
        }
        return false;
    }

    private static bool TypeNameMatchesAt(
        string specification,
        int start,
        string metadataFullName,
        StringComparison comparison)
    {
        while (start < specification.Length
            && char.IsWhiteSpace(specification[start]))
            start++;
        if (start + metadataFullName.Length > specification.Length
            || !specification.AsSpan(start, metadataFullName.Length)
                .Equals(metadataFullName, comparison))
            return false;

        var end = start + metadataFullName.Length;
        while (end < specification.Length
            && char.IsWhiteSpace(specification[end]))
            end++;
        return end == specification.Length
            || specification[end] is '[' or ',' or ']' or '*' or '&';
    }

    private static TypeNameBracketKind ClassifyTypeNameBracket(
        string specification,
        int bracketIndex,
        Stack<TypeNameBracketKind> brackets)
    {
        var previous = bracketIndex - 1;
        while (previous >= 0 && char.IsWhiteSpace(specification[previous]))
            previous--;
        if (brackets.TryPeek(out var parent)
            && parent == TypeNameBracketKind.GenericArguments
            && (previous < 0 || specification[previous] is '[' or ','))
            return TypeNameBracketKind.QualifiedArgument;

        for (var index = previous; index >= 0
            && specification[index] is not '[' and not ']' and not ','; index--)
        {
            if (specification[index] == '`')
                return TypeNameBracketKind.GenericArguments;
        }
        return TypeNameBracketKind.ArrayModifier;
    }

    private enum TypeNameBracketKind
    {
        GenericArguments,
        QualifiedArgument,
        ArrayModifier,
    }

    private static (HashSet<string> Names,
        HashSet<string> IgnoreCaseNames, bool HasDynamicLookup)
        FindReflectionTypeNames(ModuleDefinition module)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var ignoreCaseNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var dynamic = false;
        foreach (var method in EnumerateAllTypes(module)
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody))
        {
            var instructions = method.Body.Instructions;
            for (var index = 0; index < instructions.Count; index++)
            {
                if (instructions[index].Operand is not MethodReference called)
                    continue;
                var isGetType = called.DeclaringType.FullName == "System.Type"
                    && called.Name == "GetType" && !called.HasThis;
                var isReflectionOnlyGetType = called.Name == "ReflectionOnlyGetType"
                    && called.DeclaringType.FullName == "System.Reflection.Assembly";
                if (!isGetType && !isReflectionOnlyGetType)
                    continue;

                var nameParameter = called.Parameters
                    .Select((parameter, ordinal) => (parameter, ordinal))
                    .FirstOrDefault(item => item.parameter.ParameterType.MetadataType
                        == MetadataType.String);
                if (nameParameter.parameter is null)
                    continue;
                var depth = called.Parameters.Count - 1 - nameParameter.ordinal;
                var resolved = new HashSet<string>(StringComparer.Ordinal);
                if (!TryResolveStringProducers(
                    method, index, depth, resolved,
                    new HashSet<VariableDefinition>()))
                {
                    dynamic = true;
                    continue;
                }

                if (LookupMayIgnoreCase(method, index, called))
                    ignoreCaseNames.UnionWith(resolved);
                else
                    names.UnionWith(resolved);
            }
        }
        return (names, ignoreCaseNames, dynamic);
    }

    private static string ReflectionFullName(TypeDefinition type)
    {
        if (type.DeclaringType is not null)
            return ReflectionFullName(type.DeclaringType) + "+" + type.Name;
        return string.IsNullOrEmpty(type.Namespace)
            ? type.Name
            : type.Namespace + "." + type.Name;
    }

    private static bool MatchesReflectionName(
        HashSet<string> exactNames,
        HashSet<string> ignoreCaseNames,
        string metadataName)
        => exactNames.Contains(metadataName)
            || ignoreCaseNames.Contains(metadataName);

    /// <summary>
    /// Finds statically known reflection names by tracing the actual name
    /// argument through evaluation-stack producers and local stores. If the
    /// value is dynamic, the caller preserves every member of that kind: this
    /// deliberately trades obfuscation strength for runtime correctness.
    /// </summary>
    private static (HashSet<string> Names,
        HashSet<string> IgnoreCaseNames, bool HasDynamicLookup)
        FindReflectionMemberNames(
            ModuleDefinition module,
            string? typeMethodName,
            string? typeInfoMethodName,
            string? runtimeExtensionMethodName = null,
            string? protectedImplMethodName = null)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var ignoreCaseNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var hasDynamicLookup = false;
        foreach (var method in EnumerateAllTypes(module)
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody))
        {
            var instructions = method.Body.Instructions;
            for (var index = 0; index < instructions.Count; index++)
            {
                if (instructions[index].Operand is not MethodReference called
                    || !(((typeMethodName is not null
                                && called.Name == typeMethodName)
                            || (protectedImplMethodName is not null
                                && called.Name == protectedImplMethodName))
                            && called.DeclaringType.FullName == "System.Type"
                        || (typeInfoMethodName is not null
                            && called.DeclaringType.FullName
                                == "System.Reflection.TypeInfo"
                            && called.Name == typeInfoMethodName)
                        || (runtimeExtensionMethodName is not null
                            && called.DeclaringType.FullName
                                == "System.Reflection.RuntimeReflectionExtensions"
                            && called.Name == runtimeExtensionMethodName)))
                    continue;

                var nameParameter = called.Parameters
                    .Select((parameter, ordinal) => (parameter, ordinal))
                    .FirstOrDefault(item =>
                        item.parameter.ParameterType.MetadataType
                            == MetadataType.String);
                if (nameParameter.parameter is null)
                    continue;
                var nameDepth = called.Parameters.Count - 1
                    - nameParameter.ordinal;
                var resolvedNames = new HashSet<string>(StringComparer.Ordinal);
                if (TryResolveStringProducers(
                    method, index, nameDepth, resolvedNames,
                    new HashSet<VariableDefinition>()))
                {
                    if (LookupMayIgnoreCase(method, index, called))
                        ignoreCaseNames.UnionWith(resolvedNames);
                    else
                        names.UnionWith(resolvedNames);
                }
                else
                {
                    hasDynamicLookup = true;
                }
            }
        }

        return (names, ignoreCaseNames, hasDynamicLookup);
    }

    private static bool LookupMayIgnoreCase(
        MethodDefinition method,
        int callIndex,
        MethodReference called)
    {
        foreach (var (parameter, ordinal) in called.Parameters
            .Select((parameter, ordinal) => (parameter, ordinal)))
        {
            var isBindingFlags = parameter.ParameterType.FullName
                == "System.Reflection.BindingFlags";
            var isIgnoreCaseBoolean = parameter.Name == "ignoreCase"
                && parameter.ParameterType.MetadataType == MetadataType.Boolean;
            if (!isBindingFlags && !isIgnoreCaseBoolean)
                continue;

            var depth = called.Parameters.Count - 1 - ordinal;
            if (!TryResolveInt32Producer(method, callIndex, depth, out var value))
                return true;
            if (isBindingFlags
                && (value & (int)System.Reflection.BindingFlags.IgnoreCase) != 0)
                return true;
            if (isIgnoreCaseBoolean && value != 0)
                return true;
        }

        return false;
    }

    private static bool TryResolveInt32Producer(
        MethodDefinition method,
        int beforeIndex,
        int depthFromTop,
        out int value)
    {
        value = 0;
        if (!TryFindStackProducer(
                method, beforeIndex, depthFromTop, out var producerIndex))
            return false;
        var instruction = method.Body.Instructions[producerIndex];
        value = instruction.OpCode.Code switch
        {
            Code.Ldc_I4_M1 => -1,
            Code.Ldc_I4_0 => 0,
            Code.Ldc_I4_1 => 1,
            Code.Ldc_I4_2 => 2,
            Code.Ldc_I4_3 => 3,
            Code.Ldc_I4_4 => 4,
            Code.Ldc_I4_5 => 5,
            Code.Ldc_I4_6 => 6,
            Code.Ldc_I4_7 => 7,
            Code.Ldc_I4_8 => 8,
            Code.Ldc_I4_S => (sbyte)instruction.Operand,
            Code.Ldc_I4 => (int)instruction.Operand,
            _ => 0,
        };
        return instruction.OpCode.Code is >= Code.Ldc_I4_M1 and <= Code.Ldc_I4;
    }

    private static bool TryResolveStringProducers(
        MethodDefinition method,
        int beforeIndex,
        int depthFromTop,
        HashSet<string> values,
        HashSet<VariableDefinition> visitedLocals)
    {
        if (!TryFindStackProducer(
                method, beforeIndex, depthFromTop,
                out var producerIndex))
            return false;

        var producer = method.Body.Instructions[producerIndex];
        if (producer.OpCode == OpCodes.Ldstr
            && producer.Operand is string literal)
        {
            values.Add(literal);
            return true;
        }

        if (!TryGetLoadedLocal(method, producer, out var local)
            || local is null
            || !visitedLocals.Add(local))
            return false;

        try
        {
            // Any address-taking permits an indirect or by-ref write that is
            // not represented by stloc. Treat the local as dynamic even when
            // every visible stloc contains the same literal.
            if (method.Body.Instructions.Any(instruction =>
                    TryGetAddressedLocal(method, instruction, out var addressed)
                    && addressed == local))
                return false;

            var foundStore = false;
            foreach (var (instruction, index) in method.Body.Instructions
                .Select((instruction, index) => (instruction, index)))
            {
                if (!TryGetStoredLocal(method, instruction, out var stored)
                    || stored != local)
                    continue;

                foundStore = true;
                if (!TryResolveStringProducers(
                    method, index, 0, values, visitedLocals))
                    return false;
            }

            return foundStore;
        }
        finally
        {
            visitedLocals.Remove(local);
        }
    }

    private static bool TryFindStackProducer(
        MethodDefinition method,
        int beforeIndex,
        int depthFromTop,
        out int producerIndex)
    {
        var instructions = method.Body.Instructions;
        if (method.Body.HasExceptionHandlers)
        {
            producerIndex = -1;
            return false;
        }

        var incomingTargets = instructions
            .SelectMany(instruction => instruction.Operand switch
            {
                Instruction target => [target],
                Instruction[] targets => targets,
                _ => Array.Empty<Instruction>(),
            })
            .ToHashSet();
        var depth = depthFromTop;
        for (var index = beforeIndex - 1; index >= 0; index--)
        {
            var instruction = instructions[index];
            // Linear reverse stack simulation is sound only inside one basic
            // block. A branch target or terminator means multiple producers
            // may feed the value, so force kind-wide conservative preservation.
            if ((index < beforeIndex - 1 && incomingTargets.Contains(instruction))
                || instruction.OpCode.FlowControl is FlowControl.Branch
                    or FlowControl.Cond_Branch or FlowControl.Return
                    or FlowControl.Throw)
            {
                producerIndex = -1;
                return false;
            }
            var pushes = GetPushCount(instruction);
            if (pushes > depth)
            {
                producerIndex = index;
                return true;
            }
            depth = depth - pushes + GetPopCount(instruction);
        }

        producerIndex = -1;
        return false;
    }

    private static int GetPushCount(Instruction instruction)
    {
        if (instruction.OpCode.StackBehaviourPush
            == StackBehaviour.Varpush)
        {
            if (instruction.OpCode == OpCodes.Newobj)
                return 1;
            return instruction.Operand is MethodReference method
                && method.ReturnType.MetadataType != MetadataType.Void ? 1 : 0;
        }

        return instruction.OpCode.StackBehaviourPush switch
        {
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1_push1 => 2,
            _ => 1,
        };
    }

    private static int GetPopCount(Instruction instruction)
    {
        if (instruction.OpCode.StackBehaviourPop == StackBehaviour.Varpop)
        {
            if (instruction.Operand is not MethodReference method)
                return 0;
            var count = method.Parameters.Count;
            if (instruction.OpCode != OpCodes.Newobj && method.HasThis)
                count++;
            return count;
        }

        return instruction.OpCode.StackBehaviourPop switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 or StackBehaviour.Popi
                or StackBehaviour.Popref => 1,
            StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1
                or StackBehaviour.Popi_popi or StackBehaviour.Popi_popi8
                or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8
                or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
            _ => 3,
        };
    }

    private static bool TryGetLoadedLocal(
        MethodDefinition method,
        Instruction instruction,
        out VariableDefinition? local)
    {
        local = instruction.OpCode.Code switch
        {
            Code.Ldloc_0 => method.Body.Variables.ElementAtOrDefault(0),
            Code.Ldloc_1 => method.Body.Variables.ElementAtOrDefault(1),
            Code.Ldloc_2 => method.Body.Variables.ElementAtOrDefault(2),
            Code.Ldloc_3 => method.Body.Variables.ElementAtOrDefault(3),
            Code.Ldloc or Code.Ldloc_S => instruction.Operand as VariableDefinition,
            _ => null,
        };
        return local is not null;
    }

    private static bool TryGetStoredLocal(
        MethodDefinition method,
        Instruction instruction,
        out VariableDefinition? local)
    {
        local = instruction.OpCode.Code switch
        {
            Code.Stloc_0 => method.Body.Variables.ElementAtOrDefault(0),
            Code.Stloc_1 => method.Body.Variables.ElementAtOrDefault(1),
            Code.Stloc_2 => method.Body.Variables.ElementAtOrDefault(2),
            Code.Stloc_3 => method.Body.Variables.ElementAtOrDefault(3),
            Code.Stloc or Code.Stloc_S => instruction.Operand as VariableDefinition,
            _ => null,
        };
        return local is not null;
    }

    private static bool TryGetAddressedLocal(
        MethodDefinition method,
        Instruction instruction,
        out VariableDefinition? local)
    {
        local = instruction.OpCode.Code switch
        {
            Code.Ldloca or Code.Ldloca_S =>
                instruction.Operand as VariableDefinition,
            _ => null,
        };
        return local is not null;
    }

    private static bool HasDataContractAttribute(TypeDefinition type)
    {
        if (!type.HasCustomAttributes)
            return false;
        foreach (var attr in type.CustomAttributes)
        {
            if (attr.AttributeType.Name
                == "DataContractAttribute")
                return true;
        }
        return false;
    }

    private static bool HasFieldSerializationAttribute(FieldDefinition field)
    {
        if (!field.HasCustomAttributes)
            return false;
        foreach (var attr in field.CustomAttributes)
        {
            var name = attr.AttributeType.Name;
            if (name == "JsonPropertyNameAttribute"
                || name == "DataMemberAttribute"
                || name == "JsonPropertyAttribute"
                || name == "XmlElementAttribute"
                || name == "XmlAttributeAttribute")
                return true;
        }
        return false;
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

    private static string GenerateUniqueName(Random rng, HashSet<string> used)
    {
        var length = 2;
        while (true)
        {
            var sb = new StringBuilder(length + 1);
            sb.Append('_');
            for (var i = 0; i < length; i++)
                sb.Append(AlphaNumChars[rng.Next(AlphaNumChars.Length)]);
            var candidate = sb.ToString();
            if (used.Add(candidate))
                return candidate;
            length++;
        }
    }
}
