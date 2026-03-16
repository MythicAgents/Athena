using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Obfuscator.Source.Transforms;

/// <summary>
/// Rewrites string literals into XOR-encrypted byte arrays
/// with calls to a runtime decryptor method.
/// </summary>
public sealed class StringEncryptionTransform : CSharpSyntaxRewriter
{
    private readonly string _className;
    private readonly string _methodName;
    private readonly string _namespace;
    private readonly int _seed;
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

    public SyntaxTree Rewrite(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var rewritten = Visit(root);
        return tree.WithRootAndOptions(rewritten, tree.Options);
    }

    public override SyntaxNode? VisitLiteralExpression(
        LiteralExpressionSyntax node)
    {
        if (node.Kind() != SyntaxKind.StringLiteralExpression)
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

        return CreateDecryptorCall(value, node);
    }

    // TODO: Handle InterpolatedStringExpression by encrypting
    // InterpolatedStringText portions and rebuilding via
    // string.Concat. Skipped for initial implementation.

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
}
