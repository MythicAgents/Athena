using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Obfuscator.Source.Transforms;

public sealed class ApiCallHidingTransform : CSharpSyntaxRewriter
{
    // Value = true means static, false means instance
    private static readonly HashSet<(string Type, string Method)>
        SensitiveApis = new()
    {
        ("System.Diagnostics.Process", "Start"),
        ("System.Reflection.Assembly", "Load"),
        ("System.Reflection.Assembly", "LoadFrom"),
        ("System.Reflection.Assembly", "LoadFile"),
        ("System.IO.File", "ReadAllBytes"),
        ("System.IO.File", "ReadAllText"),
        ("System.IO.File", "WriteAllBytes"),
        ("System.IO.File", "WriteAllText"),
        ("System.Net.Sockets.Socket", "Connect"),
        ("System.Net.Http.HttpClient", "SendAsync"),
        ("System.Net.WebClient", "DownloadData"),
    };

    private readonly string _callerClassName;
    private readonly string _invokeMethodName;
    private readonly string _callerNamespace;
    private readonly int _seed;
    private readonly List<(string TypeName, string MethodName)> _hiddenCalls = [];
    private SemanticModel? _semanticModel;

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

    public SyntaxTree Rewrite(SyntaxTree tree, SemanticModel semanticModel)
    {
        if (semanticModel.SyntaxTree != tree)
            throw new ArgumentException(
                "Semantic model must belong to the rewritten tree.",
                nameof(semanticModel));
        _semanticModel = semanticModel;
        var root = tree.GetRoot();
        var rewritten = (CompilationUnitSyntax)Visit(root)!;
        _semanticModel = null;

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
            var invocationOperation = _semanticModel!.GetOperation(node)
                as IInvocationOperation;
            var method = invocationOperation?.TargetMethod
                ?? _semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
            var methodName = method?.Name;
            var typeName = method?.ContainingType.ToDisplayString(
                SymbolDisplayFormat.CSharpErrorMessageFormat);

            if (methodName is not null
                && typeName is not null
                && SensitiveApis.Contains((typeName, methodName)))
            {
                if (invocationOperation is null)
                    return base.VisitInvocationExpression(node);

                var hasByReferenceArgument = node.ArgumentList.Arguments.Any(
                        argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None))
                    || invocationOperation.Arguments.Any(argument =>
                        argument.Parameter is { RefKind: not RefKind.None });
                if (hasByReferenceArgument
                    || method!.Parameters.Any(
                        parameter => parameter.RefKind != RefKind.None))
                    return base.VisitInvocationExpression(node);
                _hiddenCalls.Add((typeName, methodName));

                ExpressionSyntax? instanceExpr = method!.IsStatic
                    ? null
                    : memberAccess.Expression;

                var invocation = BuildIndirectInvocation(
                    typeName, methodName,
                    instanceExpr, node, method, invocationOperation);

                // Void-returning invocations cannot be cast, including in
                // expression-bodied members. Statement expressions also skip
                // the cast to avoid CS0201.
                if (method!.ReturnsVoid
                    || node.Parent is ExpressionStatementSyntax)
                {
                    return invocation
                        .WithLeadingTrivia(node.GetLeadingTrivia())
                        .WithTrailingTrivia(
                            node.GetTrailingTrivia());
                }

                // Preserve the bound return type so downstream overload and
                // extension-method binding remains static. Keep dynamic only
                // when the invoked method itself returns dynamic.
                var returnType = method.ReturnType.TypeKind == TypeKind.Dynamic
                    ? IdentifierName("dynamic")
                    : ParseTypeName(method.ReturnType.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat));
                var cast = CastExpression(returnType, invocation);
                return ParenthesizedExpression(cast)
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }
        }

        return base.VisitInvocationExpression(node);
    }


    private InvocationExpressionSyntax BuildIndirectInvocation(
        string typeName,
        string methodName,
        ExpressionSyntax? instanceExpr,
        InvocationExpressionSyntax original,
        IMethodSymbol method,
        IInvocationOperation invocationOperation)
    {
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

        var boundArguments = BindArguments(original, invocationOperation);
        var arrayElements = boundArguments.Select(argument =>
            (ExpressionSyntax)CastExpression(
                NullableType(PredefinedType(
                    Token(SyntaxKind.ObjectKeyword))),
                ParenthesizedExpression(argument.Syntax.Expression)));

        var argsArray = ArrayCreationExpression(
            Token(SyntaxTriviaList.Empty, SyntaxKind.NewKeyword,
                TriviaList(Space)),
            ArrayType(
                NullableType(PredefinedType(
                    Token(SyntaxKind.ObjectKeyword))),
                SingletonList(
                    ArrayRankSpecifier(
                        SingletonSeparatedList<ExpressionSyntax>(
                            OmittedArraySizeExpression())))),
            InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList<ExpressionSyntax>(arrayElements)));

        var parameterTypes = method.Parameters.Select(parameter =>
        {
            ExpressionSyntax typeExpression = TypeOfExpression(
                ParseTypeName(parameter.Type.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat)));
            if (parameter.RefKind != RefKind.None)
            {
                typeExpression = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        typeExpression,
                        IdentifierName("MakeByRefType")));
            }
            return typeExpression;
        });
        var parameterTypesArray = ArrayCreationExpression(
            Token(SyntaxTriviaList.Empty, SyntaxKind.NewKeyword,
                TriviaList(Space)),
            ArrayType(
                ParseTypeName("global::System.Type"),
                SingletonList(
                    ArrayRankSpecifier(
                        SingletonSeparatedList<ExpressionSyntax>(
                            OmittedArraySizeExpression())))),
            InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList(parameterTypes)));

        var argumentOrdinals = ArrayCreationExpression(
            Token(SyntaxTriviaList.Empty, SyntaxKind.NewKeyword,
                TriviaList(Space)),
            ArrayType(
                PredefinedType(Token(SyntaxKind.IntKeyword)),
                SingletonList(
                    ArrayRankSpecifier(
                        SingletonSeparatedList<ExpressionSyntax>(
                            OmittedArraySizeExpression())))),
            InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList<ExpressionSyntax>(boundArguments.Select(argument =>
                    LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(argument.Parameter!.Ordinal))))));

        var argumentIsExpandedParams = ArrayCreationExpression(
            Token(SyntaxTriviaList.Empty, SyntaxKind.NewKeyword,
                TriviaList(Space)),
            ArrayType(
                PredefinedType(Token(SyntaxKind.BoolKeyword)),
                SingletonList(
                    ArrayRankSpecifier(
                        SingletonSeparatedList<ExpressionSyntax>(
                            OmittedArraySizeExpression())))),
            InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList<ExpressionSyntax>(boundArguments.Select(argument =>
                    LiteralExpression(argument.IsExpandedParams
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression)))));

        var parameterIsParams = ArrayCreationExpression(
            Token(SyntaxTriviaList.Empty, SyntaxKind.NewKeyword,
                TriviaList(Space)),
            ArrayType(
                PredefinedType(Token(SyntaxKind.BoolKeyword)),
                SingletonList(
                    ArrayRankSpecifier(
                        SingletonSeparatedList<ExpressionSyntax>(
                            OmittedArraySizeExpression())))),
            InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList<ExpressionSyntax>(method.Parameters.Select(parameter =>
                    LiteralExpression(parameter.IsParams
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression)))));

        var instanceArg = Argument(
            instanceExpr
                ?? LiteralExpression(
                    SyntaxKind.NullLiteralExpression));

        return InvocationExpression(
            callerAccess,
            ArgumentList(SeparatedList(new[]
            {
                typeNameArg,
                methodNameArg,
                instanceArg,
                Argument(parameterTypesArray),
                Argument(argsArray),
                Argument(argumentOrdinals),
                Argument(parameterIsParams),
                Argument(argumentIsExpandedParams),
            })));
    }

    private static (ArgumentSyntax Syntax, IParameterSymbol Parameter,
        bool IsExpandedParams)[] BindArguments(
        InvocationExpressionSyntax invocationSyntax,
        IInvocationOperation invocationOperation)
    {
        return invocationSyntax.ArgumentList.Arguments.Select(sourceArgument =>
        {
            var operation = invocationOperation.Arguments.FirstOrDefault(candidate =>
                candidate.Syntax is ArgumentSyntax argumentSyntax
                && argumentSyntax.SyntaxTree == sourceArgument.SyntaxTree
                && argumentSyntax.Span == sourceArgument.Span);

            // Roslyn represents expanded params arguments as one implicit
            // IArgumentOperation whose value is an implicit array creation.
            // Its explicit initializer element operations retain the source
            // expression syntax, so associate each source argument with that
            // containing params operation.
            operation ??= invocationOperation.Arguments.FirstOrDefault(candidate =>
                candidate.ArgumentKind == ArgumentKind.ParamArray
                && ContainsExplicitSourceSyntax(
                    candidate.Value, sourceArgument.Expression));

            var parameter = operation?.Parameter
                ?? throw new InvalidOperationException(
                    "Could not bind invocation argument to a parameter.");
            return (
                Syntax: sourceArgument,
                Parameter: parameter,
                IsExpandedParams:
                    operation!.ArgumentKind == ArgumentKind.ParamArray);
        }).ToArray();
    }

    private static bool ContainsExplicitSourceSyntax(
        IOperation operation,
        ExpressionSyntax sourceExpression)
    {
        if (!operation.IsImplicit
            && operation.Syntax.SyntaxTree == sourceExpression.SyntaxTree
            && sourceExpression.Span.Contains(operation.Syntax.Span))
            return true;

        return operation.ChildOperations.Any(child =>
            ContainsExplicitSourceSyntax(child, sourceExpression));
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
