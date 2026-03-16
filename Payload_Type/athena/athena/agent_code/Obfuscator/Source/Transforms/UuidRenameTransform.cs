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
    private readonly UuidRenameMap _map;
    private HashSet<string> _contractTypedVars = new();

    public UuidRenameTransform(UuidRenameMap map)
    {
        _map = map;
    }

    public SyntaxTree Rewrite(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        _contractTypedVars = CollectContractTypedNames(root);
        var rewritten = Visit(root);
        return tree.WithRootAndOptions(rewritten!, tree.Options);
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

    public override SyntaxNode? VisitNamespaceDeclaration(
        NamespaceDeclarationSyntax node)
    {
        var visited = (NamespaceDeclarationSyntax)base.VisitNamespaceDeclaration(node)!;
        var nameText = node.Name.ToString();
        if (TryGetRenamed(nameText, out var renamed))
            return visited.WithName(ParseName(renamed));
        return visited;
    }

    public override SyntaxNode? VisitFileScopedNamespaceDeclaration(
        FileScopedNamespaceDeclarationSyntax node)
    {
        var visited = (FileScopedNamespaceDeclarationSyntax)
            base.VisitFileScopedNamespaceDeclaration(node)!;
        var nameText = node.Name.ToString();
        if (TryGetRenamed(nameText, out var renamed))
            return visited.WithName(ParseName(renamed));
        return visited;
    }

    public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
    {
        var visited = (UsingDirectiveSyntax)base.VisitUsingDirective(node)!;
        var nameText = node.NamespaceOrType.ToString();
        if (TryGetRenamed(nameText, out var renamed))
            return visited.WithName(ParseName(renamed));
        return visited;
    }

    public override SyntaxNode? VisitInterfaceDeclaration(
        InterfaceDeclarationSyntax node)
    {
        var visited = (InterfaceDeclarationSyntax)base.VisitInterfaceDeclaration(node)!;
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        if (UuidRenameMap.IsAlwaysRename(node.Identifier.Text)
            && TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        var visited = (RecordDeclarationSyntax)base.VisitRecordDeclaration(node)!;
        if (UuidRenameMap.IsAlwaysRename(node.Identifier.Text)
            && TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        var visited = (EnumDeclarationSyntax)base.VisitEnumDeclaration(node)!;
        if (UuidRenameMap.IsAlwaysRename(node.Identifier.Text)
            && TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitConstructorDeclaration(
        ConstructorDeclarationSyntax node)
    {
        var visited = (ConstructorDeclarationSyntax)
            base.VisitConstructorDeclaration(node)!;
        if (UuidRenameMap.IsAlwaysRename(node.Identifier.Text)
            && TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        if (node.Modifiers.Any(SyntaxKind.OverrideKeyword))
            return visited;
        if (!UuidRenameMap.IsAlwaysRename(node.Identifier.Text)
            && !IsInsideContractType(node))
            return visited;
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitPropertyDeclaration(
        PropertyDeclarationSyntax node)
    {
        var visited = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
        if (!UuidRenameMap.IsAlwaysRename(node.Identifier.Text)
            && !IsInsideContractType(node))
            return visited;
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
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
            if (TryGetRenamed(vars[i].Identifier.Text, out var renamed))
            {
                vars = vars.Replace(
                    vars[i],
                    vars[i].WithIdentifier(Identifier(renamed)));
                changed = true;
            }
        }
        return changed
            ? visited.WithDeclaration(decl.WithVariables(vars))
            : visited;
    }

    public override SyntaxNode? VisitParameter(ParameterSyntax node)
    {
        var visited = (ParameterSyntax)base.VisitParameter(node)!;
        if (!UuidRenameMap.IsAlwaysRename(node.Identifier.Text)
            && !IsInsideContractType(node))
            return visited;
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (!TryGetRenamed(node.Identifier.Text, out var renamed))
            return base.VisitIdentifierName(node);

        // Type/interface/namespace names are always renamed
        if (UuidRenameMap.IsAlwaysRename(node.Identifier.Text))
        {
            return node
                .WithIdentifier(Identifier(renamed))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

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
        // the right side of a member access.
        if (UuidRenameMap.IsInterfaceMember(node.Identifier.Text)
            && node.Parent is not MemberAccessExpressionSyntax
            && IsInsideContractType(node))
        {
            return node
                .WithIdentifier(Identifier(renamed))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        return base.VisitIdentifierName(node);
    }

    private bool IsMemberAccessOnContractType(
        MemberAccessExpressionSyntax memberAccess)
    {
        var expr = memberAccess.Expression;
        return expr switch
        {
            ThisExpressionSyntax => true,
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

    public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    {
        var visited = (GenericNameSyntax)base.VisitGenericName(node)!;
        if (!TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited;

        if (UuidRenameMap.IsAlwaysRename(node.Identifier.Text))
            return visited.WithIdentifier(Identifier(renamed));

        if (node.Parent is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name == node
            && IsMemberAccessOnContractType(memberAccess))
            return visited.WithIdentifier(Identifier(renamed));

        return visited;
    }

    public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
    {
        var fullText = node.ToString();
        if (TryGetRenamed(fullText, out var renamed))
        {
            return IdentifierName(renamed)
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }
        return base.VisitQualifiedName(node);
    }

    private bool IsInsideContractType(SyntaxNode node)
    {
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
