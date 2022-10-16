using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class IfStatementBuilder : StatementBuilder<IfStatementSyntax>
{
    public override void Write(int ident, IfStatementSyntax statement, ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.Write(statement.Parent is ElseClauseSyntax ? 0 : ident, "if (");
        ExpressionBuilder.WriteSyntax(ident, statement.Condition, parameters, bodyBuilder);
        Writer.Write(") ");
        WriteLocationComment(statement);
        Writer.Write(' ');
        
        WriteSyntax(ident, statement.Statement, parameters, bodyBuilder);
        
        if (statement.Else is not null)
        {
            Writer.Write(ident, "else ");
            WriteSyntax(ident, statement.Else.Statement, parameters, bodyBuilder);
        }
    }
}