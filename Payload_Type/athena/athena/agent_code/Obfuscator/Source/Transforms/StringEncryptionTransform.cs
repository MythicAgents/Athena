using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Obfuscator.Source.Transforms;

/// <summary>
/// Rewrites string literals into XOR-encrypted byte arrays
/// with calls to a runtime decryptor method.
/// </summary>
public sealed class StringEncryptionTransform : CSharpSyntaxRewriter
{
    private const string SemanticExemptionKind =
        "StringEncryptionSemanticExemption";
    private static readonly SyntaxAnnotation SemanticExemption =
        new(SemanticExemptionKind);
    private readonly string _className;
    private readonly string _methodName;
    private readonly string _namespace;
    private readonly int _seed;
    private SemanticModel? _semanticModel;
    private int _stringIndex;

    public StringEncryptionTransform(
        string decryptorClassName,
        string decryptorMethodName,
        string decryptorNamespace,
        int seed)
    {
        _className = decryptorClassName;
        _methodName = decryptorMethodName;
        _namespace = decryptorNamespace;
        _seed = seed;
    }

    public SyntaxTree Rewrite(
        SyntaxTree tree,
        SemanticModel? semanticModel = null)
    {
        if (semanticModel is not null)
            tree = MarkSemanticExemptions(tree, semanticModel);
        var root = tree.GetRoot();
        var rewritten = Visit(root);
        return tree.WithRootAndOptions(rewritten, tree.Options);
    }

    public SyntaxTree MarkSemanticExemptions(
        SyntaxTree tree,
        SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
        var root = tree.GetRoot();
        var exempt = root.DescendantNodes()
            .OfType<LiteralExpressionSyntax>()
            .Where(node => node.IsKind(SyntaxKind.StringLiteralExpression))
            .Where(IsPluginNameImplementationLiteral)
            .ToHashSet();

        foreach (var invocationSyntax in root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetOperation(invocationSyntax)
                    is not IInvocationOperation invocation
                || !IsReflectionLookup(invocation.TargetMethod))
                continue;

            var nameArgument = invocation.Arguments.FirstOrDefault(argument =>
                argument.Parameter?.Type.SpecialType
                    == SpecialType.System_String
                && argument.Parameter.Name is "name" or "typeName");
            if (nameArgument is null)
                continue;

            foreach (var literal in TraceKnownStringLiterals(
                nameArgument.Value, root, semanticModel,
                new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default)))
                exempt.Add(literal);
        }

        var annotated = root.ReplaceNodes(
            exempt,
            (_, rewritten) => rewritten.WithAdditionalAnnotations(
                SemanticExemption));
        _semanticModel = null;
        return tree.WithRootAndOptions(annotated, tree.Options);
    }

    public override SyntaxNode? VisitLiteralExpression(
        LiteralExpressionSyntax node)
    {
        if (node.Kind() != SyntaxKind.StringLiteralExpression)
            return base.VisitLiteralExpression(node);

        if (node.HasAnnotations(SemanticExemptionKind))
            return base.VisitLiteralExpression(node);

        var value = node.Token.ValueText;

        if (value.Length == 0)
            return base.VisitLiteralExpression(node);

        if (IsInsideNameof(node))
            return base.VisitLiteralExpression(node);

        if (IsInsideAttribute(node))
            return base.VisitLiteralExpression(node);

        if (IsConstDeclaration(node))
            return base.VisitLiteralExpression(node);

        if (IsInsideSwitchLabel(node))
            return base.VisitLiteralExpression(node);

        if (IsInsidePattern(node))
            return base.VisitLiteralExpression(node);

        if (IsPluginNameImplementationLiteral(node))
            return base.VisitLiteralExpression(node);

        return CreateDecryptorCall(value, node);
    }

    public override SyntaxNode? VisitInterpolatedStringExpression(
        InterpolatedStringExpressionSyntax node)
    {
        // Apply the same exclusion rules as regular string literals
        if (IsInsideAttribute(node))
            return base.VisitInterpolatedStringExpression(node);
        if (IsConstDeclaration(node))
            return base.VisitInterpolatedStringExpression(node);
        if (IsInsideSwitchLabel(node))
            return base.VisitInterpolatedStringExpression(node);
        if (IsInsidePattern(node))
            return base.VisitInterpolatedStringExpression(node);

        // Visit children first so nested string literals inside {}
        // holes are also encrypted, then encrypt the text spans.
        var visited = (InterpolatedStringExpressionSyntax)
            base.VisitInterpolatedStringExpression(node)!;

        var newContents =
            new List<InterpolatedStringContentSyntax>(
                visited.Contents.Count);
        bool changed = false;

        foreach (var content in visited.Contents)
        {
            if (content is InterpolatedStringTextSyntax text)
            {
                // ValueText is the unescaped text value
                var rawText = text.TextToken.ValueText;
                if (rawText.Length == 0)
                {
                    newContents.Add(content);
                    continue;
                }

                // Encrypt the text and wrap in an interpolation hole:
                // $"Hello {x}" → $"{_Ns._Dec._D(bytes, key)}{x}"
                var decryptorCall = CreateDecryptorCall(rawText, content);
                newContents.Add(Interpolation(decryptorCall));
                changed = true;
            }
            else
            {
                newContents.Add(content);
            }
        }

        return changed
            ? visited.WithContents(List(newContents))
            : visited;
    }

    private ExpressionSyntax CreateDecryptorCall(
        string value,
        SyntaxNode original)
    {
        var key = ComputeKey(_seed, _stringIndex);
        _stringIndex++;

        var utf8Bytes = Encoding.UTF8.GetBytes(value);
        var encrypted = new byte[utf8Bytes.Length];
        for (int i = 0; i < utf8Bytes.Length; i++)
            encrypted[i] = (byte)(utf8Bytes[i] ^ key);

        var byteElements = encrypted.Select(
            b => LiteralExpression(
                SyntaxKind.NumericLiteralExpression,
                Literal(FormatHex(b), b)));

        var newKeyword = Token(
            SyntaxTriviaList.Empty,
            SyntaxKind.NewKeyword,
            TriviaList(Space));

        var byteArray = ArrayCreationExpression(
            newKeyword,
            ArrayType(
                PredefinedType(Token(SyntaxKind.ByteKeyword)),
                SingletonList(
                    ArrayRankSpecifier(
                        SingletonSeparatedList<ExpressionSyntax>(
                            OmittedArraySizeExpression())))),
            InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList<ExpressionSyntax>(byteElements)));

        var keyLiteral = LiteralExpression(
            SyntaxKind.NumericLiteralExpression,
            Literal(FormatHex(key), key));

        var castKey = CastExpression(
            PredefinedType(Token(SyntaxKind.ByteKeyword)),
            keyLiteral);

        var memberAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(_namespace),
                IdentifierName(_className)),
            IdentifierName(_methodName));

        var invocation = InvocationExpression(
            memberAccess,
            ArgumentList(SeparatedList(new[]
            {
                Argument(byteArray),
                Argument(castKey),
            })));

        return invocation
            .WithLeadingTrivia(original.GetLeadingTrivia())
            .WithTrailingTrivia(original.GetTrailingTrivia());
    }

    private static byte ComputeKey(int seed, int stringIndex)
    {
        var key = (byte)((seed + stringIndex) & 0xFF);
        return key == 0 ? (byte)1 : key;
    }

    private static string FormatHex(byte value)
    {
        return $"0x{value:X2}";
    }

    private static bool IsInsideNameof(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is not InvocationExpressionSyntax invocation)
                continue;
            if (invocation.Expression is IdentifierNameSyntax id
                && id.Identifier.Text == "nameof")
                return true;
        }
        return false;
    }

    private static bool IsInsideAttribute(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is AttributeArgumentSyntax)
                return true;
        }
        return false;
    }

    private static bool IsConstDeclaration(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is FieldDeclarationSyntax field
                && field.Modifiers.Any(SyntaxKind.ConstKeyword))
                return true;
            if (ancestor is LocalDeclarationStatementSyntax local
                && local.Modifiers.Any(SyntaxKind.ConstKeyword))
                return true;
        }
        return false;
    }

    private static bool IsInsidePattern(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is ConstantPatternSyntax)
                return true;
        }
        return false;
    }

    private static bool IsInsideSwitchLabel(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is CaseSwitchLabelSyntax
                || ancestor is CasePatternSwitchLabelSyntax
                || ancestor is SwitchExpressionArmSyntax)
                return true;
            if (ancestor is SwitchStatementSyntax
                || ancestor is SwitchExpressionSyntax)
                return false;
        }
        return false;
    }

    private bool IsPluginNameImplementationLiteral(
        LiteralExpressionSyntax node)
    {
        if (_semanticModel is null)
            return false;

        var propertySyntax = node.Ancestors()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault();
        if (propertySyntax is null)
            return false;

        var property = _semanticModel.GetDeclaredSymbol(propertySyntax);
        if (property is null
            || property.Type.SpecialType != SpecialType.System_String)
            return false;

        if (property.ExplicitInterfaceImplementations.Any(IsPluginNameProperty))
            return true;

        if (property.Name != "Name")
            return false;

        foreach (var contract in property.ContainingType.AllInterfaces)
        {
            if (!IsPluginInterface(contract))
                continue;

            foreach (var member in contract.GetMembers("Name")
                .OfType<IPropertySymbol>())
            {
                if (SymbolEqualityComparer.Default.Equals(
                    property.ContainingType.FindImplementationForInterfaceMember(member),
                    property))
                    return true;
            }
        }

        return false;
    }

    private static IEnumerable<LiteralExpressionSyntax> TraceKnownStringLiterals(
        IOperation operation,
        SyntaxNode root,
        SemanticModel semanticModel,
        HashSet<ILocalSymbol> visitedLocals)
    {
        var constant = semanticModel.GetConstantValue(operation.Syntax);
        if (constant.HasValue && constant.Value is string)
        {
            var constantLiterals = operation.Syntax.DescendantNodesAndSelf()
                .OfType<LiteralExpressionSyntax>()
                .Where(node => node.IsKind(SyntaxKind.StringLiteralExpression))
                .ToArray();
            if (constantLiterals.Length > 0)
            {
                foreach (var literal in constantLiterals)
                    yield return literal;
                yield break;
            }
        }

        switch (operation)
        {
            case ILiteralOperation literal
                when literal.ConstantValue.HasValue
                    && literal.ConstantValue.Value is string
                    && literal.Syntax is LiteralExpressionSyntax syntax:
                yield return syntax;
                yield break;

            case IConversionOperation conversion:
                foreach (var result in TraceKnownStringLiterals(
                    conversion.Operand, root, semanticModel, visitedLocals))
                    yield return result;
                yield break;

            case IParenthesizedOperation parenthesized:
                foreach (var result in TraceKnownStringLiterals(
                    parenthesized.Operand, root, semanticModel, visitedLocals))
                    yield return result;
                yield break;

            case ILocalReferenceOperation localReference
                when visitedLocals.Add(localReference.Local):
                if (HasWriteAfterDeclaration(
                    root, semanticModel, localReference.Local))
                    yield break;
                foreach (var declaration in localReference.Local.DeclaringSyntaxReferences)
                {
                    if (declaration.GetSyntax() is not VariableDeclaratorSyntax variable
                        || variable.Initializer is null
                        || semanticModel.GetOperation(variable.Initializer.Value)
                            is not { } initializer)
                        continue;
                    foreach (var result in TraceKnownStringLiterals(
                        initializer, root, semanticModel, visitedLocals))
                        yield return result;
                }
                yield break;
        }
    }

    private static bool HasWriteAfterDeclaration(
        SyntaxNode root,
        SemanticModel semanticModel,
        ILocalSymbol local)
    {
        foreach (var assignment in root.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>())
        {
            if (SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(assignment.Left).Symbol, local))
                return true;
        }

        return false;
    }

    private static bool IsReflectionLookup(IMethodSymbol method)
    {
        var containingType = method.ContainingType.ToDisplayString();
        return containingType switch
        {
            "System.Type" => method.Name is "GetProperty" or "GetField"
                or "GetMethod" or "GetEvent" or "GetMember"
                or "GetNestedType" or "GetInterface" or "InvokeMember"
                or "GetType" or "GetMethodImpl" or "GetPropertyImpl",
            "System.Reflection.TypeInfo" => method.Name is "GetDeclaredProperty"
                or "GetDeclaredField" or "GetDeclaredMethod"
                or "GetDeclaredMethods" or "GetDeclaredEvent"
                or "GetDeclaredNestedType",
            "System.Reflection.RuntimeReflectionExtensions" => method.Name
                is "GetRuntimeProperty" or "GetRuntimeField"
                or "GetRuntimeMethod" or "GetRuntimeEvent",
            "System.Reflection.Assembly" =>
                method.Name == "ReflectionOnlyGetType",
            _ => false,
        };
    }

    private static bool IsPluginNameProperty(IPropertySymbol property)
    {
        return property.Name == "Name"
            && property.Type.SpecialType == SpecialType.System_String
            && IsPluginInterface(property.ContainingType);
    }

    private static bool IsPluginInterface(INamedTypeSymbol type)
    {
        return type.TypeKind == TypeKind.Interface
            && type.Name == "IPlugin"
            && type.ContainingNamespace.ToDisplayString() == "Agent.Interfaces";
    }
}
