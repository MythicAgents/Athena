using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Obfuscator.Config;

/// <summary>
/// Scans the source declarations that form the host/plugin ABI.
/// Contract identity is retained as metadata name plus source provenance so
/// later semantic rewrites can distinguish same-named unrelated declarations.
/// </summary>
public static class ContractScanner
{
    private static readonly string[] ContractNamespaceRoots =
        ["Agent.Interfaces", "Agent.Models"];

    private static readonly Lazy<MetadataReference[]> PlatformReferences = new(
        () => ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray());

    public static ContractNames Scan(string contractsDir)
    {
        var trees = Directory.EnumerateFiles(
                contractsDir, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => CSharpSyntaxTree.ParseText(
                File.ReadAllText(path), path: path))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "ContractScan", trees, PlatformReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var declaredTypes = trees
            .SelectMany(tree => GetTypeDeclarations(tree.GetRoot())
                .Select(node => compilation.GetSemanticModel(tree)
                    .GetDeclaredSymbol(node)))
            .OfType<INamedTypeSymbol>()
            .Where(IsInContractNamespace)
            .GroupBy(GetMetadataName, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var allInterfaces = declaredTypes
            .Where(type => type.TypeKind == TypeKind.Interface)
            .ToArray();
        var roots = allInterfaces
            .Where(type => type.Name == "IPlugin"
                && type.ContainingNamespace.ToDisplayString()
                    .Equals("Agent.Interfaces", StringComparison.Ordinal))
            .ToArray();
        // Select only canonical roots and interfaces that descend from one of
        // those roots. Then close upward over the selected descendants so a
        // diamond contributes every ancestor without admitting siblings that
        // merely share one of those ancestors.
        var pluginFacing = new HashSet<INamedTypeSymbol>(
            roots, SymbolEqualityComparer.Default);
        foreach (var candidate in allInterfaces
            .OrderBy(GetMetadataName, StringComparer.Ordinal))
        {
            if (candidate.AllInterfaces.Any(ancestor =>
                    roots.Contains(
                        ancestor.OriginalDefinition,
                        SymbolEqualityComparer.Default)))
                pluginFacing.Add(candidate);
        }

        foreach (var descendant in pluginFacing.ToArray()
            .OrderBy(GetMetadataName, StringComparer.Ordinal))
            foreach (var ancestor in descendant.AllInterfaces)
                pluginFacing.Add(ancestor.OriginalDefinition);

        var members = new HashSet<string>(StringComparer.Ordinal);
        var contractDtos = new HashSet<INamedTypeSymbol>(
            SymbolEqualityComparer.Default);
        var pending = new Queue<ITypeSymbol>();
        foreach (var contractInterface in pluginFacing)
        {
            foreach (var inheritedInterface in contractInterface.Interfaces)
                pending.Enqueue(inheritedInterface);
            EnqueueTypeParameterConstraints(
                contractInterface.TypeParameters, pending);
            foreach (var member in contractInterface.GetMembers())
            {
                if (member.IsImplicitlyDeclared
                    || member is IMethodSymbol
                        { MethodKind: not MethodKind.Ordinary })
                    continue;
                members.Add(member.Name);
                EnqueueMemberTypes(member, pending);
            }
        }

        while (pending.TryDequeue(out var type))
        {
            foreach (var named in ExpandNamedTypes(type))
            {
                var definition = named.OriginalDefinition;
                if (pluginFacing.Contains(definition))
                    continue;
                if (!IsSourceDeclaration(definition)
                    || !IsInContractNamespace(definition)
                    || !contractDtos.Add(definition))
                    continue;

                if (definition.BaseType is { } baseType)
                    pending.Enqueue(baseType);
                foreach (var implementedInterface in definition.Interfaces)
                    pending.Enqueue(implementedInterface);
                EnqueueTypeParameterConstraints(definition.TypeParameters, pending);

                if (definition.TypeKind == TypeKind.Delegate)
                {
                    if (definition.DelegateInvokeMethod is { } invoke)
                        EnqueueMemberTypes(invoke, pending);
                    continue;
                }

                foreach (var member in definition.GetMembers())
                {
                    if (member.IsImplicitlyDeclared)
                        continue;

                    if (definition.TypeKind == TypeKind.Interface)
                    {
                        if (member is IMethodSymbol
                                { MethodKind: not MethodKind.Ordinary })
                            continue;
                        members.Add(member.Name);
                        EnqueueMemberTypes(member, pending);
                    }
                    else if (member is IPropertySymbol or IFieldSymbol)
                    {
                        members.Add(member.Name);
                        EnqueueMemberTypes(member, pending);
                    }
                }
            }
        }

        var selectedTypes = pluginFacing.Cast<INamedTypeSymbol>()
            .Concat(contractDtos)
            .GroupBy(GetMetadataName, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(GetMetadataName, StringComparer.Ordinal)
            .ToArray();
        var declarations = selectedTypes
            .SelectMany(type => type.DeclaringSyntaxReferences
                .GroupBy(reference => (
                    Path: NormalizeSourcePath(reference.SyntaxTree.FilePath),
                    RawKind: reference.GetSyntax().RawKind))
                .SelectMany(group => group
                    .OrderBy(reference => reference.Span.Start)
                    .Select((reference, ordinal) => new ContractDeclaration(
                        GetMetadataName(type),
                        group.Key.Path,
                        reference.Span.Start,
                        reference.Span.Length,
                        group.Key.RawKind,
                        ordinal))))
            .OrderBy(item => item.MetadataName, StringComparer.Ordinal)
            .ThenBy(item => item.FilePath, StringComparer.Ordinal)
            .ThenBy(item => item.SpanStart)
            .ToList();
        var recordParams = selectedTypes
            .SelectMany(type => type.DeclaringSyntaxReferences)
            .Select(reference => reference.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .Where(record => record.ParameterList is not null)
            .SelectMany(record => record.ParameterList!.Parameters)
            .Select(parameter => parameter.Identifier.ValueText)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        var namespaces = ContractNamespaceRoots
            .Where(root => declaredTypes.Any(type =>
                type.ContainingNamespace.ToDisplayString().Equals(
                    root, StringComparison.Ordinal)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return new ContractNames(
            pluginFacing.Select(type => type.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal).ToList(),
            members.OrderBy(name => name, StringComparer.Ordinal).ToList(),
            contractDtos.Select(type => type.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal).ToList(),
            namespaces,
            recordParams)
        {
            ContractDeclarations = declarations,
        };
    }

    private static IEnumerable<SyntaxNode> GetTypeDeclarations(SyntaxNode root) =>
        root.DescendantNodes().Where(node =>
            node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);

    private static void EnqueueMemberTypes(ISymbol member, Queue<ITypeSymbol> pending)
    {
        switch (member)
        {
            case IMethodSymbol method:
                pending.Enqueue(method.ReturnType);
                foreach (var parameter in method.Parameters)
                    pending.Enqueue(parameter.Type);
                EnqueueTypeParameterConstraints(method.TypeParameters, pending);
                break;
            case IPropertySymbol property:
                pending.Enqueue(property.Type);
                foreach (var parameter in property.Parameters)
                    pending.Enqueue(parameter.Type);
                break;
            case IFieldSymbol field:
                pending.Enqueue(field.Type);
                break;
            case IEventSymbol eventSymbol:
                pending.Enqueue(eventSymbol.Type);
                break;
        }
    }

    private static void EnqueueTypeParameterConstraints(
        IEnumerable<ITypeParameterSymbol> parameters,
        Queue<ITypeSymbol> pending)
    {
        foreach (var parameter in parameters)
            foreach (var constraint in parameter.ConstraintTypes)
                pending.Enqueue(constraint);
    }

    private static IEnumerable<INamedTypeSymbol> ExpandNamedTypes(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                foreach (var item in ExpandNamedTypes(array.ElementType))
                    yield return item;
                yield break;
            case IPointerTypeSymbol pointer:
                foreach (var item in ExpandNamedTypes(pointer.PointedAtType))
                    yield return item;
                yield break;
            case INamedTypeSymbol named:
                yield return named;
                foreach (var argument in named.TypeArguments)
                    foreach (var item in ExpandNamedTypes(argument))
                        yield return item;
                yield break;
        }
    }

    private static bool IsSourceDeclaration(INamedTypeSymbol type) =>
        type.Locations.Any(location => location.IsInSource);

    private static bool IsInContractNamespace(INamedTypeSymbol type)
    {
        var namespaceName = type.ContainingNamespace.ToDisplayString();
        return ContractNamespaceRoots.Any(root =>
            namespaceName.Equals(root, StringComparison.Ordinal)
            || namespaceName.StartsWith(root + ".", StringComparison.Ordinal));
    }

    internal static string GetMetadataName(INamedTypeSymbol type)
    {
        var ownName = type.MetadataName;
        if (type.ContainingType is not null)
            return GetMetadataName(type.ContainingType) + "+" + ownName;
        var namespaceName = type.ContainingNamespace.ToDisplayString();
        return string.IsNullOrEmpty(namespaceName)
            ? ownName
            : namespaceName + "." + ownName;
    }

    internal static string NormalizeSourcePath(string path) =>
        PathIdentity.Normalize(path);
}

public sealed record ContractDeclaration(
    string MetadataName,
    string FilePath,
    int SpanStart,
    int SpanLength,
    int DeclarationRawKind,
    int DeclarationOrdinal);

public record ContractNames(
    List<string> Interfaces,
    List<string> InterfaceMembers,
    List<string> Types,
    List<string> Namespaces,
    List<string> RecordParams)
{
    public List<ContractDeclaration> ContractDeclarations { get; init; } = [];
}
