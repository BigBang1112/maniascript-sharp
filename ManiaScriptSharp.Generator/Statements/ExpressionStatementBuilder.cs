using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ExpressionStatementBuilder : StatementBuilder<ExpressionStatementSyntax>
{
    public override void Write(int ident, ExpressionStatementSyntax statement,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.WriteIdent(ident);
        
        try
        {
            if (ExpressionBuilder.WriteSyntax(ident, statement.Expression, parameters, bodyBuilder))
            {
                Writer.Write(';');
            }
        }
        catch (ExpressionStatementException ex)
        {
            Writer.Write("// " + ex.Message);
        }
        
        Writer.Write(' ');
        WriteLocationComment(statement);
        Writer.WriteLine();
    }
}