using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ForStatementWriter : StatementWriter<ForStatementSyntax>
{
    public override void Write(ForStatementSyntax statement)
    {
        Writer.Write(Indent, "for (");

        var variableDeclaratorSyntax = statement.Declaration?.Variables.FirstOrDefault();

        if (variableDeclaratorSyntax?.Initializer is null)
        {
            Writer.Write("/* no declaration or its variables */");
        }
        else
        {
            Writer.Write(Standardizer.StandardizeName(variableDeclaratorSyntax.Identifier.Text));
            Writer.Write(", ");
            WriteSyntax(variableDeclaratorSyntax.Initializer.Value);
            Writer.Write(", ");

            if (statement.Condition is BinaryExpressionSyntax binaryExpressionSyntax)
            {
                switch (binaryExpressionSyntax.OperatorToken.Text)
                {
                    case "<=":
                        WriteSyntax(binaryExpressionSyntax.Right);
                        break;
                    case "<":
                        WriteSyntax(binaryExpressionSyntax.Right);
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
        WriteLocationComment();
        Writer.Write(' ');
        WriteSyntax(statement.Statement);
    }
}