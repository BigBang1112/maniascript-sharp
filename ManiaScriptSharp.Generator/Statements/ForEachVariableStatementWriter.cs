using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ForEachVariableStatementWriter : StatementWriter<ForEachVariableStatementSyntax>
{
    public override void Write(ForEachVariableStatementSyntax statement)
    {
        Writer.Write(Indent, "foreach (");

        if (statement.Variable is DeclarationExpressionSyntax declaration)
        {
            if (declaration.Designation is SingleVariableDesignationSyntax singleVariableDesignation)
            {
                Writer.Write(Standardizer.StandardizeName(singleVariableDesignation.Identifier.Text));
            }
            else if (declaration.Designation is DiscardDesignationSyntax)
            {
                Writer.Write("/* random string */");
            }
            else if (declaration.Designation is ParenthesizedVariableDesignationSyntax parenthesizedVariableDesignation)
            {
                if (parenthesizedVariableDesignation.Variables.Count == 2)
                {
                    Writer.Write(Standardizer.StandardizeName(parenthesizedVariableDesignation.Variables[0].ToString()));
                    Writer.Write(" => ");
                    Writer.Write(Standardizer.StandardizeName(parenthesizedVariableDesignation.Variables[1].ToString()));
                }
                else
                {
                    Writer.Write("/* variable count != 2 */");
                }
            }
            else
            {
                Writer.Write("/* unknown designation */");
            }
        }
        else
        {
            Writer.Write("/* var is not declaration syntax */");
        }

        Writer.Write(" in ");
        WriteSyntax(statement.Expression);

        //
        Writer.Write(") ");
        WriteLocationComment();
        Writer.Write(' ');
        WriteSyntax(statement.Statement);
    }
}
