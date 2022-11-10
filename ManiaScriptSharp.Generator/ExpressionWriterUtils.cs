using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace ManiaScriptSharp.Generator;

public record ExpressionWriterUtils(int Indent, ExpressionSyntax Expression, ImmutableArray<ParameterSyntax> Parameters, ManiaScriptBodyBuilder BodyBuilder)
    : WriterUtils(Indent, Parameters, BodyBuilder)
{
    public TextWriter Writer => BodyBuilder.Writer;

    public ExpressionWriter? GetExpressionWriter() => Expression switch
    {
        InvocationExpressionSyntax => new InvocationExpressionWriter(),
        IdentifierNameSyntax => new IdentifierNameExpressionWriter(),
        MemberAccessExpressionSyntax => new MemberAccessExpressionWriter(),
        LiteralExpressionSyntax => new LiteralExpressionWriter(),
        AssignmentExpressionSyntax => new AssignmentExpressionWriter(),
        IsPatternExpressionSyntax => new IsPatternExpressionWriter(),
        BinaryExpressionSyntax => new BinaryExpressionWriter(),
        ParenthesizedExpressionSyntax => new ParenthesizedExpressionWriter(),
        PrefixUnaryExpressionSyntax => new PrefixUnaryExpressionWriter(),
        TypeSyntax => new TypeWriter(),
        ParenthesizedLambdaExpressionSyntax => new ParenthesizedLambdaExpressionWriter(),
        InterpolatedStringExpressionSyntax => new InterpolatedStringExpressionWriter(),
        ElementAccessExpressionSyntax => new ElementAccessExpressionWriter(),
        _ => null
    };
}