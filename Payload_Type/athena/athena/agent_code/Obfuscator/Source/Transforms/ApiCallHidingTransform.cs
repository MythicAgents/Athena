using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Obfuscator.Source.Transforms;

public sealed class ApiCallHidingTransform : CSharpSyntaxRewriter
{
    // (TypeIdentifier, MethodName) pairs matched by syntax identifier text.
    private static readonly HashSet<(string Type, string Method)> SensitiveApis =
    [
        ("Process", "Start"),
        ("Assembly", "Load"),
        ("Assembly", "LoadFrom"),
        ("Assembly", "LoadFile"),
        ("File", "ReadAllBytes"),
        ("File", "ReadAllText"),
        ("File", "WriteAllBytes"),
        ("File", "WriteAllText"),
        ("Socket", "Connect"),
        ("HttpClient", "SendAsync"),
        ("WebClient", "DownloadData"),
    ];

    private readonly string _callerClassName;
    private readonly string _invokeMethodName;
    private readonly string _callerNamespace;
    private readonly int _seed;
    private readonly List<(string TypeName, string MethodName)> _hiddenCalls = [];

    public ApiCallHidingTransform(
        string callerClassName,
        string invokeMethodName,
        string callerNamespace,
        int seed)
    {
        _callerClassName = callerClassName;
        _invokeMethodName = invokeMethodName;
        _callerNamespace = callerNamespace;
        _seed = seed;
    }

    public SyntaxTree Rewrite(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var rewritten = (CompilationUnitSyntax)Visit(root)!;

        if (_hiddenCalls.Count > 0)
            rewritten = AddDynamicDependencyAttributes(rewritten);

        return tree.WithRootAndOptions(rewritten, tree.Options);
    }

    public override SyntaxNode? VisitInvocationExpression(
        InvocationExpressionSyntax node)
    {
        if (node.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Kind() == SyntaxKind.SimpleMemberAccessExpression)
        {
            var methodName = memberAccess.Name.Identifier.Text;
            var typeName = ExtractTypeName(memberAccess.Expression);

            if (typeName is not null
                && SensitiveApis.Contains((typeName, methodName)))
            {
                _hiddenCalls.Add((typeName, methodName));
                return BuildIndirectCall(typeName, methodName, node)
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }
        }

        return base.VisitInvocationExpression(node);
    }

    // Returns the rightmost simple identifier from an expression,
    // covering both "Process" (IdentifierName) and "System.Diagnostics.Process"
    // (MemberAccess chain).
    private static string? ExtractTypeName(ExpressionSyntax expr)
    {
        return expr switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax ma
                when ma.Kind() == SyntaxKind.SimpleMemberAccessExpression
                => ma.Name.Identifier.Text,
            _ => null,
        };
    }

    private ExpressionSyntax BuildIndirectCall(
        string typeName,
        string methodName,
        InvocationExpressionSyntax original)
    {
        // Build: _Ns._Caller._Invoke("TypeName", "MethodName", new object?[] { arg1, arg2 })
        var callerAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(_callerNamespace),
                IdentifierName(_callerClassName)),
            IdentifierName(_invokeMethodName));

        var typeNameArg = Argument(
            LiteralExpression(SyntaxKind.StringLiteralExpression,
                Literal(typeName)));

        var methodNameArg = Argument(
            LiteralExpression(SyntaxKind.StringLiteralExpression,
                Literal(methodName)));

        var originalArgs = original.ArgumentList.Arguments;
        var arrayElements = originalArgs.Select(a =>
            (ExpressionSyntax)CastExpression(
                NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
                a.Expression));

        var argsArray = ArrayCreationExpression(
            Token(SyntaxTriviaList.Empty, SyntaxKind.NewKeyword,
                TriviaList(Space)),
            ArrayType(
                NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
                SingletonList(
                    ArrayRankSpecifier(
                        SingletonSeparatedList<ExpressionSyntax>(
                            OmittedArraySizeExpression())))),
            InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList<ExpressionSyntax>(arrayElements)));

        var invocation = InvocationExpression(
            callerAccess,
            ArgumentList(SeparatedList(new[]
            {
                typeNameArg,
                methodNameArg,
                Argument(argsArray),
            })));

        return CastExpression(
            IdentifierName("dynamic"), invocation);
    }

    private CompilationUnitSyntax AddDynamicDependencyAttributes(
        CompilationUnitSyntax root)
    {
        // DynamicDependency is only valid on constructor, method, or field.
        // Find the first method or constructor to attach attributes to.
        var target = (MemberDeclarationSyntax?)
            root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault()
            ?? root.DescendantNodes()
                .OfType<ConstructorDeclarationSyntax>()
                .FirstOrDefault();

        if (target is null)
            return root;

        var attributes = _hiddenCalls
            .DistinctBy(c => (c.TypeName, c.MethodName))
            .Select(c => BuildDynamicDependencyAttribute(c.TypeName, c.MethodName))
            .ToArray();

        var attrList = AttributeList(SeparatedList(attributes))
            .WithTrailingTrivia(LineFeed);

        var updated = target.AddAttributeLists(attrList);
        return root.ReplaceNode(target, updated);
    }

    private static AttributeSyntax BuildDynamicDependencyAttribute(
        string typeName, string methodName)
    {
        // [System.Diagnostics.CodeAnalysis.DynamicDependency("MethodName", "TypeName", "")]
        var fullAttr = ParseName(
            "System.Diagnostics.CodeAnalysis.DynamicDependency");

        return Attribute(fullAttr,
            AttributeArgumentList(SeparatedList(new[]
            {
                AttributeArgument(
                    LiteralExpression(SyntaxKind.StringLiteralExpression,
                        Literal(methodName))),
                AttributeArgument(
                    LiteralExpression(SyntaxKind.StringLiteralExpression,
                        Literal(typeName))),
                AttributeArgument(
                    LiteralExpression(SyntaxKind.StringLiteralExpression,
                        Literal(""))),
            })));
    }
}
