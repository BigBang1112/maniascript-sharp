using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ExpressionStatementWriter : StatementWriter<ExpressionStatementSyntax>
{
    public override void Write(ExpressionStatementSyntax statement)
    {
        Writer.WriteIndent(Indent);
        
        try
        {
            if (WriteSyntax(statement.Expression))
            {
                if (statement.Expression is not InvocationExpressionSyntax invocationExpressionSyntax || GetSymbol(invocationExpressionSyntax) is not IMethodSymbol { IsVirtual: true })
                {
                    Writer.Write(';');
                }
            }
        }
        catch (ExpressionStatementException ex)
        {
            Writer.Write("// " + ex.Message);
        }
        
        Writer.Write(' ');
        WriteLocationComment();
        Writer.WriteLine();
    }
}