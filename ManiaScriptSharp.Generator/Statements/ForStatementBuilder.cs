using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ForStatementBuilder : StatementBuilder<ForStatementSyntax>
{
    public override void Write(int ident, ForStatementSyntax statement,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.Write(ident, "for (");

        var variableDeclaratorSyntax = statement.Declaration?.Variables.FirstOrDefault();

        if (variableDeclaratorSyntax?.Initializer is null)
        {
            Writer.Write("/* no declaration or its variables */");
        }
        else
        {
            Writer.Write(Standardizer.StandardizeName(variableDeclaratorSyntax.Identifier.Text));
            Writer.Write(", ");
            ExpressionBuilder.WriteSyntax(ident, variableDeclaratorSyntax.Initializer.Value, parameters, bodyBuilder);
            Writer.Write(", ");

            if (statement.Condition is BinaryExpressionSyntax binaryExpressionSyntax)
            {
                switch (binaryExpressionSyntax.OperatorToken.Text)
                {
                    case "<=":
                        ExpressionBuilder.WriteSyntax(ident, binaryExpressionSyntax.Right, parameters, bodyBuilder);
                        break;
                    case "<":
                        ExpressionBuilder.WriteSyntax(ident, binaryExpressionSyntax.Right, parameters, bodyBuilder);
                        Writer.Write(" - 1");
                        break;
                    default:
                        Writer.Write("/* unknown condition */");
                        break;
                }
            }
        }
        
        //
        Writer.Write(") ");
        WriteLocationComment(statement);
        Writer.Write(' ');
        WriteSyntax(ident, statement.Statement, parameters, bodyBuilder);
    }
}