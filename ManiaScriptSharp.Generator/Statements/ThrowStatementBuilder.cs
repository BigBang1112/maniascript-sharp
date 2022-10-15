using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ThrowStatementBuilder : StatementBuilder<ThrowStatementSyntax>
{
    public override void Write(int ident, ThrowStatementSyntax statement, ImmutableDictionary<string, ParameterSyntax> parameters,
        ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.WriteLine("// Throwing exceptions is not supported");
        
        /*Writer.Write(ident, "error(\"");
        
        if (statement.Expression is ObjectCreationExpressionSyntax objectCreationExpressionSyntax)
        {
            ExpressionBuilder.WriteSyntax(ident, objectCreationExpressionSyntax.Type, parameters, bodyBuilder);
        }
        
        Writer.WriteLine("\");");*/
    }
}