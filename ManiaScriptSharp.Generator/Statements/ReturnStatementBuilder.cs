using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ReturnStatementBuilder : StatementBuilder<ReturnStatementSyntax>
{
    public override void Write(int ident, ReturnStatementSyntax statement, ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.Write(ident, "return");
        
        if (statement.Expression is not null)
        {
            Writer.Write(' ');
            ExpressionBuilder.WriteSyntax(ident, statement.Expression, parameters, bodyBuilder);
        }
        
        Writer.Write("; ");
        WriteLocationComment(statement);
        Writer.WriteLine();
    }
}