using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Obfuscator.Source.Transforms;

/// <summary>
/// Plans and applies identifier renames to one immutable, semantically complete
/// C# project compilation. It is intentionally not wired into the publish path
/// until a project-graph planner can apply one map to every selected project.
/// </summary>
public static class AgentSemanticRenamer
{
    public static AgentSemanticProjectSetRenameResult TransformProjectSet(
        IReadOnlyList<CSharpCompilation> compilations,
        Guid payloadUuid,
        int seed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilations);
        if (compilations.Count == 0)
            throw new ArgumentException("At least one compilation is required.", nameof(compilations));
        foreach (var compilation in compilations)
            ThrowForErrors(compilation, "An input compilation is not semantically complete.");

        var plan = AgentSemanticRenamePlanner.Create(compilations, payloadUuid, seed, cancellationToken);
        var rewritten = compilations
            .Select(compilation => Rewrite(compilation, plan, cancellationToken))
            .ToArray();
        RebindProjectSetReferences(compilations, rewritten);
        return new AgentSemanticProjectSetRenameResult(rewritten, plan);
    }

    private static void RebindProjectSetReferences(
        IReadOnlyList<CSharpCompilation> originalCompilations,
        CSharpCompilation[] rewritten)
    {
        for (var index = 0; index < rewritten.Length; index++)
        {
            foreach (var reference in rewritten[index].References.ToArray())
            {
                var referencedIdentity = GetReferencedAssemblyIdentity(
                    originalCompilations[index], reference);
                if (referencedIdentity is null)
                    continue;

                var referencedIndexes = originalCompilations
                    .Select((compilation, candidateIndex) => (compilation, candidateIndex))
                    .Where(pair => pair.compilation.Assembly.Identity.Equals(referencedIdentity))
                    .Select(pair => pair.candidateIndex)
                    .ToArray();
                if (referencedIndexes.Length == 1)
                {
                    rewritten[index] = rewritten[index].ReplaceReference(
                        reference,
                        rewritten[referencedIndexes[0]].ToMetadataReference(
                            reference.Properties.Aliases,
                            reference.Properties.EmbedInteropTypes));
                }
            }
            ThrowForErrors(rewritten[index], "Semantic project-set renaming produced an invalid compilation.");
        }
    }

    private static AssemblyIdentity? GetReferencedAssemblyIdentity(
        CSharpCompilation compilation,
        MetadataReference reference) =>
        reference switch
        {
            CompilationReference compilationReference =>
                compilationReference.Compilation.Assembly.Identity,
            _ => compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly
                ? assembly.Identity
                : null,
        };

    public static AgentSemanticRenameResult Transform(
        CSharpCompilation compilation,
        Guid payloadUuid,
        int seed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ThrowForErrors(compilation, "The input compilation is not semantically complete.");

        var plan = AgentSemanticRenamePlanner.Create(compilation, payloadUuid, seed, cancellationToken);
        var rewritten = Rewrite(compilation, plan, cancellationToken);
        ThrowForErrors(rewritten, "Semantic renaming produced an invalid compilation.");
        return new AgentSemanticRenameResult(rewritten, plan);
    }

    private static CSharpCompilation Rewrite(
        CSharpCompilation compilation,
        AgentSemanticRenamePlan plan,
        CancellationToken cancellationToken)
    {
        var rewritten = compilation;
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var root = tree.GetRoot(cancellationToken);
            var newRoot = new SemanticRenameRewriter(model, plan, cancellationToken).Visit(root)
                ?? throw new AgentSemanticRenameException("Roslyn returned no rewritten syntax root.");
            rewritten = rewritten.ReplaceSyntaxTree(
                tree,
                tree.WithRootAndOptions(newRoot, tree.Options));
        }
        return rewritten;
    }

    private static void ThrowForErrors(Compilation compilation, string message)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length != 0)
            throw new AgentSemanticRenameException(message + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
    }
}

public sealed record AgentSemanticRenameResult(
    CSharpCompilation Compilation,
    AgentSemanticRenamePlan Plan);

public sealed record AgentSemanticProjectSetRenameResult(
    IReadOnlyList<CSharpCompilation> Compilations,
    AgentSemanticRenamePlan Plan);

public sealed class AgentSemanticRenameException : InvalidOperationException
{
    public AgentSemanticRenameException(string message) : base(message) { }
}

public sealed class AgentSemanticRenamePlan
{
    private readonly IReadOnlyDictionary<ISymbol, string> _names;
    private readonly IReadOnlyDictionary<string, string> _namesByMetadataIdentity;

    internal AgentSemanticRenamePlan(
        IReadOnlyDictionary<ISymbol, string> names,
        IReadOnlyDictionary<string, string> namesBySymbolKey,
        IReadOnlyDictionary<string, string> namesByMetadataIdentity)
    {
        _names = names;
        NamesBySymbolKey = namesBySymbolKey;
        _namesByMetadataIdentity = namesByMetadataIdentity;
    }

    public IReadOnlyDictionary<string, string> NamesBySymbolKey { get; }

    internal bool TryGetName(ISymbol? symbol, out string name)
    {
        if (symbol is not null)
        {
            symbol = Normalize(symbol);
            if (_names.TryGetValue(symbol, out name!))
                return true;
            if (_namesByMetadataIdentity.TryGetValue(
                    AgentSemanticRenamePlanner.CanonicalMetadataIdentity(symbol), out name!))
                return true;
        }
        name = string.Empty;
        return false;
    }

    internal static ISymbol Normalize(ISymbol symbol) => symbol switch
    {
        IAliasSymbol alias => alias.Target,
        IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor } method
            => method.ContainingType,
        IMethodSymbol { ReducedFrom: { } reducedFrom } => reducedFrom.OriginalDefinition,
        IParameterSymbol parameter => NormalizeParameter(parameter),
        _ => symbol.OriginalDefinition
    };

    private static IParameterSymbol NormalizeParameter(IParameterSymbol parameter)
    {
        var original = parameter.OriginalDefinition;
        if (original.ContainingSymbol is IMethodSymbol { ReducedFrom: { } reduced }
            && original.Ordinal + 1 < reduced.OriginalDefinition.Parameters.Length)
            return reduced.OriginalDefinition.Parameters[original.Ordinal + 1];

        if (original.ContainingSymbol is IMethodSymbol { AssociatedSymbol: IPropertySymbol { IsIndexer: true } indexer }
            && original.Ordinal < indexer.OriginalDefinition.Parameters.Length)
            return indexer.OriginalDefinition.Parameters[original.Ordinal];

        return original;
    }
}

public static class AgentSemanticRenamePlanner
{
    private const string HashDomain = "athena.agent.semantic-rename.v1";
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public static AgentSemanticRenamePlan Create(
        CSharpCompilation compilation,
        Guid payloadUuid,
        int seed,
        CancellationToken cancellationToken = default) =>
        Create(new[] { compilation }, payloadUuid, seed, cancellationToken);

    public static AgentSemanticRenamePlan Create(
        IReadOnlyList<CSharpCompilation> compilations,
        Guid payloadUuid,
        int seed,
        CancellationToken cancellationToken = default)
    {
        ValidateInputCompilations(compilations, cancellationToken);

        var (candidates, inferredNameDependencies) =
            CollectCandidatesAndInferredDependencies(compilations, cancellationToken);
        var recordParameterPairs = AddSynthesizedRecordProperties(candidates);
        var union = BuildInitialSymbolUnion(candidates, recordParameterPairs);
        var preserved = CollectPreservedSymbols(
            compilations, candidates, inferredNameDependencies, union, cancellationToken);

        var groups = candidates
            .GroupBy(union.Find, SymbolEqualityComparer.Default)
            .Select(group => group.ToArray())
            .ToArray();
        PropagateGroupPreservation(groups, preserved);

        var allocatable = groups
            .Where(group => !group.Any(preserved.Contains))
            .Select(group => new AllocationGroup(
                group,
                group.Select(FamilyIdentity)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal).ToArray()))
            .OrderBy(group => group.CanonicalKey, StringComparer.Ordinal)
            .ToArray();

        return AllocateSymbolNames(allocatable, candidates, payloadUuid, seed, cancellationToken);
    }

    private static void ValidateInputCompilations(
        IReadOnlyList<CSharpCompilation> compilations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compilations);
        if (compilations.Count == 0)
            throw new ArgumentException("At least one compilation is required.", nameof(compilations));
        var inputErrors = compilations
            .SelectMany(compilation => compilation.GetDiagnostics(cancellationToken))
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (inputErrors.Length != 0)
        {
            throw new AgentSemanticRenameException(
                "Cannot plan semantic renames for a compilation with errors."
                + Environment.NewLine
                + string.Join(Environment.NewLine, inputErrors.Select(error => error.ToString())));
        }
    }

    private static (HashSet<ISymbol> Candidates, HashSet<ISymbol> InferredNameDependencies)
        CollectCandidatesAndInferredDependencies(
            IReadOnlyList<CSharpCompilation> compilations,
            CancellationToken cancellationToken)
    {
        var candidates = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var inferredNameDependencies = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var compilation in compilations)
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            foreach (var node in tree.GetRoot(cancellationToken).DescendantNodesAndSelf())
            {
                if (IsDeclarationNode(node))
                {
                    var symbol = model.GetDeclaredSymbol(node, cancellationToken)
                        ?? throw new AgentSemanticRenameException(
                            $"Could not bind renameable declaration '{node.Kind()}' at {node.GetLocation().GetLineSpan()}.");
                    AddCandidateSymbols(candidates, symbol);
                }

                if (IsInferredAnonymousOrTupleMember(node, out var expression))
                {
                    var symbol = model.GetSymbolInfo(expression, cancellationToken).Symbol;
                    if (symbol is not null && IsSourceOwned(symbol))
                        inferredNameDependencies.Add(AgentSemanticRenamePlan.Normalize(symbol));
                }
            }
        }

        return (candidates, inferredNameDependencies);
    }

    private static SymbolUnion BuildInitialSymbolUnion(
        HashSet<ISymbol> candidates,
        IReadOnlyList<(ISymbol Parameter, ISymbol Property)> recordParameterPairs)
    {
        var union = new SymbolUnion(candidates);
        foreach (var namespaces in candidates
            .OfType<INamespaceSymbol>()
            .GroupBy(CanonicalMetadataIdentity, StringComparer.Ordinal))
        {
            var symbols = namespaces.Cast<ISymbol>().ToArray();
            for (var index = 1; index < symbols.Length; index++)
                union.Union(symbols[0], symbols[index]);
        }
        foreach (var (parameter, property) in recordParameterPairs)
            union.Union(parameter, property);
        LinkOverloadFamilies(candidates, union);
        return union;
    }

    private static HashSet<ISymbol> CollectPreservedSymbols(
        IReadOnlyList<CSharpCompilation> compilations,
        HashSet<ISymbol> candidates,
        HashSet<ISymbol> inferredNameDependencies,
        SymbolUnion union,
        CancellationToken cancellationToken)
    {
        var preserved = new HashSet<ISymbol>(inferredNameDependencies, SymbolEqualityComparer.Default);
        foreach (var namespaceSymbol in candidates.OfType<INamespaceSymbol>()
            .Where(IsExternallyAugmentedNamespace))
            preserved.Add(namespaceSymbol);
        foreach (var compilation in compilations)
            PreserveEntryPoint(compilation, preserved, cancellationToken);
        PreserveReflectionContracts(compilations, candidates, preserved, cancellationToken);
        var candidatesByMetadataIdentity = candidates
            .GroupBy(CanonicalMetadataIdentity, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var compilation in compilations)
        {
            BuildAbiFamilies(compilation, candidates, candidatesByMetadataIdentity,
                union, preserved, cancellationToken);
        }
        return preserved;
    }

    private static void PropagateGroupPreservation(
        IReadOnlyList<ISymbol[]> groups,
        HashSet<ISymbol> preserved)
    {
        foreach (var group in groups.Where(g => g.Any(preserved.Contains)))
        {
            foreach (var symbol in group)
                preserved.Add(symbol);
        }
    }

    private static AgentSemanticRenamePlan AllocateSymbolNames(
        IReadOnlyList<AllocationGroup> allocatable,
        HashSet<ISymbol> candidates,
        Guid payloadUuid,
        int seed,
        CancellationToken cancellationToken)
    {
        var used = new HashSet<string>(candidates.Select(symbol => symbol.Name), StringComparer.Ordinal);
        var names = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        var byKey = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var byMetadataIdentity = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var uuid = payloadUuid.ToString("N");

        foreach (var group in allocatable)
        {
            string generated;
            var attempt = 0;
            do
            {
                generated = GenerateName(uuid, seed, group.CanonicalKey, attempt++);
            } while (!used.Add(generated));

            foreach (var symbol in group.Symbols)
            {
                names[symbol] = generated;
                byKey[StableSymbolKey(symbol, cancellationToken)] = generated;
                byMetadataIdentity[CanonicalMetadataIdentity(symbol)] = generated;
            }
        }

        return new AgentSemanticRenamePlan(
            names.ToImmutableDictionary(SymbolEqualityComparer.Default),
            byKey.ToImmutableSortedDictionary(StringComparer.Ordinal),
            byMetadataIdentity.ToImmutableSortedDictionary(StringComparer.Ordinal));
    }

    private static bool IsDeclarationNode(SyntaxNode node) => node is
        BaseNamespaceDeclarationSyntax or BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or
        MethodDeclarationSyntax or LocalFunctionStatementSyntax or PropertyDeclarationSyntax or
        EventDeclarationSyntax or EnumMemberDeclarationSyntax or VariableDeclaratorSyntax or
        ParameterSyntax or TypeParameterSyntax or SingleVariableDesignationSyntax or
        CatchDeclarationSyntax { Identifier.RawKind: not 0 } or ForEachStatementSyntax or FromClauseSyntax or LetClauseSyntax or
        JoinClauseSyntax or JoinIntoClauseSyntax or QueryContinuationSyntax;

    private static void AddCandidateSymbols(HashSet<ISymbol> candidates, ISymbol symbol)
    {
        for (ISymbol? current = symbol; current is not null;
             current = (current as INamespaceSymbol)?.ContainingNamespace)
        {
            var normalized = AgentSemanticRenamePlan.Normalize(current);
            if (IsCandidateSymbol(normalized))
                candidates.Add(normalized);
        }
    }

    private static bool IsCandidateSymbol(ISymbol symbol) =>
        IsRenameable(symbol)
        && IsSourceOwned(symbol)
        && (!IsGeneratedOnly(symbol) || IsConfigurableJsonTypeInfoProperty(symbol));

    private static bool IsRenameable(ISymbol symbol) => symbol switch
    {
        INamespaceSymbol { IsGlobalNamespace: false } => true,
        INamedTypeSymbol => true,
        IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation or MethodKind.LocalFunction } => true,
        IPropertySymbol or IFieldSymbol or IEventSymbol or IParameterSymbol or ITypeParameterSymbol
            or ILocalSymbol or IRangeVariableSymbol => true,
        _ => false
    };

    private static bool IsSourceOwned(ISymbol symbol) =>
        symbol.Locations.Any(location => location.IsInSource);

    private static bool IsGeneratedOnly(ISymbol symbol) =>
        symbol.DeclaringSyntaxReferences.Length != 0
        && symbol.DeclaringSyntaxReferences.All(reference =>
        {
            var path = reference.SyntaxTree.FilePath;
            return !string.IsNullOrEmpty(path) && (!File.Exists(path)
                || path.Split(Path.DirectorySeparatorChar)
                    .Any(segment => segment is "obj" or "bin"));
        });

    private static bool IsConfigurableJsonTypeInfoProperty(ISymbol symbol) =>
        symbol is IPropertySymbol
        {
            Type: INamedTypeSymbol
            {
                Name: "JsonTypeInfo",
                TypeArguments.Length: 1,
                ContainingNamespace: { } metadataNamespace
            } propertyType
        }
        && metadataNamespace.ToDisplayString() == "System.Text.Json.Serialization.Metadata"
        && IsSourceOwned(propertyType.TypeArguments[0]);

    private static bool IsExternallyAugmentedNamespace(INamespaceSymbol namespaceSymbol) =>
        namespaceSymbol.ConstituentNamespaces.Any(constituent => !IsSourceOwned(constituent));

    private static IReadOnlyList<(ISymbol Parameter, ISymbol Property)> AddSynthesizedRecordProperties(
        HashSet<ISymbol> candidates)
    {
        var pairs = new List<(ISymbol, ISymbol)>();
        foreach (var parameter in candidates.OfType<IParameterSymbol>().ToArray())
        {
            if (parameter.ContainingSymbol is not IMethodSymbol
                { MethodKind: MethodKind.Constructor, ContainingType.IsRecord: true } constructor)
                continue;
            var property = constructor.ContainingType.GetMembers(parameter.Name)
                .OfType<IPropertySymbol>()
                .FirstOrDefault();
            if (property is null)
                continue;
            property = (IPropertySymbol)AgentSemanticRenamePlan.Normalize(property);
            candidates.Add(property);
            pairs.Add((parameter, property));
        }
        return pairs;
    }

    private static bool IsInferredAnonymousOrTupleMember(
        SyntaxNode node, out ExpressionSyntax expression)
    {
        if (node is AnonymousObjectMemberDeclaratorSyntax { NameEquals: null } anonymous)
        {
            expression = anonymous.Expression;
            return expression is IdentifierNameSyntax or MemberAccessExpressionSyntax;
        }
        if (node is ArgumentSyntax { NameColon: null, Parent: TupleExpressionSyntax } tuple)
        {
            expression = tuple.Expression;
            return expression is IdentifierNameSyntax or MemberAccessExpressionSyntax;
        }
        expression = null!;
        return false;
    }

    private static void LinkOverloadFamilies(
        HashSet<ISymbol> candidates,
        SymbolUnion union)
    {
        // A method-group or nameof reference can bind to several overloads but
        // has only one identifier token. Keep each source overload set under
        // one generated spelling so every such reference remains representable.
        foreach (var containingTypeGroup in candidates
            .OfType<IMethodSymbol>()
            .Where(method => method.MethodKind == MethodKind.Ordinary)
            .GroupBy(method => method.ContainingType, SymbolEqualityComparer.Default))
        {
            foreach (var overloads in containingTypeGroup
                .GroupBy(method => method.Name, StringComparer.Ordinal))
            {
                var members = overloads.Cast<ISymbol>().ToArray();
                for (var index = 1; index < members.Length; index++)
                    union.Union(members[0], members[index]);
            }
        }
    }

    private static void PreserveEntryPoint(
        CSharpCompilation compilation,
        ISet<ISymbol> preserved,
        CancellationToken cancellationToken)
    {
        var entryPoint = compilation.GetEntryPoint(cancellationToken);
        if (entryPoint is not null)
            preserved.Add(AgentSemanticRenamePlan.Normalize(entryPoint));
    }

    private static void PreserveReflectionContracts(
        IReadOnlyList<CSharpCompilation> compilations,
        HashSet<ISymbol> candidates,
        ISet<ISymbol> preserved,
        CancellationToken cancellationToken)
    {
        foreach (var compilation in compilations)
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            foreach (var invocation in tree.GetRoot(cancellationToken)
                         .DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation)
                    continue;

                PreserveJsonSerializationContracts(operation, candidates, preserved);
                if (!TryGetConstantStringArgument(operation, out var reflectedName))
                    continue;

                var method = operation.TargetMethod.OriginalDefinition;
                var containingType = method.ContainingType.ToDisplayString();
                if (method.Name == "GetType" && containingType is "System.Type" or "System.Reflection.Assembly")
                {
                    PreserveReflectedTypes(reflectedName, compilations, candidates, preserved);
                    continue;
                }

                if (containingType != "System.Type"
                    || method.Name is not ("GetMethod" or "GetProperty" or "GetField"
                        or "GetEvent" or "GetMember" or "InvokeMember"))
                    continue;

                var reflectedType = UnwrapConversion(operation.Instance) is ITypeOfOperation typeOf
                    ? typeOf.TypeOperand
                    : null;
                PreserveReflectedMembers(reflectedName, reflectedType,
                    includeNestedTypes: method.Name == "GetMember", candidates, preserved);
            }
        }
    }

    private static void PreserveJsonSerializationContracts(
        IInvocationOperation operation,
        HashSet<ISymbol> candidates,
        ISet<ISymbol> preserved)
    {
        if (!IsReflectionJsonCall(operation.TargetMethod))
            return;
        var reachable = CollectReachableJsonTypes(GetJsonTargetTypes(operation));
        var identities = reachable
            .SelectMany(GetUnannotatedJsonMembers)
            .Select(CanonicalMetadataIdentity)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var candidate in candidates.Where(s => identities.Contains(CanonicalMetadataIdentity(s))))
            preserved.Add(candidate);
    }

    private static bool IsReflectionJsonCall(IMethodSymbol method)
    {
        var containingType = method.ContainingType.ToDisplayString();
        if (containingType is not ("System.Text.Json.JsonSerializer" or "Newtonsoft.Json.JsonConvert"))
            return false;
        if (!method.Name.StartsWith("Serialize", StringComparison.Ordinal)
            && !method.Name.StartsWith("Deserialize", StringComparison.Ordinal))
            return false;
        return !method.Parameters.Any(parameter =>
            parameter.Type.Name is "JsonTypeInfo" or "JsonSerializerContext");
    }

    private static IEnumerable<ITypeSymbol> GetJsonTargetTypes(IInvocationOperation operation)
    {
        foreach (var typeArgument in operation.TargetMethod.TypeArguments)
            yield return typeArgument;
        foreach (var argument in operation.Arguments)
        {
            if (UnwrapConversion(argument.Value) is ITypeOfOperation typeOf)
                yield return typeOf.TypeOperand;
        }
        if (operation.TargetMethod.Name.StartsWith("Serialize", StringComparison.Ordinal)
            && operation.Arguments.Length > 0
            && operation.Arguments[0].Value.Type is { } firstArgType)
            yield return firstArgType;
    }

    private static HashSet<INamedTypeSymbol> CollectReachableJsonTypes(IEnumerable<ITypeSymbol> roots)
    {
        var pending = new Queue<ITypeSymbol>(roots);
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        while (pending.TryDequeue(out var current))
        {
            if (current is IArrayTypeSymbol array)
                pending.Enqueue(array.ElementType);
            if (current is not INamedTypeSymbol named)
                continue;
            foreach (var arg in named.TypeArguments)
                pending.Enqueue(arg);
            if (IsSourceOwned(named.OriginalDefinition) && visited.Add(named.OriginalDefinition))
                EnqueueJsonTypeRelationships(named.OriginalDefinition, pending);
        }
        return visited;
    }

    private static void EnqueueJsonTypeRelationships(
        INamedTypeSymbol type, Queue<ITypeSymbol> pending)
    {
        if (type.BaseType is { } baseType)
            pending.Enqueue(baseType);
        foreach (var prop in type.GetMembers().OfType<IPropertySymbol>().Where(IsPublicInstanceMember))
            pending.Enqueue(prop.Type);
        foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(IsPublicInstanceMember))
            pending.Enqueue(field.Type);
    }

    private static IEnumerable<ISymbol> GetUnannotatedJsonMembers(INamedTypeSymbol type)
    {
        var members = type.GetMembers()
            .Where(member => member is IPropertySymbol or IFieldSymbol)
            .Where(member => IsPublicInstanceMember(member) && !HasExplicitJsonAttribute(member));
        var ctorParams = type.InstanceConstructors
            .Where(IsPublicInstanceMember)
            .SelectMany(ctor => ctor.Parameters);
        return members.Concat(ctorParams).Select(AgentSemanticRenamePlan.Normalize);
    }

    private static bool IsPublicInstanceMember(ISymbol symbol) =>
        symbol.DeclaredAccessibility == Accessibility.Public && !symbol.IsStatic;

    private static bool HasExplicitJsonAttribute(ISymbol symbol) =>
        symbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name is "JsonPropertyNameAttribute"
                or "JsonPropertyAttribute"
                or "JsonIgnoreAttribute");

    private static bool TryGetConstantStringArgument(
        IInvocationOperation invocation, out string value)
    {
        var argument = invocation.Arguments.FirstOrDefault(candidate => candidate.Parameter?.Ordinal == 0);
        if (argument?.Value.ConstantValue is { HasValue: true, Value: string constant })
        {
            value = constant;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static IOperation? UnwrapConversion(IOperation? operation)
    {
        while (operation is IConversionOperation conversion)
            operation = conversion.Operand;
        return operation;
    }

    private static void PreserveReflectedTypes(
        string reflectedName,
        IReadOnlyList<CSharpCompilation> compilations,
        HashSet<ISymbol> candidates,
        ISet<ISymbol> preserved)
    {
        if (!ReflectionTypeNameParser.TryParse(reflectedName, out var components))
            return;

        foreach (var component in components)
        {
            foreach (var type in FindReflectedTypeCandidates(component, compilations, candidates))
                PreserveTypeIdentity(type, candidates, preserved);
        }
    }

    private static IEnumerable<INamedTypeSymbol> FindReflectedTypeCandidates(
        ReflectedTypeComponent component,
        IReadOnlyList<CSharpCompilation> compilations,
        HashSet<ISymbol> candidates)
    {
        var candidateTypes = candidates.OfType<INamedTypeSymbol>().ToArray();
        var directMatches = candidateTypes
            .Where(type => MetadataTypeName(type) == component.MetadataName
                && MatchesAssembly(type, component.AssemblyName))
            .ToArray();
        if (directMatches.Length != 0)
            return directMatches;

        // GetTypeByMetadataName deliberately returns null for ambiguous identities.
        // Preserve every exact source candidate rather than choosing one by spelling.
        var resolvedIdentities = compilations
            .Select(compilation => compilation.GetTypeByMetadataName(component.MetadataName))
            .OfType<INamedTypeSymbol>()
            .Where(type => MatchesAssembly(type, component.AssemblyName))
            .Select(CanonicalMetadataIdentity)
            .ToHashSet(StringComparer.Ordinal);
        return candidateTypes.Where(type =>
            resolvedIdentities.Contains(CanonicalMetadataIdentity(type)));
    }

    private static bool MatchesAssembly(INamedTypeSymbol type, string? assemblyName) =>
        assemblyName is null || type.ContainingAssembly.Identity.Name == assemblyName;

    private readonly record struct ReflectedTypeComponent(string MetadataName, string? AssemblyName);

    private sealed class ReflectionTypeNameParser
    {
        private readonly string _text;
        private readonly List<ReflectedTypeComponent> _components = new();
        private int _position;

        private ReflectionTypeNameParser(string text) => _text = text;

        internal static bool TryParse(
            string text, out IReadOnlyList<ReflectedTypeComponent> components)
        {
            var parser = new ReflectionTypeNameParser(text);
            var parsed = parser.TryParseTypeSpec(allowAssemblyName: true, closingDelimiter: '\0');
            parser.SkipWhitespace();
            components = parsed && parser._position == text.Length
                ? parser._components
                : Array.Empty<ReflectedTypeComponent>();
            return components.Count != 0;
        }

        private bool TryParseTypeSpec(bool allowAssemblyName, char closingDelimiter)
        {
            SkipWhitespace();
            var nameStart = _position;
            while (_position < _text.Length
                   && _text[_position] is not ('[' or ']' or '*' or '&' or ','))
                _position++;
            var metadataName = _text[nameStart.._position].Trim();
            if (metadataName.Length == 0)
                return false;

            var componentIndex = _components.Count;
            _components.Add(new ReflectedTypeComponent(metadataName, null));

            if (!TryParseGenericArguments() || !TrySkipTypeModifiers())
                return false;

            SkipWhitespace();
            return !allowAssemblyName
                || _position >= _text.Length
                || _text[_position] != ','
                || TryParseAssemblyQualification(componentIndex, metadataName, closingDelimiter);
        }

        private bool TryParseGenericArguments()
        {
            if (_position >= _text.Length || _text[_position] != '[' || IsArrayModifier())
                return true;

            _position++;
            var firstArgument = true;
            while (true)
            {
                SkipWhitespace();
                if (_position >= _text.Length || _text[_position] == ']')
                    return false;
                if (!firstArgument)
                {
                    if (_text[_position] != ',')
                        return false;
                    _position++;
                    SkipWhitespace();
                }

                var bracketed = _position < _text.Length && _text[_position] == '[';
                if (bracketed)
                    _position++;
                if (!TryParseTypeSpec(bracketed, ']'))
                    return false;
                SkipWhitespace();
                if (bracketed && (_position >= _text.Length || _text[_position++] != ']'))
                    return false;
                SkipWhitespace();

                firstArgument = false;
                if (_position < _text.Length && _text[_position] == ']')
                {
                    _position++;
                    return true;
                }
            }
        }

        private bool TrySkipTypeModifiers()
        {
            while (_position < _text.Length && _text[_position] == '[' && IsArrayModifier())
            {
                _position++;
                while (_position < _text.Length && _text[_position] != ']')
                    _position++;
                if (_position >= _text.Length)
                    return false;
                _position++;
            }
            while (_position < _text.Length && _text[_position] == '*')
                _position++;
            if (_position < _text.Length && _text[_position] == '&')
                _position++;
            return true;
        }

        private bool TryParseAssemblyQualification(
            int componentIndex,
            string metadataName,
            char closingDelimiter)
        {
            _position++;
            SkipWhitespace();
            var assemblyStart = _position;
            while (_position < _text.Length && _text[_position] is not (',' or ']'))
                _position++;
            var assemblyName = _text[assemblyStart.._position].Trim();
            if (assemblyName.Length == 0)
                return false;
            _components[componentIndex] = new ReflectedTypeComponent(metadataName, assemblyName);

            while (_position < _text.Length && _text[_position] != closingDelimiter)
                _position++;
            return true;
        }

        private bool IsArrayModifier()
        {
            if (_position + 1 >= _text.Length)
                return false;
            return _text[_position + 1] is ']' or ',' or '*'
                or '-' or '.' or >= '0' and <= '9';
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                _position++;
        }
    }

    private static void PreserveTypeIdentity(
        INamedTypeSymbol type,
        HashSet<ISymbol> candidates,
        ISet<ISymbol> preserved)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
            preserved.Add(AgentSemanticRenamePlan.Normalize(current));
        for (var current = type.ContainingNamespace;
             current is { IsGlobalNamespace: false };
             current = current.ContainingNamespace)
        {
            var candidate = candidates.FirstOrDefault(symbol => symbol is INamespaceSymbol
                && CanonicalMetadataIdentity(symbol) == CanonicalMetadataIdentity(current));
            if (candidate is not null)
                preserved.Add(candidate);
        }
    }

    private static void PreserveReflectedMembers(
        string reflectedName,
        ITypeSymbol? reflectedType,
        bool includeNestedTypes,
        HashSet<ISymbol> candidates,
        ISet<ISymbol> preserved)
    {
        HashSet<string>? containingTypes = null;
        if (reflectedType is INamedTypeSymbol namedType)
        {
            containingTypes = new HashSet<string>(StringComparer.Ordinal);
            for (var current = namedType; current is not null; current = current.BaseType)
                containingTypes.Add(CanonicalMetadataIdentity(current));
        }

        foreach (var candidate in candidates.Where(symbol =>
                     symbol.Name == reflectedName
                     && (symbol is IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol
                         || includeNestedTypes && symbol is INamedTypeSymbol)
                     && (containingTypes is null || symbol.ContainingType is not null
                         && containingTypes.Contains(CanonicalMetadataIdentity(symbol.ContainingType)))))
        {
            if (candidate is INamedTypeSymbol nestedType)
                PreserveTypeIdentity(nestedType, candidates, preserved);
            else
                preserved.Add(candidate);
        }
    }

    private static void BuildAbiFamilies(
        CSharpCompilation compilation,
        HashSet<ISymbol> candidates,
        IReadOnlyDictionary<string, ISymbol[]> candidatesByMetadataIdentity,
        SymbolUnion union,
        HashSet<ISymbol> preserved,
        CancellationToken cancellationToken)
    {
        foreach (var symbol in candidates.ToArray())
        {
            if (symbol is IMethodSymbol method)
            {
                LinkOverride(method, method.OverriddenMethod, candidates,
                    candidatesByMetadataIdentity, union, preserved);
                if (HasImplicitInteropEntryPoint(method))
                    preserved.Add(symbol);
            }
            else if (symbol is IPropertySymbol property)
                LinkOverride(property, property.OverriddenProperty, candidates,
                    candidatesByMetadataIdentity, union, preserved);
            else if (symbol is IEventSymbol @event)
                LinkOverride(@event, @event.OverriddenEvent, candidates,
                    candidatesByMetadataIdentity, union, preserved);
        }

        foreach (var type in EnumerateSourceTypes(compilation.Assembly.GlobalNamespace))
        {
            foreach (var @interface in type.AllInterfaces)
            foreach (var member in @interface.GetMembers())
            {
                var implementation = type.FindImplementationForInterfaceMember(member);
                if (implementation is null)
                    continue;
                implementation = AgentSemanticRenamePlan.Normalize(implementation);
                if (!candidates.Contains(implementation))
                    continue;
                var interfaceMember = AgentSemanticRenamePlan.Normalize(member);
                if (TryFindSourceEquivalent(interfaceMember, candidates,
                        candidatesByMetadataIdentity, out var sourceInterfaceMember))
                    union.Union(implementation, sourceInterfaceMember);
                else
                    preserved.Add(implementation);
            }
        }
    }

    private static void LinkOverride(
        ISymbol symbol,
        ISymbol? overridden,
        HashSet<ISymbol> candidates,
        IReadOnlyDictionary<string, ISymbol[]> candidatesByMetadataIdentity,
        SymbolUnion union,
        HashSet<ISymbol> preserved)
    {
        if (overridden is null)
            return;
        overridden = AgentSemanticRenamePlan.Normalize(overridden);
        if (TryFindSourceEquivalent(overridden, candidates,
                candidatesByMetadataIdentity, out var sourceOverridden))
            union.Union(symbol, sourceOverridden);
        else
            preserved.Add(symbol);
    }

    private static bool TryFindSourceEquivalent(
        ISymbol symbol,
        HashSet<ISymbol> candidates,
        IReadOnlyDictionary<string, ISymbol[]> candidatesByMetadataIdentity,
        out ISymbol sourceSymbol)
    {
        if (IsSourceOwned(symbol) && candidates.Contains(symbol))
        {
            sourceSymbol = symbol;
            return true;
        }

        if (candidatesByMetadataIdentity.TryGetValue(
                CanonicalMetadataIdentity(symbol), out var matches)
            && matches.Length == 1)
        {
            sourceSymbol = matches[0];
            return true;
        }

        sourceSymbol = null!;
        return false;
    }

    private static bool HasImplicitInteropEntryPoint(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (name is not ("System.Runtime.InteropServices.DllImportAttribute"
                or "System.Runtime.InteropServices.LibraryImportAttribute"))
                continue;
            var entryPoint = attribute.NamedArguments
                .FirstOrDefault(pair => pair.Key == "EntryPoint");
            if (entryPoint.Key is null || entryPoint.Value.IsNull
                || string.IsNullOrEmpty(entryPoint.Value.Value as string))
                return true;
        }
        return false;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateSourceTypes(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamespaceSymbol childNamespace)
            {
                foreach (var type in EnumerateSourceTypes(childNamespace))
                    yield return type;
            }
            else if (member is INamedTypeSymbol type)
            {
                if (IsSourceOwned(type))
                    yield return type;
                foreach (var nested in EnumerateSourceTypes(type))
                    yield return nested;
            }
        }
    }

    internal static string CanonicalMetadataIdentity(ISymbol symbol)
    {
        symbol = AgentSemanticRenamePlan.Normalize(symbol);
        return symbol switch
        {
            INamespaceSymbol ns => $"namespace|{ns.ToDisplayString()}",
            INamedTypeSymbol type => $"type|{type.ContainingAssembly?.Identity.Name}|{MetadataTypeName(type)}",
            IMethodSymbol method => $"method|{CanonicalMetadataIdentity(method.ContainingType)}|{method.MetadataName}|{MethodSignature(method)}",
            IPropertySymbol property => $"property|{CanonicalMetadataIdentity(property.ContainingType)}|{property.MetadataName}|{ParameterSignature(property.Parameters)}|{TypeIdentity(property.Type)}",
            IFieldSymbol field => $"field|{CanonicalMetadataIdentity(field.ContainingType)}|{field.MetadataName}|{TypeIdentity(field.Type)}",
            IEventSymbol @event => $"event|{CanonicalMetadataIdentity(@event.ContainingType)}|{@event.MetadataName}|{TypeIdentity(@event.Type)}",
            IParameterSymbol parameter => $"parameter|{CanonicalMetadataIdentity(parameter.ContainingSymbol)}|{parameter.Ordinal}",
            ITypeParameterSymbol parameter => $"type-parameter|{CanonicalMetadataIdentity(parameter.ContainingSymbol)}|{parameter.Ordinal}",
            _ => $"source|{symbol.Kind}|{symbol.ContainingAssembly?.Identity.Name}|{symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}"
        };
    }

    private static string FamilyIdentity(ISymbol symbol)
    {
        symbol = AgentSemanticRenamePlan.Normalize(symbol);
        ISymbol? overridden = symbol switch
        {
            IMethodSymbol method => method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            IEventSymbol @event => @event.OverriddenEvent,
            _ => null
        };
        if (overridden is not null)
            return FamilyIdentity(overridden);

        if (symbol.ContainingType is { } type)
        {
            var interfaceIdentities = type.AllInterfaces
                .SelectMany(@interface => @interface.GetMembers())
                .Where(member => type.FindImplementationForInterfaceMember(member) is { } implementation
                    && SymbolEqualityComparer.Default.Equals(
                        AgentSemanticRenamePlan.Normalize(implementation), symbol))
                .Select(CanonicalMetadataIdentity)
                .OrderBy(identity => identity, StringComparer.Ordinal)
                .ToArray();
            if (interfaceIdentities.Length != 0)
                return interfaceIdentities[0];
        }

        return symbol is ILocalSymbol or IRangeVariableSymbol
            ? StableSymbolKey(symbol, default)
            : CanonicalMetadataIdentity(symbol);
    }

    private static string MetadataTypeName(INamedTypeSymbol type)
    {
        var names = new Stack<string>();
        for (var current = type.OriginalDefinition; current is not null; current = current.ContainingType)
            names.Push(current.MetadataName);
        var typeName = string.Join("+", names);
        var ns = type.ContainingNamespace?.ToDisplayString();
        return string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
    }

    private static string MethodSignature(IMethodSymbol method) =>
        $"{method.Arity}|{ParameterSignature(method.Parameters)}|{TypeIdentity(method.ReturnType)}";

    private static string ParameterSignature(ImmutableArray<IParameterSymbol> parameters) =>
        string.Join(",", parameters.Select(parameter =>
            $"{parameter.RefKind}:{TypeIdentity(parameter.Type)}"));

    private static string TypeIdentity(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string StableSymbolKey(ISymbol symbol, CancellationToken cancellationToken)
    {
        // Workspaces' SymbolKey is internal in the supported Roslyn package.  This
        // source symbol key carries semantic identity plus every declaration's
        // content fingerprint and syntax coordinate; unlike a simple spelling it
        // distinguishes overloads, shadows, partial declarations, and locals.
        var display = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var declarations = symbol.DeclaringSyntaxReferences
            .Select(reference =>
            {
                var treeText = reference.SyntaxTree.GetText(cancellationToken).ToString();
                var fingerprint = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(treeText)));
                var syntax = reference.GetSyntax(cancellationToken);
                return $"{fingerprint}:{syntax.RawKind}:{syntax.Span.Start}:{syntax.Span.Length}";
            })
            .OrderBy(value => value, StringComparer.Ordinal);
        return $"{symbol.Kind}|{display}|{string.Join(";", declarations)}";
    }

    private static string GenerateName(string uuid, int seed, string symbolKey, int attempt)
    {
        var material = Encoding.UTF8.GetBytes(
            $"{HashDomain}\0{uuid}\0{seed}\0{symbolKey}\0{attempt}");
        var hash = SHA256.HashData(material);
        var builder = new StringBuilder("_");
        for (var index = 0; index < 12; index++)
            builder.Append(Alphabet[hash[index] % Alphabet.Length]);
        return builder.ToString();
    }

    private sealed record AllocationGroup(ISymbol[] Symbols, string[] Keys)
    {
        public string CanonicalKey { get; } = string.Join("\n", Keys);
    }

    private sealed class SymbolUnion
    {
        private readonly Dictionary<ISymbol, ISymbol> _parents;

        public SymbolUnion(IEnumerable<ISymbol> symbols)
        {
            _parents = new Dictionary<ISymbol, ISymbol>(SymbolEqualityComparer.Default);
            foreach (var symbol in symbols)
                _parents[symbol] = symbol;
        }

        public ISymbol Find(ISymbol symbol)
        {
            var parent = _parents[symbol];
            if (!SymbolEqualityComparer.Default.Equals(parent, symbol))
                _parents[symbol] = Find(parent);
            return _parents[symbol];
        }

        public void Union(ISymbol left, ISymbol right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (!SymbolEqualityComparer.Default.Equals(leftRoot, rightRoot))
                _parents[rightRoot] = leftRoot;
        }
    }
}

internal sealed class SemanticRenameRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _model;
    private readonly AgentSemanticRenamePlan _plan;
    private readonly CancellationToken _cancellationToken;

    public SemanticRenameRewriter(
        SemanticModel model,
        AgentSemanticRenamePlan plan,
        CancellationToken cancellationToken)
    {
        _model = model;
        _plan = plan;
        _cancellationToken = cancellationToken;
    }

    public override SyntaxNode? VisitAttribute(AttributeSyntax node)
    {
        var generatedName = GetRenamedJsonTypeInfoPropertyName(node);
        var rewritten = (AttributeSyntax?)base.VisitAttribute(node);
        if (rewritten?.ArgumentList is null || generatedName is null)
            return rewritten;

        var nameExpression = SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal(generatedName));
        var arguments = rewritten.ArgumentList.Arguments;
        var existing = arguments.FirstOrDefault(argument =>
            argument.NameEquals?.Name.Identifier.ValueText == "TypeInfoPropertyName");
        arguments = existing is not null
            ? arguments.Replace(existing, existing.WithExpression(nameExpression))
            : arguments.Add(SyntaxFactory.AttributeArgument(
                SyntaxFactory.NameEquals("TypeInfoPropertyName"), null, nameExpression));
        return rewritten.WithArgumentList(rewritten.ArgumentList.WithArguments(arguments));
    }

    private string? GetRenamedJsonTypeInfoPropertyName(AttributeSyntax node)
    {
        if (_model.GetSymbolInfo(node, _cancellationToken).Symbol is not IMethodSymbol ctor
            || ctor.ContainingType.ToDisplayString() !=
                "System.Text.Json.Serialization.JsonSerializableAttribute")
            return null;

        var typeOf = node.ArgumentList?.Arguments
            .Select(argument => argument.Expression)
            .OfType<TypeOfExpressionSyntax>()
            .FirstOrDefault();
        var contextDeclaration = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (typeOf is null || contextDeclaration is null)
            return null;

        var serializedType = _model.GetTypeInfo(typeOf.Type, _cancellationToken).Type;
        var contextType = _model.GetDeclaredSymbol(contextDeclaration, _cancellationToken);
        if (serializedType is null || contextType is null)
            return null;

        return FindRenamedJsonTypeInfoProperty(contextType, serializedType);
    }

    private string? FindRenamedJsonTypeInfoProperty(
        INamedTypeSymbol contextType, ITypeSymbol serializedType)
    {
        var contextIdentity = AgentSemanticRenamePlanner.CanonicalMetadataIdentity(contextType);
        var generatedProperties = contextType.GetMembers().OfType<IPropertySymbol>()
            .Concat(_model.SyntaxTree.GetRoot(_cancellationToken).DescendantNodes()
                .OfType<SimpleNameSyntax>()
                .Select(name => _model.GetSymbolInfo(name, _cancellationToken).Symbol)
                .OfType<IPropertySymbol>());

        foreach (var property in generatedProperties)
        {
            if (AgentSemanticRenamePlanner.CanonicalMetadataIdentity(property.ContainingType) != contextIdentity)
                continue;
            if (!IsJsonTypeInfoFor(property, serializedType) && property.Name != serializedType.Name)
                continue;
            if (_plan.TryGetName(property, out var mappedName))
                return mappedName;
        }
        return null;
    }

    private static bool IsJsonTypeInfoFor(IPropertySymbol property, ITypeSymbol serializedType) =>
        property.Type is INamedTypeSymbol
        {
            Name: "JsonTypeInfo",
            TypeArguments.Length: 1,
            ContainingNamespace: { } metadataNamespace
        } propertyType
        && metadataNamespace.ToDisplayString() == "System.Text.Json.Serialization.Metadata"
        && SymbolEqualityComparer.Default.Equals(propertyType.TypeArguments[0], serializedType);

    public override SyntaxToken VisitToken(SyntaxToken token)
    {
        if (!token.IsKind(SyntaxKind.IdentifierToken) || token.Parent is null)
            return base.VisitToken(token);
        if (token.Parent is IdentifierNameSyntax identifier && identifier.IsVar)
            return base.VisitToken(token);

        var symbol = ResolveSymbol(token);
        if (_plan.TryGetName(symbol, out var name))
            return SyntaxFactory.Identifier(token.LeadingTrivia, name, token.TrailingTrivia);
        return base.VisitToken(token);
    }

    private ISymbol? ResolveSymbol(SyntaxToken token)
    {
        var parent = token.Parent!;
        if (TryResolveDeclaredSymbol(parent, token, out var declared))
            return declared;

        if (parent is NameColonSyntax nameColon && nameColon.Name.Identifier == token
            && nameColon.Parent is ArgumentSyntax argument
            && _model.GetOperation(argument, _cancellationToken) is IArgumentOperation operation)
            return operation.Parameter;

        if (parent is IdentifierNameSyntax or GenericNameSyntax)
        {
            var info = _model.GetSymbolInfo(parent, _cancellationToken);
            if (info.Symbol is not null)
                return info.Symbol;
            if (info.CandidateSymbols.Length == 1)
                return info.CandidateSymbols[0];
            if (info.CandidateSymbols.Length > 1)
            {
                var mapped = info.CandidateSymbols
                    .Select(candidate => (Symbol: candidate,
                        HasName: _plan.TryGetName(candidate, out var candidateName),
                        Name: candidateName))
                    .ToArray();
                if (mapped.All(candidate => candidate.HasName)
                    && mapped.Select(candidate => candidate.Name)
                        .Distinct(StringComparer.Ordinal).Count() == 1)
                    return mapped[0].Symbol;
            }
        }
        return null;
    }

    private bool TryResolveDeclaredSymbol(SyntaxNode parent, SyntaxToken token, out ISymbol? symbol)
    {
        if (parent is ConstructorDeclarationSyntax ctor && ctor.Identifier == token)
        {
            symbol = _model.GetDeclaredSymbol(ctor, _cancellationToken)?.ContainingType;
            return symbol is not null;
        }
        if (parent is DestructorDeclarationSyntax dtor && dtor.Identifier == token)
        {
            symbol = _model.GetDeclaredSymbol(dtor, _cancellationToken)?.ContainingType;
            return symbol is not null;
        }
        if (GetDeclarationIdentifier(parent) == token)
        {
            symbol = _model.GetDeclaredSymbol(parent, _cancellationToken);
            return symbol is not null;
        }
        symbol = null;
        return false;
    }

    private static SyntaxToken GetDeclarationIdentifier(SyntaxNode node) => node switch
    {
        BaseTypeDeclarationSyntax d => d.Identifier,
        DelegateDeclarationSyntax d => d.Identifier,
        MethodDeclarationSyntax d => d.Identifier,
        LocalFunctionStatementSyntax d => d.Identifier,
        PropertyDeclarationSyntax d => d.Identifier,
        EventDeclarationSyntax d => d.Identifier,
        EnumMemberDeclarationSyntax d => d.Identifier,
        VariableDeclaratorSyntax d => d.Identifier,
        ParameterSyntax d => d.Identifier,
        TypeParameterSyntax d => d.Identifier,
        SingleVariableDesignationSyntax d => d.Identifier,
        CatchDeclarationSyntax d => d.Identifier,
        ForEachStatementSyntax d => d.Identifier,
        FromClauseSyntax d => d.Identifier,
        LetClauseSyntax d => d.Identifier,
        JoinClauseSyntax d => d.Identifier,
        JoinIntoClauseSyntax d => d.Identifier,
        QueryContinuationSyntax d => d.Identifier,
        _ => default,
    };
}
