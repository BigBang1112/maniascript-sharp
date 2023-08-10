using ManiaScriptSharp.Generator.Expressions;
using ManiaScriptSharp.Generator.Statements;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Linq;

static class LinqChecker
{
    public static bool GenerateSupportedLinqMethod(StatementWriter statementWriter, ExpressionSyntax? expressionSyntax, out string? replacementCode)
    {
        if (expressionSyntax is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccessExpressionSyntax } invocationExpressionSyntax)
        {
            replacementCode = null;
            return false;
        }

        var methodSymbol = statementWriter.BodyBuilder.SemanticModel.GetSymbolInfo(memberAccessExpressionSyntax.Name).Symbol;

        if (methodSymbol?.ContainingNamespace.ToString() != "System.Linq")
        {
            replacementCode = null;
            return false;
        }

        var methodName = memberAccessExpressionSyntax.Name.Identifier.Text;
        var lambdaSyntax = invocationExpressionSyntax.ArgumentList.Arguments[0].Expression as SimpleLambdaExpressionSyntax ?? throw new Exception("Linq method without lambda");
        
        switch (methodName)
        {
            case "Count":
                return GenerateLinqCount(statementWriter, memberAccessExpressionSyntax.Expression, lambdaSyntax, out replacementCode);
            default:
                replacementCode = null;
                return false;
        }
    }

    private static bool GenerateLinqCount(StatementWriter statementWriter, ExpressionSyntax collectionSyntax, SimpleLambdaExpressionSyntax lambdaSyntax, out string? replacementCode)
    {
        var writer = statementWriter.Writer;
        var indent = statementWriter.Indent;

        var paramName = Standardizer.StandardizeName(lambdaSyntax.Parameter.Identifier.Text);
        var countName = "Count";

        writer.WriteLine(indent, "// Start of LINQ");
        writer.Write(indent, "declare Integer ");
        writer.Write(countName);
        writer.WriteLine(" = 0;");
        writer.Write(indent, "foreach (");
        writer.Write(paramName);
        writer.Write(" in ");
        writer.Write(collectionSyntax);
        writer.WriteLine(") {");

        if (lambdaSyntax.Block is not null)
        {
            foreach (var statement in lambdaSyntax.Block.Statements.Take(lambdaSyntax.Block.Statements.Count - 1))
            {
                statementWriter.WriteSyntax(statement, indent);
            }
        }

        writer.Write(indent + 1, "if (");

        if (lambdaSyntax.ExpressionBody is not null)
        {
            statementWriter.WriteSyntax(lambdaSyntax.ExpressionBody);
        }
        else if (lambdaSyntax.Block is not null)
        {
            if (lambdaSyntax.Block.Statements.Last() is not ReturnStatementSyntax { Expression: not null } returnStatementSyntax)
            {
                throw new Exception("Lambda without valid return");
            }

            statementWriter.WriteSyntax(returnStatementSyntax.Expression);
        }
        else
        {
            throw new Exception("Lambda without body");
        }

        writer.WriteLine(") {");
        writer.Write(indent + 2, countName);
        writer.WriteLine(" += 1;");
        writer.WriteLine(indent + 1, "}");
        writer.WriteLine(indent, "}");
        writer.WriteLine(indent, "// End of LINQ");

        replacementCode = countName;

        return true;
    }
}
