using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ThrowStatementBuilder : StatementBuilder<ThrowStatementSyntax>
{
    public override void Write(int ident, ThrowStatementSyntax statement, ImmutableDictionary<string, ParameterSyntax> parameters,
        ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.Write(ident, "assert(False, \"Exception was thrown: ");
        
        if (statement.Expression is ObjectCreationExpressionSyntax objectCreationExpressionSyntax)
        {
            ExpressionBuilder.WriteSyntax(ident, objectCreationExpressionSyntax.Type, parameters, bodyBuilder);
        }

        Writer.Write(" in ");

        var loc = statement.GetLocation();
        var line = loc.GetLineSpan();
        var fileName = Path.GetFileName(line.Path);
        Writer.Write(fileName);
        Writer.Write(" [");
        Writer.Write(line.StartLinePosition.Line + 1);
        Writer.Write(',');
        Writer.Write(line.StartLinePosition.Character + 1);
        Writer.WriteLine("]\");");
    }
}