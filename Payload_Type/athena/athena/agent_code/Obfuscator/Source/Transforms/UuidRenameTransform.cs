using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Obfuscator.Config;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Obfuscator.Source.Transforms;

/// <summary>
/// Renames contract interfaces, types, members, parameters, and namespaces
/// using a UUID-derived deterministic mapping.
/// </summary>
public sealed class UuidRenameTransform : CSharpSyntaxRewriter
{
    private static readonly Lazy<MetadataReference[]> PlatformReferences = new(
        () => ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray());

    private readonly UuidRenameMap _map;
    private HashSet<string> _contractTypedVars = new();
    private IReadOnlyDictionary<string, (IParameterSymbol Symbol, string Renamed)>?
        _constructorParameterRenames;
    private SemanticModel? _semanticModel;

    public UuidRenameTransform(UuidRenameMap map)
    {
        _map = map;
    }

    private static SyntaxToken RenameIdentifier(
        SyntaxToken original, string renamed) =>
        Identifier(
            original.LeadingTrivia,
            SyntaxKind.IdentifierToken,
            renamed,
            renamed,
            original.TrailingTrivia);

    public SyntaxTree Rewrite(SyntaxTree tree)
    {
        var compilation = CSharpCompilation.Create(
            "SourceRewrite",
            [tree],
            PlatformReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary));
        return Rewrite(tree, compilation.GetSemanticModel(
            tree, ignoreAccessibility: true));
    }

    public SyntaxTree Rewrite(SyntaxTree tree, SemanticModel semanticModel)
    {
        if (semanticModel.SyntaxTree != tree)
            throw new ArgumentException(
                "Semantic model must belong to the rewritten tree.",
                nameof(semanticModel));
        _semanticModel = semanticModel;
        try
        {
            return RewriteCore(tree);
        }
        finally
        {
            _semanticModel = null;
        }
    }

    private SyntaxTree RewriteCore(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        _contractTypedVars = CollectContractTypedNames(root);
        var rewritten = Visit(root);
        return tree.WithRootAndOptions(rewritten!, tree.Options);
    }

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var visited = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;
        if (!_map.HasContractDeclarationProvenance || _semanticModel is null)
            return visited;

        var members = new List<MemberDeclarationSyntax>();
        for (var index = 0; index < node.Members.Count; index++)
        {
            var originalMember = node.Members[index];
            var visitedMember = visited.Members[index];
            if (originalMember is NamespaceDeclarationSyntax originalNamespace
                && visitedMember is NamespaceDeclarationSyntax visitedNamespace)
            {
                var (preserved, relocated) = RelocateNestedContractNamespaces(
                    originalNamespace, visitedNamespace);
                members.Add(preserved);
                members.AddRange(relocated);
            }
            else
            {
                members.Add(visitedMember);
            }
        }

        return visited.WithMembers(List(members));
    }

    private (NamespaceDeclarationSyntax Preserved,
        IReadOnlyList<NamespaceDeclarationSyntax> Relocated)
        RelocateNestedContractNamespaces(
            NamespaceDeclarationSyntax original,
            NamespaceDeclarationSyntax visited)
    {
        var preservedMembers = new List<MemberDeclarationSyntax>();
        var relocated = new List<NamespaceDeclarationSyntax>();
        for (var index = 0; index < original.Members.Count; index++)
        {
            var originalMember = original.Members[index];
            var visitedMember = visited.Members[index];
            if (originalMember is not NamespaceDeclarationSyntax originalNested
                || visitedMember is not NamespaceDeclarationSyntax visitedNested)
            {
                preservedMembers.Add(visitedMember);
                continue;
            }

            var (preservedNested, nestedRelocated) =
                RelocateNestedContractNamespaces(originalNested, visitedNested);
            var namespaceIdentity = _semanticModel?.GetDeclaredSymbol(originalNested)
                ?.ToDisplayString();
            var destination = namespaceIdentity is null
                ? null
                : CorrectedNamespaceText(namespaceIdentity);
            if (namespaceIdentity is not null
                && destination != namespaceIdentity
                && IsContractNamespaceDeclaration(originalNested))
            {
                var relocatedName = ParseName(destination!)
                    .WithLeadingTrivia(originalNested.Name.GetLeadingTrivia())
                    .WithTrailingTrivia(originalNested.Name.GetTrailingTrivia());
                relocated.Add(preservedNested.WithName(relocatedName));
            }
            else
            {
                preservedMembers.Add(preservedNested);
            }
            relocated.AddRange(nestedRelocated);
        }

        return (visited.WithMembers(List(preservedMembers)), relocated);
    }

    /// <summary>
    /// Pre-scan the tree to find variable, field, property, and parameter
    /// names whose declared type is in the rename map.
    /// </summary>
    private HashSet<string> CollectContractTypedNames(SyntaxNode root)
    {
        var names = new HashSet<string>();
        var mappings = _map.GetAllMappings();

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case FieldDeclarationSyntax field:
                    if (IsContractType(field.Declaration.Type, mappings))
                        foreach (var v in field.Declaration.Variables)
                            names.Add(v.Identifier.Text);
                    break;
                case PropertyDeclarationSyntax prop:
                    if (IsContractType(prop.Type, mappings))
                        names.Add(prop.Identifier.Text);
                    break;
                case ParameterSyntax param when param.Type is not null:
                    if (IsContractType(param.Type, mappings))
                        names.Add(param.Identifier.Text);
                    break;
                case LocalDeclarationStatementSyntax local:
                    if (IsContractType(local.Declaration.Type, mappings))
                        foreach (var v in local.Declaration.Variables)
                            names.Add(v.Identifier.Text);
                    break;
                case DeclarationExpressionSyntax declExpr:
                    if (IsContractType(declExpr.Type, mappings)
                        && declExpr.Designation
                            is SingleVariableDesignationSyntax svd)
                        names.Add(svd.Identifier.Text);
                    break;
                case InvocationExpressionSyntax invocation:
                    CollectOutVarsFromGenericCall(
                        invocation, mappings, names);
                    break;
            }
        }

        return names;
    }

    private static bool IsContractType(
        TypeSyntax type, Dictionary<string, string> mappings)
    {
        var typeName = type switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            QualifiedNameSyntax q => q.ToString(),
            _ => null
        };
        return typeName is not null && mappings.ContainsKey(typeName);
    }

    /// <summary>
    /// When a generic method like TryGetModule&lt;IFileModule&gt;(...)
    /// has out var parameters, the declared type is "var" which
    /// doesn't match any contract type. This method inspects
    /// generic type arguments to infer that the out var is
    /// contract-typed.
    /// </summary>
    private static void CollectOutVarsFromGenericCall(
        InvocationExpressionSyntax invocation,
        Dictionary<string, string> mappings,
        HashSet<string> names)
    {
        GenericNameSyntax? genericName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma
                when ma.Name is GenericNameSyntax g => g,
            GenericNameSyntax g => g,
            _ => null
        };

        if (genericName is null)
            return;

        var hasContractTypeArg = genericName.TypeArgumentList
            .Arguments
            .Any(arg => IsContractType(arg, mappings));

        if (!hasContractTypeArg)
            return;

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword)
                && arg.Expression
                    is DeclarationExpressionSyntax declExpr
                && declExpr.Designation
                    is SingleVariableDesignationSyntax svd)
            {
                names.Add(svd.Identifier.Text);
            }
        }
    }

    public override SyntaxNode? VisitNamespaceDeclaration(
        NamespaceDeclarationSyntax node)
    {
        var visited = (NamespaceDeclarationSyntax)base.VisitNamespaceDeclaration(node)!;
        var nameText = node.Name.ToString();
        if (_map.HasContractDeclarationProvenance
            && !IsContractNamespaceDeclaration(node))
            return visited.WithName(node.Name);
        if (TryGetRenamed(nameText, out var renamed))
            return visited.WithName(ParseName(renamed));
        // Overwrite the base-visited name (which may have renamed sub-segments
        // that collide with contract type names) with a corrected name built
        // from the original to rename only the known namespace prefix.
        var corrected = CorrectedNamespaceName(node.Name);
        return ReferenceEquals(corrected, node.Name)
            ? visited
            : visited.WithName(corrected);
    }

    public override SyntaxNode? VisitFileScopedNamespaceDeclaration(
        FileScopedNamespaceDeclarationSyntax node)
    {
        var visited = (FileScopedNamespaceDeclarationSyntax)
            base.VisitFileScopedNamespaceDeclaration(node)!;
        var nameText = node.Name.ToString();
        if (_map.HasContractDeclarationProvenance
            && !IsContractNamespaceDeclaration(node))
            return visited.WithName(node.Name);
        if (TryGetRenamed(nameText, out var renamed))
            return visited.WithName(ParseName(renamed));
        var corrected = CorrectedNamespaceName(node.Name);
        return ReferenceEquals(corrected, node.Name)
            ? visited
            : visited.WithName(corrected);
    }

    public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
    {
        var visited = (UsingDirectiveSyntax)base.VisitUsingDirective(node)!;
        var nameText = node.NamespaceOrType.ToString();
        if (_map.HasContractDeclarationProvenance
            && _semanticModel?.GetSymbolInfo(node.NamespaceOrType).Symbol
                is INamespaceSymbol namespaceSymbol
            && !NamespaceContainsCanonicalType(namespaceSymbol))
            return visited.WithNamespaceOrType(node.NamespaceOrType);
        if (TryGetRenamed(nameText, out var renamed))
            return visited.WithName(ParseName(renamed));
        if (node.NamespaceOrType is not NameSyntax usingName)
            return visited;
        var corrected = CorrectedNamespaceName(usingName);
        return ReferenceEquals(corrected, usingName)
            ? visited
            : visited.WithName(corrected);
    }

    /// <summary>
    /// Builds a namespace name that renames only the longest recognized
    /// prefix from the map, leaving any trailing sub-segments untouched.
    /// This prevents sub-namespace segments that share a name with a
    /// contract type from being renamed via <see cref="VisitIdentifierName"/>,
    /// which would cause a namespace/type collision (CS0118).
    /// </summary>
    private NameSyntax CorrectedNamespaceName(NameSyntax originalName)
    {
        if (originalName is not QualifiedNameSyntax qn)
        {
            var text = originalName.ToString();
            if (TryGetRenamed(text, out var r))
                return (NameSyntax)IdentifierName(r)
                    .WithLeadingTrivia(originalName.GetLeadingTrivia())
                    .WithTrailingTrivia(originalName.GetTrailingTrivia());
            return originalName;
        }

        var fullText = qn.ToString();
        if (TryGetRenamed(fullText, out var renamed))
            return (NameSyntax)IdentifierName(renamed)
                .WithLeadingTrivia(qn.GetLeadingTrivia())
                .WithTrailingTrivia(qn.GetTrailingTrivia());

        var leftText = qn.Left.ToString();
        if (TryGetRenamed(leftText, out var leftRenamed))
        {
            var newLeft = (NameSyntax)IdentifierName(leftRenamed)
                .WithLeadingTrivia(qn.Left.GetLeadingTrivia())
                .WithTrailingTrivia(qn.Left.GetTrailingTrivia());
            return qn.WithLeft(newLeft);
        }

        var correctedLeft = CorrectedNamespaceName(qn.Left);
        if (!ReferenceEquals(correctedLeft, qn.Left))
            return qn.WithLeft(correctedLeft);

        return originalName;
    }

    public override SyntaxNode? VisitInterfaceDeclaration(
        InterfaceDeclarationSyntax node)
    {
        var visited = (InterfaceDeclarationSyntax)base.VisitInterfaceDeclaration(node)!;
        if (IsSemanticContractDeclaration(node)
            && TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
        return visited;
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var previousRenames = _constructorParameterRenames;
        _constructorParameterRenames = GetConstructorParameterRenames(
            node.ParameterList is { } parameterList
                ? parameterList.Parameters
                : Enumerable.Empty<ParameterSyntax>());

        ClassDeclarationSyntax visited;
        try
        {
            visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        }
        finally
        {
            _constructorParameterRenames = previousRenames;
        }

        visited = visited.WithMembers(SplitMultiVariableContractFields(
            node.Members, visited.Members));
        if (IsSemanticContractDeclaration(node)
            && TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
        return visited;
    }

    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        var visited = (RecordDeclarationSyntax)base.VisitRecordDeclaration(node)!;
        visited = visited.WithMembers(SplitMultiVariableContractFields(
            node.Members, visited.Members));
        if (IsSemanticContractDeclaration(node)
            && TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
        return visited;
    }

    public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        var visited = (EnumDeclarationSyntax)base.VisitEnumDeclaration(node)!;
        if (IsSemanticContractDeclaration(node)
            && TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
        return visited;
    }

    public override SyntaxNode? VisitEnumMemberDeclaration(
        EnumMemberDeclarationSyntax node)
    {
        var visited = (EnumMemberDeclarationSyntax)
            base.VisitEnumMemberDeclaration(node)!;
        if (_semanticModel?.GetDeclaredSymbol(node) is not IFieldSymbol field
            || !IsSemanticContractType(field.ContainingType)
            || !TryGetRenamed(field.Name, out var renamed))
            return visited;

        return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
    }

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var previousRenames = _constructorParameterRenames;
        _constructorParameterRenames = GetConstructorParameterRenames(
            node.ParameterList is { } parameterList
                ? parameterList.Parameters
                : Enumerable.Empty<ParameterSyntax>());

        StructDeclarationSyntax visited;
        try
        {
            visited = (StructDeclarationSyntax)base.VisitStructDeclaration(node)!;
        }
        finally
        {
            _constructorParameterRenames = previousRenames;
        }

        visited = visited.WithMembers(SplitMultiVariableContractFields(
            node.Members, visited.Members));
        if (IsSemanticContractDeclaration(node)
            && TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
        return visited;
    }

    public override SyntaxNode? VisitDelegateDeclaration(
        DelegateDeclarationSyntax node)
    {
        var visited = (DelegateDeclarationSyntax)base.VisitDelegateDeclaration(node)!;
        if (_semanticModel?.GetDeclaredSymbol(node) is INamedTypeSymbol symbol
            && IsCanonicalDeclaration(symbol, node)
            && TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
        return visited;
    }

    private IReadOnlyDictionary<string, (IParameterSymbol Symbol, string Renamed)>
        GetConstructorParameterRenames(IEnumerable<ParameterSyntax> parameters)
    {
        return parameters
            .Select(parameter => (
                Parameter: parameter,
                Symbol: _semanticModel?.GetDeclaredSymbol(parameter) as IParameterSymbol))
            .Where(item => item.Symbol is not null
                && ShouldRenameParameter(item.Parameter)
                && TryGetRenamed(item.Parameter.Identifier.Text, out _))
            .ToDictionary(
                item => item.Parameter.Identifier.Text,
                item => (item.Symbol!, _map.GetRenamed(item.Parameter.Identifier.Text)));
    }

    public override SyntaxNode? VisitConstructorDeclaration(
        ConstructorDeclarationSyntax node)
    {
        var previousRenames = _constructorParameterRenames;
        _constructorParameterRenames = GetConstructorParameterRenames(
            node.ParameterList.Parameters);

        ConstructorDeclarationSyntax visited;
        try
        {
            visited = (ConstructorDeclarationSyntax)
                base.VisitConstructorDeclaration(node)!;
        }
        finally
        {
            _constructorParameterRenames = previousRenames;
        }

        if (IsSemanticContractType(
                _semanticModel?.GetDeclaredSymbol(node)?.ContainingType)
            && TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
        return visited;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        if (!IsContractMethodSymbol(_semanticModel?.GetDeclaredSymbol(node))
            || !TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited;
        return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
    }

    private bool IsContractMethodSymbol(IMethodSymbol? methodSymbol)
    {
        if (methodSymbol is null)
            return false;

        if (IsSemanticContractType(methodSymbol.ContainingType))
            return true;

        if (methodSymbol.ExplicitInterfaceImplementations.Any(implementation =>
                IsSemanticContractType(implementation.ContainingType)))
            return true;

        for (var overridden = methodSymbol.OverriddenMethod;
             overridden is not null;
             overridden = overridden.OverriddenMethod)
        {
            if (IsContractMethodSymbol(overridden))
                return true;
        }

        var containingType = methodSymbol.ContainingType;
        foreach (var contractInterface in containingType.AllInterfaces
                     .Where(IsSemanticContractType))
        {
            foreach (var contractMethod in contractInterface.GetMembers(methodSymbol.Name)
                         .OfType<IMethodSymbol>())
            {
                var implementation = containingType
                    .FindImplementationForInterfaceMember(contractMethod);
                if (SymbolEqualityComparer.Default.Equals(
                        implementation?.OriginalDefinition,
                        methodSymbol.OriginalDefinition))
                    return true;
            }
        }

        return false;
    }

    public override SyntaxNode? VisitPropertyDeclaration(
        PropertyDeclarationSyntax node)
    {
        var visited = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
        if (!IsContractPropertySymbol(_semanticModel?.GetDeclaredSymbol(node))
            || !TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited;
        if (IsInsideContractDto(node))
            visited = AddJsonPropertyName(visited, node.Identifier.Text);
        return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
    }

    private bool IsContractPropertySymbol(IPropertySymbol? propertySymbol)
    {
        if (propertySymbol is null)
            return false;

        if (IsSemanticContractType(propertySymbol.ContainingType))
            return true;

        if (propertySymbol.ExplicitInterfaceImplementations.Any(implementation =>
                IsSemanticContractType(implementation.ContainingType)))
            return true;

        for (var overridden = propertySymbol.OverriddenProperty;
             overridden is not null;
             overridden = overridden.OverriddenProperty)
        {
            if (IsContractPropertySymbol(overridden))
                return true;
        }

        var containingType = propertySymbol.ContainingType;
        foreach (var contractInterface in containingType.AllInterfaces
                     .Where(IsSemanticContractType))
        {
            foreach (var contractProperty in contractInterface.GetMembers(propertySymbol.Name)
                         .OfType<IPropertySymbol>())
            {
                var implementation = containingType
                    .FindImplementationForInterfaceMember(contractProperty);
                if (SymbolEqualityComparer.Default.Equals(
                        implementation?.OriginalDefinition,
                        propertySymbol.OriginalDefinition))
                    return true;
            }
        }

        return false;
    }

    private SyntaxList<MemberDeclarationSyntax> SplitMultiVariableContractFields(
        SyntaxList<MemberDeclarationSyntax> originalMembers,
        SyntaxList<MemberDeclarationSyntax> visitedMembers)
    {
        var expanded = new List<MemberDeclarationSyntax>(visitedMembers.Count);
        for (var memberIndex = 0; memberIndex < visitedMembers.Count;
             memberIndex++)
        {
            if (originalMembers[memberIndex] is not FieldDeclarationSyntax original
                || visitedMembers[memberIndex] is not FieldDeclarationSyntax visited
                || original.Declaration.Variables.Count < 2
                || !IsInsideContractDto(original)
                || !original.Declaration.Variables.Any(variable =>
                    TryGetRenamed(variable.Identifier.Text, out _)))
            {
                expanded.Add(visitedMembers[memberIndex]);
                continue;
            }

            for (var variableIndex = 0;
                 variableIndex < visited.Declaration.Variables.Count;
                 variableIndex++)
            {
                var originalVariable = original.Declaration.Variables[variableIndex];
                var variable = visited.Declaration.Variables[variableIndex];
                var field = visited.WithDeclaration(visited.Declaration.WithVariables(
                    SingletonSeparatedList(variable)));

                if (variableIndex < visited.Declaration.Variables.Count - 1)
                {
                    var separator = visited.Declaration.Variables
                        .GetSeparator(variableIndex);
                    field = field.WithSemicolonToken(Token(
                        separator.LeadingTrivia,
                        SyntaxKind.SemicolonToken,
                        separator.TrailingTrivia));
                }

                if (variableIndex > 0)
                    field = field.WithLeadingTrivia();

                if (TryGetRenamed(
                        originalVariable.Identifier.Text, out var renamed))
                {
                    variable = variable.WithIdentifier(RenameIdentifier(
                        originalVariable.Identifier, renamed));
                    field = field.WithDeclaration(field.Declaration.WithVariables(
                        SingletonSeparatedList(variable)));
                    field = AddJsonPropertyName(
                        field, originalVariable.Identifier.Text);
                }

                expanded.Add(field);
            }
        }

        return List(expanded);
    }

    public override SyntaxNode? VisitFieldDeclaration(
        FieldDeclarationSyntax node)
    {
        var visited = (FieldDeclarationSyntax)
            base.VisitFieldDeclaration(node)!;
        if (!IsInsideContractDto(node)
            || node.Declaration.Variables.Count != 1)
            return visited;

        var original = node.Declaration.Variables[0].Identifier.Text;
        if (!TryGetRenamed(original, out var renamed))
            return visited;

        var variable = visited.Declaration.Variables[0]
            .WithIdentifier(RenameIdentifier(
                node.Declaration.Variables[0].Identifier, renamed));
        visited = visited.WithDeclaration(
            visited.Declaration.WithVariables(
                SingletonSeparatedList(variable)));
        return AddJsonPropertyName(visited, original);
    }

    private bool IsInsideContractDto(SyntaxNode node)
    {
        var declaration = node.Ancestors()
            .OfType<BaseTypeDeclarationSyntax>()
            .FirstOrDefault();
        if (_semanticModel is not null && declaration is not null)
        {
            var symbol = _semanticModel.GetDeclaredSymbol(declaration);
            return symbol is not null
                && _map.IsContractType(symbol.Name)
                && IsSemanticContractType(symbol);
        }

        for (var current = node.Parent;
             current is not null; current = current.Parent)
        {
            if (current is ClassDeclarationSyntax cls)
                return _map.IsContractType(cls.Identifier.Text);
            if (current is RecordDeclarationSyntax rec)
                return _map.IsContractType(rec.Identifier.Text);
            if (current is StructDeclarationSyntax)
                return false;
        }
        return false;
    }

    private static T AddJsonPropertyName<T>(T node, string original)
        where T : MemberDeclarationSyntax
    {
        if (node.AttributeLists.SelectMany(x => x.Attributes)
            .Any(x => x.Name.ToString().EndsWith(
                "JsonPropertyName", StringComparison.Ordinal)))
            return node;

        var attribute = Attribute(ParseName(
            "global::System.Text.Json.Serialization.JsonPropertyName"))
            .WithArgumentList(AttributeArgumentList(
                SingletonSeparatedList(AttributeArgument(
                    LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        Literal(original))))));
        return (T)node.AddAttributeLists(
            AttributeList(SingletonSeparatedList(attribute)));
    }

    public override SyntaxNode? VisitEventDeclaration(EventDeclarationSyntax node)
    {
        var visited = (EventDeclarationSyntax)base.VisitEventDeclaration(node)!;
        var isExplicitContractEvent = node.ExplicitInterfaceSpecifier is not null
            && IsSemanticContractType(
                _semanticModel?.GetTypeInfo(
                    node.ExplicitInterfaceSpecifier.Name).Type);
        if ((!IsContractEventSymbol(_semanticModel?.GetDeclaredSymbol(node))
                && !isExplicitContractEvent)
            || !TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited;
        return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
    }

    public override SyntaxNode? VisitEventFieldDeclaration(
        EventFieldDeclarationSyntax node)
    {
        var visited = (EventFieldDeclarationSyntax)
            base.VisitEventFieldDeclaration(node)!;
        if (!IsInsideContractType(node))
            return visited;
        var decl = visited.Declaration;
        var vars = decl.Variables;
        var changed = false;
        for (var i = 0; i < vars.Count; i++)
        {
            var originalVariable = node.Declaration.Variables[i];
            if (IsContractEventSymbol(
                    _semanticModel?.GetDeclaredSymbol(originalVariable)
                        as IEventSymbol)
                && TryGetRenamed(vars[i].Identifier.Text, out var renamed))
            {
                vars = vars.Replace(
                    vars[i],
                    vars[i].WithIdentifier(RenameIdentifier(
                        originalVariable.Identifier, renamed)));
                changed = true;
            }
        }
        return changed
            ? visited.WithDeclaration(decl.WithVariables(vars))
            : visited;
    }

    private bool IsContractEventSymbol(IEventSymbol? eventSymbol)
    {
        if (eventSymbol is null
            || !_map.IsInterfaceMember(eventSymbol.Name))
            return false;

        if (IsSemanticContractType(eventSymbol.ContainingType))
            return true;

        if (eventSymbol.ExplicitInterfaceImplementations.Any(implementation =>
                IsSemanticContractType(implementation.ContainingType)))
            return true;

        for (var overridden = eventSymbol.OverriddenEvent;
             overridden is not null;
             overridden = overridden.OverriddenEvent)
        {
            if (IsContractEventSymbol(overridden))
                return true;
        }

        var containingType = eventSymbol.ContainingType;
        foreach (var contractInterface in containingType.AllInterfaces
                     .Where(IsSemanticContractType))
        {
            foreach (var contractEvent in contractInterface.GetMembers(eventSymbol.Name)
                         .OfType<IEventSymbol>())
            {
                var implementation = containingType
                    .FindImplementationForInterfaceMember(contractEvent);
                if (SymbolEqualityComparer.Default.Equals(
                        implementation?.OriginalDefinition,
                        eventSymbol.OriginalDefinition))
                    return true;
            }
        }

        return false;
    }

    public override SyntaxNode? VisitParameter(ParameterSyntax node)
    {
        var visited = (ParameterSyntax)base.VisitParameter(node)!;
        if (!ShouldRenameParameter(node))
            return visited;
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));
        return visited;
    }

    private bool ShouldRenameParameter(ParameterSyntax node)
    {
        if (_semanticModel?.GetDeclaredSymbol(node) is not IParameterSymbol parameter)
            return false;

        if (IsContractRecordPositionalSymbol(parameter))
            return true;

        if (parameter.ContainingSymbol is not IMethodSymbol method)
            return false;

        if (method.MethodKind == MethodKind.Constructor)
            return IsSemanticContractType(method.ContainingType);

        return IsContractMethodParameter(parameter, method);
    }

    private bool IsContractMethodParameter(
        IParameterSymbol parameter, IMethodSymbol method)
    {
        if (method.ContainingType.TypeKind == TypeKind.Interface
            && IsSemanticContractType(method.ContainingType))
            return parameter.Ordinal < method.Parameters.Length;

        if (method.ExplicitInterfaceImplementations.Any(contractMethod =>
                IsSemanticContractType(contractMethod.ContainingType)
                && parameter.Ordinal < contractMethod.Parameters.Length))
            return true;

        foreach (var contractInterface in method.ContainingType.AllInterfaces
                     .Where(IsSemanticContractType))
        {
            foreach (var contractMethod in contractInterface.GetMembers(method.Name)
                         .OfType<IMethodSymbol>())
            {
                var implementation = method.ContainingType
                    .FindImplementationForInterfaceMember(contractMethod);
                if (parameter.Ordinal < contractMethod.Parameters.Length
                    && SymbolEqualityComparer.Default.Equals(
                        implementation?.OriginalDefinition,
                        method.OriginalDefinition))
                    return true;
            }
        }

        return false;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var referencedSymbol = _semanticModel?.GetSymbolInfo(node).Symbol;
        if (_constructorParameterRenames is not null
            && _constructorParameterRenames.TryGetValue(
                node.Identifier.Text, out var parameterRename)
            && referencedSymbol is IParameterSymbol constructorParameterReference
            && SymbolEqualityComparer.Default.Equals(
                constructorParameterReference, parameterRename.Symbol)
            && IsStandaloneValueReference(node))
        {
            return node
                .WithIdentifier(Identifier(parameterRename.Renamed))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        if (TryRewriteCanonicalTypeReference(
                node, (IdentifierNameSyntax)base.VisitIdentifierName(node)!,
                out var rewrittenType))
            return rewrittenType;

        if (!TryGetRenamed(node.Identifier.Text, out var renamed))
            return base.VisitIdentifierName(node);

        if (referencedSymbol is IMethodSymbol referencedMethod
            && _map.IsInterfaceMember(referencedMethod.Name))
        {
            if (!IsContractMethodSymbol(referencedMethod))
                return base.VisitIdentifierName(node);
            return node
                .WithIdentifier(Identifier(renamed))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        if (referencedSymbol is IPropertySymbol referencedProperty
            && _map.IsInterfaceMember(referencedProperty.Name))
        {
            if (!IsContractPropertySymbol(referencedProperty))
                return base.VisitIdentifierName(node);
            return node
                .WithIdentifier(Identifier(renamed))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        if (referencedSymbol is IEventSymbol referencedEvent)
        {
            if (!IsContractEventSymbol(referencedEvent))
                return base.VisitIdentifierName(node);
            return node
                .WithIdentifier(Identifier(renamed))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        if (referencedSymbol is IParameterSymbol referencedParameter
            && referencedParameter.ContainingSymbol is IMethodSymbol containingMethod
            && IsContractMethodParameter(referencedParameter, containingMethod))
        {
            return node
                .WithIdentifier(Identifier(renamed))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        // Rename only references bound to the mapped contract declaration.
        // A private or nested type with the same simple name is a different
        // symbol and must remain untouched.
        if (IsContractFieldReference(node)
            || IsContractRecordPositionalSymbol(referencedSymbol)
            || IsSemanticContractType(GetReferencedType(node))
            || IsJsonContextContractProperty(node))
        {
            return node
                .WithIdentifier(Identifier(renamed))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        // A successfully bound non-contract symbol is authoritative. Do not
        // let the broad syntax fallback rename locals, parameters, local
        // functions, range variables, or unrelated members that merely share
        // a contract member name. Keep fallback behavior only for unresolved
        // or error-bound source.
        if (referencedSymbol is not null
            && referencedSymbol is not ITypeSymbol { TypeKind: TypeKind.Error }
            && referencedSymbol.ContainingType?.TypeKind != TypeKind.Error)
            return base.VisitIdentifierName(node);

        // Member/param names are only renamed when accessed on a
        // contract-typed expression (e.g. this.Name, module.Execute).
        if (node.Parent is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name == node
            && IsMemberAccessOnContractType(memberAccess))
        {
            return node
                .WithIdentifier(Identifier(renamed))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        // Standalone interface member references (implicit this.Name)
        // are renamed when inside a contract class and not as
        // the right side of a member access (including ?. access).
        if (_map.IsInterfaceMember(node.Identifier.Text)
            && node.Parent is not MemberAccessExpressionSyntax
            && node.Parent is not MemberBindingExpressionSyntax
            && IsInsideContractType(node)
            && !IsInsideContractDto(node))
        {
            return node
                .WithIdentifier(Identifier(renamed))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        return base.VisitIdentifierName(node);
    }

    public override SyntaxNode? VisitNameColon(NameColonSyntax node)
    {
        var symbol = _semanticModel?.GetSymbolInfo(node.Name).Symbol;
        if (IsContractRecordPositionalSymbol(symbol)
            && TryGetRenamed(node.Name.Identifier.ValueText, out var renamed))
        {
            return node.WithName(node.Name.WithIdentifier(Identifier(
                node.Name.Identifier.LeadingTrivia,
                SyntaxKind.IdentifierToken,
                renamed,
                renamed,
                node.Name.Identifier.TrailingTrivia)));
        }
        return base.VisitNameColon(node);
    }

    private bool IsContractRecordPositionalSymbol(ISymbol? symbol)
    {
        if (symbol is not IParameterSymbol and not IPropertySymbol
            || !IsSemanticContractType(symbol.ContainingType))
            return false;

        return symbol.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is ParameterSyntax parameter
            && parameter.Parent?.Parent is RecordDeclarationSyntax);
    }

    private static bool IsStandaloneValueReference(IdentifierNameSyntax node)
    {
        return node.Parent switch
        {
            MemberAccessExpressionSyntax access when access.Name == node => false,
            MemberBindingExpressionSyntax => false,
            QualifiedNameSyntax => false,
            AliasQualifiedNameSyntax => false,
            NameColonSyntax => false,
            NameEqualsSyntax nameEquals when nameEquals.Name == node => false,
            _ => true,
        };
    }

    private bool IsMemberAccessOnContractType(
        MemberAccessExpressionSyntax memberAccess)
    {
        var expr = memberAccess.Expression;
        if (_semanticModel is not null)
        {
            var type = _semanticModel.GetTypeInfo(expr).Type;
            if (type is not null && type.TypeKind != TypeKind.Error)
                return IsContractType(type);
        }

        return expr switch
        {
            ThisExpressionSyntax => IsInsideContractType(memberAccess),
            IdentifierNameSyntax id =>
                _map.GetAllMappings().ContainsKey(id.Identifier.Text)
                || _contractTypedVars.Contains(id.Identifier.Text),
            MemberAccessExpressionSyntax inner =>
                _map.GetAllMappings()
                    .ContainsKey(inner.Name.Identifier.Text)
                || _contractTypedVars
                    .Contains(inner.Name.Identifier.Text),
            _ => false
        };
    }

    private bool IsContractType(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol parameter)
            return parameter.ConstraintTypes.Any(IsContractType);

        for (var current = type; current is not null; current = current.BaseType)
            if (IsSemanticContractType(current))
                return true;

        return type.AllInterfaces.Any(IsSemanticContractType);
    }

    public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    {
        var visited = (GenericNameSyntax)base.VisitGenericName(node)!;
        if (TryRewriteCanonicalTypeReference(node, visited, out var rewrittenType))
            return rewrittenType;

        if (!TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited;

        var referencedSymbol = _semanticModel?.GetSymbolInfo(node).Symbol;
        if (referencedSymbol is IMethodSymbol referencedMethod
            && _map.IsInterfaceMember(referencedMethod.Name))
            return IsContractMethodSymbol(referencedMethod)
                ? visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed))
                : visited;

        if (IsSemanticContractType(GetReferencedType(node)))
            return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));

        if (node.Parent is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name == node
            && IsMemberAccessOnContractType(memberAccess))
            return visited.WithIdentifier(RenameIdentifier(node.Identifier, renamed));

        return visited;
    }

    private bool TryRewriteCanonicalTypeReference(
        SimpleNameSyntax original,
        SimpleNameSyntax visited,
        out SyntaxNode rewritten)
    {
        rewritten = visited;
        if (_semanticModel?.GetAliasInfo(original) is not null
            || original.Parent is QualifiedNameSyntax
            or AliasQualifiedNameSyntax
            || original.Parent is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name == original)
            return false;

        if (GetReferencedType(original) is not INamedTypeSymbol namedType
            || namedType.TypeKind == TypeKind.Error)
            return false;

        namedType = namedType.OriginalDefinition;
        string typeIdentifier;
        string? declarationNamespace = null;
        if (_map.HasContractDeclarationProvenance)
        {
            if (!TryGetDeclarationDestinationNamespace(
                    namedType, out declarationNamespace))
                return false;

            typeIdentifier = IsSemanticContractType(namedType)
                && TryGetRenamed(namedType.Name, out var mappedType)
                    ? mappedType
                    : namedType.Name;
        }
        else
        {
            if (!IsSemanticContractType(namedType)
                || !TryGetRenamed(namedType.Name, out var mappedType))
                return false;
            typeIdentifier = mappedType;
        }

        var renamedSimple = visited.WithIdentifier(Identifier(
            visited.Identifier.LeadingTrivia,
            SyntaxKind.IdentifierToken,
            typeIdentifier,
            typeIdentifier,
            visited.Identifier.TrailingTrivia));
        if (!_map.HasContractDeclarationProvenance
            || declarationNamespace == GetReferenceDestinationNamespace(original))
        {
            rewritten = renamedSimple;
            return true;
        }

        var leadingTrivia = renamedSimple.GetLeadingTrivia();
        var trailingTrivia = renamedSimple.GetTrailingTrivia();
        var right = renamedSimple.WithoutLeadingTrivia().WithoutTrailingTrivia();
        rewritten = QualifiedName(
                ParseName("global::" + declarationNamespace), right)
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(trailingTrivia);
        return true;
    }

    private bool TryGetDeclarationDestinationNamespace(
        INamedTypeSymbol type, out string destinationNamespace)
    {
        destinationNamespace = "";
        if (_semanticModel is null)
            return false;

        var namespaceName = type.ContainingNamespace.ToDisplayString();
        if (!_map.IsContractNamespaceOrChild(namespaceName))
            return false;
        if (type.DeclaringSyntaxReferences.Length == 0)
        {
            destinationNamespace = namespaceName;
            return true;
        }

        string? destination = null;
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax();
            var declarationModel = _semanticModel.Compilation.GetSemanticModel(
                reference.SyntaxTree, ignoreAccessibility: true);
            var moves = declaration.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Any(namespaceBlock => ContainsCanonicalDeclaration(
                    namespaceBlock, declarationModel));
            var candidate = moves
                ? CorrectedNamespaceText(namespaceName)
                : namespaceName;
            if (destination is not null && destination != candidate)
                return false;
            destination = candidate;
        }

        if (destination is null)
            return false;
        destinationNamespace = destination;
        return true;
    }

    private string GetReferenceDestinationNamespace(SyntaxNode node)
    {
        var namespaceBlock = node.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        if (namespaceBlock is null)
            return "";

        var namespaceName = _semanticModel?.GetDeclaredSymbol(namespaceBlock)
            ?.ToDisplayString() ?? "";
        var moves = node.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Any(IsContractNamespaceDeclaration);
        return moves
            ? CorrectedNamespaceText(namespaceName)
            : namespaceName;
    }

    private string CorrectedNamespaceText(string namespaceName)
    {
        for (var candidate = namespaceName; !string.IsNullOrEmpty(candidate);)
        {
            if (TryGetRenamed(candidate, out var renamed))
                return renamed + namespaceName[candidate.Length..];

            var separator = candidate.LastIndexOf('.');
            if (separator < 0)
                break;
            candidate = candidate[..separator];
        }

        return namespaceName;
    }

    public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
    {
        var symbol = _semanticModel?.GetSymbolInfo(node).Symbol;
        if (_map.HasContractDeclarationProvenance
            && symbol is INamedTypeSymbol namedType
            && !IsSemanticContractType(namedType))
        {
            if (!IsDeclaredInContractNamespaceBlock(namedType))
                return VisitQualifiedTypeArguments(node);

            return CorrectedMovedTypeName(node, namedType);
        }

        if (symbol is INamespaceSymbol namespaceSymbol)
        {
            if (TryRenameContractNamespace(
                    namespaceSymbol, node, out var renamedNamespace))
                return renamedNamespace;

            if (_map.HasContractDeclarationProvenance)
            {
                if (!NamespaceContainsCanonicalType(namespaceSymbol))
                    return node;

                var corrected = CorrectedNamespaceName(node);
                return ReferenceEquals(corrected, node)
                    ? node
                    : corrected;
            }
        }

        var fullText = node.ToString();
        if (TryGetRenamed(fullText, out var renamed))
        {
            return IdentifierName(renamed)
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }
        return base.VisitQualifiedName(node);
    }

    private NameSyntax VisitQualifiedTypeArguments(NameSyntax name)
    {
        return name switch
        {
            QualifiedNameSyntax qualified => qualified
                .WithLeft(VisitQualifiedTypeArguments(qualified.Left))
                .WithRight(VisitQualifiedTypeArguments(qualified.Right)),
            AliasQualifiedNameSyntax aliased => aliased
                .WithName(VisitQualifiedTypeArguments(aliased.Name)),
            GenericNameSyntax generic => generic.WithTypeArgumentList(
                (TypeArgumentListSyntax)Visit(generic.TypeArgumentList)!),
            _ => name,
        };
    }

    private SimpleNameSyntax VisitQualifiedTypeArguments(SimpleNameSyntax name) =>
        (SimpleNameSyntax)VisitQualifiedTypeArguments((NameSyntax)name);

    private bool IsSemanticContractDeclaration(BaseTypeDeclarationSyntax node)
    {
        var symbol = _semanticModel?.GetDeclaredSymbol(node);
        return symbol is INamedTypeSymbol named
            && (!_map.HasContractDeclarationProvenance
                ? IsSemanticContractType(named)
                : IsCanonicalDeclaration(named, node));
    }

    private bool IsContractFieldReference(IdentifierNameSyntax node)
    {
        return _semanticModel?.GetSymbolInfo(node).Symbol is IFieldSymbol field
            && IsSemanticContractType(field.ContainingType);
    }

    private ITypeSymbol? GetReferencedType(SimpleNameSyntax node)
    {
        if (_semanticModel is null)
            return null;

        var alias = _semanticModel.GetAliasInfo(node);
        if (alias?.Target is ITypeSymbol aliasType)
            return aliasType;

        var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
        return symbol switch
        {
            ITypeSymbol type => type,
            IMethodSymbol { MethodKind: MethodKind.Constructor } constructor =>
                constructor.ContainingType,
            _ => null,
        };
    }

    private bool IsJsonContextContractProperty(IdentifierNameSyntax node)
    {
        if (!_map.IsContractSymbol(node.Identifier.Text)
            || node.Parent is not MemberAccessExpressionSyntax access
            || access.Name != node
            || _semanticModel is null)
            return false;

        var receiverTypes = new List<ITypeSymbol>();
        var directType = _semanticModel.GetTypeInfo(access.Expression).Type;
        if (directType is not null)
            receiverTypes.Add(directType);
        receiverTypes.AddRange(access.Expression.DescendantNodesAndSelf()
            .OfType<SimpleNameSyntax>()
            .Select(GetReferencedType)
            .OfType<ITypeSymbol>());

        return receiverTypes.Any(IsJsonSerializerContext);
    }

    private static bool IsJsonSerializerContext(ITypeSymbol type)
    {
        for (var current = type; current is not null;
             current = current.BaseType)
        {
            if (current.ToDisplayString()
                == "System.Text.Json.Serialization.JsonSerializerContext")
                return true;
        }

        return false;
    }

    private bool IsSemanticContractType(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol namedType
            || type.TypeKind == TypeKind.Error)
            return false;

        namedType = namedType.OriginalDefinition;
        if (_map.HasContractDeclarationProvenance)
        {
            var currentTreeReferences = namedType.DeclaringSyntaxReferences
                .Where(reference => reference.SyntaxTree == _semanticModel?.SyntaxTree)
                .ToArray();
            return currentTreeReferences.Length > 0
                ? currentTreeReferences.Any(reference =>
                    IsCanonicalDeclaration(namedType, reference))
                : namedType.DeclaringSyntaxReferences.Any(reference =>
                    IsCanonicalDeclaration(namedType, reference));
        }

        // Standalone transform callers that construct ContractNames directly
        // have no scanner provenance. Preserve their legacy namespace guard.
        var namespaceName = namedType.ContainingNamespace.ToDisplayString();
        return _map.IsContractSymbol(namedType.Name)
            && _map.IsContractNamespaceOrChild(namespaceName);
    }

    private static string GetMetadataName(INamedTypeSymbol type)
    {
        var ownName = type.MetadataName;
        if (type.ContainingType is not null)
            return GetMetadataName(type.ContainingType) + "+" + ownName;
        var namespaceName = type.ContainingNamespace.ToDisplayString();
        return string.IsNullOrEmpty(namespaceName)
            ? ownName
            : namespaceName + "." + ownName;
    }

    private bool IsCanonicalDeclaration(
        INamedTypeSymbol type, SyntaxNode declaration)
    {
        var reference = type.DeclaringSyntaxReferences.FirstOrDefault(candidate =>
            candidate.SyntaxTree == declaration.SyntaxTree
            && candidate.Span == declaration.Span);
        return reference is not null && IsCanonicalDeclaration(type, reference);
    }

    private bool IsCanonicalDeclaration(
        INamedTypeSymbol type, SyntaxReference declaration)
    {
        var rawKind = declaration.GetSyntax().RawKind;
        var peers = type.DeclaringSyntaxReferences
            .Where(candidate => PathIdentity.Comparer.Equals(
                    ContractScanner.NormalizeSourcePath(candidate.SyntaxTree.FilePath),
                    ContractScanner.NormalizeSourcePath(declaration.SyntaxTree.FilePath))
                && candidate.GetSyntax().RawKind == rawKind)
            .OrderBy(candidate => candidate.Span.Start)
            .ToArray();
        var ordinal = Array.IndexOf(peers, declaration);
        return ordinal >= 0 && _map.IsCanonicalContractDeclaration(
            GetMetadataName(type.OriginalDefinition),
            declaration.SyntaxTree.FilePath,
            rawKind,
            ordinal);
    }

    private bool TryRenameContractNamespace(
        INamespaceSymbol symbol,
        NameSyntax original,
        out NameSyntax renamed)
    {
        var namespaceName = symbol.ToDisplayString();
        if (_map.IsContractNamespace(namespaceName)
            && (!_map.HasContractDeclarationProvenance
                || NamespaceContainsCanonicalType(symbol))
            && TryGetRenamed(namespaceName, out var renamedText))
        {
            renamed = IdentifierName(renamedText)
                .WithLeadingTrivia(original.GetLeadingTrivia())
                .WithTrailingTrivia(original.GetTrailingTrivia());
            return true;
        }

        renamed = original;
        return false;
    }

    private bool IsDeclaredInContractNamespaceBlock(INamedTypeSymbol type)
    {
        if (_semanticModel is null)
            return false;

        foreach (var reference in type.OriginalDefinition.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax();
            var declarationModel = _semanticModel.Compilation.GetSemanticModel(
                reference.SyntaxTree, ignoreAccessibility: true);
            foreach (var namespaceBlock in declaration.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>())
            {
                if (GetTypeDeclarations(namespaceBlock)
                    .Any(candidate =>
                        declarationModel.GetDeclaredSymbol(candidate)
                            is INamedTypeSymbol candidateType
                        && IsCanonicalDeclaration(candidateType, candidate)))
                    return true;
            }
        }

        return false;
    }

    private NameSyntax CorrectedMovedTypeName(
        QualifiedNameSyntax original,
        INamedTypeSymbol type)
    {
        var corrected = CorrectedNamespaceName(original);
        if (!ReferenceEquals(corrected, original))
            return corrected;

        var namespaceName = type.ContainingNamespace.ToDisplayString();
        if (!TryGetRenamed(namespaceName, out var renamedNamespace))
            return original;

        return RenameAliasedNamespacePrefix(
            original, namespaceName, renamedNamespace);
    }

    private static NameSyntax RenameAliasedNamespacePrefix(
        NameSyntax name,
        string namespaceName,
        string renamedNamespace)
    {
        if (name is QualifiedNameSyntax qualified)
        {
            var leftText = qualified.Left.ToString();
            var aliasSeparator = leftText.IndexOf("::", StringComparison.Ordinal);
            if (aliasSeparator >= 0
                && leftText[(aliasSeparator + 2)..] == namespaceName)
            {
                var aliasPrefix = leftText[..(aliasSeparator + 2)];
                return qualified.WithLeft(ParseName(aliasPrefix + renamedNamespace)
                    .WithLeadingTrivia(qualified.Left.GetLeadingTrivia())
                    .WithTrailingTrivia(qualified.Left.GetTrailingTrivia()));
            }

            var correctedLeft = RenameAliasedNamespacePrefix(
                qualified.Left, namespaceName, renamedNamespace);
            if (!ReferenceEquals(correctedLeft, qualified.Left))
                return qualified.WithLeft(correctedLeft);
        }

        return name;
    }

    private bool ContainsCanonicalDeclaration(BaseNamespaceDeclarationSyntax node)
    {
        if (_semanticModel is null)
            return false;
        return ContainsCanonicalDeclaration(node, _semanticModel);
    }

    private bool ContainsCanonicalDeclaration(
        BaseNamespaceDeclarationSyntax node, SemanticModel semanticModel)
    {
        return GetTypeDeclarations(node)
            .Select(declaration => (
                Declaration: declaration,
                Symbol: semanticModel.GetDeclaredSymbol(declaration)))
            .Any(item => item.Symbol is INamedTypeSymbol type
                && IsCanonicalDeclaration(type, item.Declaration));
    }

    private static IEnumerable<SyntaxNode> GetTypeDeclarations(SyntaxNode root) =>
        root.DescendantNodes().Where(node =>
            node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);

    private bool IsContractNamespaceDeclaration(
        BaseNamespaceDeclarationSyntax node)
    {
        return ContainsCanonicalDeclaration(node);
    }

    private bool NamespaceContainsCanonicalType(INamespaceSymbol symbol)
    {
        return symbol.GetTypeMembers().Any(IsSemanticContractType)
            || symbol.GetNamespaceMembers().Any(NamespaceContainsCanonicalType);
    }

    private bool IsInsideContractType(SyntaxNode node)
    {
        var declaration = node.Ancestors()
            .OfType<BaseTypeDeclarationSyntax>()
            .FirstOrDefault();
        if (_semanticModel is not null && declaration is not null)
        {
            var symbol = _semanticModel.GetDeclaredSymbol(declaration);
            return symbol is not null && IsContractType(symbol);
        }

        var mappings = _map.GetAllMappings();
        var renamedValues = new HashSet<string>(mappings.Values);
        var current = node.Parent;
        while (current is not null)
        {
            switch (current)
            {
                case InterfaceDeclarationSyntax ifaceDecl:
                {
                    var name = ifaceDecl.Identifier.Text;
                    return IsMappedName(name, mappings, renamedValues);
                }
                case RecordDeclarationSyntax recordDecl:
                {
                    var name = recordDecl.Identifier.Text;
                    return IsMappedName(name, mappings, renamedValues);
                }
                case ClassDeclarationSyntax classDecl:
                {
                    var name = classDecl.Identifier.Text;
                    if (IsMappedName(name, mappings, renamedValues))
                        return true;
                    if (classDecl.BaseList is null)
                        return false;
                    foreach (var baseType in classDecl.BaseList.Types)
                    {
                        var typeName = baseType.Type switch
                        {
                            IdentifierNameSyntax id =>
                                id.Identifier.Text,
                            QualifiedNameSyntax q =>
                                q.Right.Identifier.Text,
                            _ => null
                        };
                        if (typeName is not null
                            && IsMappedName(
                                typeName, mappings, renamedValues))
                            return true;
                    }
                    return false;
                }
                case StructDeclarationSyntax:
                    return false;
            }
            current = current.Parent;
        }
        return false;
    }

    private static bool IsMappedName(
        string name,
        Dictionary<string, string> mappings,
        HashSet<string> renamedValues)
    {
        return mappings.ContainsKey(name)
            || renamedValues.Contains(name);
    }

    private bool TryGetRenamed(string original, out string renamed)
    {
        var mappings = _map.GetAllMappings();
        if (mappings.TryGetValue(original, out var value))
        {
            renamed = value;
            return true;
        }
        renamed = string.Empty;
        return false;
    }
}
