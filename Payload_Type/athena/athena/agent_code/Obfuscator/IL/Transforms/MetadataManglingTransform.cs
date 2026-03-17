using System.Text;
using Mono.Cecil;
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
    };

    private static readonly char[] AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private readonly int _seed;
    private Dictionary<string, string> _renameMappings = new();

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
        using var input = new MemoryStream(assemblyBytes);
        var resolver = new DefaultAssemblyResolver();
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

        // Scope-level name sets to avoid collisions per scope
        var usedGlobal = new HashSet<string>(StringComparer.Ordinal);

        // First pass: collect and assign renames
        RenameNamespaces(asm.MainModule, rng, usedGlobal);

        foreach (var type in EnumerateAllTypes(asm.MainModule))
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

        foreach (var type in module.Types)
        {
            if (string.IsNullOrEmpty(type.Namespace))
                continue;

            if (!nsMap.TryGetValue(type.Namespace, out var newNs))
            {
                newNs = GenerateUniqueName(rng, used);
                nsMap[type.Namespace] = newNs;
                _renameMappings[type.Namespace] = newNs;
            }

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

        // Rename the type itself
        var originalTypeName = type.FullName;
        var newTypeName = GenerateUniqueName(rng, used);
        _renameMappings[originalTypeName] = newTypeName;
        type.Name = newTypeName;

        // Rename generic parameters on the type
        RenameGenericParameters(type.GenericParameters, rng, used);

        // Rename fields
        var usedFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in type.Fields)
            RenameField(field, rng, usedFields);

        // Rename events
        var usedEvents = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evt in type.Events)
            RenameEvent(evt, rng, usedEvents);

        // Rename methods
        var usedMethods = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in type.Methods)
            RenameMethod(method, rng, usedMethods);
    }

    private void RenameField(
        FieldDefinition field,
        Random rng,
        HashSet<string> used)
    {
        var original = field.Name;
        var newName = GenerateUniqueName(rng, used);
        _renameMappings[original] = newName;
        field.Name = newName;
    }

    private void RenameEvent(
        EventDefinition evt,
        Random rng,
        HashSet<string> used)
    {
        var original = evt.Name;
        var newName = GenerateUniqueName(rng, used);
        _renameMappings[original] = newName;
        evt.Name = newName;
    }

    private void RenameMethod(
        MethodDefinition method,
        Random rng,
        HashSet<string> used)
    {
        if (ShouldPreserveMethod(method))
            return;

        var original = method.Name;
        var newName = GenerateUniqueName(rng, used);
        _renameMappings[original] = newName;
        method.Name = newName;

        // Rename generic parameters on the method
        RenameGenericParameters(method.GenericParameters, rng, used);

        // Rename parameters
        foreach (var param in method.Parameters)
            RenameParameter(param, rng, used);
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

    private static bool ShouldPreserveMethod(MethodDefinition method)
    {
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

        // Keep interface method implementations where the interface
        // is defined in an external assembly
        if (IsExternalInterfaceImpl(method))
            return true;

        // Keep delegate methods — the CLR resolves Invoke,
        // BeginInvoke, and EndInvoke by name at runtime
        if (IsDelegate(method.DeclaringType))
            return true;

        // Keep properties decorated with [JsonPropertyName]
        if (HasJsonPropertyNameAttribute(method))
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

    private static bool IsExternalInterfaceImpl(MethodDefinition method)
    {
        var type = method.DeclaringType;
        foreach (var iface in type.Interfaces)
        {
            if (iface.InterfaceType.Scope is not AssemblyNameReference)
                continue;

            TypeDefinition? resolved;
            try
            {
                resolved = iface.InterfaceType.Resolve();
            }
            catch (AssemblyResolutionException)
            {
                return true;
            }

            if (resolved == null)
                continue;

            foreach (var ifaceMethod in resolved.Methods)
            {
                if (ifaceMethod.Name == method.Name)
                    return true;
            }
        }
        return false;
    }

    private static bool HasJsonPropertyNameAttribute(MethodDefinition method)
    {
        if (!method.HasCustomAttributes)
            return false;

        foreach (var attr in method.CustomAttributes)
        {
            if (attr.AttributeType.Name == "JsonPropertyNameAttribute")
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
