using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public abstract class ExpressionBuilder
{
    protected TextWriter Writer { get; private set; } = default!;
    
    public static bool WriteSyntax(int ident, ExpressionSyntax expression,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        ExpressionBuilder? builder = expression switch
        {
            InvocationExpressionSyntax => new InvocationExpressionBuilder(),
            IdentifierNameSyntax => new IdentifierNameExpressionBuilder(),
            MemberAccessExpressionSyntax => new MemberAccessExpressionBuilder(),
            LiteralExpressionSyntax => new LiteralExpressionBuilder(),
            AssignmentExpressionSyntax => new AssignmentExpressionBuilder(),
            IsPatternExpressionSyntax => new IsPatternExpressionBuilder(),
            BinaryExpressionSyntax => new BinaryExpressionBuilder(),
            ParenthesizedExpressionSyntax => new ParenthesizedExpressionBuilder(),
            PrefixUnaryExpressionSyntax => new PrefixUnaryExpressionBuilder(),
            _ => null
        };

        if (builder is not null)
        {
            builder?.Write(ident, expression, parameters, bodyBuilder);
            return true;
        }

        bodyBuilder.Writer.Write("/* ");
        bodyBuilder.Writer.Write(expression.GetType().Name);
        bodyBuilder.Writer.Write("[");
        bodyBuilder.Writer.Write(expression);
        bodyBuilder.Writer.Write("] */");
            
        return false;
    }

    public virtual void Write(int ident, ExpressionSyntax expression,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer = bodyBuilder.Writer;
    }
}