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

    public UuidRenameTransform(UuidRenameMap map)
    {
        _map = map;
    }

    public SyntaxTree Rewrite(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var rewritten = Visit(root);
        return tree.WithRootAndOptions(rewritten!, tree.Options);
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
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        var visited = (RecordDeclarationSyntax)base.VisitRecordDeclaration(node)!;
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        var visited = (EnumDeclarationSyntax)base.VisitEnumDeclaration(node)!;
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitPropertyDeclaration(
        PropertyDeclarationSyntax node)
    {
        var visited = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitParameter(ParameterSyntax node)
    {
        var visited = (ParameterSyntax)base.VisitParameter(node)!;
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
            return visited.WithIdentifier(Identifier(renamed));
        return visited;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (TryGetRenamed(node.Identifier.Text, out var renamed))
        {
            return node
                .WithIdentifier(Identifier(renamed))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }
        return base.VisitIdentifierName(node);
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
